using OpenTK.Graphics.OpenGL4;

namespace Arcane.Rendering
{
    public struct VertexAttribute
    {
        public int Location;    // Shader layout location
        public int Size;        // Number of components (e.g., 3 for Vector3)
        public VertexAttribPointerType Type; // e.g., Float
        public bool Normalized;
        public int Stride;      // Total size of one vertex in bytes
        public int Offset;      // Offset of this attribute in bytes

        public VertexAttribute(int location, int size, VertexAttribPointerType type, bool normalized, int stride, int offset)
        {
            Location = location;
            Size = size;
            Type = type;
            Normalized = normalized;
            Stride = stride;
            Offset = offset;
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

        public Mesh(float[] vertices, uint[] indices, VertexAttribute[] attributes)
        {
            VertexCount = vertices.Length / (attributes.Length > 0 ? attributes[0].Stride / sizeof(float) : 1); // Approximate based on first attribute's stride
            IndexCount = indices.Length;

            // Create VAO
            VaoId = GL.GenVertexArray();
            GL.BindVertexArray(VaoId);

            // Create VBO
            VboId = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VboId);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            // Create EBO
            EboId = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, EboId);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            // Set Vertex Attributes
            foreach (var attribute in attributes)
            {
                GL.VertexAttribPointer(attribute.Location, attribute.Size, attribute.Type, attribute.Normalized, attribute.Stride, attribute.Offset);
                GL.EnableVertexAttribArray(attribute.Location);
            }

            GL.BindVertexArray(0); // Unbind VAO
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0); // Unbind VBO
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0); // Unbind EBO (while VAO not bound)

            _isLoaded = true;
            // Arcane.Core.Debug.Log($"Mesh loaded: VAO={VaoId}, VBO={VboId}, EBO={EboId}, Verts={VertexCount}, Indices={IndexCount}");
        }

        // Simplified constructor for position-only meshes (example)
        public Mesh(float[] positions, uint[] indices)
        {
            // Assuming 3 floats per position, tightly packed.
            int stride = 3 * sizeof(float);
            VertexAttribute[] attributes = {
                new VertexAttribute(0, 3, VertexAttribPointerType.Float, false, stride, 0) // Position at location 0
            };

            VertexCount = positions.Length / 3;
            IndexCount = indices.Length;

            VaoId = GL.GenVertexArray();
            GL.BindVertexArray(VaoId);

            VboId = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VboId);
            GL.BufferData(BufferTarget.ArrayBuffer, positions.Length * sizeof(float), positions, BufferUsageHint.StaticDraw);

            EboId = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, EboId);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            foreach (var attribute in attributes)
            {
                GL.VertexAttribPointer(attribute.Location, attribute.Size, attribute.Type, attribute.Normalized, attribute.Stride, attribute.Offset);
                GL.EnableVertexAttribArray(attribute.Location);
            }

            GL.BindVertexArray(0);
            _isLoaded = true;
        }


        public void Bind()
        {
            if (!_isLoaded) return;
            GL.BindVertexArray(VaoId);
        }

        public void Unbind()
        {
            if (!_isLoaded) return;
            GL.BindVertexArray(0);
        }

        public void Dispose()
        {
            if (!_isLoaded) return;
            GL.DeleteVertexArray(VaoId);
            GL.DeleteBuffer(VboId);
            GL.DeleteBuffer(EboId);
            _isLoaded = false;
            // Arcane.Core.Debug.Log($"Mesh disposed: VAO={VaoId}");
            GC.SuppressFinalize(this);
        }

        ~Mesh()
        {
            // Arcane.Core.Debug.LogWarning($"Mesh (VAO {VaoId}) not disposed by user, finalizer called. This can cause issues if GL context is gone.");
            // Don't call GL.Delete* here as GL context might not be current or exist.
            // Proper disposal should happen via IDisposable.Dispose()
        }
    }
}
