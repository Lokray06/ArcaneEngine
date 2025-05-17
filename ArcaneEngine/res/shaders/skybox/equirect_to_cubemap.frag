#version 330 core
// ArcaneEngine/res/shaders/skybox/equirect_to_cubemap.frag
// Fragment shader to convert a 2D equirectangular map to one face of a cubemap.
// It takes a local position (direction vector) and samples the equirectangular map.

// Input from vertex shader: local position of the vertex (direction vector)
in vec3 v_LocalPos;

// Output color for the fragment
out vec4 FragColor;

// Uniform for the 2D equirectangular map sampler
uniform sampler2D u_EquirectangularMap;

// Constant for PI, used in spherical to Cartesian conversion
const vec2 invAtan = vec2(0.1591, 0.3183); // 1/2PI, 1/PI

// Function to sample the equirectangular map given a 3D direction vector
vec2 SampleSphericalMap(vec3 v)
{
    // Convert Cartesian direction vector to spherical coordinates (phi, theta)
    // atan(y, x) gives phi (azimuthal angle)
    // acos(z) gives theta (polar angle)
    // Ensure v is normalized
    vec2 uv = vec2(atan(v.z, v.x), asin(v.y));
    uv *= invAtan; // Scale to [0,1] range
    uv += 0.5;     // Offset to [0,1] range (from [-0.5, 0.5])
    return uv;
}

void main()
{
    // Normalize the local position vector to get a direction
    vec3 dir = normalize(v_LocalPos);

    // Sample the equirectangular map using the direction vector
    vec2 uv = SampleSphericalMap(dir);

    // Fetch the color from the equirectangular map
    FragColor = texture(u_EquirectangularMap, uv);
}

