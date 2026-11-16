// Ombak animasi di sekeliling pulau untuk peta penuh (GoVistaFullMap).
// _MainTex = MinimapShoreData.distanceField (R: 0 = darat, 1 = _MaxDistance meter dari pantai).
// Tiga lapisan, semuanya hanya di laut:
//  1. pita air dangkal yang lebih terang menempel ke pantai,
//  2. garis buih tipis yang "bernapas" tepat di garis pantai,
//  3. cincin buih yang bergerak keluar dari pantai lalu memudar (putus-putus oleh noise supaya tidak kaku).
// Berbasis UI/Default: mendukung RectMask2D (_ClipRect), stencil Mask, dan warna/alpha dari RawImage.
Shader "Go Vista/UI/Map Shore Waves"
{
    Properties
    {
        [PerRendererData] _MainTex ("Distance Field", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _MaxDistance ("Max Distance (m)", Float) = 28
        _WorldSize ("Texture World Size (m)", Float) = 580

        _ShallowColor ("Shallow Water", Color) = (0.55, 0.9, 0.88, 0.45)
        _ShallowWidth ("Shallow Width (m)", Float) = 7

        _FoamColor ("Foam", Color) = (1, 1, 1, 0.95)
        _EdgeFoamWidth ("Edge Foam Width (m)", Float) = 1.3

        _RingSpacing ("Ring Spacing (m)", Float) = 5.5
        _RingWidth ("Ring Width (m)", Float) = 1.0
        _RingSpeed ("Ring Speed (rings/s)", Float) = 0.28
        _RingReach ("Ring Reach (m)", Float) = 17
        _RingAlpha ("Ring Alpha", Range(0, 1)) = 0.85

        _NoiseScale ("Noise Scale (1/m)", Float) = 0.09
        _NoiseWobble ("Noise Wobble (m)", Float) = 1.6

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float4 _ClipRect;

            float _MaxDistance, _WorldSize;
            fixed4 _ShallowColor; float _ShallowWidth;
            fixed4 _FoamColor; float _EdgeFoamWidth;
            float _RingSpacing, _RingWidth, _RingSpeed, _RingReach, _RingAlpha;
            float _NoiseScale, _NoiseWobble;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float t = _Time.y;
                float d = tex2D(_MainTex, IN.uv).r * _MaxDistance;   // meter ke darat terdekat
                float2 wp = IN.uv * _WorldSize;                        // meter, untuk noise
                float px = max(fwidth(d), 1e-4);                       // meter per piksel layar (anti-alias)

                // Noise pelan: membelokkan cincin dan memutus buih supaya terlihat alami.
                float n1 = valueNoise(wp * _NoiseScale + float2(t * 0.05, -t * 0.03));
                float n2 = valueNoise(wp * (_NoiseScale * 2.7) - float2(t * 0.11, t * 0.07));
                float wobble = (n1 - 0.5) * _NoiseWobble * saturate(d / 4.0);
                float dn = d + wobble;

                // Laut saja: darat (d = 0) tidak digambar sama sekali.
                float sea = smoothstep(0.05, 0.05 + max(px, 0.25), d);

                // 1. Air dangkal.
                float shallow = 1.0 - smoothstep(0.0, _ShallowWidth, dn);
                shallow *= shallow;

                // 2. Buih di garis pantai, lebarnya berdenyut pelan.
                float edgeWidth = _EdgeFoamWidth * (0.85 + 0.25 * sin(t * 1.6 + n1 * 6.0));
                float edge = 1.0 - smoothstep(edgeWidth - px, edgeWidth + px, d);

                // 3. Cincin yang bergerak keluar. g = jarak (meter) ke puncak cincin terdekat.
                float phase = dn / _RingSpacing - t * _RingSpeed;
                float g = abs(frac(phase) - 0.5) * _RingSpacing;
                float travel = saturate(dn / _RingReach);
                float halfWidth = _RingWidth * 0.5 * (1.0 - 0.5 * travel);  // makin tipis saat menjauh
                float ring = 1.0 - smoothstep(halfWidth - px, halfWidth + px, g);
                ring *= (1.0 - travel) * (1.0 - travel);                  // memudar saat menjauh
                ring *= smoothstep(_EdgeFoamWidth, _EdgeFoamWidth * 2.0, dn); // tidak menumpuk dengan buih pantai
                ring *= smoothstep(0.25, 0.65, n2);                       // putus-putus
                ring *= _RingAlpha;

                float foam = saturate(max(edge, ring)) * _FoamColor.a;
                float shallowA = shallow * _ShallowColor.a;
                float alpha = saturate(shallowA + foam * (1.0 - shallowA));
                fixed3 rgb = lerp(_ShallowColor.rgb, _FoamColor.rgb, alpha > 1e-4 ? foam / alpha : 0.0);

                fixed4 color = fixed4(rgb, alpha * sea) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
        ENDCG
        }
    }
}
