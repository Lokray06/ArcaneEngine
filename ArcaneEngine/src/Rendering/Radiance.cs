// Arcane/RendereringPipeline/Radiance.cs
using Arcane.SceneSystem;
using Arcane.Components; // For MeshRendererComponent, CameraComponent, Transform
using Arcane.Rendering; // For Mesh, Material, Shader
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using Arcane.Core; // For Debug

namespace Arcane.Renderering
{
    public class Radiance : Renderer // Assuming Renderer base class or interface exists
    {
        // No internal cube data anymore

        public void Init()
        {
            Debug.Log("Radiance Renderer: Initializing global GL states...");
            GL.Enable(EnableCap.DepthTest);
            GL.ClearColor(0.05f, 0.05f, 0.1f, 1.0f); // Slightly different default background
        }

        public void Render(GameObject cameraObject, Scene scene)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            if (cameraObject == null || scene == null)
            {
                Debug.LogWarning("Radiance.Render: No camera or scene provided.");
                return;
            }

            var cameraTransform = cameraObject.transform;
            var cameraComponent = cameraObject.GetComponent<CameraComponent>();

            if (cameraTransform == null || cameraComponent == null)
            {
                Debug.LogWarning("Radiance.Render: Camera GameObject is missing Transform or CameraComponent.");
                return;
            }

            // --- Setup Camera Matrices ---
            Matrix4 viewMatrix = Matrix4.LookAt(
                cameraTransform.position,
                cameraTransform.position + cameraTransform.forward,
                cameraTransform.up);

            Matrix4 projectionMatrix = Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(cameraComponent.FovDegrees),
                cameraComponent.AspectRatio,
                cameraComponent.NearPlane,
                cameraComponent.FarPlane);

            // --- Iterate and Render GameObjects with MeshRenderers ---
            foreach (var rootGo in scene.RootGameObjects)
            {
                RenderGameObjectRecursive(rootGo, viewMatrix, projectionMatrix);
            }
        }

        private void RenderGameObjectRecursive(GameObject go, Matrix4 viewMatrix, Matrix4 projectionMatrix)
        {
            if (go == null || !go.activeInHierarchy)
            {
                return;
            }

            var meshRenderer = go.GetComponent<MeshRendererComponent>();
            if (meshRenderer != null && meshRenderer.Mesh != null && meshRenderer.Material != null && meshRenderer.Material.Shader != null)
            {
                Mesh mesh = meshRenderer.Mesh;
                Material material = meshRenderer.Material;
                Shader shader = material.Shader;

                shader.Use(); // Activate the shader

                // Set common uniforms
                shader.SetMatrix4("view", viewMatrix);
                shader.SetMatrix4("projection", projectionMatrix);

                Matrix4 modelMatrix = go.transform.localToWorldMatrix;
                shader.SetMatrix4("model", modelMatrix);

                // Apply material properties (e.g., color, textures)
                material.Apply(); // This will call shader.SetVector3("objectColor", Color) etc.

                mesh.Bind(); // Bind VAO (and EBO)
                GL.DrawElements(PrimitiveType.Triangles, mesh.IndexCount, DrawElementsType.UnsignedInt, 0);
                mesh.Unbind(); // Unbind VAO

                Debug.Log($"GameObject: {go.Name}");
                Debug.Log($"Model Matrix: \n{modelMatrix.ToString()}");
                Debug.Log($"View Matrix: \n{viewMatrix.ToString()}");
                Debug.Log($"Projection Matrix: \n{projectionMatrix.ToString()}");

                shader.SetMatrix4("model", modelMatrix);
                shader.SetMatrix4("view", viewMatrix);
                shader.SetMatrix4("projection", projectionMatrix);  

                // GL.UseProgram(0); // Unbind shader - usually done once per frame or if switching shaders often
            }

            // Recursively render children
            foreach (var childTransform in go.transform.Children)
            {
                RenderGameObjectRecursive(childTransform.gameObject, viewMatrix, projectionMatrix);
            }
        }

        public void Cleanup()
        {
            Debug.Log("Radiance Renderer: Cleanup (if any renderer-specific resources were created).");
            // If Radiance created any of its own persistent GL resources (e.g., default error shader), clean them here.
            // For now, it doesn't have any.
        }
    }
}
