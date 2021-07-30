// ----------------------------------------------------------------
// <copyright file="AssimpFormatInfo.cs" company="AB4D d.o.o.">
//     Copyright (c) AB4D d.o.o.  All Rights Reserved
// </copyright>
// ----------------------------------------------------------------

using System.Diagnostics;

namespace Ab3d.DirectX
{
    [DebuggerDisplay("AssimpFormatInfo: {Name}")]
    public struct AssimpFormatInfo
    {
        public string Name;
        public string[] FileExtensions;

        public string FirstFileExtension => FileExtensions == null || FileExtensions.Length == 0
            ? null
            : FileExtensions[0];

        public AssimpFormatInfo(string name, string[] fileExtensions)
        {
            Name = name;
            FileExtensions = fileExtensions;
        }
    }
}