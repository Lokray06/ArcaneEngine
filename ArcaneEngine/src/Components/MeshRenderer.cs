// Arcane/Components/MeshRendererComponent.cs
using Arcane.SceneSystem;
using Arcane.Rendering; // For Mesh, Material

namespace Arcane.Components
{
    public class MeshRendererComponent : Component
    {
        public Mesh Mesh { get; set; }
        public Material Material { get; set; }
        // public bool IsVisible { get; set; } = true; // Future use

        public MeshRendererComponent() { }

        public MeshRendererComponent(Mesh mesh, Material material)
        {
            Mesh = mesh;
            Material = material;
        }

        public override void OnDestroy()
        {
            // Note: The MeshRenderer component doesn't OWN the Mesh or Material assets.
            // It just references them. Asset disposal should be handled by an AssetManager
            // or when the scene is destroyed if they are scene-specific.
            // If this component were to create/own them, it would dispose them here.
            // System.Console.WriteLine($"MeshRendererComponent on '{gameObject.Name}' Destroyed.");
        }
    }
}
