using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Ab3d.DirectX;
using Ab3d.Visuals;


// Ab3d.DXEngine.Assimp library can be used to import 3D models from many 3D file formats with Assimp importer library into Ab3d.DXEngine's objects.
// 
// 
// Ab3d.DXEngine (https://www.ab4d.com/DXEngine.aspx) is a super fast DirectX 11 rendering engine for Desktop .Net applications.
// Ab3d.DXEngine is built for advanced business and scientific 3D visualization.
// 
// The samples for Ab3d.DXEngine can be found in the Ab3d.DXEngine.Wpf.Samples samples project on github: https://github.com/ab4d/Ab3d.DXEngine.Wpf.Samples
//
// Ab3d.DXEngine.Assimp library creates the following objects:
// -MeshObjectNode for objects with meshes
// - SceneNode objects for groups of objects
// - SimpleMesh < PositionNormalTexture > for meshes
// - StandardMaterial for materials
//   
// 
// 
// It is also possible to import 3D objects with Assimp importer by using [Ab3d.PowerToys.Assimp](https://github.com/ab4d/Ab3d.PowerToys.Assimp).
// This library imports 3D objects into WPF objects that can be also rendered by Ab3d.DXEngine.
// In this case reading the objects is slower and takes more memory then when using Ab3d.DXEngine.Assimp because the native objects
// are first converted into managed objects in Assimp.Net library, then they are converted into WPF 3D objects and finally into Ab3d.DXEngine objects.
// When using Ab3d.DXEngine.Assimp the 3D objects are directly converted from native objects in Assimp library into Ab3d.DXEngine's objects.
// 
// An advantage of Ab3d.PowerToys.Assimp library is that it can be also used to play animatations and export 3D objects into 3D files.
// Those two features are not supported by in the current verison of Ab3d.DXEngine.Assimp.
// 
//
//
// Known issues
//
// - Only 64 bit process is supported becasue the Silk.NET.Assimp does not correctly call assimp function when using 32 bit process.
// 
// - Silk.NET.Assimp depends on Ultz.Native.Assimp library that should copy the native assimp library into the application's output folder.
//   But this works only for .Net Core and .Net 5.0 project and not for .Net Framework project. Therefore 


