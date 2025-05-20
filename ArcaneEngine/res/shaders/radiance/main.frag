#version 330 core
// ArcaneEngine/res/shaders/radiance/main.frag
// Main PBR Fragment Shader with Image-Based Lighting (IBL) integration.

// --- Outputs ---
out vec4 FragColor;

// --- Inputs from Vertex Shader ---
in VS_OUT {
    vec3 FragPos_World;    // Fragment position in world space
    vec3 Normal_World;     // Fragment normal in world space (normalized)
    vec2 TexCoords;        // Texture coordinates
    mat3 TBN;              // Tangent-Bitangent-Normal matrix for normal mapping
} fs_in;

// --- Material PBR Properties (Factors & Colors) ---
struct PBRFactors {
    vec3 AlbedoColor;
    float MetallicFactor;
    float RoughnessFactor;
    float AoFactor;
    vec3 EmissionColor;
    float EmissionStrength;
};
uniform PBRFactors u_PBRFactors;

// --- Material PBR Texture Maps ---
struct PBRMaps {
    sampler2D AlbedoMap;
    sampler2D NormalMap;
    sampler2D MetallicMap;
    sampler2D RoughnessMap;
    sampler2D AoMap;
    sampler2D EmissionMap;

    // Flags to indicate if a map is used (1 for true, 0 for false)
    int UseAlbedoMap;
    int UseNormalMap;
    int UseMetallicMap;
    int UseRoughnessMap;
    int UseAoMap;
    int UseEmissionMap;
};
uniform PBRMaps u_PBRMaps;

// --- UV Modifiers ---
uniform vec2 u_UvTiling;
uniform vec2 u_UvOffset;

// --- Camera & Lighting ---
uniform vec3 u_CameraPos_World;

// Directional Light
struct DirLight {
    vec3 Direction_World; // Direction FROM light source
    vec3 Color;
    float Intensity;
};
uniform DirLight u_DirLight;
uniform int u_UseDirLight; // 0 or 1

// Point Lights
#define MAX_POINT_LIGHTS 4
struct PointLight {
    vec3 Position_World;
    vec3 Color;
    float Intensity;
    // Attenuation
    float Constant;
    float Linear;
    float Quadratic;
};
uniform PointLight u_PointLights[MAX_POINT_LIGHTS];
uniform int u_NumPointLights;

// --- IBL Uniforms ---
uniform samplerCube u_IrradianceMap;    // For diffuse IBL
uniform samplerCube u_PrefilteredMap;   // For specular IBL (pre-filtered mipmapped cubemap)
uniform sampler2D u_BrdfLut;            // 2D BRDF lookup texture
uniform float u_MaxReflectionLod;       // Max LOD for prefiltered map sampling

// --- Constants ---
const float PI = 3.14159265359;
const float EPSILON = 0.00001; // Small value to prevent division by zero

// --- Helper Functions ---

// Calculates Normal Distribution Function (NDF) using Trowbridge-Reitz GGX
float DistributionGGX(vec3 N, vec3 H, float roughness) {
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;

    float num = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

    return num / max(denom, EPSILON); // Max to avoid division by zero
}

// Calculates Geometry Function using Schlick-GGX
float GeometrySchlickGGX(float NdotV, float roughness) {
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0; // For direct lighting

    float num = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return num / max(denom, EPSILON);
}

// Calculates Smith's Method for Geometry Function (combines visibility for light and view)
float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness) {
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx_V = GeometrySchlickGGX(NdotV, roughness); // For view vector
    float ggx_L = GeometrySchlickGGX(NdotL, roughness); // For light vector

    return ggx_V * ggx_L;
}

