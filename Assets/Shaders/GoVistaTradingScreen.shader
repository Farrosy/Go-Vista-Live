// Layar chart trading animasi untuk kota (Tahap 3 polish grafik). Semua dihitung di GPU dari _Time:
// candlestick yang bergeser ke kiri, garis rata-rata bercahaya, grid, garis harga terakhir, dan bingkai neon.
// Tanpa texture dan tanpa script per frame, jadi murah di HP. Warna HDR (>1) supaya ikut bloom kota.
// Pakai di Quad (UV 0..1). _Aspect = lebar/tinggi quad supaya ketebalan garis sama di kedua sumbu.
Shader "GoVista/TradingScreen" {
    Properties {
        [HDR] _UpColor ("Candle naik", Color) = (0.12, 1.3, 0.5, 1)
        [HDR] _DownColor ("Candle turun", Color) = (1.5, 0.18, 0.28, 1)
        [HDR] _LineColor ("Garis rata-rata", Color) = (0.35, 1.0, 1.9, 1)
        [HDR] _FrameColor ("Bingkai neon", Color) = (0.25, 1.1, 2.0, 1)
        _BgTop ("Latar atas", Color) = (0.03, 0.06, 0.14, 1)
        _BgBottom ("Latar bawah", Color) = (0.01, 0.02, 0.06, 1)
        _GridColor ("Grid", Color) = (0.12, 0.22, 0.4, 1)
        _Candles ("Jumlah candle", Range(8, 48)) = 28
        _Speed ("Kecepatan geser (candle/detik)", Range(0, 3)) = 0.6
        _Seed ("Seed (beda per layar)", Float) = 0
        _Aspect ("Aspek lebar/tinggi", Float) = 1.8
        _Frame ("Tebal bingkai", Range(0, 0.1)) = 0.025
    }
    SubShader {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass {
            Name "TradingScreen"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _UpColor;
                half4 _DownColor;
                half4 _LineColor;
                half4 _FrameColor;
                half4 _BgTop;
                half4 _BgBottom;
                half4 _GridColor;
                float _Candles;
                float _Speed;
                float _Seed;
                float _Aspect;
                float _Frame;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            float Hash(float n) { return frac(sin(n * 12.9898 + _Seed * 78.233) * 43758.5453); }

            // Harga "pasar" pada indeks candle k: gabungan gelombang + sedikit acak, rentang kira-kira 0.1..0.9.
            float Price(float k) {
                float s = _Seed * 1.7;
                return 0.5 + 0.2 * sin(k * 0.23 + s) + 0.11 * sin(k * 0.71 + 2.1 * s)
                           + 0.06 * sin(k * 1.9 + 3.3 * s) + 0.06 * (Hash(k) - 0.5);
            }

            // Harga mulus (untuk garis rata-rata) di posisi candle pecahan.
            float Smooth(float x) {
                float k = floor(x);
                float f = frac(x);
                float a = (Price(k - 1) + Price(k) + Price(k + 1)) / 3.0;
                float b = (Price(k) + Price(k + 1) + Price(k + 2)) / 3.0;
                return lerp(a, b, f * f * (3.0 - 2.0 * f));
            }

            Varyings vert(Attributes input) {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings input) : SV_Target {
                float2 uv = input.uv;
                float t = _Time.y * _Speed;
                // Area chart di dalam bingkai + header tipis di atas.
                float2 p = (uv - float2(0.04, 0.08)) / float2(0.92, 0.74);
                half3 col = lerp(_BgBottom.rgb, _BgTop.rgb, uv.y);

                // Grid: horizontal tetap, vertikal ikut bergeser bersama candle.
                float x = p.x * _Candles + frac(t);
                float gy = abs(frac(p.y * 5.0) - 0.5);
                float gx = abs(frac(x / 4.0) - 0.5);
                float inChart = step(0.0, p.x) * step(p.x, 1.0) * step(0.0, p.y) * step(p.y, 1.0);
                float grid = max(smoothstep(0.48, 0.5, gy), smoothstep(0.485, 0.5, gx));
                col += _GridColor.rgb * grid * 0.6 * inChart;

                // Candlestick.
                float k = floor(x) + floor(t);
                float local = frac(x);
                float open = Price(k - 1);
                float close = Price(k);
                float hi = max(open, close) + 0.02 + 0.04 * Hash(k + 31.0);
                float lo = min(open, close) - 0.02 - 0.04 * Hash(k + 57.0);
                float bodyLo = min(open, close);
                float bodyHi = max(open, close) + 0.004;
                float body = step(abs(local - 0.5), 0.3) * step(bodyLo, p.y) * step(p.y, bodyHi);
                float wick = step(abs(local - 0.5), 0.05) * step(lo, p.y) * step(p.y, hi);
                half3 candleCol = close >= open ? _UpColor.rgb : _DownColor.rgb;
                col = lerp(col, candleCol, saturate(body + wick) * inChart);

                // Garis rata-rata bercahaya.
                float m = Smooth(x + floor(t) - 0.5);
                float d = abs(p.y - m);
                col += _LineColor.rgb * (smoothstep(0.012, 0.004, d) + 0.18 * smoothstep(0.05, 0.0, d)) * inChart;

                // Garis harga terakhir (putus-putus) + titik denyut di kanan.
                float last = Price(floor(t) + _Candles);
                float dash = step(0.5, frac(p.x * 30.0 - t));
                float lastLine = smoothstep(0.006, 0.0, abs(p.y - last)) * dash * inChart;
                col += _LineColor.rgb * lastLine * 0.6;

                // Header: tiga blok "ticker" bercahaya yang berkedip pelan.
                float header = step(0.86, uv.y) * step(uv.y, 0.94);
                float blocks = step(0.04, uv.x) * step(uv.x, 0.3) + step(0.34, uv.x) * step(uv.x, 0.5)
                             + step(0.78, uv.x) * step(uv.x, 0.96);
                float pulse = 0.65 + 0.35 * sin(_Time.y * 2.0 + _Seed);
                col += _FrameColor.rgb * header * blocks * 0.35 * pulse;

                // Garis pindai tipis + bingkai neon.
                col *= 0.92 + 0.08 * sin(uv.y * 220.0 - _Time.y * 6.0);
                float2 e = min(uv, 1.0 - uv) * float2(_Aspect, 1.0);
                float edge = min(e.x, e.y);
                col = lerp(col, _FrameColor.rgb, smoothstep(_Frame, _Frame * 0.4, edge));
                col += _FrameColor.rgb * 0.25 * smoothstep(_Frame * 3.0, _Frame, edge);

                col = MixFog(col, input.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
