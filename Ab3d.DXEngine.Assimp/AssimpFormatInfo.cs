// ----------------------------------------------------------------
// <copyright file="AssimpFormatInfo.cs" company="AB4D d.o.o.">
//     Copyright (c) AB4D d.o.o.  All Rights Reserved
// </copyright>
// ----------------------------------------------------------------

using System.Diagnostics;

namespace Ab3d.DirectX
{
    /// <summary>
    /// AssimpFormatInfo struct provides file format name and possible file extensions.
    /// </summary>
    [DebuggerDisplay("AssimpFormatInfo: {Name}")]
    public struct AssimpFormatInfo
    {
        /// <summary>
        /// Name of this file format
        /// </summary>
        public string Name;

        /// <summary>
        /// An array of file extensions
        /// </summary>
        public string[] FileExtensions;

        /// <summary>
        /// Gets the first file extension for this file format
        /// </summary>
        public string FirstFileExtension => FileExtensions == null || FileExtensions.Length == 0
            ? null
            : FileExtensions[0];


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">name</param>
        /// <param name="fileExtensions">an array of file extensions</param>
        public AssimpFormatInfo(string name, string[] fileExtensions)
        {
            Name = name;
            FileExtensions = fileExtensions;
        }
    }
}