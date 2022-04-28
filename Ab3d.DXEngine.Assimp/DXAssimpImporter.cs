// ----------------------------------------------------------------
// <copyright file="DXAssimpImporter.cs" company="AB4D d.o.o.">
//     Copyright (c) AB4D d.o.o.  All Rights Reserved
// </copyright>
// ----------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using SharpDX;
using SharpDX.Direct3D11;
using Silk.NET.Assimp;

namespace Ab3d.DirectX
{
    /// <summary>
    /// DXAssimpImporter can import 3D models from many 3D file formats into SceneNode objects for Ab3d.DXEngine rendering engine.
    /// </summary>
    public unsafe class DXAssimpImporter : IDisposable
    {
        private Assimp _assimp;

        private string _texturesPath;
        private Func<string, Stream> _resolveResourceFunc;
        private bool _automaticallyCloseResourceStream;

        private bool _isLogSteamAttached;
        private LogStream _attachedLogStream;

        private Dictionary<string, (ShaderResourceView, bool)> _texturesCache;

        private DXDevice _dxDevice;

        /// <summary>
        /// Gets the DXAssimpConverter that was used to convert assimp scene into SceneNode objects.
        /// </summary>
        public DXAssimpConverter AssimpConverter { get; private set; }

        /// <summary>
        /// Gets the Assimp's Scene object that was created when the 3D file was read.
        /// </summary>
        public Scene* ImportedAssimpScene { get; private set; }

        /// <summary>
        /// Gets a dictionary that can be used to get a 3D object by its name (key = name, value = object)
        /// </summary>
        public Dictionary<string, object> NamedObjects
        {
            get
            {
                return AssimpConverter?.NamedObjects;
            }
        }


        /// <summary>
        /// Gets or sets a Boolean that specifies if this instance of DXAssimpImporter has been disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Gets or sets Assimp PostProcessSteps (see assimp documentation for more information).
        /// Default value is FlipUVs | GenerateSmoothNormals | Triangulate.
        /// </summary>
        public PostProcessSteps AssimpPostProcessSteps { get; set; }


        /// <summary>
        /// Gets or sets a logger callback action that takes two strings (message and data).
        /// </summary>
        public Action<string, string> LoggerCallback { get; set; }

        /// <summary>
        /// Gets or sets a Boolean that specifies if verbose logging is enabled.
        /// </summary>
        public bool IsVerboseLoggingEnabled { get; set; }
        

        #region AssimpVersion, supported file Formats

        private static Version _assimpVersion;

        /// <summary>
        /// Gets the version of the native Assimp library.
        /// </summary>
        public Version AssimpVersion
        {
            get
            {
                if (_assimpVersion == null)
                {
                    var major = (int)_assimp.GetVersionMajor();
                    var minor = (int)_assimp.GetVersionMinor();
                    var revision = (int)_assimp.GetVersionRevision();

                    if (revision < 0) // revision may be negative
                        revision = 0;

                    _assimpVersion = new Version(major, minor, revision);
                }

                return _assimpVersion;
            }
        }

        private static AssimpFormatInfo[] _supportedImportFormats;
        private static string[] _supportedImportFileExtensions;

        /// <summary>
        /// Gets an array of <see cref="AssimpFormatInfo"/> that provide detailed information on supported import file formats.
        /// </summary>
        public AssimpFormatInfo[] SupportedImportFormats
        {
            get
            {
                if (_supportedImportFormats == null)
                    LoadSupportedImportFormats();

                return _supportedImportFormats;
            }
        }
        
        /// <summary>
        /// Gets an array of supported file extensions for importing (file extension are written without star and dot as prefix, g.e. "fxb", "obj", etc.
        /// </summary>
        public string[] SupportedImportFileExtensions
        {
            get
            {
                if (_supportedImportFileExtensions == null)
                    LoadSupportedImportFormats();

                return _supportedImportFileExtensions;
            }
        }

