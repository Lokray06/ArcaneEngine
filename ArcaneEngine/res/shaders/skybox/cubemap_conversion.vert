#version 330 core
// ArcaneEngine/res/shaders/skybox/cubemap_conversion.vert
// Generic vertex shader for processes that render to a cubemap face (e.g., equirectangular to cubemap, convolution).
// It transforms the vertices of a unit cube by the provided view and projection matrices.
// The local vertex positions are passed through as texture coordinates for the fragment shader.

// Input vertex attribute: position of the cube's vertices
layout (location = 0) in vec3 a_Position;

// Output to fragment shader: local position of the vertex, used as a direction vector
out vec3 v_LocalPos;

// Uniforms for transforming the vertex positions
uniform mat4 u_ProjectionMatrix; // Typically a 90-degree perspective projection
uniform mat4 u_ViewMatrix;       // View matrix for one of the 6 cubemap faces

void main()
{
    // Pass the local position of the vertex to the fragment shader.
    // This will be used as the direction vector to sample from the source map
    // or to calculate lighting.
    v_LocalPos = a_Position;

    // Transform the vertex position by the view and projection matrices.
    // No model matrix is needed as we are rendering a unit cube directly in view space.
    // The .xyww swizzle is a common trick for skyboxes/cubemap rendering where you want the
    // geometry to always be at the far clip plane, ensuring it's drawn behind everything.
    // This makes the depth value 1.0 after perspective division.
    gl_Position = u_ProjectionMatrix * u_ViewMatrix * vec4(a_Position, 1.0);
}

