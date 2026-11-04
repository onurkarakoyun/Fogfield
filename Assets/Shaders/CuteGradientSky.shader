// Fogfield / CuteGradientSky — dreamy three-stop gradient skybox with soft drifting "fog puffs".
Shader "Fogfield/CuteGradientSky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.55, 0.50, 0.95, 1)
        _HorizonColor ("Horizon", Color) = (1.00, 0.76, 0.78, 1)
        _BottomColor ("Bottom", Color) = (1.00, 0.88, 0.72, 1)
        _HorizonHeight ("Horizon Height", Range(-1, 1)) = 0.05
        _Blend ("Blend", Range(0.05, 2)) = 0.6
        _PuffColor ("Puff Color", Color) = (1, 1, 1, 0.18)
        _PuffScale ("Puff Scale", Float) = 3.0
        _PuffSpeed ("Puff Speed", Float) = 0.01
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
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y - _HorizonHeight;
                float up = saturate(y / _Blend);
                float down = saturate(-y / (_Blend * 0.6));
                float3 col = lerp(_HorizonColor.rgb, _TopColor.rgb, smoothstep(0, 1, up));
                col = lerp(col, _BottomColor.rgb, smoothstep(0, 1, down));

                // soft puffs drifting around the horizon band
                float2 uv = float2(atan2(d.x, d.z) / 6.2831853, d.y) * _PuffScale;
                uv.x += _Time.y * _PuffSpeed;
                float n = noise(uv * 2.0) * 0.6 + noise(uv * 4.3) * 0.4;
                float band = exp(-abs(y) * 4.0);
                col = lerp(col, _PuffColor.rgb, smoothstep(0.55, 0.8, n) * band * _PuffColor.a);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
