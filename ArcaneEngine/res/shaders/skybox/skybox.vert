#version 330 core
// ArcaneEngine/res/shaders/skybox/skybox.vert
// Basic vertex shader for rendering the skybox cube.
// It transforms vertex positions by the view and projection matrices
// and passes the texture coordinates (vertex positions) to the fragment shader.

// Input vertex attribute: position of the cube's vertices
layout (location = 0) in vec3 a_Position;

// Output to fragment shader: texture coordinates for sampling the cubemap
// For a skybox, the vertex positions themselves are used as texture coordinates.
out vec3 v_TexCoords;

// Uniforms for transforming the vertex positions
// The view matrix is modified to remove translation so the skybox follows the camera.
uniform mat4 u_ViewMatrix;       // Camera's view matrix (without translation)
uniform mat4 u_ProjectionMatrix; // Camera's projection matrix

void main()
{
    // Pass the vertex position as texture coordinates for the cubemap lookup
    v_TexCoords = a_Position;

    // Transform the vertex position.
    // Note: The view matrix passed to the skybox shader should typically have its translation components zeroed out
    // to ensure the skybox remains centered around the camera.
    // The w component is set to 1.0 to ensure perspective division works correctly.
    // By setting z = w, we ensure that the skybox is always rendered at the far clip plane,
    // which is a common optimization to ensure it's always behind everything else.
    vec4 pos = u_ProjectionMatrix * u_ViewMatrix * vec4(a_Position, 1.0);
    gl_Position = pos.xyww; // Ensure z component is w for depth testing (LEQUAL)
}

