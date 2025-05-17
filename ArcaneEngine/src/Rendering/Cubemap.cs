// ArcaneEngine/src/Rendering/Cubemap.cs
using OpenTK.Graphics.OpenGL4;
using Arcane.Core;
using Arcane.AssetManager; // For Asset and Importer.TextureData
using System;
using System.Collections.Generic; // For List<string>
using OpenTK.Mathematics; // For Matrix4, Vector3 etc. in ConvertEquirectangularToCubemapGPU

namespace Arcane.Rendering
{
    public class Cubemap : IDisposable
    {
        public int Id { get; private set; }
        private bool _isDisposed = false;
        private const int CUBEMAP_FACE_COUNT = 6;

        // For internal use (e.g. framebuffer attachments)
        internal Cubemap(int width, int height, PixelInternalFormat internalFormat, bool generateMipmaps = false)
        {
            Id = GL.GenTexture();
            GL.BindTexture(TextureTarget.TextureCubeMap, Id);

            for (int i = 0; i < CUBEMAP_FACE_COUNT; i++)
            {
                // Initialize with null data, actual data will be rendered to it or uploaded later
                GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0, internalFormat,
                              width, height, 0, PixelFormat.Rgb, PixelType.Float, IntPtr.Zero);
            }

            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter,
                generateMipmaps ? (int)TextureMinFilter.LinearMipmapLinear : (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            if (generateMipmaps)
            {
                GL.GenerateMipmap(GenerateMipmapTarget.TextureCubeMap);
            }

            GL.BindTexture(TextureTarget.TextureCubeMap, 0);
            GLDebug.CheckError($"Cubemap internal constructor (ID: {Id})");
            _isDisposed = (Id == 0);
        }


        /// <summary>
        /// Creates a Cubemap from 6 individual face texture assets.
        /// Faces should be provided in the order: +X, -X, +Y, -Y, +Z, -Z.
        /// </summary>
        public Cubemap(List<Asset> faceAssets)
        {
            if (faceAssets == null || faceAssets.Count != CUBEMAP_FACE_COUNT)
            {
                Debug.LogError($"Cubemap: Must provide {CUBEMAP_FACE_COUNT} face assets.");
                Id = 0; _isDisposed = true; return;
            }

            Id = GL.GenTexture();
            GL.BindTexture(TextureTarget.TextureCubeMap, Id);

            int width = 0, height = 0;

            for (int i = 0; i < CUBEMAP_FACE_COUNT; i++)
            {
                Asset faceAsset = faceAssets[i];
                if (faceAsset == null || faceAsset.Type != AssetType.Texture)
                {
                    Debug.LogError($"Cubemap: Face asset {i} is null or not a Texture.");
                    GL.DeleteTexture(Id); Id = 0; _isDisposed = true; return;
                }

                if (!faceAsset.IsLoaded) faceAsset.Load();
                if (!(faceAsset.Data is TextureData texData)) // Accessing public Importer.TextureData
                {
                    Debug.LogError($"Cubemap: Failed to load or cast texture data for face {i} ('{faceAsset.Name}').");
                    GL.DeleteTexture(Id); Id = 0; _isDisposed = true; return;
                }

                if (i == 0)
                {
                    width = texData.Width;
                    height = texData.Height;
                }
                else if (texData.Width != width || texData.Height != height)
                {
                    Debug.LogWarning($"Cubemap: Face {i} ('{faceAsset.Name}') has different dimensions. Resizing or errors may occur.");
                }

                PixelFormat format = texData.Channels == 4 ? PixelFormat.Rgba : PixelFormat.Rgb;
                PixelInternalFormat internalFormat = texData.Channels == 4 ? PixelInternalFormat.Rgba8 : PixelInternalFormat.Rgb8;

                GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0, internalFormat,
                              texData.Width, texData.Height, 0, format, PixelType.UnsignedByte, texData.PixelData);
                GLDebug.CheckError($"Cubemap face {i} load");
            }

            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.GenerateMipmap(GenerateMipmapTarget.TextureCubeMap);

            GL.BindTexture(TextureTarget.TextureCubeMap, 0);
            _isDisposed = (Id == 0);
            if (!_isDisposed) Debug.Log($"Cubemap created from 6 faces. ID: {Id}");
        }

