// Arcane/Components/CameraComponent.cs
using Arcane.SceneSystem; // For Component base class
using System.Numerics;    // For potential future use with math types if needed

namespace Arcane.Components
{
    public class CameraComponent : Component
    {
        /// <summary>
        /// Vertical field of view in degrees.
        /// </summary>
        public float FovDegrees { get; set; } = 60.0f;

        /// <summary>
        /// The aspect ratio (width / height) of the camera's view.
        /// This should typically be updated by the Engine or Renderer based on the window size.
        /// </summary>
        public float AspectRatio { get; set; } = 16.0f / 9.0f; // Default aspect ratio

        /// <summary>
        /// The near clipping plane distance. Objects closer than this will not be rendered.
        /// </summary>
        public float NearPlane { get; set; } = 0.1f;

        /// <summary>
        /// The far clipping plane distance. Objects further than this will not be rendered.
        /// </summary>
        public float FarPlane { get; set; } = 1000.0f;

        /// <summary>
        /// Is this the primary camera used for rendering the main scene?
        /// A more complex system might use layers, render targets, or a CameraManager.
        /// For now, this can be a simple flag if you need to distinguish.
        /// </summary>
        public bool IsMainCamera { get; set; } = false; // Default to false, set to true for the main rendering camera

        // --- Lifecycle Methods (Override if needed) ---

        public override void OnAwake()
        {
            // System.Console.WriteLine($"CameraComponent on '{gameObject.Name}' Awoken.");
            // You could potentially register this camera with a CameraManager here
            // or set it as the SceneManager.MainCamera if IsMainCamera is true and no other main camera exists.
            // For example:
            // if (IsMainCamera && Arcane.SceneSystem.SceneManager.MainCamera == null)
            // {
            //     Arcane.SceneSystem.SceneManager.SetMainCamera(gameObject); // Assuming SceneManager has such a method
            // }
        }

        public override void Start()
        {
            // System.Console.WriteLine($"CameraComponent on '{gameObject.Name}' Started.");
        }

        public override void OnDestroy()
        {
            // System.Console.WriteLine($"CameraComponent on '{gameObject.Name}' Destroyed.");
            // If this was the main camera, clear it from SceneManager
            // if (IsMainCamera && Arcane.SceneSystem.SceneManager.MainCamera == this.gameObject)
            // {
            //      Arcane.SceneSystem.SceneManager.ClearMainCamera(); // Assuming SceneManager has such a method
            // }
        }

        // Note: Update() and FixedUpdate() are often not needed for a basic camera component,
        // as its properties are usually static or driven by other scripts (e.g., a camera controller script).
        // The Transform of the GameObject it's attached to controls its position and orientation.
    }
}
