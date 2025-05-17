#version 330 core
// ArcaneEngine/res/shaders/skybox/brdf_integration.frag
// Fragment shader to generate the 2D BRDF integration map (LUT).
// This LUT stores the scale and bias factor for the specular IBL split-sum approximation.
// It integrates the BRDF over the hemisphere for varying NdotV and roughness values.

// Input from vertex shader: UV coordinates of the screen quad
// v_TexCoords.x maps to NdotV
// v_TexCoords.y maps to roughness
in vec2 v_TexCoords;

// Output color for the fragment (Scale and Bias for BRDF)
out vec2 FragColor; // Outputting two components (Scale, Bias)

const float PI = 3.14159265359;
const uint SAMPLE_COUNT = 1024u; // Number of samples for Monte Carlo integration

// Generates a low-discrepancy Hammersley sequence point.
vec2 Hammersley(uint i, uint N)
{
    float radicalInverseVDC = 0.0;
    float invN = 1.0 / float(N);
    float base = 0.5; // For base 2
    uint temp_i = i;
    while(temp_i > 0u)
    {
        radicalInverseVDC += float(temp_i % 2u) * base;
        temp_i /= 2u;
        base *= 0.5;
    }
    return vec2(float(i) * invN, radicalInverseVDC);
}

// GGX Normal Distribution Function (NDF)
float DistributionGGX(vec3 N, vec3 H, float roughness)
{
    float a      = roughness*roughness;
    float a2     = a*a;
    float NdotH  = max(dot(N, H), 0.0);
    float NdotH2 = NdotH*NdotH;

    float num   = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

    return num / max(denom, 0.0000001); // Prevent division by zero
}

// Geometry function (Schlick-GGX)
float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = (roughness + 1.0);
    float k = (r*r) / 8.0; // (roughness + 1)^2 / 8 for direct lighting
    // For IBL, k = roughness^2 / 2
    // float k_ibl = (roughness*roughness) / 2.0;

    float num   = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return num / denom;
}

// Geometry Smith function (combines Schlick-GGX for light and view)
float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2  = GeometrySchlickGGX(NdotV, roughness);
    float ggx1  = GeometrySchlickGGX(NdotL, roughness);

    return ggx1 * ggx2;
}

// Importance samples the GGX distribution for a given roughness.
// Returns a sample direction (H, the microfacet normal) in tangent space.
vec3 ImportanceSampleGGX_H(vec2 Xi, float roughness)
{
    float a = roughness * roughness;

    float phi = 2.0 * PI * Xi.x;
    // Xi.y is cos^2(theta_h) for GGX, so cos(theta_h) = sqrt(Xi.y)
    // More common formulation for GGX importance sampling of H:
    float cosTheta_h = sqrt((1.0 - Xi.y) / (1.0 + (a*a - 1.0) * Xi.y));
    float sinTheta_h = sqrt(1.0 - cosTheta_h*cosTheta_h);

    vec3 H;
    H.x = cos(phi) * sinTheta_h;
    H.y = sin(phi) * sinTheta_h;
    H.z = cosTheta_h;
    return H;
}


void main()
{
    float NdotV = v_TexCoords.x; // Should be in [0, 1]
    float roughness = v_TexCoords.y; // Should be in [0, 1]

    // Clamp NdotV to avoid issues at grazing angles if it's slightly outside [0,1] due to interpolation
    NdotV = max(min(NdotV, 1.0), 0.001); // Avoid NdotV = 0

    vec3 V; // View vector
    V.x = sqrt(1.0 - NdotV*NdotV); // sin(theta_v)
    V.y = 0.0;
    V.z = NdotV;                   // cos(theta_v) (N is (0,0,1) in tangent space)

    vec3 N = vec3(0.0, 0.0, 1.0); // Normal in tangent space

    float A = 0.0; // Corresponds to the "scale" part of the split-sum approximation
    float B = 0.0; // Corresponds to the "bias" part

    for(uint i = 0u; i < SAMPLE_COUNT; ++i)
    {
        vec2 Xi = Hammersley(i, SAMPLE_COUNT);
        vec3 H  = ImportanceSampleGGX_H(Xi, roughness); // Sample microfacet normal H in tangent space
        vec3 L  = normalize(2.0 * dot(V, H) * H - V); // Calculate light vector L (reflection of V about H)

        float NdotL = max(L.z, 0.0); // N is (0,0,1), so N.L = L.z
        float NdotH = max(H.z, 0.0); // N.H = H.z
        float VdotH = max(dot(V, H), 0.0);

        if(NdotL > 0.0)
        {
            

            float G = GeometrySmith(N, V, L, roughness);
            float G_Vis = (G * VdotH) / (NdotH * NdotV + 0.00001); // G_Vis = G * V.H / (N.H * N.V)
            
            float Fc = pow(1.0 - VdotH, 5.0); // Fresnel factor for F0 = 0 ( (1-c)^5 )
            
            A += (1.0 - Fc) * G_Vis * NdotL;
            B += Fc * G_Vis * NdotL;
        }
    }
    
    // Normalize the accumulated values
    A /= float(SAMPLE_COUNT);
    B /= float(SAMPLE_COUNT);

    FragColor = vec2(A, B);
}

