using Arcane.SceneSystem;
using OpenTK.Mathematics;

namespace Arcane.Components
{
    public class PointLight : Component
    {
        public Vector3 Color { get; set; } = Vector3.One;
        public float Intensity { get; set; } = 1.0f;
        // Attenuation factors - set sensible defaults or adjust as needed
        public float Constant { get; set; } = 1.0f;
        public float Linear { get; set; } = 0.09f;
        public float Quadratic { get; set; } = 0.032f;

        public PointLight() { }

        public PointLight(Vector3 color, float intensity, float linear = 0.09f, float quadratic = 0.032f)
        {
            Color = color;
            Intensity = intensity;
            Linear = linear;
            Quadratic = quadratic;
        }
    }
}