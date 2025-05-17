// ArcaneEngine/src/Renderering/Radiance.cs
using Arcane.SceneSystem;
using Arcane.Components;
using Arcane.Core; // For Debug
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using Arcane.Rendering; // For Material, Shader, Mesh, GLDebug, Skybox, Cubemap
using Arcane.AssetManager;
using Arcane.Renderering; // For Asset, AssetRegistry

// Note: Namespace was "Arcane.Renderering" (with 3 'r's), changed to "Arcane.Rendering" for consistency
namespace Arcane.Rendering
{
    public class Radiance : Renderer // Changed from Renderer to IRenderer to match interface name
    {
        // Example Light Structures (matching shader)
        private struct PointLightData
        {
            public Vector3 Position_World;
            public Vector3 Color;
            public float Intensity;
            public float Constant;
            public float Linear;
            public float Quadratic;
        }

        private struct DirLightData
        {
            public Vector3 Direction_World;
            public Vector3 Color;
            public float Intensity;
        }

        private List<PointLightData> _testPointLights = new List<PointLightData>();
        private DirLightData _testDirLight;
        private bool _useTestDirLight = true;

        private Skybox _skybox; // Added for IBL
        private Cubemap _environmentCubemap; // To store the initially loaded environment map

        // Define texture units for IBL maps, starting after material maps (0-5)
        private const TextureUnit IrradianceMapUnit = TextureUnit.Texture6;
        private const TextureUnit PrefilteredMapUnit = TextureUnit.Texture7;
        private const TextureUnit BrdfLutUnit = TextureUnit.Texture8;


        public void Init()
        {
            Debug.Log("Radiance Renderer: Initializing for PBR with IBL...");
            GL.ClearColor(0.1f, 0.1f, 0.15f, 1.0f);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less); // Default depth function
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Enable(EnableCap.TextureCubeMapSeamless); // Important for seamless cubemap filtering

            // Enable SRGB Framebuffer if outputting linear colors and display is sRGB
            // GL.Enable(EnableCap.FramebufferSrgb); 
            // GLDebug.CheckError("Radiance.Init - After FramebufferSrgb");

            int samples; // Use GL.GetInteger with the correct enum for multisample buffer samples
            GL.GetInteger(GetPName.Samples, out samples); // Use GetPName.Samples for default framebuffer
            if (samples > 0)
            {
                GL.Enable(EnableCap.Multisample);
                Debug.Log($"Radiance.Init: Multisample enabled by context ({samples} samples).");
            }
            else
            {
                Debug.LogWarning("Radiance.Init: Multisample not enabled by context or not supported.");
            }

            // Initialize test lights
            _testPointLights.Add(new PointLightData
            {
                Position_World = new Vector3(0.0f, 3.0f, 4.0f),
                Color = new Vector3(1.0f, 1.0f, 0.8f),
                Intensity = 100.0f,
                Constant = 1.0f,
                Linear = 0.09f,
                Quadratic = 0.032f
            });

            _testDirLight = new DirLightData
            {
                Direction_World = Vector3.Normalize(new Vector3(-0.5f, -1.0f, -0.3f)),
                Color = new Vector3(1.0f, 1.0f, 0.9f),
                Intensity = 1.5f
            };
            _useTestDirLight = true;