        #endregion

        /// <summary>
        /// Constructor that uses default texture loader and requires DXDevice as parameter
        /// </summary>
        /// <param name="dxDevice">DXDevice that is used to create textures</param>
        /// <param name="assimpApi">optional Assimp API that is used for this DXAssimpImporter. When null then teh default Assimp API that is provided by Silk.Net.Assimp is used.</param>
        public DXAssimpImporter(DXDevice dxDevice, Silk.NET.Assimp.Assimp assimpApi = null)
        {
            if (dxDevice == null)
                throw new ArgumentNullException(nameof(dxDevice));

            _dxDevice = dxDevice;

            _assimp = assimpApi ?? Silk.NET.Assimp.Assimp.GetApi();

            AssimpPostProcessSteps = PostProcessSteps.FlipUVs | PostProcessSteps.GenerateSmoothNormals | PostProcessSteps.Triangulate;

            AssimpConverter = new DXAssimpConverter(DefaultAssimpTextureLoader);
        }

        /// <summary>
        /// Constructor that uses custom texture loader.
        /// </summary>
        /// <param name="textureLoader">custom texture loader</param>
        public DXAssimpImporter(DXAssimpConverter.TextureLoaderDelegate textureLoader)
        {
            if (textureLoader == null) 
                throw new ArgumentNullException(nameof(textureLoader));

            _assimp = Silk.NET.Assimp.Assimp.GetApi();

            AssimpConverter = new DXAssimpConverter(textureLoader);
        }


        /// <summary>
        /// ReadModel method reads 3D models from specified file and returns the 3D models as DXEngine's SceneNode and MeshObjectNode objects.
        /// </summary>
        /// <param name="fileName">file name</param>
        /// <param name="texturesPath">optional: directory name where the textures files are; if null than the same path as fileName is used</param>
        /// <returns>DXEngine's SceneNode and MeshObjectNode objects</returns>
        public SceneNode ReadSceneNodes(string fileName, string texturesPath = null)
        {
            ImportedAssimpScene = ReadFileToAssimpScene(fileName);

            if (!string.IsNullOrEmpty(texturesPath))
                _texturesPath = texturesPath;
            else
                _texturesPath = System.IO.Path.GetDirectoryName(fileName); // Get textures path from file name

            var dxAssimpConverter = new DXAssimpConverter(DefaultAssimpTextureLoader);
            var rootSceneNode = dxAssimpConverter.ConvertAssimpScene(ImportedAssimpScene);

            return rootSceneNode;
        }

        /// <summary>
        /// ReadModel3D method reads 3D models from stream and returns the 3D models as DXEngine's SceneNode and MeshObjectNode objects.
        /// When the model have additional textures, the resolveResourceFunc must be set to a method that converts the resource name into a Stream.
        /// </summary>
        /// <param name="fileStream">file stream</param>
        /// <param name="formatHint">file extension to serve as a hint to Assimp to choose which importer to use - for example ".dae"</param>
        /// <param name="resolveResourceFunc">method that converts the resource name into Stream - used to read additional resources (materials and textures)</param>
        /// <param name="automaticallyCloseResourceStream">when true (by default) then the resource stream that is returned by resolveResourceFunc is closed after the texture is read</param>
        /// <returns>DXEngine's SceneNode and MeshObjectNode objects</returns>
        public SceneNode ReadSceneNodes(Stream fileStream, string formatHint, Func<string, Stream> resolveResourceFunc = null, bool automaticallyCloseResourceStream = true)
        {
            ImportedAssimpScene = ReadFileToAssimpScene(fileStream, formatHint);

            return ProcessImportedScene(texturesPath: null, resolveResourceFunc, automaticallyCloseResourceStream);
        }

