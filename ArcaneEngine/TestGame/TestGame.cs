using Arcane.SceneSystem;
using Arcane.Components;
using Arcane.Core;
using Arcane.Rendering; // Consistent namespace
using Arcane.AssetManager;
using OpenTK.Mathematics;
using System.IO;
using System;

namespace TestGame
{
    public class TestGame
    {
        public static void Main(string[] args)
        {
            Debug.Log("Program Start: Initializing Arcane Engine for PBR Test with IBL...");
            Engine engineInstance = new Engine();
            engineInstance.Initialize(4, showFpsInTitle: true); // MSAA samples, show FPS

            // --- Determine Project Root and Shader Paths ---
            string projectRootPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
            string pbrVertexShaderPath = Path.Combine(projectRootPath, "res", "shaders", "radiance", "main.vert");
            string pbrFragmentShaderPath = Path.Combine(projectRootPath, "res", "shaders", "radiance", "main.frag");

            Shader pbrShader = new Shader(pbrVertexShaderPath, pbrFragmentShaderPath, isFilePath: true);
            if (pbrShader.ProgramId == 0)
            {
                Debug.LogError("Failed to load PBR shaders. Exiting.");
                return;
            }

            // --- Load Cube Mesh from OBJ ---
            string cubeObjRelativePath = Path.Combine("Models", "box.obj");
            string fullCubeObjPathForRegistry = Path.Combine(projectRootPath, "TestGame", "Assets", cubeObjRelativePath);
            Asset cubeAssetMetadata = AssetRegistry.GetAssetMetadataByPath(fullCubeObjPathForRegistry);
            Mesh cubeGpuMesh = null;
            if (cubeAssetMetadata != null)
            {
                cubeGpuMesh = new Arcane.Rendering.Mesh(cubeAssetMetadata);
                if (!cubeGpuMesh.IsLoadedCorrectly())
                {
                    Debug.LogError($"Failed to create/load GPU mesh from asset: {cubeAssetMetadata.Name}. Check OBJ file.");
                    pbrShader.Dispose();
                    return;
                }
            }
            else
            {
                Debug.LogError($"OBJ asset metadata not found: {fullCubeObjPathForRegistry}. Ensure it's in TestGame/Assets/Models/.");
                pbrShader.Dispose();
                return;
            }

            // --- Load Cube Mesh from OBJ ---
            string sphereObjRelativePath = Path.Combine("Models", "sphere.obj");
            string sphereObjPathForRegistry = Path.Combine(projectRootPath, "TestGame", "Assets", sphereObjRelativePath);
            Asset sphereAssetMetadata = AssetRegistry.GetAssetMetadataByPath(sphereObjPathForRegistry);
            Mesh sphereGpuMesh = null;
            if (cubeAssetMetadata != null)
            {
                sphereGpuMesh = new Arcane.Rendering.Mesh(sphereAssetMetadata);
                if (!sphereGpuMesh.IsLoadedCorrectly())
                {
                    Debug.LogError($"Failed to create/load GPU mesh from asset: {sphereAssetMetadata.Name}. Check OBJ file.");
                    pbrShader.Dispose();
                    return;
                }
            }
            else
            {
                Debug.LogError($"OBJ asset metadata not found: {sphereObjPathForRegistry}. Ensure it's in TestGame/Assets/Models/.");
                pbrShader.Dispose();
                return;
            }

            // --- Load Textures for Shiny Red Plastic Material ---
            string redAlbedoTextureRelativePath = Path.Combine("Textures", "Prototype", "Red", "texture_01.png");
            string fullRedTexturePathForRegistry = Path.Combine(projectRootPath, "TestGame", "Assets", redAlbedoTextureRelativePath);
            Asset redTextureMetadata = AssetRegistry.GetAssetMetadataByPath(fullRedTexturePathForRegistry);
            Texture redAlbedoGpuTexture = null;
            if (redTextureMetadata != null)
            {
                redAlbedoGpuTexture = new Texture(redTextureMetadata);
                if (redAlbedoGpuTexture.Id == 0) redAlbedoGpuTexture = null;
            }
            else { Debug.LogWarning($"Texture asset metadata not found: {fullRedTexturePathForRegistry}"); }

            // --- Load Textures for "used-stainless-steel-bl" Material ---
            string steelTextureBasePath = Path.Combine("Textures", "PBR", "used-stainless-steel-bl");

            Texture steelAlbedoMap = LoadTextureHelper(projectRootPath, Path.Combine(steelTextureBasePath, "used-stainless-steel_albedo.png"));
            Texture steelMetallicMap = LoadTextureHelper(projectRootPath, Path.Combine(steelTextureBasePath, "used-stainless-steel_metallic.png"));
            Texture steelRoughnessMap = LoadTextureHelper(projectRootPath, Path.Combine(steelTextureBasePath, "used-stainless-steel_roughness.png"));
            Texture steelNormalMap = LoadTextureHelper(projectRootPath, Path.Combine(steelTextureBasePath, "used-stainless-steel_normal-ogl.png"));
            Texture steelAoMap = LoadTextureHelper(projectRootPath, Path.Combine(steelTextureBasePath, "used-stainless-steel_ao.png"));
            // Height map (used-stainless-steel_height.png) is not directly used by the current PBR shader,
            // but could be used for parallax mapping or displacement mapping in the future.

            // --- Create PBR Materials ---
            Material shinyRedPlastic = new Material(pbrShader)
            {
                AlbedoMap = null,
                AlbedoColor = (redAlbedoGpuTexture == null) ? new Vector3(1.0f, 0.2f, 0.2f) : Vector3.One,
                MetallicFactor = 1.0f, // Non-metallic
                RoughnessFactor = 1f,
                AoFactor = 1.0f,
                UseNormalMap = false // Assuming prototype red doesn't have a specific normal map
            };

            Material stainlessSteelMaterial = new Material(pbrShader)
            {
                AlbedoMap = steelAlbedoMap,
                AlbedoColor = (steelAlbedoMap == null) ? new Vector3(0.56f, 0.57f, 0.58f) : Vector3.One, // Typical steel albedo if map fails

                MetallicMap = steelMetallicMap,
                MetallicFactor = (steelMetallicMap == null) ? 1.0f : 1.0f, // Steel is metallic, map overrides factor if present

                RoughnessMap = steelRoughnessMap,
                RoughnessFactor = (steelRoughnessMap == null) ? 0.3f : 1.0f, // Map overrides factor

                NormalMap = steelNormalMap,
                UseNormalMap = (steelNormalMap != null),

                AoMap = steelAoMap,
                AoFactor = (steelAoMap == null) ? 1.0f : 1.0f, // Map overrides factor
            };


            // --- Scene Setup ---
            Scene testScene = new Scene("PBR_IBL_Full_Textured_Steel_Scene");

            GameObject cameraGO = new GameObject("MainCamera");
            cameraGO.transform.localPosition = new Vector3(0, 1f, 5f);
            cameraGO.transform.LookAt(Vector3.Zero, Vector3.UnitY);
            var camComp = cameraGO.AddComponent<CameraComponent>();
            camComp.FarPlane = 2000f;
            cameraGO.AddComponent<CameraController>();
            testScene.AddGameObject(cameraGO);

            GameObject pbrCube1 = new GameObject("ShinyRedPlasticCube");
            pbrCube1.transform.localPosition = new Vector3(-1.5f, 0.5f, 0);
            pbrCube1.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f); // Adjusted scale
            // pbrCube1.AddComponent<SpinnerComponent>().RotationSpeed = 20.0f;
            var pbrRenderer1 = pbrCube1.AddComponent<MeshRendererComponent>();
            pbrRenderer1.Mesh = cubeGpuMesh;
            pbrRenderer1.Material = shinyRedPlastic;
            testScene.AddGameObject(pbrCube1);

