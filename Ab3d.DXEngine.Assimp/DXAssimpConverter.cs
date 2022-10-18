// ----------------------------------------------------------------
// <copyright file="DXAssimpConverter.cs" company="AB4D d.o.o.">
//     Copyright (c) AB4D d.o.o.  All Rights Reserved
// </copyright>
// ----------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Ab3d.DirectX;
using Ab3d.DirectX.Materials;
using SharpDX;
using SharpDX.Text;
using Silk.NET.Assimp;
using Encoding = System.Text.Encoding;
using Point = System.Windows.Point;

namespace Ab3d.DirectX
{
    /// <summary>
    /// DXAssimpConverter class provides method that can convert native assimp Scene object into Ab3d.DXEngine objects.
    /// </summary>
    public class DXAssimpConverter
    {
        private SimpleMesh<PositionNormalTexture>[] _allMeshes;

        /// <summary>
        /// Gets an array of all created DXEngine meshes
        /// </summary>
        public SimpleMesh<PositionNormalTexture>[] AllMeshes => _allMeshes;


        private StandardMaterial[] _allMaterials;

        /// <summary>
        /// Gets an array of all created StandardMaterials.
        /// </summary>
        public StandardMaterial[] AllMaterials => _allMaterials;

        

        /// <summary>
        /// TextureLoaderDelegate delegate defines the method layout that loads the texture from the assimp data and sets the appropriate fields in the specified standardMaterial.
        /// </summary>
        /// <param name="standardMaterial">StandardMaterial that will be updated by the loaded texture</param>
        /// <param name="textureFileName">texture file name as specified in the loaded file</param>
        /// <param name="wrapMode">wrap mode</param>
        /// <param name="blendFactor">All color components (rgb) are multipled with this factor before any further processing is done. 1 by default.</param>
        public delegate void TextureLoaderDelegate(StandardMaterial standardMaterial, string textureFileName, TextureWrapMode wrapMode, float blendFactor);

        /// <summary>
        /// Gets or sets a Boolean that specifies if simple triangle fan triangulation is used instead of standard triangulation. 
        /// This property is used only when the 3D model is not triangulated by assimp importer (when Triangulate PostProcessSteps is not used).
        /// Default value is false.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>UseSimpleTriangulation</b> gets or sets a Boolean that specifies if simple triangle fan triangulation is used instead of standard triangulation.
        /// </para>
        /// <para>
        /// Simple triangle fan triangulation is much faster, but can correctly triangulate only convex polygons.
        /// Standard triangulation is much slower but can triangulate convex and concave polygons.
        /// </para>
        /// <para>
        /// This property is used only when the 3D model is not triangulated by assimp importer (when Triangulate PostProcessSteps is not used).
        /// </para>
        /// <para>
        /// By default standard triangulation is used but if you know that the read models use only convex polygons, you can speed up reading 3D models with setting UseSimpleTriangulation to true.
        /// </para>
        /// </remarks>
        public bool UseSimpleTriangulation { get; set; }

        /// <summary>
        /// Gets or sets a Boolean that specifies if normals are calculated when they are not defined in the file.
        /// Default value is true.
        /// </summary>
        public bool CalculateNormals { get; set; }

        /// <summary>
        /// Gets a dictionary that can be used to get a 3D object by its name (key = name, value = object)
        /// </summary>
        public Dictionary<string, object> NamedObjects { get; private set; }

        /// <summary>
        /// Specifies the used texture loader
        /// </summary>
        public readonly TextureLoaderDelegate TextureLoader;


        /// <summary>
        /// TriangulatorFunc is a static property that can be set to a Func that takes list of 2D positions, triangulates them and returns a list of triangle indices (list of int values).
        /// When this property is not set, then the triangulator from Ab3d.PowerToys will be used (loaded by Reflection).
        /// </summary>
        public static Func<List<System.Windows.Point>, List<int>> TriangulatorFunc { get; set; }

        private static Func<List<System.Windows.Point>, List<int>> _powerToysTriangulator;

