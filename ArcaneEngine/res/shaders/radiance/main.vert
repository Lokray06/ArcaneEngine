#version 330 core
// ArcaneEngine/res/shaders/radiance/main.vert

// Input vertex attributes
layout(location = 0) in vec3 a_Position;   // Vertex position in model space
layout(location = 1) in vec3 a_Normal;     // Vertex normal in model space
layout(location = 2) in vec2 a_TexCoords;  // Texture coordinates
layout(location = 3) in vec3 a_Tangent;    // Vertex tangent in model space (for normal mapping)
// Bitangent can be derived in vertex or fragment shader if needed: cross(a_Normal, a_Tangent)

// Uniforms for transformations
uniform mat4 u_ModelMatrix;
uniform mat4 u_ViewMatrix;
uniform mat4 u_ProjectionMatrix;

// Output to Fragment Shader
out VS_OUT {
    vec3 FragPos_World; // Fragment position in world space
    vec3 Normal_World;  // Fragment normal in world space (normalized)
    vec2 TexCoords;     // Texture coordinates
    mat3 TBN;           // Tangent-to-World space transformation matrix
} vs_out;

void main() {
    // Transform vertex position to world space and then to clip space
    vec4 worldPos4 = u_ModelMatrix * vec4(a_Position, 1.0);
    vs_out.FragPos_World = worldPos4.xyz;
    gl_Position = u_ProjectionMatrix * u_ViewMatrix * worldPos4;

    // Pass texture coordinates directly
    vs_out.TexCoords = a_TexCoords;

    // Calculate Normal_World and TBN matrix
    // Normals, tangents, (and bitangents) should be transformed by the normal matrix,
    // which is the transpose inverse of the model matrix (or upper 3x3 of it if no non-uniform scaling).
    // For simplicity, if u_ModelMatrix involves only rotation and uniform scale,
    // mat3(u_ModelMatrix) can be used. For non-uniform scale, mat3(transpose(inverse(u_ModelMatrix))) is correct.
    mat3 normalMatrix = mat3(transpose(inverse(u_ModelMatrix))); // Correct for non-uniform scaling

    vec3 T_world = normalize(normalMatrix * a_Tangent);
    vec3 N_world = normalize(normalMatrix * a_Normal);

    // Re-orthogonalize T with respect to N (Gram-Schmidt)
    T_world = normalize(T_world - dot(T_world, N_world) * N_world);

    // Calculate Bitangent (B_world)
    vec3 B_world = cross(N_world, T_world);
    // Note: Handedness of B might need adjustment based on UV conventions or how bitangents are generated/stored.
    // If normal maps look inverted, try `vec3 B_world = cross(T_world, N_world);`
    // or pass a bitangent sign from vertex attributes if available.

    vs_out.Normal_World = N_world; // This is the interpolated vertex normal in world space
    vs_out.TBN = mat3(T_world, B_world, N_world); // Create TBN matrix for transforming tangent-space normals to world space
}
