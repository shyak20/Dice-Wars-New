Shader "DiceGame/UI Fade Opacity Maskable"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1, 1, 1, 1)

        [Header(Soft fade mask)]
        _FadeMaskTex("Fade mask texture", 2D) = "white" {}
        _FadeMaskOrigin("Mask origin (world XY)", Vector) = (0, 0, 0, 0)
        _FadeMaskAxisX("Mask U axis (xyz) / lengthSq (w)", Vector) = (1, 0, 0, 1)
        _FadeMaskAxisY("Mask V axis (xyz) / lengthSq (w)", Vector) = (0, 1, 0, 1)
        _FadeOpacityMin("Opacity at mask 0", Range(0, 1)) = 0
        _FadeOpacityMax("Opacity at mask 1", Range(0, 1)) = 1
        _FadeEdgeSoftness("Edge softness", Range(0, 1)) = 0.15

        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _ColorMask("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]

        Pass
        {
            Name "UIFadeOpacityMaskable"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_FadeMaskTex);
            SAMPLER(sampler_FadeMaskTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float4 _FadeMaskOrigin;
                float4 _FadeMaskAxisX;
                float4 _FadeMaskAxisY;
                half _FadeOpacityMin;
                half _FadeOpacityMax;
                half _FadeEdgeSoftness;
            CBUFFER_END

            float2 WorldToMaskUv(float3 worldPos)
            {
                float3 rel = worldPos - _FadeMaskOrigin.xyz;
                float u = dot(_FadeMaskAxisX.xyz, rel) / max(_FadeMaskAxisX.w, 1e-5);
                float v = dot(_FadeMaskAxisY.xyz, rel) / max(_FadeMaskAxisY.w, 1e-5);
                return float2(u, v);
            }

            half FadeMaskVisibility(half maskAlpha)
            {
                half softness = saturate(_FadeEdgeSoftness);
                half rampMin = lerp((half)0.5, (half)0.0, softness);
                half rampMax = lerp((half)0.5, (half)1.0, softness);
                half t = smoothstep(rampMin, rampMax, saturate(maskAlpha));
                return lerp(_FadeOpacityMin, _FadeOpacityMax, t);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.worldPos = worldPos;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;

                float2 maskUv = WorldToMaskUv(input.worldPos);
                half maskAlpha = 0;
                if (maskUv.x >= 0 && maskUv.x <= 1 && maskUv.y >= 0 && maskUv.y <= 1)
                    maskAlpha = SAMPLE_TEXTURE2D(_FadeMaskTex, sampler_FadeMaskTex, maskUv).a;
                col.a *= FadeMaskVisibility(maskAlpha);

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
