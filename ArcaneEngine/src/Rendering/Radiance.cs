// ArcaneEngine/src/Rendering/Radiance.cs
using Arcane.SceneSystem;
using Arcane.Components;
using Arcane.Core;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using Arcane.Rendering;
using Arcane.AssetManager;
using Arcane.Renderering; // For IRenderer interface

namespace Arcane.Rendering
{
    public class Radiance : Renderer
    {
        // IBL Texture Units remain constant
        private const TextureUnit IrradianceMapUnit = TextureUnit.Texture6;
        private const TextureUnit PrefilteredMapUnit = TextureUnit.Texture7;
        private const TextureUnit BrdfLutUnit = TextureUnit.Texture8;

        private const int MAX_SHADER_POINT_LIGHTS = 4;

        public void Init()
        {
            Debug.Log("Radiance Renderer: Initializing...");
            GL.ClearColor(0.1f, 0.1f, 0.15f, 1.0f);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Enable(EnableCap.TextureCubeMapSeamless);

            int samples;
            GL.GetInteger(GetPName.Samples, out samples);
            if (samples > 0)
            {
                GL.Enable(EnableCap.Multisample);
                Debug.Log($"Radiance.Init: Multisample enabled by context ({samples} samples).");
            }
            else
            {
                Debug.LogWarning("Radiance.Init: Multisample not enabled by context or not supported.");
            }

            // IBL setup (Skybox creation and HDRI loading) is now done at the Scene/Game level.
            // Renderer Init remains simpler.
            Debug.Log("Radiance Renderer: Initialized.");
        }

        private void CollectLightsFromScene(Scene scene, List<PointLight> pointLights, List<DirectionalLight> dirLights)
        {
            pointLights.Clear();
            dirLights.Clear();
            if (scene == null) return;
            foreach (GameObject rootGo in scene.RootGameObjects)
            {
                CollectLightsRecursive(rootGo, pointLights, dirLights);
            }
        }

        private void CollectLightsRecursive(GameObject currentGo, List<PointLight> pointLights, List<DirectionalLight> dirLights)
        {
            if (currentGo == null || !currentGo.activeInHierarchy) return;

            PointLight pl = currentGo.GetComponent<PointLight>();
            if (pl != null) pointLights.Add(pl);

            DirectionalLight dl = currentGo.GetComponent<DirectionalLight>();
            if (dl != null) dirLights.Add(dl);

            foreach (Transform childTransform in currentGo.transform.Children)
            {
                CollectLightsRecursive(childTransform.gameObject, pointLights, dirLights);
            }
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

            List<PointLight> activePointLights = new List<PointLight>();
            List<DirectionalLight> activeDirLights = new List<DirectionalLight>();
            CollectLightsFromScene(scene, activePointLights, activeDirLights);

            foreach (GameObject rootGo in scene.RootGameObjects)
            {
                RenderGameObjectRecursive(rootGo, viewMatrix, projectionMatrix, cameraPosWorld, activePointLights, activeDirLights, scene.Skybox);
            }

            // Render Skybox from the scene if available
            if (scene.Skybox != null)
            {
                scene.Skybox.Render(viewMatrix, projectionMatrix);
            }

            GL.BindVertexArray(0);
            GLDebug.CheckError("Radiance.Render - Frame End");
        }

