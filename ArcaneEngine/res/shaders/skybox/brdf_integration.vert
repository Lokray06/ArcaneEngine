#version 330 core
// ArcaneEngine/res/shaders/skybox/brdf_integration.vert
// Vertex shader for generating the BRDF integration LUT.
// It renders a full-screen quad and passes UV coordinates to the fragment shader.

// Input vertex attributes: position and UV coordinates of the quad
layout (location = 0) in vec3 a_Position; // NDC quad vertex positions (-1 to 1)
layout (location = 1) in vec2 a_TexCoords;  // UV coordinates (0 to 1)

// Output to fragment shader: UV coordinates
out vec2 v_TexCoords;

void main()
{
    // Pass the UV coordinates directly to the fragment shader.
    // These UVs will map to (NdotV, roughness) in the fragment shader.
    v_TexCoords = a_TexCoords;

    // Output the vertex position directly.
    // The input a_Position is already in Normalized Device Coordinates (NDC)
    // for a screen-space quad.
    gl_Position = vec4(a_Position, 1.0);
}

