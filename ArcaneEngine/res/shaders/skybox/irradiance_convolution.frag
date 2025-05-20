#version 330 core
in vec3 v_LocalPos; // Normal direction for this fragment
out vec4 FragColor;

uniform samplerCube u_EnvironmentMap;

// --- TUNABLES ---
// You can expose these as uniforms to tweak from C# without recompiling shaders
const uint NUM_IRRADIANCE_SAMPLES = 6000u; // Increase for better quality, decrease for speed
const float INPUT_LOD_BIAS = 1.5;       // LOD bias for sampling u_EnvironmentMap. Higher = more blur on input.
                                         // Try values from 0.0 (no bias) up to 2.0 or 3.0.
const float MAX_SAMPLE_CLAMP_VALUE = 35.0; // Clamp value for individual samples from u_EnvironmentMap.
                                         // Adjust based on your HDRI's intensity.

const float PI = 3.14159265359;

// Hammersley sequence for QMC (seems correct)
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

// Generates a cosine-weighted sample direction on the hemisphere around Z-axis (0,0,1)
// Xi.x for phi, Xi.y for theta distribution
vec3 CosineSampleHemisphere(vec2 Xi) {
    float phi = 2.0 * PI * Xi.x;
    // sin^2(theta) = Xi.y -> sin(theta) = sqrt(Xi.y)
    // This distributes samples more towards the horizon of the hemisphere,
    // which is correct for cosine-weighted importance sampling (P(theta, phi) ~ cos(theta)sin(theta)).
    float sinTheta = sqrt(Xi.y);
    float cosTheta = sqrt(1.0 - Xi.y); // This implies sinTheta^2 = Xi.y

    vec3 H; // Sample vector in tangent space
    H.x = cos(phi) * sinTheta;
    H.y = sin(phi) * sinTheta;
    H.z = cosTheta; // Aligned with the Z-axis (normal) in tangent space
    return H; // Already normalized if math is correct
}

void main() {
    vec3 N = normalize(v_LocalPos); // The normal for this point on the cubemap face
    vec3 irradiance = vec3(0.0);

    // Create tangent-space basis robustly (seems correct)
    vec3 world_up_reference = abs(N.z) < 0.999 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
    vec3 tangent = normalize(cross(world_up_reference, N));
    vec3 bitangent = normalize(cross(N, tangent)); // Ensure consistent winding
    mat3 TBN = mat3(tangent, bitangent, N); // Transforms from tangent space to world space

    for(uint i = 0u; i < NUM_IRRADIANCE_SAMPLES; ++i) {
        vec2 Xi = Hammersley(i, NUM_IRRADIANCE_SAMPLES);
        vec3 tangentSample = CosineSampleHemisphere(Xi);
        vec3 worldSampleVec = TBN * tangentSample; // Already normalized if TBN and tangentSample are

        // Sample from the environment map with an LOD bias
        vec3 rawSampleColor = textureLod(u_EnvironmentMap, worldSampleVec, INPUT_LOD_BIAS).rgb;

        // Clamp the sampled color to reduce impact of extreme fireflies
        vec3 clampedSampleColor = clamp(rawSampleColor, vec3(0.0), vec3(MAX_SAMPLE_CLAMP_VALUE));

        // For cosine importance sampling, the PDF includes cos(theta)/PI.
        // The integral is integral(L(w) * cos(theta) * dw).
        // The Monte Carlo estimator: (1/N) * Sum_i ( L(wi) * cos(theta_i) / PDF(wi) )
        // If PDF(wi) = cos(theta_i) / PI, then estimator = (1/N) * Sum_i ( L(wi) * PI ).
        // So, we sum L(wi) (which is 'clampedSampleColor') and then multiply by PI/N later.
        irradiance += clampedSampleColor;
    }

    if(NUM_IRRADIANCE_SAMPLES > 0u) {
        irradiance = irradiance * PI * (1.0 / float(NUM_IRRADIANCE_SAMPLES));
    } else {
        irradiance = vec3(0.0);
    }

    FragColor = vec4(irradiance, 1.0);
}