namespace Ab3d.DXEngine.Assimp.Samples
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _loadedFileName;
        private DXAssimpImporter _dxAssimpImporter;
        private SceneNode _readSceneNodes;

        public MainWindow()
        {
            InitializeComponent();

            var dragAndDropHelper = new DragAndDropHelper(this, ".*");
            dragAndDropHelper.FileDropped += (sender, args) =>
            {
                LoadModel(args.FileName);
            };

            MainDXViewportView.DXSceneInitialized += delegate (object sender, EventArgs args)
            {
                try
                {
                    //// Using custom Assimp native library:
                    //var customAssimpLibraryNameContainer = new CustomAssimpLibraryNameContainer();
                    //customAssimpLibraryNameContainer.SetCustomWindows86Path(@"C:\Assimp\Assimp32.dll");
                    //customAssimpLibraryNameContainer.SetCustomWindows64Path(@"C:\Assimp\Assimp64.dll");

                    //var assimpApi = new Silk.NET.Assimp.Assimp(Silk.NET.Assimp.Assimp.CreateDefaultContext(customAssimpLibraryNameContainer.GetLibraryName()));
                    //_dxAssimpImporter = new DXAssimpImporter(MainDXViewportView.DXScene.DXDevice, assimpApi);


                    // Use default Assimp native library:
                    _dxAssimpImporter = new DXAssimpImporter(MainDXViewportView.DXScene.DXDevice);
                }
                catch (Exception ex)
                {
                    LogMessage("Error creating DXAssimpImporter: " + ex.Message);

                    LoadButton.IsEnabled = false;
                    StartupInfoTextBlock.Visibility = Visibility.Collapsed;

                    // In case the DXAssimpImporter cannot be created, check if Assimp64.dll or Assimp32.dll are present
                    CheckNativeAssimpLibraryFile();

                    return;
                }

                //_dxAssimpImporter.LoggerCallback += delegate (string message, string data)
                //{
                //    LogMessage(message);
                //};

                //_dxAssimpImporter.IsVerboseLoggingEnabled = true;


                try
                {
                    var assimpVersion = _dxAssimpImporter.AssimpVersion;

                    var supportedImportFormats = _dxAssimpImporter.SupportedImportFormats;
                    var supportedImportFileExtensions = _dxAssimpImporter.SupportedImportFileExtensions;

                    InfoTextBox.Text = $"Assimp native library version: {assimpVersion}\r\nSupported import formats:\r\n" + string.Join(", ", supportedImportFileExtensions);


                    //string startUpFileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\duck.dae");
                    //LoadModel(startUpFileName);
                }
                catch (Exception ex)
                {
                    LogMessage("Error getting assimp info: " + ex.Message);
                }
            };

            this.Unloaded += delegate (object sender, RoutedEventArgs args)
            {
                MainDXViewportView.Dispose();
            };
        }


        private void LoadModel(string fileName)
        {
            if (MainDXViewportView.DXScene == null ||
                MainDXViewportView.DXScene.DXDevice == null ||
                _dxAssimpImporter == null)
            {
                return;
            }

            StartupInfoTextBlock.Visibility = Visibility.Collapsed;


            if (_readSceneNodes != null)
            {
                _readSceneNodes.Dispose();
                _readSceneNodes = null;
            }

            MainViewport.Children.Clear();

            ClearLog();

            try
            {
                _loadedFileName = fileName; // This is needed to get file folder to read textures

                _readSceneNodes = _dxAssimpImporter.ReadSceneNodes(fileName);
                _dxAssimpImporter.DisposeAssimpScene(); // Dispose the native Assimp objects after they are converted into DXEngine objects

                // IMPORT FROM STREAM:
                //
                //using (var fs = System.IO.File.OpenRead(fileName))
                //{
                //    readSceneNodes = _dxAssimpImporter.ReadSceneNodes(
                //        fs,
                //        formatHint: System.IO.Path.GetExtension(fileName),
                //        resolveResourceFunc: delegate (string textureFileName)
                //                             {
                //                                 string fullFileName = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(fileName), textureFileName);

                //                                 if (System.IO.File.Exists(fullFileName))
                //                                     return System.IO.File.OpenRead(fullFileName);
                //                                 else
                //                                     return null;
                //                             },
                //        automaticallyCloseResourceStream: true);
                //}

                if (_readSceneNodes != null)
                {
                    var sceneNodeVisual3D = new SceneNodeVisual3D(_readSceneNodes);
                    MainViewport.Children.Add(sceneNodeVisual3D);

                    Camera1.Distance = _readSceneNodes.Bounds.GetDiagonalLength() * 2;
                    Camera1.TargetPosition = _readSceneNodes.Bounds.GetCenterPosition().ToWpfPoint3D();
                }
            }
            catch (Exception ex)
            {
                LogMessage("Error reading file:\r\n" + ex.Message);
            }
        }

        private void LoadButton_OnClick(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.InitialDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");

            var fileExtensions = string.Join(", *.", _dxAssimpImporter.SupportedImportFileExtensions);
            fileExtensions = "*." + fileExtensions;
            //foreach (var supportedImportFileExtension in _dxAssimpImporter.SupportedImportFileExtensions)
            //    fileExtensions.AppendFormat("(*.{0})|*.{0};", supportedImportFileExtension);

            openFileDialog.Filter = $"3D model files: ({fileExtensions}) | {fileExtensions.Replace(',',';')}";// + fileExtensions.ToString();
            openFileDialog.Title = "Open 3D model file file";

            if ((openFileDialog.ShowDialog() ?? false) && !string.IsNullOrEmpty(openFileDialog.FileName))
                LoadModel(openFileDialog.FileName);
        }

        private void CheckNativeAssimpLibraryFile()
        {
            var assimpLibraryFiles = System.IO.Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "Assimp*.dll");

            if (assimpLibraryFiles == null || assimpLibraryFiles.Length == 0)
                LogMessage("Assimp64.dll or Assimp32.dll files are not present in the base directory (the same directory as the application's exe file). To use assimp library make sure that for 64 bit process the Assimp64.dll and for 32 bit process the Assimp32.dll files are copied to the applications directory.");
        }

        private void LogMessage(string message)
        {
            InfoTextBox.Text += message + Environment.NewLine;
        }
        
        private void ClearLog()
        {
            InfoTextBox.Text = "";
        }
    }
}
