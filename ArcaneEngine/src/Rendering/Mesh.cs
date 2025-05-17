using OpenTK.Graphics.OpenGL4;
using Arcane.Core;
using Arcane.AssetManager;
using System;
using System.Collections.Generic;

namespace Arcane.Rendering
{
    public struct VertexAttribute
    {
        public int Location;
        public int Size;
        public VertexAttribPointerType Type;
        public bool Normalized;
        public int Stride;
        public int Offset;

        public VertexAttribute(int location, int size, VertexAttribPointerType type, bool normalized, int stride, int offset)
        {
            Location = location; Size = size; Type = type; Normalized = normalized; Stride = stride; Offset = offset;
        }
    }

    public class Mesh : IDisposable
    {
        public int VaoId { get; private set; }
        public int VboId { get; private set; }
        public int EboId { get; private set; }
        public int VertexCount { get; private set; }
        public int IndexCount { get; private set; }
        private bool _isLoaded = false;
        private bool _isDisposed = false;

        public Mesh(float[] vertices, uint[] indices, VertexAttribute[] attributes)
        {
            if (vertices == null) { Debug.LogError("Mesh Constructor: 'vertices' is null."); return; }
            if (indices == null) { Debug.LogError("Mesh Constructor: 'indices' is null."); return; }
            if (attributes == null || attributes.Length == 0) { Debug.LogError("Mesh Constructor: 'attributes' is null or empty."); return; }

            if (attributes[0].Stride == 0) VertexCount = 0;
            else
            {
                int floatsPerVertex = attributes[0].Stride / sizeof(float);
                VertexCount = (floatsPerVertex == 0 || vertices.Length % floatsPerVertex != 0) ? 0 : vertices.Length / floatsPerVertex;
                if (VertexCount == 0 && vertices.Length > 0) Debug.LogError("Mesh Constructor: Vertex data length incompatible with stride.");
            }
            IndexCount = indices.Length;

            VaoId = GL.GenVertexArray();
            if (VaoId == 0) { Debug.LogError("Mesh Constructor: GL.GenVertexArray() failed."); return; }
            GL.BindVertexArray(VaoId);

            VboId = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VboId);
            if (vertices.Length > 0) GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            EboId = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, EboId);
            if (indices.Length > 0) GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            foreach (var attr in attributes)
            {
                GL.VertexAttribPointer(attr.Location, attr.Size, attr.Type, attr.Normalized, attr.Stride, attr.Offset);
                GL.EnableVertexAttribArray(attr.Location);
            }
            GL.BindVertexArray(0);
            _isLoaded = (VaoId != 0 && IndexCount > 0);
            GLDebug.CheckError("Mesh Constructor (Raw Data) - End");
        }

        public Mesh(float[] positions, uint[] indices) : this(
            positions, indices,
            new VertexAttribute[] { new VertexAttribute(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0) })
        { }

        public Mesh(Asset meshAsset) : this(
            (meshAsset?.Type == AssetType.Mesh && (meshAsset.IsLoaded || meshAsset.LoadInternalOnly() != null) ? (meshAsset.Data as MeshData)?.InterleavedVertices : null),
            (meshAsset?.Type == AssetType.Mesh && meshAsset.IsLoaded ? (meshAsset.Data as MeshData)?.Indices.ToArray() : null),
            (meshAsset?.Type == AssetType.Mesh && meshAsset.IsLoaded ? (meshAsset.Data as MeshData)?.VertexAttributes : null)
            )
        {
            if (meshAsset == null) { Debug.LogError("Mesh(Asset): Null asset provided."); return; }
            if (meshAsset.Type != AssetType.Mesh) { Debug.LogError($"Mesh(Asset): Asset '{meshAsset.Name}' is not a Mesh."); return; }
            if (!_isLoaded) { Debug.LogError($"Mesh(Asset): Failed to init from asset '{meshAsset.Name}'. Base constructor likely failed due to invalid MeshData."); }
        }

        public void Bind()
        {
            if (!_isLoaded || _isDisposed) return;
            GL.BindVertexArray(VaoId);
        }

        public void Unbind()
        {
            if (_isDisposed) return;
            GL.BindVertexArray(0);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            if (VaoId != 0) { GL.DeleteVertexArray(VaoId); VaoId = 0; }
            if (VboId != 0) { GL.DeleteBuffer(VboId); VboId = 0; }
            if (EboId != 0) { GL.DeleteBuffer(EboId); EboId = 0; }
            _isLoaded = false; _isDisposed = true;
            GC.SuppressFinalize(this);
            GLDebug.CheckError("Mesh.Dispose");
        }
        ~Mesh() { if (!_isDisposed) Debug.LogWarning($"Mesh Finalizer for VAO {VaoId}. Explicit Dispose() not called."); }
    }

    internal static class AssetExtensions // Keep this helper
    {
        public static object LoadInternalOnly(this Asset asset)
        {
            if (!asset.IsLoaded) asset.Load();
            return asset.Data;
        }
    }
}