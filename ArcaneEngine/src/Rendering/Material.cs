// ArcaneEngine/src/Rendering/Material.cs
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using System;
using Arcane.Core; // For Debug

namespace Arcane.Rendering
{
    public class Material : IDisposable
    {
        public Shader Shader { get; set; }

        // --- Albedo ---
        public Vector3 AlbedoColor { get; set; } = Vector3.One; // Default white
        public Texture AlbedoMap { get; set; } = null;

        // --- Metallic ---
        public float MetallicFactor { get; set; } = 0.0f; // Default non-metallic
        public Texture MetallicMap { get; set; } = null;

        // --- Roughness ---
        public float RoughnessFactor { get; set; } = 0.5f; // Default mid-roughness
        public Texture RoughnessMap { get; set; } = null;

        // --- Ambient Occlusion ---
        public float AoFactor { get; set; } = 1.0f; // Default no occlusion
        public Texture AoMap { get; set; } = null;

        // --- Normal Map ---
        public Texture NormalMap { get; set; } = null;
        public bool UseNormalMap { get; set; } = false;

        // --- Emission ---
        public Vector3 EmissionColor { get; set; } = Vector3.Zero;
        public float EmissionStrength { get; set; } = 1.0f;
        public Texture EmissionMap { get; set; } = null;

        // --- Tiling and Offset for UVs ---
        public Vector2 UvTiling { get; set; } = Vector2.One;
        public Vector2 UvOffset { get; set; } = Vector2.Zero;

        // --- Static Default Textures ---
        private static Texture _defaultWhiteTexture;
        private static Texture _defaultBlackTexture;
        private static Texture _defaultNormalTexture;
        private static bool _defaultsInitialized = false;

        private bool _isDisposed = false;

        public Material(Shader shader)
        {
            Shader = shader ?? throw new ArgumentNullException(nameof(shader));
            // Ensure InitializeDefaultTextures() is called from Engine.Initialize() or Renderer.Init()
            // after OpenGL context is current, not directly from here if context might not be ready.
            // For safety, Apply() will also check and try to initialize if needed.
        }

        public static void InitializeDefaultTextures()
        {
            if (_defaultsInitialized) return;

            // This method MUST be called when an OpenGL context is current.
            try
            {
                _defaultWhiteTexture = CreateSolidColorTexture(255, 255, 255, 255, "DefaultWhite");
                _defaultBlackTexture = CreateSolidColorTexture(0, 0, 0, 255, "DefaultBlack");
                _defaultNormalTexture = CreateSolidColorTexture(128, 128, 255, 255, "DefaultNormalFlat"); // (0,0,1) normal

                _defaultsInitialized = true;
                Debug.Log("Material: Default textures initialized successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Material.InitializeDefaultTextures: Failed! Ensure an OpenGL context is current. Error: {ex.Message}");
                // Optionally re-throw or handle more gracefully.
            }
        }

        private static Texture CreateSolidColorTexture(byte r, byte g, byte b, byte a, string name)
        {
            int texID = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texID);
            byte[] data = { r, g, b, a }; // RGBA order
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, data);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest); // Use Nearest for 1x1
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GLDebug.CheckError($"Material.CreateSolidColorTexture - {name}");
            return new Texture(texID, 1, 1, $"arcane_default_{name.ToLowerInvariant()}");
        }

        public void Apply()
        {
            if (_isDisposed) return;
            if (Shader == null || Shader.ProgramId == 0)
            {
                Debug.LogWarning("Material.Apply: Shader is null or shader program is invalid (ProgramId = 0). Cannot apply material.");
                return;
            }
            if (!_defaultsInitialized)
            {
                Debug.LogWarning("Material.Apply: Default textures were not initialized. Attempting initialization now. This should ideally be done once at engine startup.");
                InitializeDefaultTextures();
                if (!_defaultsInitialized)
                {
                    Debug.LogError("Material.Apply: Failed to initialize default textures. Material application might fail.");
                    return;
                }
            }

            Shader.Use();

            Shader.SetVector3("u_PBRFactors.AlbedoColor", AlbedoColor);
            Shader.SetInt("u_PBRMaps.UseAlbedoMap", AlbedoMap != null ? 1 : 0);

            Shader.SetFloat("u_PBRFactors.MetallicFactor", MetallicFactor);
            Shader.SetInt("u_PBRMaps.UseMetallicMap", MetallicMap != null ? 1 : 0);

            Shader.SetFloat("u_PBRFactors.RoughnessFactor", RoughnessFactor);
            Shader.SetInt("u_PBRMaps.UseRoughnessMap", RoughnessMap != null ? 1 : 0);

            Shader.SetFloat("u_PBRFactors.AoFactor", AoFactor);
            Shader.SetInt("u_PBRMaps.UseAoMap", AoMap != null ? 1 : 0);

            Shader.SetInt("u_PBRMaps.UseNormalMap", NormalMap != null && UseNormalMap ? 1 : 0);

            Shader.SetVector3("u_PBRFactors.EmissionColor", EmissionColor);
            Shader.SetFloat("u_PBRFactors.EmissionStrength", EmissionStrength);
            Shader.SetInt("u_PBRMaps.UseEmissionMap", EmissionMap != null ? 1 : 0);

            Shader.SetVector2("u_UvTiling", UvTiling);
            Shader.SetVector2("u_UvOffset", UvOffset);

            Texture texToBind;

            texToBind = AlbedoMap ?? _defaultWhiteTexture;
            texToBind.Bind(TextureUnit.Texture0);
            Shader.SetInt("u_PBRMaps.AlbedoMap", 0); // Sampler uniform points to texture unit 0

            texToBind = (NormalMap != null && UseNormalMap) ? NormalMap : _defaultNormalTexture;
            texToBind.Bind(TextureUnit.Texture1);
            Shader.SetInt("u_PBRMaps.NormalMap", 1);

            texToBind = MetallicMap ?? _defaultWhiteTexture;
            texToBind.Bind(TextureUnit.Texture2);
            Shader.SetInt("u_PBRMaps.MetallicMap", 2);

            texToBind = RoughnessMap ?? _defaultWhiteTexture;
            texToBind.Bind(TextureUnit.Texture3);
            Shader.SetInt("u_PBRMaps.RoughnessMap", 3);

            texToBind = AoMap ?? _defaultWhiteTexture;
            texToBind.Bind(TextureUnit.Texture4);
            Shader.SetInt("u_PBRMaps.AoMap", 4);

            texToBind = EmissionMap ?? _defaultBlackTexture;
            texToBind.Bind(TextureUnit.Texture5);
            Shader.SetInt("u_PBRMaps.EmissionMap", 5);

            GLDebug.CheckError("Material.Apply - After setting all uniforms and textures");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    Shader?.Dispose(); // Material owns its shader instance
                    Shader = null;
                    AlbedoMap = null; MetallicMap = null; RoughnessMap = null; AoMap = null; NormalMap = null; EmissionMap = null;
                }
                _isDisposed = true;
            }
        }

        public static void DisposeDefaultTextures()
        {
            if (!_defaultsInitialized) return;

            Action<Texture> safeDelete = (tex) =>
            {
                if (tex != null && tex.Id != 0) GL.DeleteTexture(tex.Id);
            };

            safeDelete(_defaultWhiteTexture);
            safeDelete(_defaultBlackTexture);
            safeDelete(_defaultNormalTexture);

            _defaultWhiteTexture = null;
            _defaultBlackTexture = null;
            _defaultNormalTexture = null;
            _defaultsInitialized = false;
            Debug.Log("Material: Default textures disposed.");
        }

        ~Material()
        {
            Dispose(false);
        }
    }
}
