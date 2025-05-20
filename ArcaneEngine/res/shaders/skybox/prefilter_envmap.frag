#version 330 core
in vec3 v_LocalPos;
out vec4 FragColor;

uniform samplerCube u_EnvironmentMap; // Your HDR environment cubemap (mipmapped)
uniform float u_Roughness;            // Current roughness for this prefilter mip level (0.0 to 1.0)
// uniform float u_SourceCubemapResolution; // Passed from C#, e.g., 512.0

const float PI = 3.14159265359;
// Consider making NUM_SAMPLES a uniform if you want to experiment easily from C#
const uint NUM_SAMPLES = 6000u;
const float MAX_CLAMP_VALUE = 35.0; // Clamping for individual samples

// Hammersley sequence (seems correct)
vec2 Hammersley(uint i, uint N) {
    float radicalInverseVDC = 0.0;
    float invN = 1.0 / float(N);
    float base = 0.5;
    uint temp_i = i;
    while(temp_i > 0u) {
        radicalInverseVDC += float(temp_i % 2u) * base;
        temp_i /= 2u;
        base *= 0.5;
    }
    return vec2(float(i) * invN, radicalInverseVDC);
}

// Importance Sample GGX (using the common formula, ensure alpha_sq is not zero)
vec3 ImportanceSampleGGX(vec2 Xi, vec3 N, float roughness) {
    float alpha = roughness * roughness; // Standard mapping for GGX alpha
    // float alpha = roughness; // If your previous version used 'alpha = roughness', be consistent.
                               // Common practice is (roughness*roughness) for alpha.
                               // Let's assume your u_Roughness from C# is perceptual roughness [0,1]
                               // and alpha for GGX is (perceptualRoughness)^2.

    float alpha_sq = alpha * alpha;
    alpha_sq = max(alpha_sq, 0.0001); // Prevent division by zero or issues with alpha_sq = 0

    float phi = 2.0 * PI * Xi.x;
    // Formula for cos(theta_h) for GGX NDF importance sampling
    float cosTheta = sqrt((1.0 - Xi.y) / (1.0 + (alpha_sq - 1.0) * Xi.y));
    float sinTheta = sqrt(max(0.0, 1.0 - cosTheta * cosTheta)); // max to ensure non-negative

    vec3 H_tangent;
    H_tangent.x = cos(phi) * sinTheta;
    H_tangent.y = sin(phi) * sinTheta;
    H_tangent.z = cosTheta;

    // Create orthonormal basis (TBN) around N
    vec3 up_vec = abs(N.z) < 0.999 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
    vec3 tangent_x = normalize(cross(up_vec, N));
    vec3 bitangent_y = normalize(cross(N, tangent_x)); // Ensure normalized
    // vec3 H_world = tangent_x * H_tangent.x + bitangent_y * H_tangent.y + N * H_tangent.z;
    mat3 TBN = mat3(tangent_x, bitangent_y, N);
    vec3 H_world = TBN * H_tangent;

    return normalize(H_world); // Ensure H is normalized
}

void main() {
    vec3 N = normalize(v_LocalPos); // Normal of the cubemap face pixel
    vec3 V = N; // When pre-filtering cubemap, view vector is same as normal

    vec3 prefilteredColorSum = vec3(0.0);
    float totalWeight = 0.0;

    for(uint i = 0u; i < NUM_SAMPLES; ++i) {
        vec2 Xi = Hammersley(i, NUM_SAMPLES);
        // Use u_Roughness directly for sampling distribution
        vec3 H = ImportanceSampleGGX(Xi, N, u_Roughness);
        vec3 L = normalize(2.0 * dot(V, H) * H - V); // Reflection vector

        float NdotL = max(dot(N, L), 0.0);
        if(NdotL > 0.0) {
            // --- Corrected LOD Calculation ---
            // u_SourceCubemapResolution is passed from C# (e.g., 512.0 for a 512x512 cubemap)
            // float sourceResolution = 512.0; // Or use a uniform: uniform float u_SourceCubemapResolution;

            // Max mip level index is log2(size). For a 512x512 (levels 0-9), textureQueryLevels is 10.
            // So, max index = textureQueryLevels(u_EnvironmentMap) - 1.
            float maxMipLevel = float(textureQueryLevels(u_EnvironmentMap) - 1);

            // Linearly interpolate LOD based on roughness.
            // This is a common heuristic.
            float lod = u_Roughness * maxMipLevel;

            vec3 sampledColor = textureLod(u_EnvironmentMap, L, lod).rgb;

            // Clamp the sample to reduce fireflies (optional, but often helpful)
            sampledColor = clamp(sampledColor, vec3(0.0), vec3(MAX_CLAMP_VALUE));

            prefilteredColorSum += sampledColor * NdotL;
            totalWeight += NdotL;
        }
    }

    vec3 finalPrefilteredColor = vec3(0.0);
    if(totalWeight > 0.00001) { // Avoid division by zero
        finalPrefilteredColor = prefilteredColorSum / totalWeight;
    }

    FragColor = vec4(finalPrefilteredColor, 1.0);
}