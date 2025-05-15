# Radiance Implementation Plan for Arcane Engine

This document outlines the development plan for the "Radiance" rendering pipeline within the Arcane Engine, with a central focus on implementing Clustered Shading. It follows a phased approach, providing learning resources, examples, OpenTK-specific hints, pipeline stage focus, relevant equations, and visual aids.

---

**Phase 1: Core Setup & Primitives for Clustering**

This phase establishes the foundational elements needed before diving into the core Clustered Shading logic.

---

### 1. Rendering Pipeline Architecture & HDR

* **Description:**
    * Establish the main rendering loop and system architecture. Design an `IRenderSystem` interface.
    * Implement High Dynamic Range (HDR) rendering from the outset using floating-point framebuffers.
* **Pipeline Stage Focus:**
    * **Architecture:** Overarching, from Application Stage to Output Merger.
    * **HDR:** Pixel Shading (output HDR values), Output Merger (floating-point framebuffer).
* **OpenTK Hints & Guidance:**
    * `OpenTK.Windowing.Desktop.GameWindow` for the main window.
    * `GL.ClearColor()`, `GL.Enable(EnableCap.DepthTest)`.
    * Shader management (`GL.CreateShader`, `GL.LinkProgram`, etc.).
    * VAOs, VBOs, EBOs for basic geometry (`GL.GenVertexArray`, `GL.BufferData`, `GL.DrawElements`).
    * **HDR FBO:** `GL.GenFramebuffer()`, `GL.BindFramebuffer()`. Color attachment: `GL.TexImage2D(..., PixelInternalFormat.Rgba16f, ...PixelType.Float, ...)`. Depth attachment: `GL.RenderbufferStorage(..., RenderbufferStorage.Depth24Stencil8, ...)`. Check completeness with `GL.CheckFramebufferStatus()`.
* **Learning Resources:**
    * LearnOpenGL - HDR: <https://learnopengl.com/Advanced-Lighting/HDR>
    * OpenTK Tutorials for basic setup.
* **Visuals:**
    * ![HDR Color Picker](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@latest/manual/Images/HDRColorPicker.png)

---

### 2. G-Buffer Pass / Z-Prepass

* **Description:** Implement a pass to generate necessary geometric information.
    * **For Clustered Deferred:** Render scene geometry to a G-Buffer (multiple render targets storing normals, albedo, depth, material properties).
    * **For Clustered Forward (or if only depth is needed for active clusters):** Implement a Z-Prepass to generate a high-resolution depth buffer.
* **Pipeline Stage Focus:**
    * **Vertex Shader:** Transforms vertices.
    * **Pixel Shading:** Outputs G-Buffer data (normals, albedo, roughness, metallic, etc.) or simply writes depth.
    * **Output Merger:** Writes to multiple render targets (G-Buffer) or a single depth texture.
* **OpenTK Hints & Guidance:**
    * **G-Buffer FBO:** Use `GL.DrawBuffers()` to specify multiple color attachments if creating a G-Buffer. Each attachment would be a texture (e.g., normals in `Rgba16f`, albedo in `Srgb8Alpha8`, depth in `DepthComponent32f`).
    * **Z-Prepass FBO:** Similar to shadow map FBO; color writing disabled (`GL.DrawBuffer(DrawBufferMode.None)`), depth texture attached.
    * Ensure shaders output correct data to each G-Buffer target using `layout(location = N) out vec4 FragDataN;`.
* **Learning Resources:**
    * LearnOpenGL - Deferred Shading (for G-Buffer concepts): <https://learnopengl.com/Advanced-Lighting/Deferred-Shading>
    * "Optimizing a Z-Prepass" articles.
