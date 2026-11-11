// URP port of the original Built-in surface shader "ASCL/Window" (Strumpy-style glass).
// Tinted cubemap reflection (perturbed by a normal map) as albedo + emission, alpha = _Opacity.
Shader "ASCL/Window"
{
    Properties
    {
        _NormalBoost("_NormalBoost", Float) = 1
        _NormalMap("_NormalMap", 2D) = "bump" {}
        _envMap("_envMap", Cube) = "black" {}
        _EnvStrength("_EnvStrength", Float) = 0.5
        _TintColor("_TintColor", Color) = (0,0,0,1)
        _Opacity("_Opacity", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="False" "RenderPipeline"="UniversalPipeline" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _NormalMap_ST;
            half4 _TintColor;
            half _NormalBoost;
            half _EnvStrength;
            half _Opacity;
        CBUFFER_END

        TEXTURE2D(_NormalMap);
        SAMPLER(sampler_NormalMap);
        TEXTURECUBE(_envMap);
        SAMPLER(sampler_envMap);

        #define ASCL_UV_TRANSFORM(uv) TRANSFORM_TEX(uv, _NormalMap)

        // The original custom lighting doubled light intensity (res *= atten * 2.0).
        #define ASCL_WINDOW_LEGACY_LIGHT_SCALE 2.0h
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ASCLVertex
            #pragma fragment Frag

            #define _SURFACE_TYPE_TRANSPARENT 1

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "ASCL_URPCommon.hlsl"

            half4 Frag(ASCLVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));
                normalTS.xy *= _NormalBoost;

                half3 bitangentWS = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS);
                half3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld));

                InputData inputData = ASCLCreateInputData(input, normalWS);

                half3 reflectDir = reflect(-inputData.viewDirectionWS, normalWS);
                half3 albedo = _TintColor.rgb * SAMPLE_TEXTURECUBE(_envMap, sampler_envMap, reflectDir).rgb;
                half3 emission = _EnvStrength * albedo;

                // Original lighting had Specular = Gloss = 0, i.e. diffuse only.
                half3 color = ASCLComputeLighting(inputData, albedo, 0.0h, 0.0h, half3(0, 0, 0),
                                                  ASCL_WINDOW_LEGACY_LIGHT_SCALE) + emission;
                color = MixFog(color, inputData.fogCoord);
                return half4(color, saturate(_Opacity));
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
