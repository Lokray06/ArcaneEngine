using Arcane.Core;
using System.IO;
using System;
using StbImageSharp; // For textures
using System.Collections.Generic;
using System.Linq;
using OpenTK.Mathematics;
using ObjLoader.Loader.Loaders;
using Arcane.Rendering;

namespace Arcane.AssetManager
{
    // MeshData holds geometry information parsed from files like OBJ
    public class MeshData
    {
        public List<Vector3> Positions { get; set; } = new List<Vector3>();
        public List<Vector3> Normals { get; set; } = new List<Vector3>();
        public List<Vector2> TexCoords { get; set; } = new List<Vector2>();
        public List<Vector3> Tangents { get; set; } = new List<Vector3>();
        public List<uint> Indices { get; set; } = new List<uint>();
        public float[] InterleavedVertices { get; set; }
        public VertexAttribute[] VertexAttributes { get; set; }
    }

    // HdrTextureData holds pixel data for HDR images (typically .hdr)
    public class HdrTextureData
    {
        public float[] PixelData { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Channels { get; set; }
    }

    // TextureData holds pixel data for standard LDR images (e.g., .png, .jpg)
    public class TextureData
    {
        public byte[] PixelData { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Channels { get; set; }
    }

    internal class ObjRelativeMaterialStreamProvider : IMaterialStreamProvider
    {
        private readonly string _objFileDirectory;

        public ObjRelativeMaterialStreamProvider(string objFilePath)
        {
            _objFileDirectory = Path.GetDirectoryName(objFilePath);
        }

        public Stream Open(string materialFilePath)
        {
            string fullMaterialPath = Path.Combine(_objFileDirectory, materialFilePath);
            if (File.Exists(fullMaterialPath))
            {
                return File.OpenRead(fullMaterialPath);
            }
            return null;
        }
    }

    internal static class Importer
    {
        internal static TextureData LoadTexture(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"Importer.LoadTexture: File not found at '{filePath}'.");
                return null;
            }
            try
            {
                // Flip the image vertically on load to match OpenGL's coordinate system
                // where (0,0) is typically the bottom-left for textures, but image files
                // often store (0,0) as top-left.
                // StbImageSharp loads with (0,0) at bottom-left by default.
                // OpenGL TexImage2D also expects (0,0) at bottom-left of the source data.
                // So, if your image file format has (0,0) at top-left, you need to flip.
                // Most common image formats (PNG, JPG) have (0,0) at top-left.
                // Therefore, flipping is generally needed for these.
                StbImage.stbi_set_flip_vertically_on_load(1); // 1 for true

                ImageResult image;
                using (FileStream stream = File.OpenRead(filePath))
                {
                    image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
                }

                // Reset the flip state if other parts of the application might load images
                // expecting the default (non-flipped) orientation.
                StbImage.stbi_set_flip_vertically_on_load(0); // 0 for false

                return new TextureData
                {
                    PixelData = image.Data,
                    Width = image.Width,
                    Height = image.Height,
                    Channels = 4
                };
            }
            catch (Exception ex)
            {
                StbImage.stbi_set_flip_vertically_on_load(0); // Ensure reset on error
                Debug.LogError($"Importer.LoadTexture: Failed to load image '{filePath}'. Exception: {ex.Message}");
                return null;
            }
        }

