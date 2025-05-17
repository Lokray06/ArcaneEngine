#version 330 core
// ArcaneEngine/res/shaders/skybox/skybox.frag
// Basic fragment shader for rendering the skybox.
// It samples the environment cubemap using the interpolated texture coordinates
// from the vertex shader.

// Input from vertex shader: interpolated texture coordinates (vertex positions)
in vec3 v_TexCoords;

// Output color for the fragment
out vec4 FragColor;

// Uniform for the environment cubemap sampler
uniform samplerCube u_EnvironmentMap;

void main()
{
    // Sample the environment cubemap using the texture coordinates
    // The v_TexCoords are the local positions of the skybox cube's vertices,
    // which directly serve as direction vectors to sample the cubemap.
    FragColor = texture(u_EnvironmentMap, v_TexCoords);

    // Optional: Apply gamma correction if rendering to a non-sRGB framebuffer
    // and the source HDR map was linear. If rendering to an sRGB framebuffer,
    // this conversion is often handled automatically by the hardware.
    // FragColor.rgb = pow(FragColor.rgb, vec3(1.0/2.2));
}

