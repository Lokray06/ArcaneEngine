using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Arcane.Core; // For Debug
using System.Collections.Generic; // For Dictionary
using System.IO; // For Path
using System; // For AppDomain

namespace Arcane.Rendering
{
    public enum SkyboxShaderType
    {
        SkyboxRender,
        EquirectangularToCubemap,
        IrradianceConvolution,
        PrefilterEnvironmentMap,
        BrdfIntegration
    }

    public static class SkyboxUtils
    {
        private static Mesh _skyboxCubeMesh;
        private static Mesh _screenQuadMesh;
        private static Dictionary<SkyboxShaderType, Shader> _shaders = new Dictionary<SkyboxShaderType, Shader>();

        public static readonly Matrix4 CaptureProjection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(90.0f), 1.0f, 0.1f, 10.0f);

        // Corrected CaptureViews to potentially fix upside-down skybox issues.
        // Standard OpenGL: Right-Handed System. +X right, +Y up, +Z out of screen (towards viewer).
        // When rendering cubemap faces, the camera is at the origin.
        // Target: Direction the camera is looking.
        // Up: Defines the camera's orientation (which way is "up" for that view).
        public static readonly Matrix4[] CaptureViews = new Matrix4[]
        {
            Matrix4.LookAt(Vector3.Zero, Vector3.UnitX,  -Vector3.UnitY), // Positive X (Right face): Target +X, Up -Y (OpenGL screen coords Y is often inverted)
            Matrix4.LookAt(Vector3.Zero, -Vector3.UnitX, -Vector3.UnitY), // Negative X (Left face):  Target -X, Up -Y
            Matrix4.LookAt(Vector3.Zero, Vector3.UnitY,  Vector3.UnitZ),  // Positive Y (Top face):   Target +Y, Up +Z (World's +Z becomes "up" in the texture)
            Matrix4.LookAt(Vector3.Zero, -Vector3.UnitY, -Vector3.UnitZ), // Negative Y (Bottom face):Target -Y, Up -Z (World's -Z becomes "up" in the texture)
            Matrix4.LookAt(Vector3.Zero, Vector3.UnitZ,  -Vector3.UnitY), // Positive Z (Front face): Target +Z, Up -Y
            Matrix4.LookAt(Vector3.Zero, -Vector3.UnitZ, -Vector3.UnitY)  // Negative Z (Back face):  Target -Z, Up -Y
        };
        // Previous attempt for Y faces was:
        // Matrix4.LookAt(Vector3.Zero, Vector3.UnitY,  Vector3.UnitZ),  // Positive Y
        // Matrix4.LookAt(Vector3.Zero, -Vector3.UnitY, -Vector3.UnitZ), // Negative Y
        // If this still results in an upside-down image, the issue might be in how the equirectangular map
        // itself is sampled (e.g. if its V coordinate needs flipping during the initial conversion).
        // The current SampleSphericalMap in equirect_to_cubemap.frag uses `1.0 - v.y` effectively for latitude,
        // which is common. Let's stick to the views above which are standard for LearnOpenGL style cubemaps.

        public static Mesh GetSkyboxCube()
        {
            if (_skyboxCubeMesh == null || _skyboxCubeMesh.VaoId == 0)
            {
                float[] vertices = {
                    // positions          
                    -1.0f,  1.0f, -1.0f, -1.0f, -1.0f, -1.0f,  1.0f, -1.0f, -1.0f,
                     1.0f, -1.0f, -1.0f,  1.0f,  1.0f, -1.0f, -1.0f,  1.0f, -1.0f,

                    -1.0f, -1.0f,  1.0f, -1.0f, -1.0f, -1.0f, -1.0f,  1.0f, -1.0f,
                    -1.0f,  1.0f, -1.0f, -1.0f,  1.0f,  1.0f, -1.0f, -1.0f,  1.0f,

                     1.0f, -1.0f, -1.0f,  1.0f, -1.0f,  1.0f,  1.0f,  1.0f,  1.0f,
                     1.0f,  1.0f,  1.0f,  1.0f,  1.0f, -1.0f,  1.0f, -1.0f, -1.0f,

                    -1.0f, -1.0f,  1.0f, -1.0f,  1.0f,  1.0f,  1.0f,  1.0f,  1.0f,
                     1.0f,  1.0f,  1.0f,  1.0f, -1.0f,  1.0f, -1.0f, -1.0f,  1.0f,

                    -1.0f,  1.0f, -1.0f,  1.0f,  1.0f, -1.0f,  1.0f,  1.0f,  1.0f,
                     1.0f,  1.0f,  1.0f, -1.0f,  1.0f,  1.0f, -1.0f,  1.0f, -1.0f,

                    -1.0f, -1.0f, -1.0f, -1.0f, -1.0f,  1.0f,  1.0f, -1.0f, -1.0f,
                     1.0f, -1.0f, -1.0f, -1.0f, -1.0f,  1.0f,  1.0f, -1.0f,  1.0f
                };
                uint[] indices = new uint[36];
                for (uint i = 0; i < 36; ++i) indices[i] = i;

                VertexAttribute[] attributes = { new VertexAttribute(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0) };
                _skyboxCubeMesh = new Mesh(vertices, indices, attributes);
                Debug.Log("SkyboxUtils: Skybox cube mesh created.");
            }
            return _skyboxCubeMesh;
        }

