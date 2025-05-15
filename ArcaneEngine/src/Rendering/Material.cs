// Arcane/Rendering/Material.cs
using OpenTK.Mathematics;

namespace Arcane.Rendering
{
    public class Material
    {
        public Shader Shader { get; set; }
        public Vector3 Color { get; set; } = Vector3.One; // Default to white
        // Add other properties like Texture, Shininess, etc. later

        public Material(Shader shader)
        {
            Shader = shader;
        }

        public void Apply() // Called by the renderer before drawing
        {
            Shader?.Use();
            // Set material-specific uniforms
            if (Shader != null)
            {
                Shader.SetVector3("objectColor", Color); // Example uniform
                // Shader.SetTexture("mainTex", Texture?.Id ?? 0, 0); // Example
            }
        }
    }
}