            // --- IBL Initialization ---
            try
            {
                // 1. Load an HDRI texture asset
                // Ensure "environment.hdr" or your chosen HDR file is in TestGame/Assets/Textures/HDR/
                // And that AssetRegistry scans this path.
                string projectRootPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
                string hdriRelativePath = Path.Combine("Textures", "HDR", "photosudio.hdr"); // Example path
                string fullHdriPathForRegistry = Path.Combine(projectRootPath, "TestGame", "Assets", hdriRelativePath);

                Asset hdriAsset = AssetRegistry.GetAssetMetadataByPath(fullHdriPathForRegistry);
                if (hdriAsset != null && hdriAsset.Type == AssetType.HdriTexture)
                {
                    // 2. Create a Cubemap from this HDRI asset
                    // The Cubemap constructor will handle the GPU conversion
                    // It's important that SkyboxUtils has initialized its shaders by this point,
                    // which should happen if shaders are loaded on demand or via an earlier init step.
                    // Cubemap constructor for HDRI uses SkyboxUtils.GetShader.
                    _environmentCubemap = new Cubemap(hdriAsset, 512); // 512 is a common cubemap size

                    if (_environmentCubemap.Id != 0)
                    {
                        // 3. Create _skybox = new Skybox(environmentCubemap);
                        // The Skybox constructor will generate Irradiance, Prefiltered, and BRDF LUT maps.
                        _skybox = new Skybox(_environmentCubemap);
                        Debug.Log("Radiance.Init: Skybox and IBL maps initialized successfully.");
                    }
                    else
                    {
                        Debug.LogError("Radiance.Init: Failed to create environment cubemap from HDRI asset.");
                        _skybox = null;
                    }
                }
                else
                {
                    Debug.LogError($"Radiance.Init: HDRI asset not found or not of type HdriTexture at '{fullHdriPathForRegistry}'. IBL will be disabled.");
                    _skybox = null;
                    _environmentCubemap = null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Radiance.Init: Exception during IBL setup: {ex.Message}\n{ex.StackTrace}");
                _skybox = null;
                _environmentCubemap = null;
            }
            // --- End IBL Initialization ---


            Debug.Log("Radiance Renderer: Initialized.");
        }

        public void Render(GameObject cameraGO, Scene scene)
        {
            if (cameraGO == null || scene == null)
            {
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                return;
            }

            CameraComponent cameraComponent = cameraGO.GetComponent<CameraComponent>();
            if (cameraComponent == null)
            {
                Debug.LogWarning($"Radiance.Render: Camera '{cameraGO.Name}' missing CameraComponent.");
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                return;
            }

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            Transform cameraTransform = cameraGO.transform;
            Matrix4 projectionMatrix = Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(cameraComponent.FovDegrees),
                cameraComponent.AspectRatio,
                cameraComponent.NearPlane,
                cameraComponent.FarPlane);
            Matrix4 viewMatrix = cameraTransform.localToWorldMatrix.Inverted();
            Vector3 cameraPosWorld = cameraTransform.position;

            // This flag helps to set global uniforms (like lights, IBL maps) only once per shader program if possible
            // However, with different materials potentially using different shader instances (even if same GLSL),
            // it's safer to set them after each material.Apply() or ensure a robust shader management system.
            // For IBL, BindIblMaps needs to be called when the PBR shader is active.

            foreach (GameObject rootGo in scene.RootGameObjects)
            {
                RenderGameObjectRecursive(rootGo, viewMatrix, projectionMatrix, cameraPosWorld);
            }

            // Render Skybox last (or first with depth func GL.LEQUAL)
            if (_skybox != null)
            {
                // Skybox Render changes depth function, so it's good practice to set it explicitly.
                // GL.DepthFunc(DepthFunction.Lequal); // Skybox.Render already does this
                _skybox.Render(viewMatrix, projectionMatrix);
                // GL.DepthFunc(DepthFunction.Less); // Restore default if changed
            }


            GL.BindVertexArray(0);
        }