        private static Type _triangulatorType;
        private static MethodInfo _createTriangleIndicesMethodInfo;



        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="textureLoader">TextureLoaderDelegate</param>
        public DXAssimpConverter(TextureLoaderDelegate textureLoader)
        {
            UseSimpleTriangulation = false;
            CalculateNormals = true;
            
            TextureLoader = textureLoader;
        }

        /// <summary>
        /// ConvertAssimpScene converts the specified native assimp Scene object and converts it into Ab3d.DXEngine objects returning the root SceneNode object.
        /// </summary>
        /// <param name="assimpScene"></param>
        /// <returns></returns>
        public unsafe SceneNode ConvertAssimpScene(Scene* assimpScene)
        {
            if (assimpScene == null)
                return null;

            ConvertMaterials(assimpScene);
            ConvertMeshes(assimpScene);

            var rootNode = ConvertNodes(assimpScene);

            return rootNode;
        }

        private unsafe SceneNode ConvertNodes(Scene* assimpScene)
        {
            var node = assimpScene->MRootNode;

            NamedObjects = new Dictionary<string, object>();

            if (node == null)
                return null;

            var sceneNode = new SceneNode("RootNode");
            ConvertNodes(assimpScene, node, sceneNode);

            if (sceneNode.ChildNodesCount == 1)
            {
                var childSceneNode = sceneNode.ChildNodes[0];
                sceneNode.RemoveChildAt(0);

                sceneNode = childSceneNode;
            }

            sceneNode.UpdateBounds(forceUpdate: true);

            return sceneNode;
        }
        
        private unsafe void ConvertNodes(Scene* assimpScene, Node* node, SceneNode parentSceneNode)
        {
            if (node == null)
                return;

            string nodeName = node->MName.AsString;

            bool isTransformIdentity = node->MTransformation.IsIdentity;

            if (node->MNumMeshes > 0)
            {
                for (int i = 0; i < node->MNumMeshes; i++)
                {
                    var nodeMeshIndex = node->MMeshes[i];

                    var mesh = assimpScene->MMeshes[nodeMeshIndex];
                    int materialIndex = (int)mesh->MMaterialIndex;

                    var dxMesh     = nodeMeshIndex < _allMeshes.Length ? _allMeshes[nodeMeshIndex] : null;
                    var dxMaterial = materialIndex < _allMaterials.Length ? _allMaterials[materialIndex] : null;

                    if (dxMesh != null && dxMaterial != null)
                    {
                        string name;

                        var meshName = mesh->MName.AsString;
                        if (!string.IsNullOrEmpty(nodeName) && !string.IsNullOrEmpty(meshName))
                            name = nodeName + '-' + meshName;
                        else if (!string.IsNullOrEmpty(nodeName))
                            name = nodeName;
                        else if (!string.IsNullOrEmpty(meshName))
                            name = meshName;
                        else
                            name = "";

                        var meshObjectNode = new Ab3d.DirectX.MeshObjectNode(dxMesh, dxMaterial, name);

                        dxMesh.Dispose(); // reduce reference count on the dxMesh from 2 to 1 so that when the MeshObjectNode will be disposed, it will also dispose the mesh

                        if (!isTransformIdentity)
                            meshObjectNode.Transform = new Transformation(ToSharpDXMatrix(node->MTransformation));

                        parentSceneNode.AddChild(meshObjectNode);

                        if (!string.IsNullOrEmpty(name))
                            NamedObjects[name] = meshObjectNode;
                    }
                }
            }

            if (node->MNumChildren > 0)
            {
                // Do not add child nodes to MeshObjectNode
                // So when an assimp node has child nodes and also defines meshes, 
                // then create MeshObjectNode with the name specified in assimp file
                // and then create a SceneNode with the name that has "_Group" suffix.
                if (!string.IsNullOrEmpty(nodeName) && node->MNumMeshes > 0)
                    nodeName = nodeName + "_Group";
                    
                var sceneNode = new SceneNode(nodeName);

                if (!isTransformIdentity)
                    sceneNode.Transform = new Transformation(ToSharpDXMatrix(node->MTransformation));

                parentSceneNode.AddChild(sceneNode);

                for (int i = 0; i < node->MNumChildren; i++)
                    ConvertNodes(assimpScene, node->MChildren[i], sceneNode);

                if (!string.IsNullOrEmpty(nodeName))
                    NamedObjects[nodeName] = sceneNode;
            }
        }