        /// <summary>
        /// Creates a Cubemap from an HDR equirectangular texture asset.
        /// </summary>
        public Cubemap(Asset hdriAsset, int cubemapSize = 512)
        {
            if (hdriAsset == null || hdriAsset.Type != AssetType.HdriTexture)
            {
                Debug.LogError("Cubemap(HDRI): HDRI asset is null or not an HdriTexture type.");
                Id = 0; _isDisposed = true; return;
            }

            if (!hdriAsset.IsLoaded) hdriAsset.Load();

            if (!(hdriAsset.Data is HdrTextureData hdrData)) // Accessing public Importer.HdrTextureData
            {
                Debug.LogError($"Cubemap(HDRI): Failed to load or cast HDR data for asset '{hdriAsset.Name}'.");
                Id = 0; _isDisposed = true; return;
            }

            Id = ConvertEquirectangularToCubemapGPU(hdrData, cubemapSize);
            _isDisposed = (Id == 0);

            if (!_isDisposed) Debug.Log($"Cubemap created from HDRI '{hdriAsset.Name}'. ID: {Id}, Size: {cubemapSize}x{cubemapSize}");
            else Debug.LogError($"Cubemap(HDRI): Failed to convert HDRI '{hdriAsset.Name}' to cubemap.");
        }

        private int ConvertEquirectangularToCubemapGPU(HdrTextureData hdrData, int size) // Accessing public Importer.HdrTextureData
        {
            Texture equirectangularMap = new Texture(hdrData);
            if (equirectangularMap.Id == 0)
            {
                Debug.LogError("Failed to create 2D HDR texture from HdrTextureData.");
                return 0;
            }

            int cubemapId = GL.GenTexture();
            GL.BindTexture(TextureTarget.TextureCubeMap, cubemapId);
            for (int i = 0; i < 6; ++i)
            {
                GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0, PixelInternalFormat.Rgb16f,
                              size, size, 0, PixelFormat.Rgb, PixelType.Float, IntPtr.Zero);
            }
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            int captureFBO = GL.GenFramebuffer();
            int captureRBO = GL.GenRenderbuffer();

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, captureFBO);
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, captureRBO);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24, size, size);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, captureRBO);

            Shader equirectToCubemapShader = SkyboxUtils.GetShader(SkyboxShaderType.EquirectangularToCubemap);
            if (equirectToCubemapShader == null || equirectToCubemapShader.ProgramId == 0)
            {
                Debug.LogError("ConvertEquirectangularToCubemapGPU: Failed to get equirectangularToCubemapShader.");
                GL.DeleteFramebuffer(captureFBO); GL.DeleteRenderbuffer(captureRBO); GL.DeleteTexture(cubemapId);
                equirectangularMap.Dispose(); return 0;
            }

            equirectToCubemapShader.Use();
            equirectToCubemapShader.SetMatrix4("u_ProjectionMatrix", SkyboxUtils.CaptureProjection);
            equirectangularMap.Bind(TextureUnit.Texture0);
            equirectToCubemapShader.SetInt("u_EquirectangularMap", 0);

            GL.Viewport(0, 0, size, size);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, captureFBO);
            Mesh skyboxCube = SkyboxUtils.GetSkyboxCube();
            if (skyboxCube == null || skyboxCube.VaoId == 0)
            {
                Debug.LogError("ConvertEquirectangularToCubemapGPU: Failed to get skyboxCube mesh.");
                GL.DeleteFramebuffer(captureFBO); GL.DeleteRenderbuffer(captureRBO); GL.DeleteTexture(cubemapId);
                equirectangularMap.Dispose(); return 0;
            }

            for (int i = 0; i < 6; ++i)
            {
                equirectToCubemapShader.SetMatrix4("u_ViewMatrix", SkyboxUtils.CaptureViews[i]);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                        TextureTarget.TextureCubeMapPositiveX + i, cubemapId, 0);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                skyboxCube.Bind();
                GL.DrawElements(PrimitiveType.Triangles, skyboxCube.IndexCount, DrawElementsType.UnsignedInt, 0);
            }

            GL.BindTexture(TextureTarget.TextureCubeMap, cubemapId);
            GL.GenerateMipmap(GenerateMipmapTarget.TextureCubeMap);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DeleteFramebuffer(captureFBO); GL.DeleteRenderbuffer(captureRBO);
            equirectangularMap.Dispose();

            GLDebug.CheckError("ConvertEquirectangularToCubemapGPU");
            return cubemapId;
        }

        public void Bind(TextureUnit unit = TextureUnit.Texture0)
        {
            if (_isDisposed || Id == 0) return;
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.TextureCubeMap, Id);
        }

        public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (Id != 0) { GL.DeleteTexture(Id); GLDebug.CheckError($"Cubemap.Dispose (ID: {Id})"); Id = 0; }
                _isDisposed = true;
            }
        }
        ~Cubemap() { Dispose(false); }
    }
}