// Calculates Fresnel-Schlick approximation
vec3 FresnelSchlick(float cosTheta, vec3 F0) {
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

// Fresnel Schlick with roughness term for IBL
vec3 FresnelSchlickRoughness(float cosTheta, vec3 F0, float roughness) {
    return F0 + (max(vec3(1.0 - roughness), F0) - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

// --- Main Shading Logic ---
void main() {
    // --- Apply UV Tiling and Offset ---
    vec2 texCoords = fs_in.TexCoords * u_UvTiling + u_UvOffset;

    // --- Sample Material Properties ---
    vec3 albedo = u_PBRFactors.AlbedoColor;
    if(u_PBRMaps.UseAlbedoMap == 1) {
        albedo *= texture(u_PBRMaps.AlbedoMap, texCoords).rgb;
    }
    // If rendering to sRGB framebuffer, albedo map might need sRGB to Linear conversion.
    // For now, assume albedo map is linear or engine handles conversion.
    // albedo = pow(albedo, vec3(2.2)); // If albedo map is sRGB

    float metallic = u_PBRFactors.MetallicFactor;
    if(u_PBRMaps.UseMetallicMap == 1) {
        metallic *= texture(u_PBRMaps.MetallicMap, texCoords).r; // Assuming metallic is in R channel
    }
    metallic = clamp(metallic, 0.0, 1.0);

    float roughness = u_PBRFactors.RoughnessFactor;
    if(u_PBRMaps.UseRoughnessMap == 1) {
        roughness *= texture(u_PBRMaps.RoughnessMap, texCoords).g; // Assuming roughness is in G channel
    }
    roughness = clamp(roughness, 0.005, 1.0); // Clamp min roughness to avoid artifacts

    float ao = u_PBRFactors.AoFactor;
    if(u_PBRMaps.UseAoMap == 1) {
        ao *= texture(u_PBRMaps.AoMap, texCoords).r; // Assuming AO is in R channel
    }

    vec3 emission = u_PBRFactors.EmissionColor * u_PBRFactors.EmissionStrength;
    if(u_PBRMaps.UseEmissionMap == 1) {
        emission *= texture(u_PBRMaps.EmissionMap, texCoords).rgb;
    }

    // --- Normal Mapping ---
    vec3 N = normalize(fs_in.Normal_World); // Default to interpolated vertex normal
    if(u_PBRMaps.UseNormalMap == 1) {
        vec3 tangentNormal = texture(u_PBRMaps.NormalMap, texCoords).rgb * 2.0 - 1.0;
        N = normalize(fs_in.TBN * tangentNormal);
    }

    // --- Common Vectors ---
    vec3 V = normalize(u_CameraPos_World - fs_in.FragPos_World); // View direction

    // --- Surface Reflectance (F0) ---
    // For dielectrics, F0 is typically (0.04, 0.04, 0.04)
    // For metals, F0 is the albedo color
    vec3 F0 = vec3(0.04);
    F0 = mix(F0, albedo, metallic); // Interpolate F0 based on metallicness

    // --- Direct Lighting Calculation ---
    vec3 Lo = vec3(0.0); // Sum of outgoing radiance from direct lights

    // --- Directional Light ---
    if(u_UseDirLight == 1) {
        vec3 L = normalize(-u_DirLight.Direction_World); // Direction TO light source
        vec3 H = normalize(V + L);                       // Halfway vector
        float NdotL = max(dot(N, L), 0.0);
        float radiance = u_DirLight.Intensity * NdotL;   // Simplified radiance, no distance attenuation

        if(radiance > 0.0) {
            // Cook-Torrance BRDF
            float NDF = DistributionGGX(N, H, roughness);
            float G = GeometrySmith(N, V, L, roughness);
            vec3 F = FresnelSchlick(max(dot(H, V), 0.0), F0);

            vec3 kS = F; // Specular BRDF component (Fresnel)
            vec3 kD = vec3(1.0) - kS; // Diffuse BRDF component
            kD *= (1.0 - metallic);   // Metals have no diffuse reflection

            vec3 numerator = NDF * G * F;
            float denominator = 4.0 * max(dot(N, V), EPSILON) * max(NdotL, EPSILON) + EPSILON; // Use EPSILON consistently
            vec3 specular = numerator / denominator;

            // Add to outgoing radiance
            Lo += (kD * albedo / PI + specular) * radiance * u_DirLight.Color;
        }
    }

    // --- Point Lights ---
    for(int i = 0; i < u_NumPointLights; ++i) {
        vec3 L_fragToLight = u_PointLights[i].Position_World - fs_in.FragPos_World;
        float distance = length(L_fragToLight);
        vec3 L = normalize(L_fragToLight); // Direction TO light source
        vec3 H = normalize(V + L);         // Halfway vector

        // Attenuation
        float attenuation = 1.0 / (u_PointLights[i].Constant + u_PointLights[i].Linear * distance + u_PointLights[i].Quadratic * (distance * distance));
        float radiance = u_PointLights[i].Intensity * attenuation * max(dot(N, L), 0.0);

        if(radiance > 0.0) {
            // Cook-Torrance BRDF (same as directional light)
            float NDF = DistributionGGX(N, H, roughness);
            float G = GeometrySmith(N, V, L, roughness);
            vec3 F = FresnelSchlick(max(dot(H, V), 0.0), F0);

            vec3 kS = F;
            vec3 kD = vec3(1.0) - kS;
            kD *= (1.0 - metallic);

            vec3 numerator = NDF * G * F;
            float denominator = 4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0) + EPSILON;
            vec3 specular = numerator / denominator;

            Lo += (kD * albedo / PI + specular) * radiance * u_PointLights[i].Color;
        }
    }

    // --- Image-Based Lighting (IBL) ---
// Diffuse IBL (partitioning energy with angle-dependent F_ibl)
    vec3 F_ibl_energy_partition = FresnelSchlickRoughness(max(dot(N, V), 0.0), F0, roughness);
    vec3 kS_ibl_energy_partition = F_ibl_energy_partition;
    vec3 kD_ibl_energy_partition = vec3(1.0) - kS_ibl_energy_partition;
    kD_ibl_energy_partition *= (1.0 - metallic);

    vec3 irradiance = texture(u_IrradianceMap, N).rgb;
    vec3 diffuse_ibl = irradiance * albedo; // albedo is pre-multiplied by kD_ibl_energy_partition later

// Specular IBL
    vec3 R = reflect(-V, N);
    vec3 prefilteredColor = textureLod(u_PrefilteredMap, R, roughness * u_MaxReflectionLod).rgb;
    vec2 brdf_lut_sample = texture(u_BrdfLut, vec2(max(dot(N, V), 0.0), roughness)).rg;

// CORRECTED SPECULAR IBL: Use F0 with the BRDF LUT
    vec3 specular_ibl = prefilteredColor * (F0 * brdf_lut_sample.x + brdf_lut_sample.y);

// Combine IBL contributions
// Modulate diffuse_ibl by kD_ibl_energy_partition. Specular_ibl already incorporates the necessary F0 via the LUT.
    vec3 ambient_ibl = (kD_ibl_energy_partition * diffuse_ibl + specular_ibl) * ao;

    // --- Final Color ---
    // Combine direct lighting and IBL, then add emission
    vec3 color = Lo + ambient_ibl + emission;

    // HDR Tonemapping (Reinhard example, can be more sophisticated)
    // color = color / (color + vec3(1.0));

    // Gamma Correction (if rendering to a linear framebuffer and display expects sRGB)
    // If GL_FRAMEBUFFER_SRGB is enabled, this is often done automatically.
    // color = pow(color, vec3(1.0/2.2));

    FragColor = vec4(color, 1.0);
}
