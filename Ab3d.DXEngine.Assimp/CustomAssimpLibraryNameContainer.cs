using Silk.NET.Core.Loader;

namespace Ab3d.DirectX
{
    /// <summary>Contains the library name of native Assimp libraries that can be customized.</summary>
    public class CustomAssimpLibraryNameContainer : SearchPathContainer
    {
        private string _linux = "libassimp.so.5";
        private string _macOS = "libassimp.5.dylib";
        private string _android = "libassimp.so.5";
        private string _iOS = "__Internal";
        private string _windows64 = "Assimp64.dll";
        private string _windows86 = "Assimp32.dll";

        /// <inheritdoc />
        public override string Linux => _linux;

        /// <inheritdoc />
        public override string MacOS => _macOS;

        /// <inheritdoc />
        public override string Android => _android;

        /// <inheritdoc />
        public override string IOS => _iOS;

        /// <inheritdoc />
        public override string Windows64 => _windows64;

        /// <inheritdoc />
        public override string Windows86 => _windows86;


        public void SetCustomLinuxPath(string newLibraryFilePath)
        {
            _linux = newLibraryFilePath;
        }

        public void SetCustomMacOSPath(string newLibraryFilePath)
        {
            _macOS = newLibraryFilePath;
        }

        public void SetCustomAndroidPath(string newLibraryFilePath)
        {
            _android = newLibraryFilePath;
        }

        public void SetCustomIOSPath(string newLibraryFilePath)
        {
            _iOS = newLibraryFilePath;
        }

        public void SetCustomWindows86Path(string newLibraryFilePath)
        {
            _windows86 = newLibraryFilePath;
        }

        public void SetCustomWindows64Path(string newLibraryFilePath)
        {
            _windows64 = newLibraryFilePath;
        }
    }
}