        //
        // See also: http://assimp.sourceforge.net/lib_html/materials.html
        //
        private unsafe void ConvertMaterials(Scene* assimpScene)
        {
            _allMaterials = new StandardMaterial[assimpScene->MNumMaterials];
            
            for (int i = 0; i < assimpScene->MNumMaterials; i++)
            {
                var material = assimpScene->MMaterials[i];

                var standardMaterial = new StandardMaterial();

                string textureFileName = null;
                float blendFactor = 1; // All color components (rgb) are multipled with this factor before any further processing is done.
                TextureWrapMode wrapMode = TextureWrapMode.Wrap;


                for (int j = 0; j < material->MNumProperties; j++)
                {
                    var materialProperty = material->MProperties[j];

                    var key = materialProperty->MKey.AsString;

                    switch (key)
                    {
                        case Assimp.MaterialNameBase:
                            standardMaterial.Name = ReadString(materialProperty);
                            //System.Diagnostics.Debug.WriteLine($"Material name: {standardMaterial.Name ?? "<null>"}");

                            break;

                        // Shading mode is not used (more info: http://assimp.sourceforge.net/lib_html/material_8h.html#a93e23e0201d6ed86fb4287e15218e4cf)
                        //case Assimp.MaterialShadingModelBase:
                        //    var shadingMode = *(ShadingMode*)materialProperty->MData;
                        //    break;

                        case Assimp.MaterialColorDiffuseBase:
                            standardMaterial.DiffuseColor = ReadColor3(materialProperty);
                            break;
                        
                        case Assimp.MaterialOpacityBase:
                            if (materialProperty->MType == PropertyTypeInfo.Float)
                                standardMaterial.Alpha = *(float*)materialProperty->MData;
                            break;

                        case Assimp.MaterialColorAmbientBase:
                            standardMaterial.AmbientColor = ReadColor3(materialProperty);
                            break;
                        
                        case Assimp.MaterialColorEmissiveBase:
                            standardMaterial.EmissiveColor = ReadColor3(materialProperty);
                            break;
                        
                        case Assimp.MaterialColorSpecularBase:
                            standardMaterial.SpecularColor = ReadColor3(materialProperty);
                            break;
                        
                        case Assimp.MaterialShininessBase:
                            if (materialProperty->MType == PropertyTypeInfo.Float)
                                standardMaterial.SpecularPower = *(float*)materialProperty->MData;
                            break;

                        case Assimp.MaterialShininessStrengthBase:
                            if (materialProperty->MType == PropertyTypeInfo.Float)
                            {
                                var shininessStrength = *(float*)materialProperty->MData;
                                standardMaterial.SpecularColor = new Color3(shininessStrength, shininessStrength, shininessStrength);
                            }
                            break;

                        //case Assimp.MaterialTwosidedBase:
                        // TODO:
                        //    break;


                        case Assimp.MaterialTextureBase:
                            textureFileName = ReadString(materialProperty);
                            break;
                        
                        case Assimp.MaterialMappingmodeUBase:
                        case Assimp.MaterialMappingmodeVBase:
                            // aiTextureMapMode: http://assimp.sourceforge.net/lib_html/material_8h.html#a6cbe56056751aa80e8dd714632a49de0
                            if (materialProperty->MType == PropertyTypeInfo.Integer)
                                wrapMode = *(TextureWrapMode*)materialProperty->MData;

                            break;

                        case Assimp.MaterialTexblendBase:
                            if (materialProperty->MType == PropertyTypeInfo.Float)
                                blendFactor = *(float*)materialProperty->MData;
                            break;

                        case Assimp.MaterialUvtransformBase:
                            // TODO: UV transform: Read 20 float values that define the transformation
                            break;



                        //default:
                        //    System.Diagnostics.Debug.WriteLine($"Unknown material property: {key}: type: {materialProperty->MType}; size: {materialProperty->MDataLength}");
                        //    break;
                    }
                }

                if (standardMaterial.SpecularPower == 0) // Reset specular color to black when SpecularPower is not set
                    standardMaterial.SpecularColor = Color3.Black;

                if (!string.IsNullOrEmpty(textureFileName) && TextureLoader != null)
                    TextureLoader(standardMaterial, textureFileName, wrapMode, blendFactor);

                if (standardMaterial.Alpha < 1)
                    standardMaterial.HasTransparency = true;

                _allMaterials[i] = standardMaterial;
            }
        }

