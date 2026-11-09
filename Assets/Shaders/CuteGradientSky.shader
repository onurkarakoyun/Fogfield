// Fogfield / CuteGradientSky — "above the clouds" toon sky:
// three-stop gradient, sun/moon disc with soft halo, a puffy sea of clouds below the horizon,
// drifting fog puffs on the horizon band and twinkling stars at night. Colours are driven by SkyDayCycle.
Shader "Fogfield/CuteGradientSky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.55, 0.50, 0.95, 1)
        _HorizonColor ("Horizon", Color) = (1.00, 0.76, 0.78, 1)
        _BottomColor ("Bottom", Color) = (1.00, 0.88, 0.72, 1)
        _HorizonHeight ("Horizon Height", Range(-1, 1)) = 0.0
        _Blend ("Blend", Range(0.05, 2)) = 0.6
        _PuffColor ("Puff Color", Color) = (1, 1, 1, 0.18)
        _PuffScale ("Puff Scale", Float) = 3.0
        _PuffSpeed ("Puff Speed", Float) = 0.01
        _SunDir ("Sun Direction", Vector) = (0.3, 0.25, 0.9, 0)
        _SunColor ("Sun Color", Color) = (1, 0.85, 0.7, 1)
        _SunSize ("Sun Size", Range(0.001, 0.2)) = 0.035
        _CloudSeaColor ("Cloud Sea Color", Color) = (1, 0.95, 0.98, 1)
        _CloudSeaShade ("Cloud Sea Shade", Color) = (0.82, 0.72, 0.9, 1)
        _CloudScale ("Cloud Sea Scale", Float) = 1.6
        _StarAmount ("Stars", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _TopColor, _HorizonColor, _BottomColor, _PuffColor;
                float _HorizonHeight, _Blend, _PuffScale, _PuffSpeed;
                float4 _SunDir, _SunColor, _CloudSeaColor, _CloudSeaShade;
                float _SunSize, _CloudScale, _StarAmount;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }
            float fbm(float2 p)
            {
                float a = 0.5, s = 0.0;
                for (int k = 0; k < 4; k++) { s += a * vnoise(p); p *= 2.03; a *= 0.5; }
                return s;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y - _HorizonHeight;

                // base gradient
                float up = saturate(y / _Blend);
                float3 col = lerp(_HorizonColor.rgb, _TopColor.rgb, smoothstep(0, 1, up));

                // stars (night)
                if (_StarAmount > 0.001 && y > 0)
                {
                    float2 g = float2(atan2(d.x, d.z), asin(d.y)) * 90.0;
                    float2 cell = floor(g);
                    float h = hash(cell);
                    float2 c = frac(g) - 0.5;
                    float twinkle = 0.6 + 0.4 * sin(_Time.y * (2.0 + h * 4.0) + h * 30.0);
                    float star = step(0.985, h) * smoothstep(0.12, 0.0, length(c)) * twinkle;
                    col += star * _StarAmount * smoothstep(0.0, 0.25, y);
                }

                // sun / moon disc + halo
                float3 sd = normalize(_SunDir.xyz);
                float sdot = saturate(dot(d, sd));
                float disc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.7, sdot);
                float halo = pow(sdot, 24.0) * 0.45 + pow(sdot, 4.0) * 0.12;
                col = lerp(col, _SunColor.rgb, saturate(halo));
                col = lerp(col, _SunColor.rgb * 1.15, disc);

                // drifting puffs around the horizon band
                float2 uv = float2(atan2(d.x, d.z) / 6.2831853, d.y) * _PuffScale;
                uv.x += _Time.y * _PuffSpeed;
                float n = vnoise(uv * 2.0) * 0.6 + vnoise(uv * 4.3) * 0.4;
                col = lerp(col, _PuffColor.rgb, smoothstep(0.55, 0.8, n) * exp(-abs(y) * 4.0) * _PuffColor.a);

                // sea of clouds below the horizon
                if (y < 0.02)
                {
                    float below = saturate(-y);
                    float2 p = d.xz / max(-d.y + 0.08, 0.08) * _CloudScale;
                    p += _Time.y * 0.02;
                    float c = fbm(p);
                    float puff = smoothstep(0.38, 0.62, c);
                    float3 sea = lerp(_CloudSeaShade.rgb, _CloudSeaColor.rgb, puff);
                    sea = lerp(sea, _BottomColor.rgb, 0.25);
                    // sun-lit rims on the cloud tops
                    sea += _SunColor.rgb * pow(saturate(dot(normalize(float3(d.x, 0.2, d.z)), sd)), 6.0) * 0.15 * puff;
                    float t = smoothstep(0.0, 0.08, below);
                    col = lerp(col, sea, t);
                }
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
