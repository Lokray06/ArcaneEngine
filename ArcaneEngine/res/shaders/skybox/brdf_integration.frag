#version 330 core
// ArcaneEngine/res/shaders/skybox/brdf_integration.frag
in vec2 v_TexCoords; // v_TexCoords.x = NdotV, v_TexCoords.y = roughness
out vec2 FragColor;  // Outputting two components (Scale, Bias)

const float PI = 3.14159265359;
const uint SAMPLE_COUNT = 1024u;

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

// Importance samples the GGX distribution for a given roughness.
// Returns a sample direction (H, the microfacet normal) in tangent space (N is (0,0,1)).
vec3 ImportanceSampleGGX_H(vec2 Xi, float roughness) {
    float alpha_ggx = roughness * roughness; // Map perceptual roughness to GGX alpha squared
                                         // Some definitions use alpha = roughness, then alpha_squared = alpha*alpha.
                                         // Here, alpha_ggx IS roughness^2.

    // Ensure alpha_ggx is not zero to avoid NaNs or division by zero
    alpha_ggx = max(alpha_ggx, 0.0001);

    float phi = 2.0 * PI * Xi.x;
    // Correct formula for cos(theta_h) using alpha_ggx = roughness^2
    float cosTheta_h = sqrt((1.0 - Xi.y) / (1.0 + (alpha_ggx - 1.0) * Xi.y));
    float sinTheta_h = sqrt(max(0.0, 1.0 - cosTheta_h * cosTheta_h)); // Use max to avoid negative due to precision

    vec3 H_tangent;
    H_tangent.x = cos(phi) * sinTheta_h;
    H_tangent.y = sin(phi) * sinTheta_h;
    H_tangent.z = cosTheta_h;
    return H_tangent; // Already normalized by construction
}

// Geometry function: Schlick-GGX suitable for IBL
// (Uses k_ibl = roughness^2 / 2 which is alpha_ggx / 2)
float GeometrySchlickGGX_IBL(float NdotRelevant, float roughness) {
    float alpha_ggx = roughness * roughness; // Same alpha as in ImportanceSampleGGX_H
    float k = alpha_ggx / 2.0;
    //float k = (roughness * roughness) / 2.0; // Simpler if roughness is directly [0,1]

    float num = NdotRelevant;
    float denom = NdotRelevant * (1.0 - k) + k;
    return num / max(denom, 0.000001); // Avoid division by zero
}

// Geometry Smith for IBL (N is always (0,0,1) in tangent space for this LUT)
float GeometrySmith_IBL(vec3 V_tangent, vec3 L_tangent, float roughness) {
    float NdotV = max(V_tangent.z, 0.0); // N is (0,0,1), so N.V = V.z
    float NdotL = max(L_tangent.z, 0.0); // N.L = L.z
    float ggx_V = GeometrySchlickGGX_IBL(NdotV, roughness);
    float ggx_L = GeometrySchlickGGX_IBL(NdotL, roughness);
    return ggx_V * ggx_L;
}

void main() {
    float NdotV = v_TexCoords.x;
    float roughness = v_TexCoords.y;

    // Clamp roughness to avoid issues at 0.0 if necessary for GGX (though ImportanceSampleGGX_H has max for alpha_ggx)
    roughness = max(roughness, 0.001); // Or handle alpha=0 case in GGX functions
    NdotV = max(NdotV, 0.001);      // Avoid NdotV = 0 for stability in G calculations

    vec3 V_tangent;
    V_tangent.x = sqrt(1.0 - NdotV * NdotV); // sin(theta_v)
    V_tangent.y = 0.0;                     // Arbitrary, phi_v = 0
    V_tangent.z = NdotV;                   // cos(theta_v), N_tangent is (0,0,1)

    vec3 N_tangent = vec3(0.0, 0.0, 1.0);

    float A = 0.0; // For F0 scale term
    float B = 0.0; // For F0 bias term

    for(uint i = 0u; i < SAMPLE_COUNT; ++i) {
        vec2 Xi = Hammersley(i, SAMPLE_COUNT);
        vec3 H_tangent = ImportanceSampleGGX_H(Xi, roughness);
        vec3 L_tangent = normalize(2.0 * dot(V_tangent, H_tangent) * H_tangent - V_tangent);

        float NdotL_t = max(L_tangent.z, 0.0); // N_tangent.L_tangent
        float NdotV_t = NdotV;                 // N_tangent.V_tangent (input)
        float NdotH_t = max(H_tangent.z, 0.0); // N_tangent.H_tangent
        float VdotH_t = max(dot(V_tangent, H_tangent), 0.0);

        if(NdotL_t > 0.0) {
            // G is GeometrySmith_IBL(V_tangent, L_tangent, roughness)
            // The D term is implicitly handled by the PDF of ImportanceSampleGGX_H
            // The factor 4 * NdotV * NdotL in BRDF denominator is also handled.
            // What's left from BRDF for integration with respect to H is G * F / (NdotH * PI) (or similar forms)
            // For split-sum, we are integrating (G * VdotH) / (NdotH * NdotV) effectively.
            // See LearnOpenGL for the derivation: Sum ( G_Smith * VdotH / (NdotH * NdotV) ) / num_samples

            float G_Smith = GeometrySmith_IBL(V_tangent, L_tangent, roughness);

            // This G_Vis term is directly from LearnOpenGL's BRDF integration
            // G_Vis = G_Smith(N,V,L) * VdotH / (NdotH * NdotV)
            // Note: NdotH is H_tangent.z, NdotV is V_tangent.z (input NdotV)
            float G_Vis_numerator = G_Smith * VdotH_t;
            float G_Vis_denominator = NdotH_t * NdotV_t; // NdotV_t is already max(NdotV, 0.001)

            float G_Vis = (G_Vis_denominator > 0.00001) ? (G_Vis_numerator / G_Vis_denominator) : 0.0;

            float Fc = pow(1.0 - VdotH_t, 5.0); // Schlick term for F0=1 (used to split F0 and (1-F0) parts)

            // Accumulate terms for the LUT
            // The NdotL factor from the integral measure dL is NOT part of these terms
            // when you importance sample H with a PDF proportional to D*NdotH and transform integral.
            A += (1.0 - Fc) * G_Vis;
            B += Fc * G_Vis;
        }
    }

    A /= float(SAMPLE_COUNT);
    B /= float(SAMPLE_COUNT);

    FragColor = vec2(A, B);
}