        private unsafe void ConvertMeshes(Scene* assimpScene)
        {
            _allMeshes = new SimpleMesh<PositionNormalTexture>[assimpScene->MNumMeshes];

            for (int i = 0; i < assimpScene->MNumMeshes; i++)
            {
                var mesh = assimpScene->MMeshes[i];

                // TODO: Do we need to check for points and lines ?
                //if (mesh->MPrimitiveTypes == (int) PrimitiveType.PrimitiveTypeTriangle)

                int positionsCount = (int) mesh->MNumVertices;
                int facesCount = (int) mesh->MNumFaces;

                if (positionsCount == 0 || facesCount == 0)
                    continue;


                var vertexBufferArray = new PositionNormalTexture[positionsCount];

                var vertices = mesh->MVertices;
                var normals = mesh->MNormals;
                var textureCoordinates = mesh->MTextureCoords.Element0;

                var bounds = Bounds.Empty;

                if (normals != null && textureCoordinates != null)
                {
                    // Optimized loop with positions, normals and textureCoordinates
                    for (int j = 0; j < positionsCount; j++)
                    {
                        System.Numerics.Vector3 onePosition = vertices[j];
                        var dxPosition = new SharpDX.Vector3(onePosition.X, onePosition.Y, onePosition.Z);
                        vertexBufferArray[j].Position = dxPosition;

                        bounds.Add(dxPosition);

                        System.Numerics.Vector3 oneNormal = normals[j];
                        vertexBufferArray[j].Normal = new SharpDX.Vector3(oneNormal.X, oneNormal.Y, oneNormal.Z);

                        System.Numerics.Vector3 oneTextureCoordinate = textureCoordinates[j];
                        vertexBufferArray[j].TextureCoordinate = new SharpDX.Vector2(oneTextureCoordinate.X, oneTextureCoordinate.Y);
                    }
                }
                else if (normals == null && textureCoordinates == null)
                {
                    // Optimized loop with only positions
                    for (int j = 0; j < positionsCount; j++)
                    {
                        System.Numerics.Vector3 onePosition = vertices[j];
                        var dxPosition = new SharpDX.Vector3(onePosition.X, onePosition.Y, onePosition.Z);

                        vertexBufferArray[j].Position = dxPosition;

                        bounds.Add(dxPosition);
                    }
                }
                else
                {
                    for (int j = 0; j < positionsCount; j++)
                    {
                        System.Numerics.Vector3 onePosition = vertices[j];
                        var dxPosition = new SharpDX.Vector3(onePosition.X, onePosition.Y, onePosition.Z);

                        vertexBufferArray[j].Position = dxPosition;

                        bounds.Add(dxPosition);


                        if (normals != null)
                        {
                            System.Numerics.Vector3 oneNormal = normals[j];
                            vertexBufferArray[j].Normal = new SharpDX.Vector3(oneNormal.X, oneNormal.Y, oneNormal.Z);
                        }

                        if (textureCoordinates != null)
                        {
                            System.Numerics.Vector3 oneTextureCoordinate = textureCoordinates[j];
                            vertexBufferArray[j].TextureCoordinate = new SharpDX.Vector2(oneTextureCoordinate.X, oneTextureCoordinate.Y);
                        }
                    }
                }


                // First count number of triangle indices so we can initialize the array
                int triangleIndicesCount = 0;

                for (int j = 0; j < facesCount; j++)
                {
                    int indicesCount = (int)mesh->MFaces[j].MNumIndices;
                    triangleIndicesCount += (indicesCount - 2) * 3; // for each face (triangle) we need 3 indices
                }


                int[] indexBufferArray = new int[triangleIndicesCount];
                int triangleIndiceIndex = 0;

                for (int j = 0; j < facesCount; j++)
                {
                    var oneFace = mesh->MFaces[j];

                    int indicesCount = (int) oneFace.MNumIndices;

                    if (indicesCount == 3)
                    {
                        indexBufferArray[triangleIndiceIndex]     = (int) oneFace.MIndices[0];
                        indexBufferArray[triangleIndiceIndex + 1] = (int) oneFace.MIndices[1];
                        indexBufferArray[triangleIndiceIndex + 2] = (int) oneFace.MIndices[2];

                        triangleIndiceIndex += 3;
                    }
                    else if (indicesCount == 4)
                    {
                        if (UseSimpleTriangulation)
                        {
                            indexBufferArray[triangleIndiceIndex]     = (int) oneFace.MIndices[0];
                            indexBufferArray[triangleIndiceIndex + 1] = (int) oneFace.MIndices[1];
                            indexBufferArray[triangleIndiceIndex + 2] = (int) oneFace.MIndices[2];

                            indexBufferArray[triangleIndiceIndex + 3] = (int) oneFace.MIndices[0];
                            indexBufferArray[triangleIndiceIndex + 4] = (int) oneFace.MIndices[2];
                            indexBufferArray[triangleIndiceIndex + 5] = (int) oneFace.MIndices[3];
                        }
                        else
                        {
                            // If we use regular triangulation,
                            // we can simplify the case for 4 indices with
                            // first finding the indice where the concave point is (if there is any).
                            // In case of finding the concave point we start with it and do a triangle fan from it.
                            // If concave point is not found, then we do triangle fan from the first indice.
                            var p1 = vertexBufferArray[(int)oneFace.MIndices[0]].Position;
                            var p2 = vertexBufferArray[(int)oneFace.MIndices[1]].Position;
                            var p3 = vertexBufferArray[(int)oneFace.MIndices[2]].Position;
                            var p4 = vertexBufferArray[(int)oneFace.MIndices[3]].Position;

                            var v1 = p2 - p1;
                            var v2 = p3 - p2;
                            var v3 = p4 - p3;
                            var v4 = p1 - p4;

                            var n1 = Vector3.Cross(v1, v2);
                            var n2 = Vector3.Cross(v2, v3);
                            var n3 = Vector3.Cross(v3, v4);
                            var n4 = Vector3.Cross(v4, v1);

                            var summedNormal = n1 + n2 + n3 + n4;

                            int startIndex;
                            if (Vector3.Dot(n2, summedNormal) < 0)
                                startIndex = 1;
                            else if (Vector3.Dot(n3, summedNormal) < 0)
                                startIndex = 2;
                            else if (Vector3.Dot(n3, summedNormal) < 0)
                                startIndex = 3;
                            else
                                startIndex = 0;

                            indexBufferArray[triangleIndiceIndex]     = (int)oneFace.MIndices[startIndex];
                            indexBufferArray[triangleIndiceIndex + 1] = (int)oneFace.MIndices[(startIndex + 1) % 4];
                            indexBufferArray[triangleIndiceIndex + 2] = (int)oneFace.MIndices[(startIndex + 2) % 4];

                            indexBufferArray[triangleIndiceIndex + 3] = (int)oneFace.MIndices[startIndex];
                            indexBufferArray[triangleIndiceIndex + 4] = (int)oneFace.MIndices[(startIndex + 2) % 4];
                            indexBufferArray[triangleIndiceIndex + 5] = (int)oneFace.MIndices[(startIndex + 3) % 4];
                        }

                        triangleIndiceIndex += 6;
                    }
                    else if (indicesCount > 4)
                    {
                        Func<List<Point>, List<int>> triangulator;

                        if (UseSimpleTriangulation)
                            triangulator = null;
                        else
                            triangulator = GetTriangulator(); // Get the triangulator Func (by default the triangulator from Ab3d.PowerToys will be used)


                        if (triangulator == null)
                        {
                            // Triangulate with using simple triangle fan (works for simple polygons without holes)
                            int firstIndice = (int) oneFace.MIndices[0];
                            for (int k = 1; k < indicesCount - 1; k++)
                            {
                                indexBufferArray[triangleIndiceIndex]     = firstIndice;
                                indexBufferArray[triangleIndiceIndex + 1] = (int) oneFace.MIndices[k];
                                indexBufferArray[triangleIndiceIndex + 2] = (int) oneFace.MIndices[k + 1];

                                triangleIndiceIndex += 3;
                            }
                        }
                        else
                        {
                            // To triangulate 3D positions, we first convert 3D positions to 2D positions.
                            // This is done with project the positions on the the 2D plane (we assume that all positions for one face lie on the same plane).
                            var positions2D = Project3DPositionTo2D(vertexBufferArray, oneFace.MIndices, indicesCount);

                            var triangulatedIndices = triangulator(positions2D);

                            //var triangulator3D = new Ab3d.Assimp.Common.Triangulator(positions2D);
                            //triangleIndices = triangulator3D.CreateTriangleIndices();

                            // We got triangleIndices with indexes from 0 to triangleIndices.Count
                            // Now we need to adjust the indexes to the real indexes from indices collection (those are in range from 0 to positions.count)
                            var triangulatedIndicesCount = triangulatedIndices.Count;
                            for (int k = 0; k < triangulatedIndicesCount; k++)
                                indexBufferArray[triangleIndiceIndex + k] = (int)oneFace.MIndices[triangulatedIndices[k]];

                            triangleIndiceIndex += triangulatedIndicesCount;
                        }
                    }
                    // else if < 3 just skip this face
                }

                if (triangleIndiceIndex == 0) 
                    continue; // if we did not read any triangle indices (for example when all indicesCount is less then 3 for all)

                if (triangleIndiceIndex < triangleIndicesCount) 
                    Array.Resize(ref indexBufferArray, triangleIndiceIndex); // Resize array


                if (normals == null && CalculateNormals)
                    Ab3d.DirectX.Utilities.MeshUtils.CalculateNormals(vertexBufferArray, indexBufferArray, normalize: true);


                string meshName = mesh->MName.Length > 0 ? mesh->MName.AsString : null;
                
                var dxMesh = new SimpleMesh<PositionNormalTexture>(vertexBufferArray, 
                                                                   indexBufferArray,
                                                                   inputLayoutType: InputLayoutType.Position | InputLayoutType.Normal | InputLayoutType.TextureCoordinate,
                                                                   name: meshName);

                dxMesh.Bounds = bounds;

                _allMeshes[i] = dxMesh;
            }
        }


