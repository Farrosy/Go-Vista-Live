// URP port of the original Built-in surface shader "ASCL/Window2" (alpha-blended glass).
// Opacity comes from _Color.a; painted (texture alpha=1) areas get tint + cubemap reflection.
Shader "ASCL/Window2"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _DiffuseBoost ("Diffuse Boost", Range (0.03,2)) = 0.078125
        _MainTex ("Diffuse Map", 2D) = "white" {}
        _SpecColor ("Spec Lighting Color", Color) = (0.5, 0.5, 0.5,1)
        _Gloss ("(R)Specular Boost", Range (0.03, 10.0)) = 0.078125
        _Spec ("(G)Gloss Boost", Range (0.01, 0.3)) = 0.078125
        _Cube ("Cubemap", CUBE) = "" {}
        _EnvBoost ("(A)Reflection Boost", Range (0.03, 1)) = 0.078125
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half4 _SpecColor;
            half _DiffuseBoost;
            half _Gloss;
            half _Spec;
            half _EnvBoost;
        CBUFFER_END

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        TEXTURECUBE(_Cube);
        SAMPLER(sampler_Cube);

        #define ASCL_UV_TRANSFORM(uv) TRANSFORM_TEX(uv, _MainTex)
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
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

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                InputData inputData = ASCLCreateInputData(input, normalWS);

                half3 reflectDir = reflect(-inputData.viewDirectionWS, normalWS);
                half3 env = SAMPLE_TEXTURECUBE(_Cube, sampler_Cube, reflectDir).rgb;
                half3 albedo = ASCLPaintMask(tex, _Color.rgb, 1.0h) * _DiffuseBoost
                             + _Color.rgb * tex.a * env * env * _EnvBoost;

                half specular = _Spec * tex.a;
                half gloss = _Gloss * tex.a * tex.r;

                half3 color = ASCLComputeLighting(inputData, albedo, specular, gloss, _SpecColor.rgb, 1.0h);
                color = MixFog(color, inputData.fogCoord);
                return half4(color, _Color.a);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