        /// <summary>
        /// ReadFileToAssimpScene reads the specified file and returns Assimp's Scene object.
        /// Assimp's Scene object can be manually converted into DXEngine's SceneNodes by using DXAssimpConverter class.
        /// </summary>
        /// <param name="fileName">file name</param>
        /// <returns>Assimp's Scene object</returns>
        public unsafe Scene* ReadFileToAssimpScene(string fileName)
        {
            if (fileName == null) 
                throw new ArgumentNullException(nameof(fileName));

            CheckIfDisposed();

            DisposeAssimpScene();


            var fileExtension = System.IO.Path.GetExtension(fileName);

            if (!IsImportFormatSupported(fileExtension))
            {
                LogMessage(string.Format("File extension {0} is not supported by Assimp.", fileExtension));
                return null;
            }

            LogMessage("Start importing " + fileName, "");


            var usedPostProcessSteps = AssimpPostProcessSteps;
            //if (ReadPolygonIndices)
            //    usedPostProcessSteps = usedPostProcessSteps & ~PostProcessSteps.Triangulate; // When we are reading edge lines, we need to prevent Triangulate post process step

            Scene* assimpScene;

            try
            {
                AttachLogger();
                assimpScene = _assimp.ImportFile(fileName, (uint) usedPostProcessSteps);

                LogMessage("Import complete");
            }
            catch (Exception ex)
            {
                LogMessage("Error importing: " + ex.Message);

                assimpScene = null;
            }
            finally
            {
                DetachLogger();
            }


            return assimpScene;
        }

        /// <summary>
        /// ReadFileToAssimpScene reads the specified file stream and returns Assimp's Scene object.
        /// Assimp's Scene object can be manually converted into DXEngine's SceneNodes by using DXAssimpConverter class.
        /// </summary>
        /// <param name="fileStream">file stream</param>
        /// <param name="formatHint">file extension to serve as a hint to Assimp to choose which importer to use - for example ".dae"</param>
        /// <returns>Assimp's Scene object</returns>
        public unsafe Scene* ReadFileToAssimpScene(Stream fileStream, string formatHint)
        {
            if (fileStream == null) 
                throw new ArgumentNullException(nameof(fileStream));

            CheckIfDisposed();

            DisposeAssimpScene();


            var fileExtension = System.IO.Path.GetExtension(formatHint);

            if (!IsImportFormatSupported(fileExtension))
            {
                LogMessage(string.Format("File extension {0} is not supported by Assimp.", formatHint));
                return null;
            }


            LogMessage("Start reading file stream");

            var fileBytes = new byte[fileStream.Length];
            fileStream.Read(fileBytes, 0, (int)fileStream.Length);


            LogMessage("Start importing file data");

            var usedPostProcessSteps = AssimpPostProcessSteps;
            //if (ReadPolygonIndices)
            //    usedPostProcessSteps = usedPostProcessSteps & ~PostProcessSteps.Triangulate; // When we are reading edge lines, we need to prevent Triangulate post process step


            Scene* assimpScene;

            try
            {
                AttachLogger();

                fixed (byte* fileBytesPtr = fileBytes)
                {
                    assimpScene = _assimp.ImportFileFromMemory(fileBytesPtr, (uint)fileStream.Length, (uint)usedPostProcessSteps, formatHint);
                }

                LogMessage("Import complete");
            }
            catch (Exception ex)
            {
                LogMessage("Error importing: " + ex.Message);
                throw;
            }
            finally
            {
                DetachLogger();
            }

            return assimpScene;
        }

