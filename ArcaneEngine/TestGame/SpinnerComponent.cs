using Arcane.SceneSystem;
using Arcane.Core; // Assuming your Time class is in Arcane.Core
using OpenTK.Mathematics;

namespace TestGame
{
    public class SpinnerComponent : Component
    {
        public float RotationSpeed { get; set; } = 45.0f; // Degrees per second
        public Vector3 RotationAxis { get; set; } = Vector3.UnitY; // Default to spinning around Y axis

        public override void OnAwake()
        {
            System.Console.WriteLine($"SpinnerComponent on '{gameObject.Name}' Awoken!");
        }

        public override void Start()
        {
            System.Console.WriteLine($"SpinnerComponent on '{gameObject.Name}' Started!");
        }

        public override void Update()
        {
            if (gameObject != null && gameObject.transform != null)
            {
                // Calculate rotation delta for this frame
                float angleDeltaDegrees = RotationSpeed * (float)Time.rawDeltaTimeSeconds; // Assuming Time.rawDeltaTimeSeconds is available
                Quaternion rotationDelta = Quaternion.FromAxisAngle(Vector3.Normalize(RotationAxis), MathHelper.DegreesToRadians(angleDeltaDegrees));

                // Apply rotation in local space
                gameObject.transform.localRotation *= rotationDelta;
            }
        }

        public override void OnDestroy()
        {
            System.Console.WriteLine($"SpinnerComponent on '{gameObject.Name}' Destroyed!");
        }
    }
}