* **Visuals:**
    * ![G-Buffer Visualization](https://learnopengl.com/img/advanced-lighting/deferred_g_buffer.png)
    * ![Depth Buffer Visualization](https://learnopengl.com/img/advanced-lighting/ssao_depth.png)

---

**Phase 2: Clustered Shading Core Implementation**

This phase implements the core logic of Clustered Shading, drawing heavily from the provided text by Ola Olsson et al. and Tiago Sousa. Most of this will involve Compute Shaders.

---

### 3. Building the Cluster Grid (Compute Shader)

* **Description:** Subdivide the view frustum into a 3D grid of clusters and calculate an Axis-Aligned Bounding Box (AABB) for each cluster in view space. This is Step 1 from the Clustered Shading paper.
* **Pipeline Stage Focus (Compute Pass):**
    * **Compute Shader:** Each thread calculates the AABB for one cluster.
* **Equations & Terms (DOOM 2016 Depth Slicing - Equation 2 & 3 from user text):**
    * **Depth Z for a slice (Equation 2):**
      $$ Z = \text{Near}_z \cdot \left(\frac{\text{Far}_z}{\text{Near}_z}\right)^{\text{slice}/\text{numSlices}} $$
      * $Z$: View space depth for the boundary of a slice.
      * $\text{Near}_z, \text{Far}_z$: Near and far plane distances in view space.
      * $\text{slice}$: Current depth slice index (0 to numSlices-1).
      * $\text{numSlices}$: Total number of depth subdivisions.
    * **Slice index from depth Z (Equation 3 - for assigning pixels to clusters later):**
      $$ \text{slice} = \left\lfloor \frac{\log(Z) \cdot \text{numSlices}}{\log(\text{Far}_z/\text{Near}_z)} - \frac{\text{numSlices} \cdot \log(\text{Near}_z)}{\log(\text{Far}_z/\text{Near}_z)} \right\rfloor $$
      * Can be simplified to $\text{slice} = \lfloor \log(Z) \cdot \text{scale} + \text{bias} \rfloor$ by pre-calculating scale and bias.
* **OpenTK Hints & Guidance:**
    * **Compute Shader:** Write GLSL compute shader. Dispatch with `GL.DispatchCompute(numGroupsX, numGroupsY, numGroupsZ)`. `gl_NumWorkGroups`, `gl_WorkGroupID`, `gl_LocalInvocationID`, `gl_GlobalInvocationID` are key built-ins.
    * **Shader Storage Buffer Object (SSBO) for Clusters:**
        * Define a struct for cluster AABBs (minPoint, maxPoint `vec4`s for alignment).
        * Create SSBO: `GL.GenBuffer()`, `GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssboHandle)`.
        * Allocate memory: `GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeInBytes, IntPtr.Zero, BufferUsageHint.StaticDraw)`. (Data written by compute shader).
        * Bind SSBO to a binding point: `GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, bindingPointIndex, ssboHandle)`.
    * **Uniforms:** Pass `zNear`, `zFar`, screen dimensions, inverse projection matrix, tile sizes.
    * **Helper GLSL functions (from user text):** `screen2View()` and `lineIntersectionToZPlane()`.
    * Use `barrier()` or `memoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit)` if needed, though for a single-pass grid build where each thread writes to its own unique cluster, explicit barriers within the shader might not be critical if dispatch is for all clusters.
* **Learning Resources:**
    * User-provided text on "Building a Cluster Grid".
    * LearnOpenGL - Compute Shaders: <https://learnopengl.com/Advanced-OpenGL/Compute-Shaders>
    * OpenGL Wiki - Shader Storage Buffer Object: <https://www.khronos.org/opengl/wiki/Shader_Storage_Buffer_Object>
* **Visuals:**
    * ![Depth Slicing Schemes](https://placehold.co/600x200/EFEFEF/AAAAAA?text=Depth+Slicing+Schemes+Comparison) (Illustrating Linear NDC, Linear View, Exponential)
    * ![Cluster Grid Visualization](https://placehold.co/600x300/EFEFEF/AAAAAA?text=Sponza+Scene+with+Cluster+Z-Index+Colors) (Similar to user's Sponza image)

---

### 4. Determining Active Clusters (Compute Shader - Optional but Recommended)

* **Description:** Identify which clusters are actually visible or contain geometry by checking against the depth buffer (from Z-Prepass or G-Buffer). This combines Steps 3 & 4 from the Clustered Shading paper.
* **Pipeline Stage Focus (Compute Pass/Passes):**
    * **Mark Active Clusters Pass:**
        * **Compute Shader:** Each thread (representing a pixel or tile of pixels) reads depth, calculates its cluster ID, and marks the cluster as active.
    * **Compact Cluster List Pass:**
        * **Compute Shader:** Threads (representing clusters) check if they are active and, if so, atomically add their ID to a compact list.
* **OpenTK Hints & Guidance:**
    * **Input:** Depth texture from Z-Prepass/G-Buffer.
    * **`markActiveClusters` Compute Shader:**
        * Dispatch one thread per pixel or per tile of pixels.
        * Sample depth texture: `texture(depthSampler, screenCoord).r`.
        * Use `getClusterIndex(vec3 pixelCoord)` (which uses `getDepthSlice` - Equation 3) from user text.
        * Output to a `bool clusterActive[]` SSBO or `imageAtomicOr` to an `imageBuffer`.
    * **`buildCompactClusterList` Compute Shader:**
        * Input: `clusterActive[]` boolean array/image.
        * Output: `uint uniqueActiveClusters[]` SSBO and a `uint globalActiveClusterCount` (atomic counter SSBO or UBO updated by CPU after readback, or use `atomicAdd` on an SSBO-backed counter).
        * Use `atomicAdd(globalActiveClusterCount, 1)` to get a unique offset for writing to `uniqueActiveClusters`.
        * Dispatch one thread per cluster.
    * `GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit)` or `ShaderStorageBarrierBit` between passes if results of one are read by the next.
* **Learning Resources:**
    * User-provided text on "Determining Active Clusters".
    * OpenGL Wiki - Atomic Counter: <https://www.khronos.org/opengl/wiki/Atomic_Counter>
    * "DirectX 11 Rendering in Battlefield 3" (mentions tile-based processing and active cluster determination).
* **Visuals:**
    * ![Active Clusters Visualization](https://placehold.co/600x300/EFEFEF/AAAAAA?text=Scene+Highlighting+Active+Clusters)

---

### 5. Light Culling & Assignment (Compute Shader)

* **Description:** For each active cluster, determine which lights intersect its AABB and build a per-cluster light list. This is Step 5 from the Clustered Shading paper.
* **Pipeline Stage Focus (Compute Pass):**
    * **Compute Shader:** Threads (representing active clusters or batches of lights/clusters) perform light-AABB intersection tests and populate light lists.
* **Equations & Terms:**
    * **Sphere-AABB Intersection:** (From user text - `testSphereAABB`)
      The core idea is to find the squared distance from the sphere's center to the closest point on the AABB. If `squaredDistance <= radius * radius`, they intersect.
      `sqDistPointAABB(center, clusterAABB)`:
      ```glsl
      // vec3 center (light position in view space)
      // ClusterAABB minPoint, maxPoint
      float sqDist = 0.0;
      for (int i = 0; i < 3; ++i) {
          float v = center[i];
          if (v < clusterAABB.minPoint[i]) sqDist += (clusterAABB.minPoint[i] - v) * (clusterAABB.minPoint[i] - v);
          if (v > clusterAABB.maxPoint[i]) sqDist += (v - clusterAABB.maxPoint[i]) * (v - clusterAABB.maxPoint[i]);
      }
      return sqDist;
      ```
* **OpenTK Hints & Guidance:**
    * **Data Structures (SSBOs - from user text diagram):**
        * `globalLightList[]`: Contains all light data (position, color, range, type, etc.). Input.
        * `clusterAABBs[]`: From Step 3. Input.
        * `uniqueActiveClusters[]` (Optional): List of active cluster indices. Input.
        * `lightGrid[]`: Output. Array with size = total clusters. Each element: `struct { uint offset; uint count; }`. Stores start offset into `globalLightIndexList` and number of lights for that cluster.
        * `globalLightIndexList[]`: Output. Linear list storing indices of lights from `globalLightList`. Populated by this pass.
        * `globalIndexCount` (atomic): Used to manage writing to `globalLightIndexList`.
    * **Compute Shader Logic (based on user text):**
        * Dispatch threads per active cluster (if using compacted list) or all clusters.
        * Each thread iterates through lights (or batches of lights using shared memory as described in user text).
        * Perform `testSphereAABB` (or other light type specific tests).
        * If a light intersects, store its index.
        * After checking all lights for a cluster, use `atomicAdd(globalIndexCount, numVisibleLightsForThisCluster)` to get an `offset`.
        * Write the light indices for this cluster into `globalLightIndexList` starting at `offset`.
        * Write `offset` and `numVisibleLightsForThisCluster` into `lightGrid[clusterIndex]`.
    * Use `shared` memory in compute shaders for light data to reduce global memory reads if batching lights per workgroup.
    * `GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit)` is crucial after this pass if the `lightGrid` and `globalLightIndexList` are to be read by subsequent rendering shaders.
* **Learning Resources:**
    * User-provided text on "Light Culling Methods".
    * "Real-Time Collision Detection" by Christer Ericson (for intersection tests).
    * Emil Persson (Humus) - Practical Clustered Shading presentation.
* **Visuals:**
    * ![Light Culling Diagram](https://placehold.co/600x400/EFEFEF/AAAAAA?text=Light+Culling+Data+Structures+Diagram) (Similar to Olsson's diagram in user text)
    * ![Cluster with Assigned Lights](https://placehold.co/600x300/EFEFEF/AAAAAA?text=Cluster+Showing+Intersecting+Light+Volumes)

---

**Phase 3: Shading & PBR Integration**

With the light lists per cluster established, this phase focuses on rendering the scene using this information.

---

### 6. PBR Material System (Full Implementation)

* **Description:** Fully implement the PBR material system (Metallic/Roughness) with texture support for albedo, normals, metallic, roughness, and AO.
* **Pipeline Stage Focus:**
    * **Application Stage:** Loading PBR textures and material parameters.
    * **Vertex Shader:** Transforming necessary attributes.
    * **Pixel Shading:** PBR calculations using material properties.
* **Equations & Terms:** (Refer to PBR section in Phase 1, Step 3 for detailed BRDF equations)
* **OpenTK Hints & Guidance:**
    * Texture loading and uniform setup as in Phase 1, Step 3.
    * Ensure shaders correctly sample all PBR maps.
* **Learning Resources:** (Same as Phase 1, Step 3)
* **Visuals:** (Same as Phase 1, Step 3)

---

### 7. Shading with Clustered Light Lists

* **Description:** Implement the final shading pass. For each pixel/fragment:
    1. Determine its cluster.
    2. Retrieve the list of lights affecting that cluster from `lightGrid` and `globalLightIndexList`.
    3. Iterate through this per-cluster light list and apply PBR lighting calculations for each light.
    This is Step 6 from the Clustered Shading paper.
* **Pipeline Stage Focus:**
    * **Vertex Shader:** Calculate view-space position/depth for cluster lookup.
    * **Pixel Shading:**
        * Determine fragment's cluster ID (using `getClusterIndex` - Equation 3).
        * Read `offset` and `count` from `lightGrid[clusterID]`.
        * Loop `count` times, reading light indices from `globalLightIndexList[offset + i]`.
        * For each light index, fetch light data from `globalLightList[]` (or a UBO containing light data).
        * Perform PBR lighting calculation (diffuse and specular) and accumulate.
* **OpenTK Hints & Guidance:**
    * **Inputs to Pixel Shader:**
        * `lightGrid` SSBO.
        * `globalLightIndexList` SSBO.
        * `globalLightList` SSBO or UBO.
        * G-Buffer textures (if Clustered Deferred) or material textures (if Clustered Forward).
        * Fragment's view-space position/depth (from vertex shader or G-Buffer).
    * The pixel shader needs the same `getClusterIndex` logic as the active cluster determination pass.
    * This is where the main PBR shading logic (from Phase 1, Step 3 and Phase 3, Step 6) is combined with the per-cluster light lists.
* **Learning Resources:**
    * Original Clustered Shading paper by Olsson et al.
    * LearnOpenGL - PBR Lighting (for the per-light PBR calculation).
* **Visuals:**
    * ![Scene Rendered with Clustered Shading](https://placehold.co/600x300/EFEFEF/AAAAAA?text=Scene+with+Many+Lights+using+Clustered+Shading)

---

**Phase 4: Shadows & Initial Post-Processing**

Integrate shadows and foundational post-processing effects.

---

### 8. Shadow Mapping (CSM, Spot/Point)

* **Description:** Implement shadow mapping. Shadow map generation is per-light. Shadow application in the main shading pass will use the light information (and its shadow map) from the cluster's light list.
* **Pipeline Stage Focus:**
    * **Shadow Map Generation (per light, per cascade):** Same as traditional shadow mapping (Vertex Shader, Rasterization, Depth Output).
    * **Main Shading Pass (Pixel Shading):** When iterating through cluster lights, if a light casts shadows, sample its shadow map.
* **Equations & Terms:** (Same as Phase 2, Step 6 for basic shadow test)
* **OpenTK Hints & Guidance:**
    * Shadow map generation FBOs and depth textures as before.
    * For each light in the `globalLightList` that casts shadows, you'll need to store/access its shadow map texture and light-space matrix.
    * In the Clustered Shading pixel shader, when processing a light from the cluster list, check if it has a shadow map. If so, perform the shadow test.
* **Learning Resources:** (Same as Phase 2, Steps 6 & 7)
* **Visuals:** (Same as Phase 2, Steps 6 & 7)

---

### 9. Tone Mapping

* **Description:** Convert HDR rendered colors (from Clustered Shading pass) to LDR for display.
* **Pipeline Stage Focus (Post-Processing Pass):** (Same as Phase 3, Step 8 in previous plan)
* **Equations & Terms:** (Same as Phase 3, Step 8 in previous plan)
* **OpenTK Hints & Guidance:** (Same as Phase 3, Step 8 in previous plan)
* **Learning Resources:** (Same as Phase 3, Step 8 in previous plan)
* **Visuals:** (Same as Phase 3, Step 8 in previous plan)

---

### 10. Bloom

* **Description:** Simulate light scattering for bright areas.
* **Pipeline Stage Focus (Multiple Post-Processing Passes):** (Same as Phase 3, Step 9 in previous plan)
* **Equations & Terms:** (Same as Phase 3, Step 9 in previous plan for brightness extraction)
* **OpenTK Hints & Guidance:** (Same as Phase 3, Step 9 in previous plan)
* **Learning Resources:** (Same as Phase 3, Step 9 in previous plan)
* **Visuals:** (Same as Phase 3, Step 9 in previous plan)

---

### 11. Anti-Aliasing (MSAA or Post-Process AA)

* **Description:**
    * **MSAA:** If using Clustered Forward Shading, MSAA can be effective.
    * **Post-Process AA (FXAA/SMAA):** If using Clustered Deferred Shading (G-Buffer makes MSAA hard) or as an alternative.
* **Pipeline Stage Focus:**
    * **MSAA:** Rasterization, Per-Sample Operations, Output Merger.
    * **Post-Process AA:** Post-processing pass (Vertex & Pixel Shading).
* **OpenTK Hints & Guidance:** (Similar to Phase 3, Step 10 in previous plan for MSAA; and Phase 5, Step 17 for Post-AA)
* **Learning Resources:** (Similar to Phase 3, Step 10 and Phase 5, Step 17 in previous plan)
* **Visuals:** (Similar to Phase 3, Step 10 and Phase 5, Step 17 in previous plan)

---

**Phase 5: Advanced GI & Reflections**

Implement techniques for more global light interaction and realistic reflections.

---

### 12. Screen Space Ambient Occlusion (SSAO)

* **Description:** Approximate ambient occlusion using screen-space depth/normals.
* **Pipeline Stage Focus:** (Same as Phase 3, Step 11 in previous plan)
    * Requires depth and normal information, readily available if using a G-Buffer for Clustered Deferred, or from a Z-Prepass/Geometry Pre-pass if Clustered Forward.
* **Equations & Terms:** (Same as Phase 3, Step 11 in previous plan)
* **OpenTK Hints & Guidance:** (Same as Phase 3, Step 11 in previous plan)
* **Learning Resources:** (Same as Phase 3, Step 11 in previous plan)
* **Visuals:** (Same as Phase 3, Step 11 in previous plan)

---

### 13. Reflection Probes (Static Cubemaps & IBL)

* **Description:** Capture environment into cubemaps for reflections and Image-Based Lighting.
* **Pipeline Stage Focus:** (Same as Phase 4, Step 12 in previous plan)
    * The IBL contribution (diffuse and specular from cubemaps) is added to the direct lighting calculated via Clustered Shading in the main pixel shader.
* **Equations & Terms:** (Same as Phase 4, Step 12 in previous plan)
* **OpenTK Hints & Guidance:** (Same as Phase 4, Step 12 in previous plan)
* **Learning Resources:** (Same as Phase 4, Step 12 in previous plan)
* **Visuals:** (Same as Phase 4, Step 12 in previous plan)

---

### 14. Baked Global Illumination (Lightmaps & Light Probes)

* **Description:** Pre-calculate indirect lighting.
* **Pipeline Stage Focus (Runtime Sampling):** (Same as Phase 4, Step 13 in previous plan)
    * Baked GI is typically added to the final lighting result, complementing the dynamic direct and IBL lighting.
* **Equations & Terms (Spherical Harmonics):** (Same as Phase 4, Step 13 in previous plan)
* **OpenTK Hints & Guidance (Runtime Sampling):** (Same as Phase 4, Step 13 in previous plan)
* **Learning Resources:** (Same as Phase 4, Step 13 in previous plan)
* **Visuals:** (Same as Phase 4, Step 13 in previous plan)

---

**Phase 6: Advanced Visuals & Optional Features**

Add further polish and optional effects. These are mostly post-processing.

---

### 15. Color Grading

* **Description:** Adjust final image color and tone using LUTs.
* **Pipeline Stage Focus (Post-Processing Pass):** (Same as Phase 5, Step 14 in previous plan)
* **OpenTK Hints & Guidance:** (Same as Phase 5, Step 14 in previous plan)
* **Learning Resources:** (Same as Phase 5, Step 14 in previous plan)
* **Visuals:** (Same as Phase 5, Step 14 in previous plan)

---

### 16. Planar Reflections (Optional)

* **Description:** Perfect reflections on flat surfaces.
* **Pipeline Stage Focus:** (Same as Phase 5, Step 15 in previous plan)
* **OpenTK Hints & Guidance:** (Same as Phase 5, Step 15 in previous plan)
* **Learning Resources:** (Same as Phase 5, Step 15 in previous plan)
* **Visuals:** (Same as Phase 5, Step 15 in previous plan)

---

### 17. Further Soft Shadow Improvements (Optional - VSM/ESM)

* **Description:** Advanced soft shadow techniques.
* **Pipeline Stage Focus:** (Same as Phase 5, Step 16 in previous plan)
* **Equations & Terms (VSM):** (Same as Phase 5, Step 16 in previous plan)
* **OpenTK Hints & Guidance (VSM):** (Same as Phase 5, Step 16 in previous plan)
* **Learning Resources:** (Same as Phase 5, Step 16 in previous plan)
* **Visuals:** (Same as Phase 5, Step 16 in previous plan)

---

### 18. Depth of Field (Optional)

* **Description:** Simulate camera focus effects.
* **Pipeline Stage Focus (Post-Processing Pass/Passes):** (Same as Phase 5, Step 18 in previous plan)
* **Equations & Terms (CoC):** (Same as Phase 5, Step 18 in previous plan)
* **OpenTK Hints & Guidance:** (Same as Phase 5, Step 18 in previous plan)
* **Learning Resources:** (Same as Phase 5, Step 18 in previous plan)
* **Visuals:** (Same as Phase 5, Step 18 in previous plan)

---

### 19. Motion Blur (Optional)

* **Description:** Simulate blur from motion.
* **Pipeline Stage Focus:** (Same as Phase 5, Step 19 in previous plan)
* **Equations & Terms (Velocity Calculation):** (Same as Phase 5, Step 19 in previous plan)
* **OpenTK Hints & Guidance:** (Same as Phase 5, Step 19 in previous plan)
* **Learning Resources:** (Same as Phase 5, Step 19 in previous plan)
* **Visuals:** (Same as Phase 5, Step 19 in previous plan)

---

### 20. Dynamic Reflection Probes (Optional Enhancement)

* **Description:** Runtime updates for reflection cubemaps.
* **Pipeline Stage Focus:** (Same as Phase 5, Step 20 in previous plan)
* **OpenTK Hints & Guidance:** (Same as Phase 5, Step 20 in previous plan)
* **Learning Resources:** (Same as Phase 5, Step 20 in previous plan)
* **Visuals:** (Same as Phase 5, Step 20 in previous plan)

---

**Phase 7: Workflow & Polish (Ongoing)**

Focus on usability, scalability, and editor integration.

---

### 21. Scalability & Quality Tiers

* **Description:** Allow features/quality to be adjusted.
* **Pipeline Stage Focus:** (Same as Phase 6, Step 21 in previous plan)
* **OpenTK Hints & Guidance:** (Same as Phase 6, Step 21 in previous plan)
* **Learning Resources:** (Same as Phase 6, Step 21 in previous plan)
* **Visuals:** (Same as Phase 6, Step 21 in previous plan)

---

### 22. Editor Integration

* **Description:** Control and preview features in the Arcane Engine editor.
* **Pipeline Stage Focus:** (Same as Phase 6, Step 22 in previous plan)
* **OpenTK Hints & Guidance:** (Same as Phase 6, Step 22 in previous plan)
* **Learning Resources:** (Same as Phase 6, Step 22 in previous plan)
* **Visuals:** (Same as Phase 6, Step 22 in previous plan)

---

## OpenTK API Reference (Examples)

This section provides a brief description of some common OpenTK `GL` functions mentioned in this plan. It's not exhaustive but covers key operations. Most functions are static methods of the `OpenTK.Graphics.OpenGL4.GL` class.

* **`GL.ClearColor(float r, float g, float b, float a)` / `GL.ClearColor(Color4 color)`**
    * **Description:** Specifies the red, green, blue, and alpha values used by `GL.Clear` to clear the color buffers.
    * **Parameters:** RGBA color components (typically 0.0 to 1.0).
* **`GL.Clear(ClearBufferMask mask)`**
    * **Description:** Clears buffers to preset values.
    * **Parameters:** `mask` is a bitwise OR of values like `ClearBufferMask.ColorBufferBit`, `ClearBufferMask.DepthBufferBit`, `ClearBufferMask.StencilBufferBit`.
* **`GL.Enable(EnableCap cap)` / `GL.Disable(EnableCap cap)`**
    * **Description:** Enables or disables various OpenGL capabilities.
    * **Parameters:** `cap` is an enum like `EnableCap.DepthTest`, `EnableCap.Blend`, `EnableCap.CullFace`, `EnableCap.Multisample`.
* **`GL.CreateShader(ShaderType type)`**
    * **Description:** Creates an empty shader object.
    * **Parameters:** `type` is `ShaderType.VertexShader`, `ShaderType.FragmentShader`, `ShaderType.ComputeShader`, etc.
    * **Returns:** The ID (integer handle) of the new shader object.
* **`GL.ShaderSource(int shader, string source)`**
    * **Description:** Replaces the source code in a shader object.
    * **Parameters:** `shader` is the shader ID, `source` is the GLSL code string.
* **`GL.CompileShader(int shader)`**
    * **Description:** Compiles a shader object.
* **`GL.GetShaderInfoLog(int shader)`**
    * **Description:** Returns the information log for a shader object, useful for compilation errors.
    * **Returns:** A string containing the log.
* **`GL.CreateProgram()`**
    * **Description:** Creates an empty program object.
    * **Returns:** The ID (integer handle) of the new program object.
* **`GL.AttachShader(int program, int shader)`**
    * **Description:** Attaches a shader object to a program object.
* **`GL.LinkProgram(int program)`**
    * **Description:** Links a program object, creating executables for the shaders.
* **`GL.GetProgramInfoLog(int program)`**
    * **Description:** Returns the information log for a program object, useful for linking errors.
* **`GL.UseProgram(int program)`**
    * **Description:** Installs a program object as part of current rendering state.
* **`GL.DispatchCompute(int num_groups_x, int num_groups_y, int num_groups_z)`**
    * **Description:** Launches one or more compute work groups.
    * **Parameters:** Number of work groups to launch in X, Y, and Z dimensions.
* **`GL.MemoryBarrier(MemoryBarrierFlags flags)`**
    * **Description:** Defines a barrier ordering memory transactions. Essential for synchronizing access to shared resources between shader invocations (e.g., SSBOs, images).
    * **Parameters:** `flags` like `MemoryBarrierFlags.ShaderStorageBarrierBit`, `ShaderImageAccessBarrierBit`, `AllBarrierBits`.
* **`GL.BindBufferBase(BufferRangeTarget target, int index, int buffer)`**
    * **Description:** Binds a buffer object to an indexed buffer target (e.g., for SSBOs or UBOs).
    * **Parameters:** `target` (e.g., `BufferRangeTarget.ShaderStorageBuffer`), `index` (binding point index), `buffer` (buffer handle).
* **`GL.GenVertexArray()` / `GL.GenVertexArrays(int n, out int arrays)`**
    * **Description:** Generates vertex array object names.
    * **Returns:** An integer handle (or fills an array of handles).
* **`GL.BindVertexArray(int array)`**
    * **Description:** Binds a vertex array object, making it the current VAO.
* **`GL.GenBuffer()` / `GL.GenBuffers(int n, out int buffers)`**
    * **Description:** Generates buffer object names.
* **`GL.BindBuffer(BufferTarget target, int buffer)`**
    * **Description:** Binds a named buffer object to a specified target (e.g., `BufferTarget.ArrayBuffer`, `BufferTarget.ElementArrayBuffer`, `BufferTarget.ShaderStorageBuffer`).
* **`GL.BufferData(BufferTarget target, int size, IntPtr data, BufferUsageHint usage)` / `GL.BufferData<T>(BufferTarget target, int size, T[] data, BufferUsageHint usage)` (where T is struct)**
    * **Description:** Creates and initializes a buffer object's data store.
    * **Parameters:** `size` is data store size in bytes, `data` is a pointer or array, `usage` is `BufferUsageHint.StaticDraw`, `DynamicDraw`, etc.
* **`GL.VertexAttribPointer(int index, int size, VertexAttribPointerType type, bool normalized, int stride, int offset)`**
    * **Description:** Defines an array of generic vertex attribute data. Configures how OpenGL should interpret vertex buffer data for a specific attribute in a VAO.
    * **Parameters:** `index` is attribute location, `size` is number of components (1-4), `type` is data type (e.g., `VertexAttribPointerType.Float`), `normalized` specifies if fixed-point data should be normalized, `stride` is byte offset between consecutive attributes, `offset` is byte offset of the first component.
* **`GL.EnableVertexAttribArray(int index)`**
    * **Description:** Enables a generic vertex attribute array.
* **`GL.DrawElements(PrimitiveType mode, int count, DrawElementsType type, IntPtr indices)`**
    * **Description:** Renders primitives from array data, using an EBO for indices.
    * **Parameters:** `mode` is primitive type (e.g., `PrimitiveType.Triangles`), `count` is number of elements to be rendered, `type` is index data type (e.g., `DrawElementsType.UnsignedInt`), `indices` is offset into EBO or pointer if no EBO bound.
* **`GL.GenFramebuffer()` / `GL.GenFramebuffers(int n, out int framebuffers)`**
    * **Description:** Generates framebuffer object names.
* **`GL.BindFramebuffer(FramebufferTarget target, int framebuffer)`**
    * **Description:** Binds a framebuffer object to a target (`FramebufferTarget.Framebuffer`, `ReadFramebuffer`, `DrawFramebuffer`).
* **`GL.TexImage2D(TextureTarget target, int level, PixelInternalFormat internalFormat, int width, int height, int border, PixelFormat format, PixelType type, IntPtr pixels)`**
    * **Description:** Specifies a two-dimensional texture image.
    * **Parameters:** `target` (e.g., `TextureTarget.Texture2D`), `level` (mipmap level), `internalFormat` (format on GPU), `width`, `height`, `border` (must be 0), `format` (format of pixel data), `type` (data type of pixel data), `pixels` (pointer to image data).
* **`GL.FramebufferTexture2D(FramebufferTarget target, FramebufferAttachment attachment, TextureTarget texTarget, int texture, int level)`**
    * **Description:** Attaches a level of a texture object as a logical buffer to the currently bound framebuffer object.
* **`GL.RenderbufferStorage(RenderbufferTarget target, RenderbufferStorage internalformat, int width, int height)`**
    * **Description:** Establishes data storage, format, and dimensions of a renderbuffer object's image.
* **`GL.FramebufferRenderbuffer(FramebufferTarget target, FramebufferAttachment attachment, RenderbufferTarget renderbuffertarget, int renderbuffer)`**
    * **Description:** Attaches a renderbuffer object as a logical buffer to the currently bound framebuffer object.
* **`GL.CheckFramebufferStatus(FramebufferTarget target)`**
    * **Description:** Checks the completeness status of a framebuffer.
    * **Returns:** A `FramebufferErrorCode` enum (e.g., `FramebufferErrorCode.FramebufferComplete`).
* **`GL.ActiveTexture(TextureUnit texture)`**
    * **Description:** Selects which texture unit subsequent texture state calls affect.
* **`GL.GetUniformLocation(int program, string name)`**
    * **Description:** Returns the location of a uniform variable.
    * **Returns:** Integer location, or -1 if not found.
* **`GL.Uniform1(int location, float v0)` / `GL.Uniform3(int location, Vector3 vector)` / `GL.UniformMatrix4(int location, bool transpose, ref Matrix4 matrix)` etc.**
    * **Description:** Specifies the value of a uniform variable for the current program object.
* **`GL.Viewport(int x, int y, int width, int height)`**
    * **Description:** Sets the viewport rectangle for rendering.
* **`GL.DrawBuffer(DrawBufferMode mode)` / `GL.DrawBuffers(int n, DrawBuffersEnum[] bufs)`**
    * **Description:** `DrawBuffer` specifies a single color buffer for drawing. `DrawBuffers` specifies multiple color buffers for drawing (MRT).
* **`GL.ReadBuffer(ReadBufferMode mode)`**
    * **Description:** Selects a color buffer source for pixels.
* **`GL.BlitFramebuffer(int srcX0, int srcY0, int srcX1, int srcY1, int dstX0, int dstY0, int dstX1, int dstY1, ClearBufferMask mask, BlitFramebufferFilter filter)`**
    * **Description:** Copies a block of pixels from the read framebuffer to the draw framebuffer. Used for resolving multisample FBOs.
* **`GL.TexImage3D(TextureTarget target, int level, PixelInternalFormat internalformat, int width, int height, int depth, int border, PixelFormat format, PixelType type, IntPtr pixels)`**
    * **Description:** Specifies a three-dimensional texture image (used for `Texture2DArray` and `Texture3D`).
* **`GL.FramebufferTextureLayer(FramebufferTarget target, FramebufferAttachment attachment, int texture, int level, int layer)`**
    * **Description:** Attaches a single layer of a 2D array texture or 3D texture to an FBO.
* **`GL.Enable(EnableCap.Blend)` / `GL.BlendFunc(BlendingFactor sfactor, BlendingFactor dfactor)` / `GL.BlendEquation(BlendEquationMode mode)`**
    * **Description:** Controls pixel arithmetic for blending. `BlendFunc` defines the pixel blending factors. `BlendEquation` defines how source and destination are combined.

---

This plan provides a structured approach. Remember to iterate, test frequently, and consult multiple resources for each topic. Good luck with the development of Radiance!