#version 330 core
// ArcaneEngine/res/shaders/skybox/irradiance_convolution.frag
// Fragment shader to convolve an environment cubemap to generate an irradiance map.
// This is used for the diffuse component of image-based lighting.
// It samples the environment map multiple times around the normal direction (v_LocalPos)
// and averages the results.

// Input from vertex shader: local position of the vertex (normal direction for this fragment)
in vec3 v_LocalPos;

// Output color for the fragment (convolved irradiance)
out vec4 FragColor;

// Uniform for the environment cubemap sampler
uniform samplerCube u_EnvironmentMap;

const float PI = 3.14159265359;

void main()
{
    // Normalize the incoming local position, which represents the normal (N) for this fragment
    vec3 N = normalize(v_LocalPos);

    // The irradiance calculation involves integrating incoming radiance over the hemisphere.
    // This is approximated by Monte Carlo sampling.

    vec3 irradiance = vec3(0.0);

    // Tangent space basis vectors
    vec3 up    = vec3(0.0, 1.0, 0.0);
    vec3 right = normalize(cross(up, N)); // Calculate right vector based on N and world up
    up       = normalize(cross(N, right));    // Recalculate up vector to be orthogonal to N and right

    float sampleDelta = 0.025; // Controls the density of samples
    float nrSamples = 0.0;
    for(float phi = 0.0; phi < 2.0 * PI; phi += sampleDelta)
    {
        for(float theta = 0.0; theta < 0.5 * PI; theta += sampleDelta)
        {
            // Spherical to Cartesian conversion for tangent space sample vector
            vec3 tangentSample = vec3(sin(theta) * cos(phi),  sin(theta) * sin(phi), cos(theta));
            // Transform sample from tangent to world space
            vec3 sampleVec = tangentSample.x * right + tangentSample.y * up + tangentSample.z * N;

            // Sample the environment map and accumulate, weighting by cos(theta) for Lambertian diffuse
            irradiance += texture(u_EnvironmentMap, sampleVec).rgb * cos(theta) * sin(theta);
            nrSamples++;
        }
    }
    // Average the samples and scale by PI (part of the irradiance integral)
    irradiance = PI * irradiance * (1.0 / nrSamples);

    FragColor = vec4(irradiance, 1.0);
}