        internal static HdrTextureData LoadHdrTextureData(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"Importer.LoadHdrTextureData: File not found at '{filePath}'.");
                return null;
            }
            try
            {
                // Flip HDR images vertically on load as well for consistency with OpenGL.
                StbImage.stbi_set_flip_vertically_on_load(1); // 1 for true

                ImageResultFloat image;
                using (FileStream stream = File.OpenRead(filePath))
                {
                    image = ImageResultFloat.FromStream(stream, ColorComponents.RedGreenBlue);
                }

                StbImage.stbi_set_flip_vertically_on_load(0); // 0 for false

                return new HdrTextureData
                {
                    PixelData = image.Data,
                    Width = image.Width,
                    Height = image.Height,
                    Channels = 3
                };
            }
            catch (Exception ex)
            {
                StbImage.stbi_set_flip_vertically_on_load(0); // Ensure reset on error
                Debug.LogError($"Importer.LoadHdrTextureData: Failed to load HDR image '{filePath}'. Exception: {ex.Message}");
                return null;
            }
        }

        internal static MeshData LoadMeshFromOBJ(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"Importer.LoadMeshFromOBJ: File not found at '{filePath}'.");
                return null;
            }

            var materialStreamProvider = new ObjRelativeMaterialStreamProvider(filePath);
            var objLoaderFactory = new ObjLoaderFactory();
            IObjLoader objLoader = objLoaderFactory.Create(materialStreamProvider);

            LoadResult objResult;
            try
            {
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    objResult = objLoader.Load(fileStream);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Importer.LoadMeshFromOBJ: Error loading OBJ file '{filePath}'. Exception: {ex.Message}");
                return null;
            }

            MeshData meshData = new MeshData();
            var uniqueVertexMap = new Dictionary<string, uint>();
            uint currentIndex = 0;

            bool hasNormals = objResult.Normals.Any();
            bool hasTexCoords = objResult.Textures.Any();

            var finalPositions = new List<Vector3>();
            var finalNormals = new List<Vector3>();
            var finalTexCoords = new List<Vector2>();

            foreach (var group in objResult.Groups)
            {
                foreach (var face in group.Faces)
                {
                    if (face.Count < 3) continue;

                    for (int i = 1; i < face.Count - 1; i++)
                    {
                        ProcessFaceVertex(face[0], objResult, meshData, uniqueVertexMap, ref currentIndex, finalPositions, finalNormals, finalTexCoords, hasNormals, hasTexCoords);
                        ProcessFaceVertex(face[i], objResult, meshData, uniqueVertexMap, ref currentIndex, finalPositions, finalNormals, finalTexCoords, hasNormals, hasTexCoords);
                        ProcessFaceVertex(face[i + 1], objResult, meshData, uniqueVertexMap, ref currentIndex, finalPositions, finalNormals, finalTexCoords, hasNormals, hasTexCoords);
                    }
                }
            }

            meshData.Positions = finalPositions;
            meshData.Normals = finalNormals;
            meshData.TexCoords = finalTexCoords;

            if (meshData.Positions.Count > 0 && meshData.Normals.Count > 0 && meshData.TexCoords.Count > 0 && meshData.Indices.Count > 0)
            {
                meshData.Tangents = CalculateTangents(meshData.Positions, meshData.TexCoords, meshData.Normals, meshData.Indices);
            }
            else
            {
                meshData.Tangents = new List<Vector3>(new Vector3[meshData.Positions.Count]);
                for (int i = 0; i < meshData.Positions.Count; i++) meshData.Tangents[i] = Vector3.UnitX;
            }

            int numVertices = meshData.Positions.Count;
            List<float> interleavedVertexData = new List<float>(numVertices * 11);
            for (int i = 0; i < numVertices; i++)
            {
                interleavedVertexData.Add(meshData.Positions[i].X);
                interleavedVertexData.Add(meshData.Positions[i].Y);
                interleavedVertexData.Add(meshData.Positions[i].Z);

                interleavedVertexData.Add(meshData.Normals.Count > i ? meshData.Normals[i].X : 0.0f);
                interleavedVertexData.Add(meshData.Normals.Count > i ? meshData.Normals[i].Y : 0.0f);
                interleavedVertexData.Add(meshData.Normals.Count > i ? meshData.Normals[i].Z : 1.0f);

                interleavedVertexData.Add(meshData.TexCoords.Count > i ? meshData.TexCoords[i].X : 0.0f);
                interleavedVertexData.Add(meshData.TexCoords.Count > i ? meshData.TexCoords[i].Y : 0.0f); // V is already flipped by STB if needed

                interleavedVertexData.Add(meshData.Tangents.Count > i ? meshData.Tangents[i].X : 1.0f);
                interleavedVertexData.Add(meshData.Tangents.Count > i ? meshData.Tangents[i].Y : 0.0f);
                interleavedVertexData.Add(meshData.Tangents.Count > i ? meshData.Tangents[i].Z : 0.0f);
            }
            meshData.InterleavedVertices = interleavedVertexData.ToArray();

            int stride = 11 * sizeof(float);
            meshData.VertexAttributes = new VertexAttribute[] {
                new VertexAttribute(0, 3, OpenTK.Graphics.OpenGL4.VertexAttribPointerType.Float, false, stride, 0 * sizeof(float)),
                new VertexAttribute(1, 3, OpenTK.Graphics.OpenGL4.VertexAttribPointerType.Float, false, stride, 3 * sizeof(float)),
                new VertexAttribute(2, 2, OpenTK.Graphics.OpenGL4.VertexAttribPointerType.Float, false, stride, 6 * sizeof(float)),
                new VertexAttribute(3, 3, OpenTK.Graphics.OpenGL4.VertexAttribPointerType.Float, false, stride, 8 * sizeof(float))
            };

            Debug.Log($"Importer.LoadMeshFromOBJ: Loaded '{filePath}'. Unique Vertices: {meshData.Positions.Count}, Indices: {meshData.Indices.Count}");
            return meshData;
        }

        private static void ProcessFaceVertex(
            ObjLoader.Loader.Data.Elements.FaceVertex objFaceVertex,
            LoadResult objResult,
            MeshData meshData,
            Dictionary<string, uint> uniqueVertexMap,
            ref uint currentIndex,
            List<Vector3> finalPositions,
            List<Vector3> finalNormals,
            List<Vector2> finalTexCoords,
            bool hasNormals, bool hasTexCoords)
        {
            string vertexKey = $"{objFaceVertex.VertexIndex}/{objFaceVertex.NormalIndex}/{objFaceVertex.TextureIndex}";

            if (uniqueVertexMap.TryGetValue(vertexKey, out uint foundIndex))
            {
                meshData.Indices.Add(foundIndex);
            }
            else
            {
                Vector3 position = new Vector3(
                    objResult.Vertices[objFaceVertex.VertexIndex - 1].X,
                    objResult.Vertices[objFaceVertex.VertexIndex - 1].Y,
                    objResult.Vertices[objFaceVertex.VertexIndex - 1].Z);
                finalPositions.Add(position);

                if (hasNormals && objFaceVertex.NormalIndex > 0 && objFaceVertex.NormalIndex <= objResult.Normals.Count)
                {
                    Vector3 normal = new Vector3(
                        objResult.Normals[objFaceVertex.NormalIndex - 1].X,
                        objResult.Normals[objFaceVertex.NormalIndex - 1].Y,
                        objResult.Normals[objFaceVertex.NormalIndex - 1].Z);
                    finalNormals.Add(normal);
                }
                else { finalNormals.Add(Vector3.UnitZ); }

                if (hasTexCoords && objFaceVertex.TextureIndex > 0 && objFaceVertex.TextureIndex <= objResult.Textures.Count)
                {
                    // StbImageSharp with stbi_set_flip_vertically_on_load(1) will load images
                    // such that the V coordinate is already suitable for OpenGL.
                    // ObjLoader.Loader.Data.VertexData.Texture also loads V as is from the file.
                    // If the OBJ file's V coordinates are standard (0=bottom, 1=top), and STB flips image data,
                    // then direct use of V is correct for OpenGL.
                    // If OBJ V is (0=top, 1=bottom), then 1.0f - V would be needed IF STB didn't flip.
                    // Since STB now flips, we can likely use the V coordinate directly.
                    Vector2 texCoord = new Vector2(
                        objResult.Textures[objFaceVertex.TextureIndex - 1].X,
                        objResult.Textures[objFaceVertex.TextureIndex - 1].Y);
                    finalTexCoords.Add(texCoord);
                }
                else { finalTexCoords.Add(Vector2.Zero); }

                uniqueVertexMap[vertexKey] = currentIndex;
                meshData.Indices.Add(currentIndex);
                currentIndex++;
            }
        }

        private static List<Vector3> CalculateTangents(List<Vector3> positions, List<Vector2> texCoords, List<Vector3> normals, List<uint> indices)
        {
            Vector3[] tangentsAccum = new Vector3[positions.Count];

            for (int i = 0; i < indices.Count; i += 3)
            {
                if (indices[i] >= positions.Count || indices[i + 1] >= positions.Count || indices[i + 2] >= positions.Count ||
                    indices[i] >= texCoords.Count || indices[i + 1] >= texCoords.Count || indices[i + 2] >= texCoords.Count)
                {
                    continue;
                }

                uint i0 = indices[i];
                uint i1 = indices[i + 1];
                uint i2 = indices[i + 2];

                Vector3 pos0 = positions[(int)i0]; Vector3 pos1 = positions[(int)i1]; Vector3 pos2 = positions[(int)i2];
                Vector2 uv0 = texCoords[(int)i0]; Vector2 uv1 = texCoords[(int)i1]; Vector2 uv2 = texCoords[(int)i2];

                Vector3 edge1 = pos1 - pos0; Vector3 edge2 = pos2 - pos0;
                Vector2 deltaUV1 = uv1 - uv0; Vector2 deltaUV2 = uv2 - uv0;

                float det = deltaUV1.X * deltaUV2.Y - deltaUV2.X * deltaUV1.Y;
                float f = (Math.Abs(det) < 1e-6f) ? 0.0f : 1.0f / det;

                Vector3 tangent = Vector3.Zero;
                if (f != 0.0f)
                {
                    tangent = new Vector3(
                        f * (deltaUV2.Y * edge1.X - deltaUV1.Y * edge2.X),
                        f * (deltaUV2.Y * edge1.Y - deltaUV1.Y * edge2.Y),
                        f * (deltaUV2.Y * edge1.Z - deltaUV1.Y * edge2.Z)
                    );
                }

                tangentsAccum[i0] += tangent;
                tangentsAccum[i1] += tangent;
                tangentsAccum[i2] += tangent;
            }

            List<Vector3> finalTangents = new List<Vector3>(positions.Count);
            for (int i = 0; i < positions.Count; i++)
            {
                if (i >= normals.Count)
                {
                    finalTangents.Add(Vector3.UnitX); continue;
                }

                Vector3 n = normals[i];
                Vector3 t = tangentsAccum[i];

                if (t.LengthSquared < 0.0001f)
                {
                    Vector3 c1 = Vector3.Cross(n, Vector3.UnitX);
                    Vector3 c2 = Vector3.Cross(n, Vector3.UnitY);
                    t = (c1.LengthSquared > c2.LengthSquared) ? c1 : c2;
                    if (t.LengthSquared < 0.0001f && Math.Abs(n.Z) < 0.999f) t = Vector3.Cross(n, Vector3.UnitZ);
                    if (t.LengthSquared < 0.0001f) t = Vector3.UnitX;
                }
                finalTangents.Add(Vector3.Normalize(t - n * Vector3.Dot(n, t)));
            }
            return finalTangents;
        }

        internal static Shader LoadShaderFromFile(string filePath)
        {
            // This method seems to be a placeholder or less used now that shaders are loaded directly.
            // If it were to be used, it would need robust parsing for combined files or proper
            // pairing for separate vert/frag. For now, direct Shader construction is preferred.
            Debug.LogWarning($"Importer.LoadShaderFromFile: This method is likely deprecated. Shaders are typically loaded directly via 'new Shader(vertPath, fragPath, true)'. Path: '{filePath}'");
            if (!File.Exists(filePath))
            {
                Debug.LogError($"Importer.LoadShaderFromFile: File not found at '{filePath}'.");
                return null;
            }
            // Fallback to a default shader if this path were to be used.
            return new Shader("default.vert", "default.frag", true);
        }

        internal static string LoadText(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"Importer.LoadText: File not found at '{filePath}'.");
                return null;
            }
            try
            {
                return File.ReadAllText(filePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"Importer.LoadText: Error reading file '{filePath}'. {e.Message}");
                return null;
            }
        }
    }
}
