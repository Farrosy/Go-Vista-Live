// Shared URP helpers for the ASCL vehicle shaders (CarColor, CarColorEnv, Window, Window2).
// Ports the original Built-in "BlinnPhong" surface-shader lighting to URP so the existing
// materials keep their look: same property names, same paint-mask (texture alpha) logic.
//
// Each shader must declare its UnityPerMaterial CBUFFER (and define ASCL_UV_TRANSFORM if it
// wants tiling/offset) BEFORE including this file.

#ifndef ASCL_URP_COMMON_INCLUDED
#define ASCL_URP_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#ifndef ASCL_UV_TRANSFORM
    #define ASCL_UV_TRANSFORM(uv) (uv)
#endif

// Built-in BlinnPhong: SurfaceOutput.Specular is multiplied by 128 to get the exponent.
#define ASCL_SPEC_EXPONENT_SCALE 128.0
#define ASCL_MIN_SPEC_EXPONENT   1e-4

struct ASCLAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct ASCLVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3  normalWS   : TEXCOORD2;
    half4  tangentWS  : TEXCOORD3; // xyz tangent, w bitangent sign
    half   fogFactor  : TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

ASCLVaryings ASCLVertex(ASCLAttributes input)
{
    ASCLVaryings output = (ASCLVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.positionCS = positionInputs.positionCS;
    output.positionWS = positionInputs.positionWS;
    output.normalWS = normalInputs.normalWS;
    output.tangentWS = half4(normalInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.uv = ASCL_UV_TRANSFORM(input.uv);
    output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
    return output;
}

InputData ASCLCreateInputData(ASCLVaryings input, half3 normalWS)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.fogCoord = input.fogFactor;
    inputData.bakedGI = SampleSH(normalWS);
    inputData.shadowMask = half4(1, 1, 1, 1);

#if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
    inputData.shadowCoord = float4(inputData.normalizedScreenSpaceUV, 0, 1);
#else
    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#endif
    return inputData;
}

// One light, Built-in BlinnPhong model:
//   diffuse  = albedo * lightColor * N.L
//   specular = lightColor * _SpecColor * pow(N.H, specular * 128) * gloss
half3 ASCLBlinnPhongLight(Light light, half3 albedo, half3 normalWS, half3 viewDirWS,
                          half specular, half gloss, half3 specColor, half diffuseScale)
{
    half3 lightColor = light.color * (light.distanceAttenuation * light.shadowAttenuation);
    half nDotL = saturate(dot(normalWS, light.direction));
    half3 halfDir = SafeNormalize(light.direction + viewDirWS);
    half nDotH = saturate(dot(normalWS, halfDir));
    half spec = pow(nDotH, max(specular * ASCL_SPEC_EXPONENT_SCALE, ASCL_MIN_SPEC_EXPONENT)) * gloss;
    return (albedo * nDotL * diffuseScale + specColor * spec) * lightColor;
}

// Ambient (SH) + main light + additional lights (Forward and Forward+/cluster loop).
// diffuseScale lets ASCL/Window keep its legacy "atten * 2" brightness.
half3 ASCLComputeLighting(InputData inputData, half3 albedo, half specular, half gloss,
                          half3 specColor, half diffuseScale)
{
    uint meshRenderingLayers = GetMeshRenderingLayer();
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV, 1.0h);
    half3 viewDirWS = inputData.viewDirectionWS;
    half3 normalWS = inputData.normalWS;

    half3 color = albedo * inputData.bakedGI * aoFactor.indirectAmbientOcclusion;

    Light mainLight = GetMainLight(inputData, inputData.shadowMask, aoFactor);
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        color += ASCLBlinnPhongLight(mainLight, albedo, normalWS, viewDirWS, specular, gloss, specColor, diffuseScale);
    }

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, inputData, inputData.shadowMask, aoFactor);
    #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
    #endif
        {
            color += ASCLBlinnPhongLight(light, albedo, normalWS, viewDirWS, specular, gloss, specColor, diffuseScale);
        }
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, inputData.shadowMask, aoFactor);
    #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
    #endif
        {
            color += ASCLBlinnPhongLight(light, albedo, normalWS, viewDirWS, specular, gloss, specColor, diffuseScale);
        }
    LIGHT_LOOP_END
#endif

    return color;
}

// Paint mask shared by CarColor / CarColorEnv / Window2:
// texture alpha = 1 -> tinted by _Color (car body), alpha = 0 -> raw texture (tyres, chrome, lights).
half3 ASCLPaintMask(half4 tex, half3 paintColor, half paintBoost)
{
    return tex.rgb * (1.0h - tex.a) + tex.a * paintColor * tex.rgb * paintBoost;
}

#endif // ASCL_URP_COMMON_INCLUDED