        /// <summary>
        /// Checks if the file extension (e.g. ".dae" or ".obj") is supported for import.
        /// </summary>
        /// <param name="fileExtension">file extension with or without leading dot</param>
        /// <returns>
        /// True if the file extension is supported, false otherwise
        /// </returns>
        public bool IsImportFormatSupported(string fileExtension)
        {
            if (string.IsNullOrEmpty(fileExtension))
                return false;

            CheckIfDisposed();

            if (fileExtension.StartsWith(".") && fileExtension.Length > 1)
                fileExtension = fileExtension.Substring(1);

            var supportedImportFileExtensions = SupportedImportFileExtensions;
            for (var i = 0; i < supportedImportFileExtensions.Length; i++)
            {
                if (supportedImportFileExtensions[i].Equals(fileExtension, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private SceneNode ProcessImportedScene(string texturesPath, Func<string, Stream> resolveResourceFunc, bool automaticallyCloseResourceStream)
        {
            if (ImportedAssimpScene == null)
                return null;
            
            _texturesPath = texturesPath;
            _resolveResourceFunc = resolveResourceFunc;
            _automaticallyCloseResourceStream = automaticallyCloseResourceStream;

            var dxAssimpConverter = new DXAssimpConverter(DefaultAssimpTextureLoader);
            var rootSceneNode = dxAssimpConverter.ConvertAssimpScene(ImportedAssimpScene);

            return rootSceneNode;
        }
        
        private void DefaultAssimpTextureLoader(StandardMaterial standardMaterial, string textureFileName, TextureWrapMode wrapMode, float blendFactor)
        {
            standardMaterial.TextureResourceName = textureFileName;

            if (blendFactor > 0 && blendFactor < 1)
                standardMaterial.DiffuseColor = new Color3(blendFactor, blendFactor, blendFactor);
            
            ShaderResourceView shaderResourceView = null;
            bool hasTransparency = false;


            if (_texturesCache == null)
                _texturesCache = new Dictionary<string, (ShaderResourceView, bool)>();

            if (_texturesCache.TryGetValue(textureFileName, out var cachedTextureInfo))
            {
                shaderResourceView = cachedTextureInfo.Item1;
                hasTransparency    = cachedTextureInfo.Item2;
            }

            if (shaderResourceView == null)
            {
                if (_resolveResourceFunc != null)
                {
                    var textureStream = _resolveResourceFunc(textureFileName);

                    if (textureStream != null)
                    {
                        var wpfBitmapImage = new System.Windows.Media.Imaging.BitmapImage();
                        wpfBitmapImage.BeginInit();
                        wpfBitmapImage.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        wpfBitmapImage.StreamSource = textureStream;
                        wpfBitmapImage.EndInit();

                        shaderResourceView = WpfMaterial.CreateTexture2D(_dxDevice, wpfBitmapImage, out var textureInfo);
                        hasTransparency = textureInfo.HasTransparency;

                        if (_automaticallyCloseResourceStream)
                            textureStream.Close();
                    }
                }
            }

            if (shaderResourceView == null)
            {
                if (System.IO.Path.DirectorySeparatorChar == '\\')
                    textureFileName = textureFileName.Replace('/', '\\'); // On Windows
                else
                    textureFileName = textureFileName.Replace('\\', '/'); // On Linux

                // Remove starting ".\" for example in duck.dae we have ".\duckCM.png"
                if (textureFileName.StartsWith(".\\") || textureFileName.StartsWith("./"))
                    textureFileName = textureFileName.Substring(2);

                // Correctly handle the texture paths that start with "\\.." - in this case the leading '\' needs to be removed
                // For example in "Assimp test models\Collada\earthCylindrical.DAE" the path to the texture is set as:
                // <init_from>\..\LWO\LWO2\MappingModes\EarthCylindric.jpg</init_from>
                if (textureFileName.StartsWith("\\.."))
                    textureFileName = textureFileName.Substring(1);


                string fullTextureFileName = textureFileName;
                if (!System.IO.Path.IsPathRooted(fullTextureFileName))
                {
                    if (!string.IsNullOrEmpty(_texturesPath))
                        fullTextureFileName = System.IO.Path.Combine(_texturesPath, fullTextureFileName);
                }

                if (System.IO.File.Exists(fullTextureFileName) && _dxDevice != null)
                {
                    shaderResourceView = TextureLoader.LoadShaderResourceView(_dxDevice.Device, fullTextureFileName, out var textureInfo);
                    hasTransparency = textureInfo.HasTransparency;
                }
            }

            if (shaderResourceView != null)
            {
                standardMaterial.DiffuseTextures = new ShaderResourceView[] { shaderResourceView };
                standardMaterial.HasTransparency = hasTransparency;

                _texturesCache.Add(standardMaterial.TextureResourceName, (shaderResourceView, hasTransparency));
            }
        }

        private void LoadSupportedImportFormats()
        {
            // The following does not work:
            //var assimpString = new AssimpString(1024);
            //_assimp.GetExtensionList(ref assimpString);

            CheckIfDisposed();

            var count = (int)_assimp.GetImportFormatCount();

            var supportedFormats = new List<AssimpFormatInfo>(count);
            var supportedImportFileExtensions = new List<string>(count);


            for (uint i = 0; i < count; i++)
            {
                var formatDescription = _assimp.GetImportFormatDescription(i);

                var description        = Marshal.PtrToStringAnsi((IntPtr)formatDescription->MName);
                var fileExtensionsText = Marshal.PtrToStringAnsi((IntPtr)formatDescription->MFileExtensions); // multiple file extensions can be returned - separated by space

                if (!string.IsNullOrEmpty(fileExtensionsText))
                {
                    var fileExtensions = fileExtensionsText.Split(' ');

                    var assimpFormatInfo = new AssimpFormatInfo(description, fileExtensions);
                    supportedFormats.Add(assimpFormatInfo);

                    supportedImportFileExtensions.AddRange(fileExtensions);
                }
            }

            _supportedImportFormats = supportedFormats.ToArray();
            _supportedImportFileExtensions = supportedImportFileExtensions.ToArray();
        }


        internal Assimp GetAssimpApi()
        {
            return _assimp;
        }


        private void AttachLogger()
        {
            if (LoggerCallback == null)
                return;

            var logStream = new LogStream(PfnLogStreamCallback.From(AssimpLogCallback));
            _assimp.AttachLogStream(logStream);

            _assimp.EnableVerboseLogging(IsVerboseLoggingEnabled ? 1 : 0);

            _attachedLogStream = logStream;
            _isLogSteamAttached = true;
        }

        private void DetachLogger()
        {
            if (!_isLogSteamAttached)
                return;

            _assimp.DetachLogStream(_attachedLogStream);
            _isLogSteamAttached = false;
        }

        private void AssimpLogCallback(byte* arg0, byte* arg1)
        {
            var messageText = Marshal.PtrToStringAnsi((IntPtr)arg0);
            var dataText = Marshal.PtrToStringAnsi((IntPtr)arg1);

            if (messageText != null && messageText.EndsWith("\n"))
                messageText = messageText.Substring(0, messageText.Length - 1);

            LogMessage(messageText, dataText);
        }

        private void LogMessage(string message, string data = "")
        {
            if (LoggerCallback != null)
                LoggerCallback(message, data);
        }

        private void CheckIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException("Cannot use disposed DXAssimpImporter");
        }

        private void DisposeAssimpScene()
        {
            if (ImportedAssimpScene != null)
            {
                _assimp.FreeScene(ImportedAssimpScene);
                ImportedAssimpScene = null;
            }

            if (NamedObjects != null)
                NamedObjects.Clear();

            if (_texturesCache != null)
                _texturesCache.Clear();
        }

        /// <summary>
        /// Disposes all unmanaged and managed resources. After calling Dispose, user cannot call any methods on this instance of DXAssimpImporter.
        /// </summary>
        public void Dispose()
        {
            if (IsDisposed)
                return;

            _assimp.FreeScene(ImportedAssimpScene);

            _assimp.Dispose();

            _dxDevice = null;
            AssimpConverter = null;
            _texturesCache = null;

            DetachLogger();

            IsDisposed = true;
        }
    }
}