        private void SetGlobalShaderUniforms(Shader shader, Vector3 cameraPosWorld)
        {
            if (shader == null || shader.ProgramId == 0) return;
            // This should be called AFTER shader.Use()

            shader.SetVector3("u_CameraPos_World", cameraPosWorld);

            // Directional Light
            shader.SetInt("u_UseDirLight", _useTestDirLight ? 1 : 0);
            if (_useTestDirLight)
            {
                shader.SetVector3("u_DirLight.Direction_World", _testDirLight.Direction_World);
                shader.SetVector3("u_DirLight.Color", _testDirLight.Color);
                shader.SetFloat("u_DirLight.Intensity", _testDirLight.Intensity);
            }

            // Point Lights
            int numActivePointLights = Math.Min(_testPointLights.Count, 4); // MAX_POINT_LIGHTS = 4 in shader
            shader.SetInt("u_NumPointLights", numActivePointLights);
            for (int i = 0; i < numActivePointLights; i++)
            {
                shader.SetVector3($"u_PointLights[{i}].Position_World", _testPointLights[i].Position_World);
                shader.SetVector3($"u_PointLights[{i}].Color", _testPointLights[i].Color);
                shader.SetFloat($"u_PointLights[{i}].Intensity", _testPointLights[i].Intensity);
                shader.SetFloat($"u_PointLights[{i}].Constant", _testPointLights[i].Constant);
                shader.SetFloat($"u_PointLights[{i}].Linear", _testPointLights[i].Linear);
                shader.SetFloat($"u_PointLights[{i}].Quadratic", _testPointLights[i].Quadratic);
            }
            GLDebug.CheckError("Radiance - After setting light uniforms");

            // Bind IBL maps if skybox is available
            if (_skybox != null)
            {
                _skybox.BindIblMaps(shader, (int)IrradianceMapUnit, (int)PrefilteredMapUnit, (int)BrdfLutUnit);
                GLDebug.CheckError("Radiance - After BindIblMaps");
            }
        }


        private void RenderGameObjectRecursive(GameObject go, Matrix4 viewMatrix, Matrix4 projectionMatrix, Vector3 cameraPosWorld)
        {
            if (go == null || !go.activeInHierarchy) return;

            MeshRendererComponent meshRenderer = go.GetComponent<MeshRendererComponent>();
            if (meshRenderer != null && meshRenderer.Mesh != null && meshRenderer.Material != null && meshRenderer.Material.Shader != null)
            {
                Mesh mesh = meshRenderer.Mesh;
                Material material = meshRenderer.Material;
                Shader shader = material.Shader;

                if (shader.ProgramId == 0)
                {
                    return;
                }

                material.Apply(); // This calls shader.Use()

                // Set global uniforms (lights, camera position, IBL maps) that depend on the shader being active
                SetGlobalShaderUniforms(shader, cameraPosWorld);

                // Set per-object uniforms (MVP)
                Matrix4 modelMatrix = go.transform.localToWorldMatrix;
                shader.SetMatrix4("u_ModelMatrix", modelMatrix);
                shader.SetMatrix4("u_ViewMatrix", viewMatrix);
                shader.SetMatrix4("u_ProjectionMatrix", projectionMatrix);

                // Optional: Normal Matrix for non-uniform scaling. Calculate if needed.
                // Matrix3 normalMatrix = new Matrix3(Matrix4.Transpose(modelMatrix.Inverted()));
                // shader.SetMatrix3("u_NormalMatrix_World", normalMatrix);


                mesh.Bind();
                if (mesh.IndexCount > 0)
                {
                    GL.DrawElements(PrimitiveType.Triangles, mesh.IndexCount, DrawElementsType.UnsignedInt, 0);
                    GLDebug.CheckError($"Radiance.Render - After DrawElements for {go.Name}");
                }
            }

            foreach (Transform childTransform in go.transform.Children)
            {
                RenderGameObjectRecursive(childTransform.gameObject, viewMatrix, projectionMatrix, cameraPosWorld);
            }
        }

        public void Cleanup()
        {
            Debug.Log("Radiance Renderer: Cleaning up...");

            _skybox?.Dispose(); // Dispose the skybox, which disposes its owned IBL maps
            _skybox = null;

            _environmentCubemap?.Dispose(); // Dispose the initially loaded environment cubemap
            _environmentCubemap = null;

            // This is crucial: SkyboxUtils manages shared shaders and meshes for IBL generation.
            // These should be cleaned up when the rendering context is about to be destroyed.
            SkyboxUtils.CleanupSharedResources();

            Debug.Log("Radiance Renderer: Cleanup complete.");
        }
    }
}