            GameObject pbrCube2 = new GameObject("StainlessSteelCube");
            pbrCube2.transform.localPosition = new Vector3(1.5f, 0.5f, 0);
            pbrCube2.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f); // Adjusted scale
            // pbrCube2.AddComponent<SpinnerComponent>().RotationSpeed = -25.0f;
            var pbrRenderer2 = pbrCube2.AddComponent<MeshRendererComponent>();
            pbrRenderer2.Mesh = sphereGpuMesh;
            pbrRenderer2.Material = stainlessSteelMaterial;
            testScene.AddGameObject(pbrCube2);

            SceneManager.LoadScene(testScene);
            engineInstance.RunLoop();

            // --- Cleanup ---
            pbrShader.Dispose();
            cubeGpuMesh?.Dispose();
            redAlbedoGpuTexture?.Dispose();

            steelAlbedoMap?.Dispose();
            steelMetallicMap?.Dispose();
            steelRoughnessMap?.Dispose();
            steelNormalMap?.Dispose();
            steelAoMap?.Dispose();

            // Materials are IDisposable. Since the pbrShader is shared, materials should not dispose it.
            // The Material class's Dispose method was already updated not to dispose shared shaders if not owned.
            // If materials created other GL resources themselves, they'd be disposed here or by the scene.
            // shinyRedPlastic.Dispose(); // Not strictly needed if it doesn't own unique GL resources beyond textures already handled
            // stainlessSteelMaterial.Dispose();

            Debug.Log("Program Main: Finished PBR IBL Test with Full PBR Textures.");
        }

        // Helper method to load textures to reduce boilerplate
        private static Texture LoadTextureHelper(string projectRoot, string relativeTexturePath)
        {
            string fullPath = Path.Combine(projectRoot, "TestGame", "Assets", relativeTexturePath);
            Asset textureAssetMetadata = AssetRegistry.GetAssetMetadataByPath(fullPath);
            Texture gpuTexture = null;
            if (textureAssetMetadata != null)
            {
                gpuTexture = new Texture(textureAssetMetadata);
                if (gpuTexture.Id == 0)
                {
                    Debug.LogWarning($"Failed to load GPU texture for: {relativeTexturePath}");
                    gpuTexture = null;
                }
                else
                {
                    Debug.Log($"Successfully loaded texture: {relativeTexturePath}");
                }
            }
            else
            {
                Debug.LogWarning($"Texture asset metadata not found: {fullPath}");
            }
            return gpuTexture;
        }
    }

    // Helper extension method for Mesh
    public static class MeshExtensions
    {
        public static bool IsLoadedCorrectly(this Arcane.Rendering.Mesh mesh)
        {
            return mesh != null && mesh.VaoId != 0 && mesh.IndexCount > 0 && mesh.VertexCount > 0;
        }
    }
}
