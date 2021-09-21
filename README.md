# Ab3d.DXEngine.Assimp

Ab3d.DXEngine.Assimp **provides importing 3D models from many 3D file formats** for Ab3d.DXEngine rendering engine (using native [Assimp importer](https://github.com/assimp/assimp)).

[Ab3d.DXEngine](https://www.ab4d.com/DXEngine.aspx) is a super fast DirectX 11 rendering engine for Desktop .Net applications. Ab3d.DXEngine is built for advanced business and scientific 3D visualization.

The following objects are created:
- MeshObjectNode for objects with meshes
- SceneNode objects for groups of objects
- SimpleMesh<PositionNormalTexture> for meshes
- StandardMaterial for materials


It is also possible to import 3D objects with Assimp importer by using [Ab3d.PowerToys.Assimp](https://github.com/ab4d/Ab3d.PowerToys.Assimp).
This library imports 3D objects into WPF objects that can be also rendered by Ab3d.DXEngine. Using Ab3d.PowerToys.Assimp is slower and takes more memory than when using Ab3d.DXEngine.Assimp because the native objects are first converted into managed objects in Assimp.Net library, then they are converted into WPF 3D objects and finally into Ab3d.DXEngine objects. When using Ab3d.DXEngine.Assimp the 3D objects are directly converted from native objects in Assimp library into Ab3d.DXEngine's objects.

An advantage of Ab3d.PowerToys.Assimp library is that it can be also used to play animations and export 3D objects into 3D files (see [Ab3d.PowerToys.Wpf.Samples](https://github.com/ab4d/Ab3d.PowerToys.Wpf.Samples) project for examples). Those two features are not supported by the current version of Ab3d.DXEngine.Assimp.


The samples for Ab3d.DXEngine can be found in the [Ab3d.DXEngine.Wpf.Samples](https://github.com/ab4d/Ab3d.DXEngine.Wpf.Samples) project.


## Known issues

* Silk.NET.Assimp depends on Ultz.Native.Assimp library that should copy the native assimp library into the application's output folder. But this works only for .Net Core and .Net 5.0 project and not for .Net Framework project. To solve that the sample for .Net framework project manually copies the native Assimp library dlls to output folder.


## Repository projects

The Ab3d.DXEngine.Assimp repository contains two Visual Studio projects:
* Ab3d.DXEngine.Assimp - the main library project
* Ab3d.DXEngine.Assimp.Samples - samples projects that show imported 3D objects


## Repository solutions

The Ab3d.DXEngine.Assimp repository contains two Visual Studio solutions:
* Ab3d.DXEngine.Assimp.net48.sln (**.NET Framework 4.8**)
* Ab3d.DXEngine.Assimp.net50.sln (**.NET 5.0**)


## Dependencies

The Ab3d.DXEngine.Assimp library uses the following dependencies:
* Ab3d.DXEngine - Core Ab3d.DXEngine assembly - https://www.nuget.org/packages/Ab3d.DXEngine
* Ab3d.DXEngine.Wpf - WPF support for Ab3d.DXEngine - https://www.nuget.org/packages/Ab3d.DXEngine.Wpf
* SharpDX.Mathematics - DirectX - Mathematics managed API - https://www.nuget.org/packages/SharpDX
* Silk.NET.Assimp - Managed wrapper for native assimp library - https://www.nuget.org/packages/Silk.NET.Assimp


In addition to the dependencies above, the The Ab3d.DXEngine.Assimp.Samples project uses the following dependencies:
* Ab3d.PowerToys - The ultimate WPF 3D helper library - https://www.nuget.org/packages/Ab3d.PowerToys


Assimp.NET assembly that is present in the libs folder.

Native assimp importer library. The source can be get from [Assimp on GitHub](https://github.com/assimp/assimp). The compiled binaries for Windows can be get from the [Ab3d.PowerToys.Wpf.Samples](https://github.com/ab4d/Ab3d.PowerToys.Wpf.Samples) project.


## See also

* [AB4D Homepage](https://www.ab4d.com/)
* [Ab3d.DXEngine](https://www.ab4d.com/DXEngine.aspx) library (DirectX rendering engine)
* [Ab3d.DXEngine.Wpf.Samples](https://github.com/ab4d/Ab3d.DXEngine.Wpf.Samples) project
* [Ab3d.PowerToys](https://www.ab4d.com/PowerToys.aspx) library homepage (the ultimate 3D helper library)
* [Ab3d.PowerToys.Wpf.Samples](https://github.com/ab4d/Ab3d.PowerToys.Wpf.Samples) project