        private unsafe string ReadString(MaterialProperty* materialProperty)
        {
            string assimpString = null;

            // Assimp string starts with string length and then data follows
            if (materialProperty->MType == PropertyTypeInfo.String && materialProperty->MDataLength > 4) // 4 bytes for string length
            {
                int length = *(int*)materialProperty->MData;

                if (length != materialProperty->MDataLength - 5) // 4 bytes for string length + one byte for '\0'
                    return null;

                if (length > 0)
                    assimpString = Encoding.UTF8.GetString(materialProperty->MData + 4, length);
            }

            return assimpString;
        }

        private static unsafe Color3 ReadColor3(MaterialProperty* materialProperty)
        {
            if (materialProperty->MType == PropertyTypeInfo.Float &&
                (materialProperty->MDataLength == 12 || materialProperty->MDataLength == 16))
            {
                float red   = *(float*)materialProperty->MData;
                float green = *(float*)(materialProperty->MData + 4);
                float blue  = *(float*)(materialProperty->MData + 8);

                var color = new Color3(red, green, blue);

                return color;
            }

            return Color3.Black;
        }

        private static SharpDX.Matrix ToSharpDXMatrix(System.Numerics.Matrix4x4 m)
        {
            // Read the row-major data into column-major WPF matrix
            return new SharpDX.Matrix(m.M11, m.M21, m.M31, m.M41,
                                      m.M12, m.M22, m.M32, m.M42,
                                      m.M13, m.M23, m.M33, m.M43,
                                      m.M14, m.M24, m.M34, m.M44);
        }

