using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Arcane.Core;
using System;

namespace Arcane.Rendering
{
    public class Skybox : IDisposable
    {
        public Cubemap EnvironmentMap { get; private set; }
        public Cubemap IrradianceMap { get; private set; }
        public Cubemap PrefilteredMap { get; private set; }
        public Texture BrdfLUTTexture { get; private set; }

        private Mesh _skyboxCubeMesh;
        private Shader _skyboxShader;
        private bool _isDisposed = false; // Added missing field

        private const int IRRADIANCE_MAP_SIZE = 1024;
        private const int PREFILTERED_MAP_SIZE = 2048;
        private const int BRDF_LUT_SIZE = 2048;


        public Skybox(Cubemap environmentMap)
        {
            EnvironmentMap = environmentMap ?? throw new ArgumentNullException(nameof(environmentMap));
            if (EnvironmentMap.Id == 0)
            {
                throw new ArgumentException("Provided environment cubemap is invalid.", nameof(environmentMap));
            }

            _skyboxCubeMesh = SkyboxUtils.GetSkyboxCube();
            _skyboxShader = SkyboxUtils.GetShader(SkyboxShaderType.SkyboxRender);

            GenerateIrradianceMap();
            GeneratePrefilteredMap();
            GenerateBrdfLUT();
            Debug.Log("Skybox initialized and IBL maps generated.");
        }

        private void GenerateIrradianceMap()
        {
            if (EnvironmentMap == null || EnvironmentMap.Id == 0)
            {
                Debug.LogError("Skybox.GenerateIrradianceMap: Source EnvironmentMap is invalid.");
                return;
            }
            Debug.Log("Skybox: Generating Irradiance Map...");
            IrradianceMap = new Cubemap(IRRADIANCE_MAP_SIZE, IRRADIANCE_MAP_SIZE, PixelInternalFormat.Rgb16f);
            if (IrradianceMap.Id == 0) { Debug.LogError("Failed to create IrradianceMap cubemap."); return; }


            int captureFBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, captureFBO);

            Shader irradianceShader = SkyboxUtils.GetShader(SkyboxShaderType.IrradianceConvolution);
            if (irradianceShader == null || irradianceShader.ProgramId == 0) { Debug.LogError("Failed to get IrradianceConvolution shader."); GL.DeleteFramebuffer(captureFBO); return; }

            irradianceShader.Use();
            irradianceShader.SetMatrix4("u_ProjectionMatrix", SkyboxUtils.CaptureProjection);
            EnvironmentMap.Bind(TextureUnit.Texture0);
            irradianceShader.SetInt("u_EnvironmentMap", 0);

            GL.Viewport(0, 0, IRRADIANCE_MAP_SIZE, IRRADIANCE_MAP_SIZE);
            for (int i = 0; i < 6; ++i)
            {
                irradianceShader.SetMatrix4("u_ViewMatrix", SkyboxUtils.CaptureViews[i]);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                        TextureTarget.TextureCubeMapPositiveX + i, IrradianceMap.Id, 0);
                GL.Clear(ClearBufferMask.ColorBufferBit);

                _skyboxCubeMesh.Bind();
                GL.DrawElements(PrimitiveType.Triangles, _skyboxCubeMesh.IndexCount, DrawElementsType.UnsignedInt, 0);
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DeleteFramebuffer(captureFBO);
            GLDebug.CheckError("Skybox.GenerateIrradianceMap");
            Debug.Log($"Skybox: Irradiance Map generated. ID: {IrradianceMap.Id}");
        }

        private void GeneratePrefilteredMap()
        {
            if (EnvironmentMap == null || EnvironmentMap.Id == 0)
            {
                Debug.LogError("Skybox.GeneratePrefilteredMap: Source EnvironmentMap is invalid.");
                return;
            }
            Debug.Log("Skybox: Generating Pre-filtered Specular Map...");

            int PREFILTERED_MAP_MIP_LEVELS = 5;
            PrefilteredMap = new Cubemap(PREFILTERED_MAP_SIZE, PREFILTERED_MAP_SIZE, PixelInternalFormat.Rgb16f, PREFILTERED_MAP_MIP_LEVELS);
            if (PrefilteredMap.Id == 0) { Debug.LogError("Failed to create PrefilteredMap cubemap."); return; }

            Shader prefilterShader = SkyboxUtils.GetShader(SkyboxShaderType.PrefilterEnvironmentMap);
            if (prefilterShader == null || prefilterShader.ProgramId == 0) { Debug.LogError("Failed to get PrefilterEnvironmentMap shader."); return; }

            prefilterShader.Use();
            prefilterShader.SetMatrix4("u_ProjectionMatrix", SkyboxUtils.CaptureProjection);
            EnvironmentMap.Bind(TextureUnit.Texture0);
            prefilterShader.SetInt("u_EnvironmentMap", 0);
            // Assuming source cubemap (EnvironmentMap) was created at a resolution that makes sense for this.
            // If EnvironmentMap was, for example, 512x512, this is okay.
            // This value is used in the shader to sample the correct mip level of the source.
            prefilterShader.SetFloat("u_SourceCubemapResolution", 512f);

            int captureFBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, captureFBO);

