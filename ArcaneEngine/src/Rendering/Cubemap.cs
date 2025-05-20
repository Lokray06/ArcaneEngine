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
        public Cubemap(int width, int height, PixelInternalFormat internalFormat, int mipLevels = 1)
        {
            Id = GL.GenTexture();
            GL.BindTexture(TextureTarget.TextureCubeMap, Id);

            // Use TexStorage2D for immutable storage for all mip levels and faces
            // Note: PixelInternalFormat maps to SizedInternalFormat for TexStorage.
            // Rgb16f is already a SizedInternalFormat.
            GL.TexStorage2D(TextureTarget2d.TextureCubeMap, mipLevels, (SizedInternalFormat)internalFormat, width, height);
            GLDebug.CheckError($"TexStorage2D texID {Id}, mips {mipLevels}, format {internalFormat}");

            // With TexStorage2D, you DO NOT call TexImage2D to allocate.
            // You would only call TexSubImage2D if you wanted to upload initial data (which you don't here, as it's for FBOs).

            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            if (mipLevels > 1)
            {
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            }
            else
            {
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            }

            GL.BindTexture(TextureTarget.TextureCubeMap, 0);
            GLDebug.CheckError($"Cubemap constructor (ID: {Id}, Mips: {mipLevels}) - After TexStorage2D");
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

        // In ArcaneEngine/src/Rendering/Cubemap.cs

        private int ConvertEquirectangularToCubemapGPU(HdrTextureData hdrData, int size)
        {
            Texture equirectangularMap = new Texture(hdrData); // Assuming HdrTextureData is from your Importer
            if (equirectangularMap.Id == 0)
            {
                Debug.LogError("ConvertEquirectangularToCubemapGPU: Failed to create 2D HDR texture from HdrTextureData.");
                return 0;
            }

            int cubemapId = 0;
            int captureFBO = 0;
            int captureRBO = 0;

            try
            {
                cubemapId = GL.GenTexture();
                GL.BindTexture(TextureTarget.TextureCubeMap, cubemapId);
                GLDebug.CheckError("ConvertEquirectangularToCubemapGPU - GenTexture/BindTexture");

                // Calculate the number of mip levels needed for a cubemap of 'size'
                // Example: size 512 -> log2(512) = 9. Mip levels 0 through 8, so 9 levels if 0-indexed.
                // Or, if 1-indexed for count: floor(log2(size)) + 1
                int numMipLevelsForEnvMap = (int)Math.Floor(Math.Log(size, 2)) + 1;

                // Use TexStorage2D to define immutable storage for ALL mip levels with the correct format
                GL.TexStorage2D(TextureTarget2d.TextureCubeMap,
                                numMipLevelsForEnvMap,
                                SizedInternalFormat.Rgb16f, // Ensure this is the correct SizedInternalFormat for your HDR data
                                size, size);
                GLDebug.CheckError($"ConvertEquirectangularToCubemapGPU - TexStorage2D (ID: {cubemapId}, Mips: {numMipLevelsForEnvMap}, Size: {size})");

                // Texture parameters
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear); // Mipmapping is intended
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GLDebug.CheckError($"ConvertEquirectangularToCubemapGPU - TexParameters (ID: {cubemapId})");

                captureFBO = GL.GenFramebuffer();
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, captureFBO);
                GLDebug.CheckError($"ConvertEquirectangularToCubemapGPU - GenFramebuffer/BindFramebuffer (FBO: {captureFBO})");

                // Optional: Depth buffer for completeness, though not strictly needed if only rendering a skybox cube
                captureRBO = GL.GenRenderbuffer();
                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, captureRBO);
                GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24, size, size);
                GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, captureRBO);
                GLDebug.CheckError($"ConvertEquirectangularToCubemapGPU - Renderbuffer setup (RBO: {captureRBO})");

                Shader equirectToCubemapShader = SkyboxUtils.GetShader(SkyboxShaderType.EquirectangularToCubemap);
                if (equirectToCubemapShader == null || equirectToCubemapShader.ProgramId == 0)
                {
                    Debug.LogError("ConvertEquirectangularToCubemapGPU: Failed to get equirectangularToCubemapShader.");
                    throw new InvalidOperationException("Failed to get equirectangularToCubemapShader."); // Or handle more gracefully
                }

                equirectToCubemapShader.Use();
                equirectToCubemapShader.SetMatrix4("u_ProjectionMatrix", SkyboxUtils.CaptureProjection);
                equirectangularMap.Bind(TextureUnit.Texture0);
                equirectToCubemapShader.SetInt("u_EquirectangularMap", 0);

                GL.Viewport(0, 0, size, size); // Set viewport for rendering to the cubemap faces
                Mesh skyboxCube = SkyboxUtils.GetSkyboxCube();
                if (skyboxCube == null || skyboxCube.VaoId == 0)
                {
                    Debug.LogError("ConvertEquirectangularToCubemapGPU: Failed to get skyboxCube mesh.");
                    throw new InvalidOperationException("Failed to get skyboxCube mesh.");
                }
                skyboxCube.Bind();

                for (int i = 0; i < 6; ++i)
                {
                    equirectToCubemapShader.SetMatrix4("u_ViewMatrix", SkyboxUtils.CaptureViews[i]);
                    // Attach MIP LEVEL 0 of the cubemap face to the FBO for rendering
                    GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                            TextureTarget.TextureCubeMapPositiveX + i, cubemapId, 0); // Target mip level 0
                    GLDebug.CheckError($"ConvertEquirectangularToCubemapGPU - FramebufferTexture2D face {i} (ID: {cubemapId})");

                    // Check Framebuffer completeness for each face, this is good for debugging
                    FramebufferErrorCode fboStatus = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
                    if (fboStatus != FramebufferErrorCode.FramebufferComplete)
                    {
                        Debug.LogError($"ConvertEquirectangularToCubemapGPU: Framebuffer not complete for face {i}! Status: {fboStatus}");
                        throw new Exception($"Framebuffer not complete for face {i}! Status: {fboStatus}");
                    }

                    GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit); // Clear depth too if depth buffer is attached
                    GL.DrawElements(PrimitiveType.Triangles, skyboxCube.IndexCount, DrawElementsType.UnsignedInt, 0);
                    GLDebug.CheckError($"ConvertEquirectangularToCubemapGPU - DrawElements face {i} (ID: {cubemapId})");
                }

                // After rendering to mip level 0 of all faces, generate the rest of the mipmap chain
                GL.BindTexture(TextureTarget.TextureCubeMap, cubemapId); // Ensure the cubemap is bound for GenerateMipmap
                GL.GenerateMipmap(GenerateMipmapTarget.TextureCubeMap);
                GLDebug.CheckError($"ConvertEquirectangularToCubemapGPU - GenerateMipmap (ID: {cubemapId})");

                return cubemapId; // Success
            }
            catch (Exception ex)
            {
                Debug.LogError($"ConvertEquirectangularToCubemapGPU - EXCEPTION: {ex.Message}\nStackTrace: {ex.StackTrace}");
                if (cubemapId != 0) GL.DeleteTexture(cubemapId);
                return 0; // Indicate failure
            }
            finally
            {
                // Cleanup Framebuffer and Renderbuffer
                if (captureFBO != 0)
                {
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                    GL.DeleteFramebuffer(captureFBO);
                }
                if (captureRBO != 0) GL.DeleteRenderbuffer(captureRBO);

                equirectangularMap?.Dispose(); // Dispose the temporary 2D HDR texture
                GLDebug.CheckError("ConvertEquirectangularToCubemapGPU - Finally block cleanup");
            }
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
