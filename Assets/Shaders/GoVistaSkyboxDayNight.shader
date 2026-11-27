// Skybox kota untuk siklus siang-malam. Memakai cubemap yang sama dengan Skybox/Cubemap (Skybox_5), lalu
// DayNightCycle mengatur _Grade: 0 = cubemap asli (siang, persis seperti sebelumnya), mendekati 1 = langit
// diwarnai ulang. Awan diambil kecerahannya saja lalu dikali gradasi _HorizonColor -> _TopColor
// (oranye saat senja, biru gelap saat malam), plus cahaya matahari di sekitar _SunDirection.
// Tint biasa tidak bisa dipakai: oranye x langit biru = hijau.
Shader "GoVista/SkyboxDayNight" {
    Properties {
        [NoScaleOffset] _Tex ("Cubemap (HDR)", Cube) = "grey" {}
        _Tint ("Tint", Color) = (0.5, 0.5, 0.5, 0.5)
        [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
        _Rotation ("Rotation", Range(0, 360)) = 0
        _Grade ("Warnai ulang (0 = asli)", Range(0, 1)) = 0
        _TopColor ("Langit atas", Color) = (0.42, 0.42, 0.55, 1)
        _HorizonColor ("Cakrawala", Color) = (1.0, 0.55, 0.28, 1)
        [HDR] _SunGlowColor ("Cahaya matahari", Color) = (0, 0, 0, 1)
        _SunGlowPower ("Lebar cahaya matahari (besar = kecil)", Range(1, 64)) = 6
        _SunDirection ("Arah ke matahari (world)", Vector) = (0, 0.2, 1, 0)
    }
    SubShader {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off

        Pass {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURECUBE(_Tex);
            SAMPLER(sampler_Tex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Exposure;
                float _Rotation;
                half _Grade;
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _SunGlowColor;
                half _SunGlowPower;
                float4 _SunDirection;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 RotateAroundY(float3 v, float degrees) {
                float a = degrees * PI / 180.0;
                float s, c;
                sincos(a, s, c);
                return float3(c * v.x - s * v.z, v.y, s * v.x + c * v.z);
            }

            Varyings vert(Attributes input) {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(RotateAroundY(input.positionOS.xyz, _Rotation));
                o.dir = input.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target {
                float3 dir = normalize(i.dir);
                // Sama seperti Skybox/Cubemap bawaan: tint 0.5 = netral di gamma maupun linear.
                #ifdef UNITY_COLORSPACE_GAMMA
                    half tintScale = 2.0;
                #else
                    half tintScale = 4.59479380;
                #endif
                half3 sky = SAMPLE_TEXTURECUBE(_Tex, sampler_Tex, dir).rgb * _Tint.rgb * tintScale;

                // Senja/malam: kecerahan awan x gradasi warna langit.
                float3 worldDir = normalize(RotateAroundY(dir, _Rotation));
                half lum = dot(sky, half3(0.2126, 0.7152, 0.0722));
                half up = saturate(worldDir.y);
                half3 gradient = lerp(_HorizonColor.rgb, _TopColor.rgb, sqrt(up));
                half3 graded = gradient * (0.45 + lum * 0.9);
                half3 color = lerp(sky, graded, _Grade);

                half sun = saturate(dot(worldDir, normalize(_SunDirection.xyz)));
                color += _SunGlowColor.rgb * pow(sun, _SunGlowPower);
                return half4(color * _Exposure, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
