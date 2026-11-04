// Fogfield / UIToon — makes photo/painted UI images (tile legend icons) look cel-shaded:
// posterized value bands, boosted saturation and dark "ink" lines where colour changes sharply.
// Based on UI/Default so it keeps masking, tinting and canvas group alpha working.
Shader "Fogfield/UIToon"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Steps ("Posterize Steps", Range(2, 12)) = 5
        _Saturation ("Saturation", Range(0, 2)) = 1.35
        _InkColor ("Ink Color", Color) = (0.16, 0.10, 0.26, 1)
        _InkStrength ("Ink Strength", Range(0, 1)) = 0.85
        _InkThreshold ("Ink Threshold", Range(0.01, 0.5)) = 0.14
        _InkWidth ("Ink Width (texels)", Range(0.5, 4)) = 1.5

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
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }

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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float4 _ClipRect;
            float _Steps, _Saturation, _InkStrength, _InkThreshold, _InkWidth;
            fixed4 _InkColor;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            float Luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.texcoord);

                // posterize brightness in bands, keep chroma
                float l = Luma(tex.rgb);
                float lq = (floor(l * _Steps) + 0.5) / _Steps;
                float3 col = tex.rgb * (lq / max(l, 0.001));
                float g = Luma(col);
                col = lerp(g.xxx, col, _Saturation);

                // ink: luminance edges from the source texture
                float2 o = _MainTex_TexelSize.xy * _InkWidth;
                float lx = Luma(tex2D(_MainTex, i.texcoord + float2(o.x, 0)).rgb) - Luma(tex2D(_MainTex, i.texcoord - float2(o.x, 0)).rgb);
                float ly = Luma(tex2D(_MainTex, i.texcoord + float2(0, o.y)).rgb) - Luma(tex2D(_MainTex, i.texcoord - float2(0, o.y)).rgb);
                float edge = smoothstep(_InkThreshold, _InkThreshold * 2.0, sqrt(lx * lx + ly * ly));
                col = lerp(col, _InkColor.rgb, edge * _InkStrength);

                fixed4 c = fixed4(saturate(col), tex.a) * i.color;
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a - 0.001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
