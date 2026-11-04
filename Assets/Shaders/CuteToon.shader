// Fogfield / CuteToon  (URP 17, Unity 6.3)
// Soft two-tone cel shading with a coloured (lilac) shadow instead of grey, rim light, candy specular,
// optional texture posterize + saturation ("painted toon" look for textured tiles), emission for gameplay
// glows, and an inverted-hull ink outline.
Shader "Fogfield/CuteToon"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        _ShadowTint ("Shadow Tint", Color) = (0.55, 0.45, 0.85, 1)
        _ShadowStep ("Shadow Step", Range(-1, 1)) = 0.05
        _ShadowSoftness ("Shadow Softness", Range(0.001, 0.5)) = 0.08
        _RimColor ("Rim Color", Color) = (1, 0.92, 0.85, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.45
        _SpecSize ("Spec Size", Range(0, 1)) = 0.92
        _SpecStrength ("Spec Strength", Range(0, 1)) = 0.35
        _Posterize ("Posterize Steps (0 = off)", Range(0, 12)) = 0
        _Saturation ("Saturation", Range(0, 2)) = 1
        _Brightness ("Brightness", Range(0.5, 1.5)) = 1
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 0)
        _OutlineColor ("Outline Color", Color) = (0.18, 0.12, 0.28, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.02
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _BaseMap_ST;
            float4 _ShadowTint;
            float _ShadowStep;
            float _ShadowSoftness;
            float4 _RimColor;
            float _RimPower;
            float _RimStrength;
            float _SpecSize;
            float _SpecStrength;
            float _Posterize;
            float _Saturation;
            float _Brightness;
            float4 _EmissionColor;
            float4 _OutlineColor;
            float _OutlineWidth;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        float3 ToonRgbToHsv(float3 c)
        {
            float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
            float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
            float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
            float d = q.x - min(q.w, q.y);
            float e = 1.0e-10;
            return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
        }

        float3 ToonHsvToRgb(float3 c)
        {
            float3 p = abs(frac(c.xxx + float3(1.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
            return c.z * lerp(1.0, saturate(p - 1.0), c.y);
        }

        // painted look: quantise value/saturation into bands, push saturation, keep hue
        float3 Toonify(float3 rgb)
        {
            float3 hsv = ToonRgbToHsv(saturate(rgb));
            hsv.y = saturate(hsv.y * _Saturation);
            if (_Posterize > 0.5)
            {
                hsv.z = (floor(hsv.z * _Posterize) + 0.5) / _Posterize;
                hsv.y = (floor(hsv.y * _Posterize * 0.75) + 0.5) / (_Posterize * 0.75);
            }
            return ToonHsvToRgb(saturate(hsv)) * _Brightness;
        }
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half4 albedo = half4(Toonify(tex.rgb * _BaseColor.rgb), tex.a * _BaseColor.a);
                float3 n = normalize(i.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float ndl = dot(n, mainLight.direction);
                float lit = smoothstep(_ShadowStep - _ShadowSoftness, _ShadowStep + _ShadowSoftness, ndl);
                lit *= lerp(0.35, 1.0, mainLight.shadowAttenuation);

                // coloured shadow: tint instead of darkening to grey
                float3 shadowCol = albedo.rgb * _ShadowTint.rgb * 0.75;
                float3 litCol = albedo.rgb * mainLight.color;
                float3 col = lerp(shadowCol, litCol, lit);

                col += albedo.rgb * SampleSH(n) * 0.35;

                float3 h = normalize(mainLight.direction + v);
                col += smoothstep(_SpecSize, _SpecSize + 0.02, dot(n, h)) * _SpecStrength * lit;

                float rim = pow(1.0 - saturate(dot(n, v)), _RimPower) * _RimStrength;
                col += _RimColor.rgb * rim * (0.4 + 0.6 * lit);

                col += _EmissionColor.rgb;

                col = MixFog(col, i.fogFactor);
                return half4(col, albedo.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float fogFactor : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = normalize(TransformObjectToWorldNormal(v.normalOS));
                float dist = length(_WorldSpaceCameraPos - posWS);
                posWS += nWS * _OutlineWidth * saturate(dist * 0.04);
                o.positionCS = TransformWorldToHClip(posWS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                if (_OutlineWidth <= 0.0001) discard;
                return half4(MixFog(_OutlineColor.rgb, i.fogFactor), 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Universal Render Pipeline/Lit"
}
