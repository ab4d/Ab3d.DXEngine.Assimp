using Silk.NET.Core.Loader;

namespace Ab3d.DirectX
{
    /// <summary>Contains the library name of native Assimp libraries that can be customized.</summary>
    public class CustomAssimpLibraryNameContainer : SearchPathContainer
    {
        private string[] _linux = new string[] { "libassimp.so.5" };
        private string[] _macOS = new string[] { "libassimp.5.dylib" };
        private string[] _android = new string[] { "libassimp.so.5" };
        private string[] _iOS = new string[] { "__Internal" };
        private string[] _windows64 = new string[] { "Assimp64.dll" };
        private string[] _windows86 = new string[] { "Assimp32.dll" };

        /// <inheritdoc />
        public override string[] Linux => _linux;

        /// <inheritdoc />
        public override string[] MacOS => _macOS;

        /// <inheritdoc />
        public override string[] Android => _android;

        /// <inheritdoc />
        public override string[] IOS => _iOS;

        /// <inheritdoc />
        public override string[] Windows64 => _windows64;

        /// <inheritdoc />
        public override string[] Windows86 => _windows86;


        public void SetCustomLinuxPath(string newLibraryFilePath)
        {
            _linux = new string[] { newLibraryFilePath };
        }

        public void SetCustomMacOSPath(string newLibraryFilePath)
        {
            _macOS = new string[] { newLibraryFilePath };
        }

        public void SetCustomAndroidPath(string newLibraryFilePath)
        {
            _android = new string[] { newLibraryFilePath };
        }

        public void SetCustomIOSPath(string newLibraryFilePath)
        {
            _iOS = new string[] { newLibraryFilePath };
        }

        public void SetCustomWindows86Path(string newLibraryFilePath)
        {
            _windows86 = new string[] { newLibraryFilePath };
        }

        public void SetCustomWindows64Path(string newLibraryFilePath)
        {
            _windows64 = new string[] { newLibraryFilePath };
        }
    }
}