            int maxMipLevels = 5;
            for (int mip = 0; mip < maxMipLevels; ++mip)
            {
                int mipWidth = (int)(PREFILTERED_MAP_SIZE * Math.Pow(0.5, mip));
                int mipHeight = (int)(PREFILTERED_MAP_SIZE * Math.Pow(0.5, mip));
                GL.Viewport(0, 0, mipWidth, mipHeight);

                float roughness = (float)mip / (float)(maxMipLevels - 1);
                prefilterShader.SetFloat("u_Roughness", roughness);

                for (int i = 0; i < 6; ++i)
                {
                    prefilterShader.SetMatrix4("u_ViewMatrix", SkyboxUtils.CaptureViews[i]);
                    GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                            TextureTarget.TextureCubeMapPositiveX + i, PrefilteredMap.Id, mip);
                    GL.Clear(ClearBufferMask.ColorBufferBit);
                    _skyboxCubeMesh.Bind();
                    GL.DrawElements(PrimitiveType.Triangles, _skyboxCubeMesh.IndexCount, DrawElementsType.UnsignedInt, 0);
                }
            }
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DeleteFramebuffer(captureFBO);
            GLDebug.CheckError("Skybox.GeneratePrefilteredMap");
            Debug.Log($"Skybox: Pre-filtered Specular Map generated. ID: {PrefilteredMap.Id}");
        }

        private void GenerateBrdfLUT()
        {
            Debug.Log("Skybox: Generating BRDF LUT...");
            int lutTexId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, lutTexId);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rg16f, BRDF_LUT_SIZE, BRDF_LUT_SIZE, 0, PixelFormat.Rg, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            BrdfLUTTexture = new Texture(lutTexId, BRDF_LUT_SIZE, BRDF_LUT_SIZE, "BrdfLUT");
            if (BrdfLUTTexture.Id == 0) { Debug.LogError("Failed to create BrdfLUTTexture."); return; }


            int captureFBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, captureFBO);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, BrdfLUTTexture.Id, 0);

            GL.Viewport(0, 0, BRDF_LUT_SIZE, BRDF_LUT_SIZE);
            Shader brdfShader = SkyboxUtils.GetShader(SkyboxShaderType.BrdfIntegration);
            if (brdfShader == null || brdfShader.ProgramId == 0) { Debug.LogError("Failed to get BrdfIntegration shader."); GL.DeleteFramebuffer(captureFBO); return; }

            brdfShader.Use();
            GL.Clear(ClearBufferMask.ColorBufferBit);

            Mesh quad = SkyboxUtils.GetScreenQuad();
            if (quad == null || quad.VaoId == 0) { Debug.LogError("Failed to get screen quad for BRDF LUT."); GL.DeleteFramebuffer(captureFBO); return; }
            quad.Bind();
            GL.DrawElements(PrimitiveType.Triangles, quad.IndexCount, DrawElementsType.UnsignedInt, 0);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DeleteFramebuffer(captureFBO);
            GLDebug.CheckError("Skybox.GenerateBrdfLUT");
            Debug.Log($"Skybox: BRDF LUT generated. ID: {BrdfLUTTexture.Id}");
        }


        public void Render(Matrix4 viewMatrix, Matrix4 projectionMatrix)
        {
            if (_isDisposed || EnvironmentMap == null || EnvironmentMap.Id == 0 || _skyboxShader == null || _skyboxShader.ProgramId == 0 || _skyboxCubeMesh == null || _skyboxCubeMesh.VaoId == 0)
                return;

            GL.DepthFunc(DepthFunction.Lequal);
            _skyboxShader.Use();

            Matrix4 skyboxViewMatrix = viewMatrix.ClearTranslation();
            _skyboxShader.SetMatrix4("u_ViewMatrix", skyboxViewMatrix);
            _skyboxShader.SetMatrix4("u_ProjectionMatrix", projectionMatrix);

            EnvironmentMap.Bind(TextureUnit.Texture0);
            _skyboxShader.SetInt("u_EnvironmentMap", 0);

            _skyboxCubeMesh.Bind();
            GL.DrawElements(PrimitiveType.Triangles, _skyboxCubeMesh.IndexCount, DrawElementsType.UnsignedInt, 0);

            GL.DepthFunc(DepthFunction.Less);
            GLDebug.CheckError("Skybox.Render");
        }

        public void BindIblMaps(Shader pbrShader, int irradianceUnitSlot, int prefilteredUnitSlot, int brdfLutUnitSlot)
        {
            if (_isDisposed || IrradianceMap == null || IrradianceMap.Id == 0 ||
                PrefilteredMap == null || PrefilteredMap.Id == 0 ||
                BrdfLUTTexture == null || BrdfLUTTexture.Id == 0)
            {
                // Debug.LogWarning("Skybox.BindIblMaps: One or more IBL maps are invalid. Skipping bind.");
                return;
            }
            if (pbrShader == null || pbrShader.ProgramId == 0)
            {
                Debug.LogWarning("Skybox.BindIblMaps: Provided PBR shader is invalid.");
                return;
            }

            IrradianceMap.Bind((TextureUnit)irradianceUnitSlot);
            pbrShader.SetInt("u_IrradianceMap", irradianceUnitSlot - (int)TextureUnit.Texture0);

            PrefilteredMap.Bind((TextureUnit)prefilteredUnitSlot);
            pbrShader.SetInt("u_PrefilteredMap", prefilteredUnitSlot - (int)TextureUnit.Texture0);
            pbrShader.SetFloat("u_MaxReflectionLod", 4.0f);

            BrdfLUTTexture.Bind((TextureUnit)brdfLutUnitSlot);
            pbrShader.SetInt("u_BrdfLut", brdfLutUnitSlot - (int)TextureUnit.Texture0);
            GLDebug.CheckError("Skybox.BindIblMaps");
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
                    IrradianceMap?.Dispose();
                    IrradianceMap = null;
                    PrefilteredMap?.Dispose();
                    PrefilteredMap = null;
                    BrdfLUTTexture?.Dispose();
                    BrdfLUTTexture = null;
                }
                // EnvironmentMap is not owned by Skybox, so it's not disposed here.
                // _skyboxCubeMesh and _skyboxShader are obtained from SkyboxUtils and their lifecycle is managed there.
                _isDisposed = true;
            }
        }
        ~Skybox() { Dispose(false); }
    }
}