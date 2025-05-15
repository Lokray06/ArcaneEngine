using Arcane.SceneSystem;
using Arcane.Components;
using Arcane.Core;
using Arcane.Rendering;
using OpenTK.Mathematics;

namespace TestGame
{
    public class TestGame
    {
        // --- Cube Data (Example) ---
        // Positions only for this simple example
        private static readonly float[] _cubeVertexPositions =
        {
            // Front face
            -0.5f, -0.5f,  0.5f, // Bottom-left
             0.5f, -0.5f,  0.5f, // Bottom-right
             0.5f,  0.5f,  0.5f, // Top-right
            -0.5f,  0.5f,  0.5f, // Top-left
            // Back face
            -0.5f, -0.5f, -0.5f,
             0.5f, -0.5f, -0.5f,
             0.5f,  0.5f, -0.5f,
            -0.5f,  0.5f, -0.5f
        };
        // If using interleaved data (Position + Color like in Radiance's old internal cube)
        // private static readonly float[] _cubeVerticesInterleaved = { /* ... your full vertex data ... */ };

        private static readonly uint[] _cubeIndices =
        {
            0, 1, 2,  0, 2, 3, // Front
            1, 5, 6,  1, 6, 2, // Right
            5, 4, 7,  5, 7, 6, // Back
            4, 0, 3,  4, 3, 7, // Left
            3, 2, 6,  3, 6, 7, // Top
            4, 5, 1,  4, 1, 0  // Bottom
        };

        public static void Main(string[] args)
        {
            Debug.Log("Program Start: Initializing Arcane Engine...");

            Engine engineInstance = new Engine();
            engineInstance.Initialize(showFpsInTitle: true); // Show FPS in title

            // --- Create Shared Assets (Mesh, Shader, Material) ---
            // Define vertex attributes for a simple position-only mesh
            // The Shader class now has a basic hardcoded shader.
            // The Mesh class constructor needs attributes.
            VertexAttribute[] cubeAttributes = {
                // Location 0: Position (3 floats), Stride = 3*sizeof(float), Offset = 0
                new VertexAttribute(0, 3, OpenTK.Graphics.OpenGL4.VertexAttribPointerType.Float, false, 3 * sizeof(float), 0)
            };
            // If using interleaved data (e.g., Pos+Color), the stride and offsets would change.
            // For example, if Pos(3)+Color(3): stride = 6*sizeof(float)
            // Pos: loc 0, size 3, offset 0
            // Color: loc 1, size 3, offset 3*sizeof(float)

            Mesh cubeMesh = new Mesh(_cubeVertexPositions, _cubeIndices, cubeAttributes);

            // Basic Shader (using the hardcoded one in Shader.cs for now)
            Shader basicShader = new Shader("path/to/vert.glsl", "path/to/frag.glsl", isFilePath: true); // Paths are ignored for now

            Material redMaterial = new Material(basicShader) { Color = new Vector3(1.0f, 0.2f, 0.2f) };
            Material blueMaterial = new Material(basicShader) { Color = new Vector3(0.2f, 0.2f, 1.0f) };


            // 1. Create a new scene
            Scene testScene = new Scene("DataDrivenTestScene");

            // 2. Create a Camera GameObject
            GameObject camera = new GameObject("MainCamera");
            camera.transform.localPosition = new Vector3(0, 1f, 4f);
            camera.transform.localRotation = Quaternion.FromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(180f)) * Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(-10f));
            camera.AddComponent<CameraComponent>();
            testScene.AddGameObject(camera);

            // 3. Create a spinning cube GameObject
            GameObject spinningCubeGO = new GameObject("SpinningRedCube");
            spinningCubeGO.transform.localPosition = new Vector3(-1.0f, 0, 0);
            var spinner = spinningCubeGO.AddComponent<SpinnerComponent>();
            spinner.RotationSpeed = 45.0f;
            // Add MeshRenderer to make it visible
            var spinningCubeRenderer = spinningCubeGO.AddComponent<MeshRendererComponent>();
            spinningCubeRenderer.Mesh = cubeMesh; // Use the shared mesh
            spinningCubeRenderer.Material = redMaterial; // Use the red material
            testScene.AddGameObject(spinningCubeGO);

            // 4. Create a static child cube
            GameObject childCubeGO = new GameObject("StaticBlueChildCube");
            childCubeGO.transform.SetParent(spinningCubeGO.transform, worldPositionStays: false);
            childCubeGO.transform.localPosition = new Vector3(1.5f, 0, 0);
            childCubeGO.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            var childCubeRenderer = childCubeGO.AddComponent<MeshRendererComponent>();
            childCubeRenderer.Mesh = cubeMesh; // Reuse the same mesh
            childCubeRenderer.Material = blueMaterial; // Use the blue material
            testScene.AddGameObject(childCubeGO); // Add to scene so it's processed (if scene doesn't auto-process children of added GOs)

            // 5. Load the scene using SceneManager
            SceneManager.LoadScene(testScene);

            // 6. Run the engine loop
            engineInstance.RunLoop();

            // Cleanup (Engine.CleanUp calls SceneManager.DestroyCurrentScene)
            // Assets like Mesh, Shader, Material might need explicit disposal if managed outside the scene
            // For this example, let's assume they are disposed when the app closes or manually.
            // A proper AssetManager would handle this.
            cubeMesh.Dispose();
            basicShader.Dispose();
            // Materials don't own shaders in this setup, so no material.Dispose() needed unless they hold other resources.

            Debug.Log("Program Main: Finished.");
        }
    }
}