        private static unsafe List<System.Windows.Point> Project3DPositionTo2D(PositionNormalTexture[] vertexBuffer, uint* indices, int indicesCount)
        {
            if (vertexBuffer == null || vertexBuffer.Length < 3)
                throw new ArgumentException("positions is null or have less then 3 element");

            if (indicesCount < 3)
                throw new ArgumentException("polygonIndices is null or have less then 3 element");


            var positions2D = new List<System.Windows.Point>(indicesCount);

            // We assume that the position lie on the same plane
            // so we do not need to calculate the proper polygon normal
            // but can assume that the normal of the first triangle will be sufficient to determine the orientation.
            // Once we have the orientation, we can do simple projection to 2D positions with eliminating the axis that have the biggest normal value
            // For example if normal is up (0,1,0), this means that all positions lie on the same xz plane and that all y values are the same => we can remove the y value

            var p1 = vertexBuffer[indices[0]].Position;
            var p2 = vertexBuffer[indices[1]].Position;
            var p3 = vertexBuffer[indices[2]].Position;

            var v1 = p1 - p2;
            var v2 = p3 - p2;

            // We use absolute normal values.
            var normal = Vector3.Cross(v1, v2);
            var nx = Math.Abs(normal.X);
            var ny = Math.Abs(normal.Y);
            var nz = Math.Abs(normal.Z);

            if (nx > ny && nx > nz)
            {
                // normal.x is the biggest => remove x values
                for (int i = 0; i < indicesCount; i++)
                {
                    var position3D = vertexBuffer[indices[i]].Position;
                    positions2D.Add(new System.Windows.Point(position3D.Y, position3D.Z));
                }
            }
            else if (ny > nz)
            {
                // normal.y is the biggest => remove y values
                for (int i = 0; i < indicesCount; i++)
                {
                    var position3D = vertexBuffer[indices[i]].Position;
                    positions2D.Add(new System.Windows.Point(position3D.X, position3D.Z));
                }
            }
            else
            {
                // normal.z is the biggest => remove z values
                for (int i = 0; i < indicesCount; i++)
                {
                    var position3D = vertexBuffer[indices[i]].Position;
                    positions2D.Add(new System.Windows.Point(position3D.X, position3D.Y));
                }
            }

            return positions2D;
        }

        private Func<List<System.Windows.Point>, List<int>> GetTriangulator()
        {
            // Use CustomTriangulator when set
            if (TriangulatorFunc != null)
                return TriangulatorFunc;

            if (_triangulatorType == null)
            {
                // Load Triangulator class and its CreateTriangleIndices method from Ab3d.PowerToys by using reflection
                _triangulatorType = Type.GetType("Ab3d.Utilities.Triangulator, Ab3d.PowerToys", throwOnError: false);

                if (_triangulatorType == null)
                    return null;

                if (_createTriangleIndicesMethodInfo == null)
                    _createTriangleIndicesMethodInfo = _triangulatorType.GetMethod("CreateTriangleIndices");

                _powerToysTriangulator = delegate (List<System.Windows.Point> positions)
                {
                    // Execute:
                    //var triangulatorInstance = new Ab3d.Utilities.Triangulator(positions);
                    //triangleIndices = triangulator3D.CreateTriangleIndices();

                    var triangulatorInstance = Activator.CreateInstance(_triangulatorType, positions);
                    var triangleIndices = (List<int>)_createTriangleIndicesMethodInfo.Invoke(triangulatorInstance, null);

                    return triangleIndices;
                };
            }

            return _powerToysTriangulator;
        }
    }
}