        private void SetGlobalShaderUniforms(Shader shader, Vector3 cameraPosWorld, List<PointLight> pointLights, List<DirectionalLight> dirLights, Skybox currentSceneSkybox)
        {
            if (shader == null || shader.ProgramId == 0) return;

            shader.SetVector3("u_CameraPos_World", cameraPosWorld);

            if (dirLights.Count > 0)
            {
                DirectionalLight dl = dirLights[0];
                if (dirLights.Count > 1) Debug.LogWarning("Radiance: Multiple directional lights found in scene. Using the first one.");
                shader.SetInt("u_UseDirLight", 1);
                shader.SetVector3("u_DirLight.Direction_World", dl.gameObject.transform.forward);
                shader.SetVector3("u_DirLight.Color", dl.Color);
                shader.SetFloat("u_DirLight.Intensity", dl.Intensity);
            }
            else
            {
                shader.SetInt("u_UseDirLight", 0);
            }

            int numActivePointLights = Math.Min(pointLights.Count, MAX_SHADER_POINT_LIGHTS);
            shader.SetInt("u_NumPointLights", numActivePointLights);
            for (int i = 0; i < numActivePointLights; i++)
            {
                PointLight pl = pointLights[i];
                shader.SetVector3($"u_PointLights[{i}].Position_World", pl.gameObject.transform.position);
                shader.SetVector3($"u_PointLights[{i}].Color", pl.Color);
                shader.SetFloat($"u_PointLights[{i}].Intensity", pl.Intensity);
                shader.SetFloat($"u_PointLights[{i}].Constant", pl.Constant);
                shader.SetFloat($"u_PointLights[{i}].Linear", pl.Linear);
                shader.SetFloat($"u_PointLights[{i}].Quadratic", pl.Quadratic);
            }

            GLDebug.CheckError("Radiance - After setting direct light uniforms");

            // Bind IBL maps if skybox is available from the scene
            if (currentSceneSkybox != null)
            {
                currentSceneSkybox.BindIblMaps(shader, (int)IrradianceMapUnit, (int)PrefilteredMapUnit, (int)BrdfLutUnit);
                GLDebug.CheckError("Radiance - After BindIblMaps");
            }
            else // Ensure IBL related uniforms are somewhat reset or indicate no IBL if shader expects them
            {
                // This depends on how the shader handles missing IBL maps.
                // For now, we assume the shader might check `u_IrradianceMap` validity or have fallbacks.
                // Alternatively, bind placeholder/default black textures or set flags.
                // For simplicity, if no skybox, no IBL maps are bound here.
            }
        }

        private void RenderGameObjectRecursive(GameObject go, Matrix4 viewMatrix, Matrix4 projectionMatrix, Vector3 cameraPosWorld, List<PointLight> pointLights, List<DirectionalLight> dirLights, Skybox currentSceneSkybox)
        {
            if (go == null || !go.activeInHierarchy) return;

            MeshRendererComponent meshRenderer = go.GetComponent<MeshRendererComponent>();
            if (meshRenderer != null && meshRenderer.Mesh != null && meshRenderer.Material != null && meshRenderer.Material.Shader != null)
            {
                Mesh mesh = meshRenderer.Mesh;
                Material material = meshRenderer.Material;
                Shader shader = material.Shader;

                if (shader.ProgramId == 0) return;

                material.Apply(); // This calls shader.Use()
                SetGlobalShaderUniforms(shader, cameraPosWorld, pointLights, dirLights, currentSceneSkybox);

                Matrix4 modelMatrix = go.transform.localToWorldMatrix;
                shader.SetMatrix4("u_ModelMatrix", modelMatrix);
                shader.SetMatrix4("u_ViewMatrix", viewMatrix);
                shader.SetMatrix4("u_ProjectionMatrix", projectionMatrix);

                mesh.Bind();
                if (mesh.IndexCount > 0)
                {
                    GL.DrawElements(PrimitiveType.Triangles, mesh.IndexCount, DrawElementsType.UnsignedInt, 0);
                    GLDebug.CheckError($"Radiance.RenderGameObjectRecursive - After DrawElements for {go.Name}");
                }
            }

            foreach (Transform childTransform in go.transform.Children)
            {
                RenderGameObjectRecursive(childTransform.gameObject, viewMatrix, projectionMatrix, cameraPosWorld, pointLights, dirLights, currentSceneSkybox);
            }
        }

        public void Cleanup()
        {
            Debug.Log("Radiance Renderer: Cleaning up...");
            // Renderer no longer owns the Skybox instance or the primary environment cubemap.
            // They are managed by the Scene and the game setup code respectively.

            SkyboxUtils.CleanupSharedResources(); // Clean up shared meshes and utility shaders.
            Debug.Log("Radiance Renderer: Cleanup complete.");
        }
    }
}