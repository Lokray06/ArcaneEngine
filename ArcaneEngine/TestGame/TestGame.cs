using Arcane.SceneSystem;
using Arcane.Components;
using Arcane.Core;
using Arcane.Rendering; // Consistent namespace
using Arcane.AssetManager;
using OpenTK.Mathematics;
using System.IO;
using System;
using System.Collections.Generic; // Added for List

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

            // --- Load Sphere Mesh from OBJ ---
            string sphereObjRelativePath = Path.Combine("Models", "sphere.obj");
            string sphereObjPathForRegistry = Path.Combine(projectRootPath, "TestGame", "Assets", sphereObjRelativePath);
            Asset sphereAssetMetadata = AssetRegistry.GetAssetMetadataByPath(sphereObjPathForRegistry);
            Mesh sphereGpuMesh = null;
            if (sphereAssetMetadata != null) // Corrected from cubeAssetMetadata to sphereAssetMetadata
            {
                sphereGpuMesh = new Arcane.Rendering.Mesh(sphereAssetMetadata);
                if (!sphereGpuMesh.IsLoadedCorrectly())
                {
                    Debug.LogError($"Failed to create/load GPU mesh from asset: {sphereAssetMetadata.Name}. Check OBJ file.");
                    pbrShader.Dispose(); // Dispose pbrShader before returning
                    cubeGpuMesh?.Dispose(); // Dispose already created cubeGpuMesh
                    return;
                }
            }
            else
            {
                Debug.LogError($"OBJ asset metadata not found: {sphereObjPathForRegistry}. Ensure it's in TestGame/Assets/Models/.");
                pbrShader.Dispose();
                cubeGpuMesh?.Dispose();
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

            // --- Create PBR Materials ---
            Material shinyRedPlastic = new Material(pbrShader)
            {
                AlbedoMap = redAlbedoGpuTexture, // Assign loaded texture
                AlbedoColor = Vector3.One, // Use white if texture is used, or red as fallback if texture is null
                MetallicFactor = 0.1f,
                RoughnessFactor = 0.3f,
                AoFactor = 1.0f,
                UseNormalMap = false
            };
            if (redAlbedoGpuTexture == null) shinyRedPlastic.AlbedoColor = new Vector3(1.0f, 0.2f, 0.2f);


            Material stainlessSteelMaterial = new Material(pbrShader)
            {
                AlbedoMap = steelAlbedoMap,
                AlbedoColor = (steelAlbedoMap == null) ? new Vector3(0.56f, 0.57f, 0.58f) : Vector3.One,
                MetallicMap = steelMetallicMap,
                MetallicFactor = 1.0f, // Factor is used if map is null, map values are used if map is present
                RoughnessMap = steelRoughnessMap,
                RoughnessFactor = 1.0f, // Factor is used if map is null
                NormalMap = steelNormalMap,
                UseNormalMap = (steelNormalMap != null),
                AoMap = steelAoMap,
                AoFactor = 1.0f // Factor is used if map is null
            };

            // --- Scene Setup ---
            Scene testScene = new Scene("PBR_IBL_Full_Textured_Steel_Scene");

            // --- IBL and Skybox Setup for the Scene ---
            string hdriRelativePath = Path.Combine("Textures", "HDR", "lilienstein_8k.hdr");
            string fullHdriPathForRegistry = Path.Combine(projectRootPath, "TestGame", "Assets", hdriRelativePath);
            Asset hdriAsset = AssetRegistry.GetAssetMetadataByPath(fullHdriPathForRegistry);
            Cubemap environmentCubemapForScene = null;

            if (hdriAsset != null && hdriAsset.Type == AssetType.HdriTexture)
            {
                environmentCubemapForScene = new Cubemap(hdriAsset, 2048);
                if (environmentCubemapForScene.Id != 0)
                {
                    Skybox sceneSkybox = new Skybox(environmentCubemapForScene);
                    testScene.Skybox = sceneSkybox; // Assign to the scene
                    Debug.Log("TestGame: Scene Skybox initialized with photosudio.hdr");
                }
                else
                {
                    Debug.LogError("TestGame: Failed to create environment cubemap for the scene from HDRI asset.");
                }
            }
            else
            {
                Debug.LogError($"TestGame: HDRI asset for scene skybox not found at '{fullHdriPathForRegistry}'. Scene will have no skybox/IBL.");
            }

            // --- Camera Setup ---
            GameObject cameraGO = new GameObject("MainCamera");
            cameraGO.transform.localPosition = new Vector3(0, 1f, 5f); // Adjusted for better view of spheres too
            cameraGO.transform.LookAt(Vector3.Zero, Vector3.UnitY);
            var camComp = cameraGO.AddComponent<CameraComponent>();
            camComp.FarPlane = 2000f;
            cameraGO.AddComponent<CameraController>();
            testScene.AddGameObject(cameraGO);

            // --- Light Setup ---
            GameObject dirLightGO = new GameObject("SunLight");
            dirLightGO.transform.localEulerAngles = new Vector3(50, -30, 0); // Pointing downwards and from an angle
            var dirLightComp = dirLightGO.AddComponent<DirectionalLight>();
            dirLightComp.Color = new Vector3(1.0f, 0.95f, 0.85f);
            dirLightComp.Intensity = 2f;
            testScene.AddGameObject(dirLightGO);

            GameObject pointLightGO = new GameObject("PointLamp");
            pointLightGO.transform.localPosition = new Vector3(2.0f, 1.5f, 2.5f);
            var pointLightComp = pointLightGO.AddComponent<PointLight>();
            pointLightComp.Color = new Vector3(1.0f, 0.6f, 0.3f);
            pointLightComp.Intensity = 3.0f; // Point light intensities often need to be higher
            pointLightComp.Linear = 0.07f;
            pointLightComp.Quadratic = 0.017f;
            testScene.AddGameObject(pointLightGO);


            // --- Object Setup ---
            GameObject pbrCube1 = new GameObject("ShinyRedPlasticCube");
            pbrCube1.transform.localPosition = new Vector3(-1.5f, 0.5f, 0);
            pbrCube1.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            var pbrRenderer1 = pbrCube1.AddComponent<MeshRendererComponent>();
            pbrRenderer1.Mesh = cubeGpuMesh;
            pbrRenderer1.Material = shinyRedPlastic;
            testScene.AddGameObject(pbrCube1);

            GameObject pbrCube2 = new GameObject("StainlessSteelSphere"); // Renamed for clarity as it uses sphere mesh
            pbrCube2.transform.localPosition = new Vector3(1.5f, 0.5f, 0);
            pbrCube2.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            var pbrRenderer2 = pbrCube2.AddComponent<MeshRendererComponent>();
            pbrRenderer2.Mesh = sphereGpuMesh; // Using sphere mesh
            pbrRenderer2.Material = stainlessSteelMaterial;
            testScene.AddGameObject(pbrCube2);

            // --- Sphere Array for PBR Material Testing ---
            int numRows = 10;
            int numCols = 10;
            float sphereSpacing = 2f; // Increased spacing slightly
            float sphereDiameter = 0.6f; // Made spheres slightly larger
            Vector3 arrayCenter = new Vector3(0.0f, 2.8f, -3.0f); // Adjusted Y and Z

            float arrayWidth = (numCols - 1) * sphereSpacing;
            float arrayHeight = (numRows - 1) * sphereSpacing;
            Vector3 startGridPosition = new Vector3(
                arrayCenter.X - arrayWidth / 2.0f,
                arrayCenter.Y + arrayHeight / 2.0f,
                arrayCenter.Z
            );

            for (int row = 0; row < numRows; row++)
            {
                for (int col = 0; col < numCols; col++)
                {
                    float metallicValue = (float)row / (float)(numRows - 1);
                    float roughnessValue = (float)col / (float)(numCols - 1);

                    GameObject sphereGO = new GameObject($"Sphere_M{metallicValue:F2}_R{roughnessValue:F2}");
                    sphereGO.transform.localPosition = new Vector3(
                        startGridPosition.X + col * sphereSpacing,
                        startGridPosition.Y - row * sphereSpacing,
                        startGridPosition.Z
                    );
                    sphereGO.transform.localScale = new Vector3(sphereDiameter, sphereDiameter, sphereDiameter);

                    Material sphereMaterial = new Material(pbrShader)
                    {
                        AlbedoColor = new Vector3(1.0f, 0.1f, 0.1f), // Red
                        MetallicFactor = metallicValue,
                        RoughnessFactor = roughnessValue,
                        AoFactor = 1.0f,
                        UseNormalMap = false
                    };

                    var sphereRenderer = sphereGO.AddComponent<MeshRendererComponent>();
                    sphereRenderer.Mesh = sphereGpuMesh;
                    sphereRenderer.Material = sphereMaterial;
                    testScene.AddGameObject(sphereGO);
                }
            }

            SceneManager.LoadScene(testScene);
            engineInstance.RunLoop();

            // --- Cleanup ---
            // SceneManager.DestroyCurrentScene() is called by Engine.CleanUp(), which disposes scene.Skybox.
            // The pbrShader is shared by many materials. Materials don't own the shader if it's passed in.
            // So, dispose the shader once here.
            pbrShader?.Dispose();

            // Dispose meshes
            cubeGpuMesh?.Dispose();
            sphereGpuMesh?.Dispose();

            // Dispose loaded textures
            redAlbedoGpuTexture?.Dispose();
            steelAlbedoMap?.Dispose();
            steelMetallicMap?.Dispose();
            steelRoughnessMap?.Dispose();
            steelNormalMap?.Dispose();
            steelAoMap?.Dispose();

            // Dispose the environment cubemap created in this Main method
            environmentCubemapForScene?.Dispose();

            // Materials created in the loop are part of GameObjects which get destroyed by SceneManager.DestroyCurrentScene -> scene.DestroyScene().
            // Component.OnDestroy can handle component-specific cleanup. Material instances don't have GL resources other than shader ref and textures.
            // Textures are handled. Shader is handled.

            Debug.Log("Program Main: Finished PBR IBL Test with Full PBR Textures.");
        }

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
                    gpuTexture = null; // Ensure it's null if failed
                }
                // else { Debug.Log($"Successfully loaded texture: {relativeTexturePath}"); } // Can be verbose
            }
            else
            {
                Debug.LogWarning($"Texture asset metadata not found: {fullPath}");
            }
            return gpuTexture;
        }
    }

    public static class MeshExtensions
    {
        public static bool IsLoadedCorrectly(this Arcane.Rendering.Mesh mesh)
        {
            // A mesh is loaded correctly if it has a VAO, and some vertices and indices.
            // VertexCount might be 0 if vertices were not processed correctly by importer or if stride was 0.
            return mesh != null && mesh.VaoId != 0 && mesh.IndexCount > 0 && mesh.VertexCount > 0;
        }
    }
}