using Arcane.SceneSystem;
using OpenTK.Mathematics;

namespace Arcane.Components
{
    public class DirectionalLight : Component
    {
        public Vector3 Color { get; set; } = Vector3.One;
        public float Intensity { get; set; } = 1.0f;
        // Direction is derived from the GameObject's transform.forward

        public DirectionalLight() { }

        public DirectionalLight(Vector3 color, float intensity)
        {
            Color = color;
            Intensity = intensity;
        }
    }
}