        public static Mesh GetScreenQuad()
        {
            if (_screenQuadMesh == null || _screenQuadMesh.VaoId == 0)
            {
                float[] quadVertices = {
                    -1.0f,  1.0f, 0.0f, 0.0f, 1.0f,
                    -1.0f, -1.0f, 0.0f, 0.0f, 0.0f,
                     1.0f, -1.0f, 0.0f, 1.0f, 0.0f,
                     1.0f,  1.0f, 0.0f, 1.0f, 1.0f
                };
                uint[] quadIndices = { 0, 1, 2, 0, 2, 3 };
                VertexAttribute[] attributes = {
                    new VertexAttribute(0, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0),
                    new VertexAttribute(1, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float))
                };
                _screenQuadMesh = new Mesh(quadVertices, quadIndices, attributes);
                Debug.Log("SkyboxUtils: Screen quad mesh created.");
            }
            return _screenQuadMesh;
        }

        public static Shader GetShader(SkyboxShaderType type)
        {
            if (_shaders.TryGetValue(type, out Shader shader) && shader != null && shader.ProgramId != 0)
            {
                return shader;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
            string vertPath = "";
            string fragPath = "";

            switch (type)
            {
                case SkyboxShaderType.SkyboxRender:
                    vertPath = Path.Combine(projectRoot, "res", "shaders", "skybox", "skybox.vert");
                    fragPath = Path.Combine(projectRoot, "res", "shaders", "skybox", "skybox.frag");
                    break;
                case SkyboxShaderType.EquirectangularToCubemap:
                    vertPath = Path.Combine(projectRoot, "res", "shaders", "skybox", "cubemap_conversion.vert");
                    fragPath = Path.Combine(projectRoot, "res", "shaders", "skybox", "equirect_to_cubemap.frag");
                    break;
                case SkyboxShaderType.IrradianceConvolution:
                    vertPath = Path.Combine(projectRoot, "res", "shaders", "skybox", "cubemap_conversion.vert");
                    fragPath = Path.Combine(projectRoot, "res", "shaders", "skybox", "irradiance_convolution.frag");
                    break;
                case SkyboxShaderType.PrefilterEnvironmentMap:
                    vertPath = Path.Combine(projectRoot, "res", "shaders", "skybox", "cubemap_conversion.vert");
                    fragPath = Path.Combine(projectRoot, "res", "shaders", "skybox", "prefilter_envmap.frag");
                    break;
                case SkyboxShaderType.BrdfIntegration:
                    vertPath = Path.Combine(projectRoot, "res", "shaders", "skybox", "brdf_integration.vert");
                    fragPath = Path.Combine(projectRoot, "res", "shaders", "skybox", "brdf_integration.frag");
                    break;
                default:
                    Debug.LogError($"SkyboxUtils: Unknown shader type requested: {type}");
                    return CreateFallbackShader();
            }

            if (!File.Exists(vertPath) || !File.Exists(fragPath))
            {
                Debug.LogError($"SkyboxUtils: Shader files not found for type {type}. Vert: '{vertPath}', Frag: '{fragPath}'");
                _shaders[type] = CreateFallbackShader();
                return _shaders[type];
            }

            shader = new Shader(vertPath, fragPath, true);
            if (shader.ProgramId == 0)
            {
                Debug.LogError($"SkyboxUtils: Failed to compile shader type {type} from files. Using fallback.");
                shader.Dispose();
                _shaders[type] = CreateFallbackShader();
                return _shaders[type];
            }

            _shaders[type] = shader;
            Debug.Log($"SkyboxUtils: Shader for {type} loaded. ID: {shader.ProgramId}");
            return shader;
        }

        private static Shader CreateFallbackShader()
        {
            const string fallbackVertSrc = @"#version 330 core
                                        layout (location = 0) in vec3 a_Position;
                                        uniform mat4 u_ProjectionMatrix;
                                        uniform mat4 u_ViewMatrix;
                                        void main() { gl_Position = u_ProjectionMatrix * u_ViewMatrix * vec4(a_Position, 1.0); }";
            const string fallbackFragSrc = @"#version 330 core
                                        out vec4 FragColor;
                                        void main() { FragColor = vec4(0.8, 0.2, 0.8, 1.0); }";

            SkyboxShaderType fallbackKey = (SkyboxShaderType)(-1); // Use a distinct key for the generic fallback
            if (_shaders.TryGetValue(fallbackKey, out Shader existingFallback) && existingFallback != null && existingFallback.ProgramId != 0)
            {
                return existingFallback;
            }

            var newFallbackShader = new Shader(fallbackVertSrc, fallbackFragSrc);
            if (newFallbackShader.ProgramId == 0) Debug.LogError("CRITICAL: Fallback shader failed to compile!");
            else Debug.Log("SkyboxUtils: Created and using a generic fallback shader.");

            _shaders[fallbackKey] = newFallbackShader;
            return newFallbackShader;
        }

        public static void CleanupSharedResources()
        {
            _skyboxCubeMesh?.Dispose();
            _skyboxCubeMesh = null;
            _screenQuadMesh?.Dispose();
            _screenQuadMesh = null;

            foreach (var kvp in _shaders)
            {
                kvp.Value?.Dispose();
            }
            _shaders.Clear();
            Debug.Log("SkyboxUtils: Shared resources cleaned up.");
        }
    }
}