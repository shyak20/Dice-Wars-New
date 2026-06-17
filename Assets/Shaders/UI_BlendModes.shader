Shader "DiceGame/UI Blend Modes"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1, 1, 1, 1)

        [KeywordEnum(Normal, Multiply, Color_Burn, Lighten, Screen, Add, Overlay, Soft_Light, Hard_Light, Vivid_Light, Divide, Subtract)]
        _BlendMode("Blend Mode", Float) = 0

        [Header(Mask)]
        [Toggle] _UseMaskImage("Use Mask Image", Float) = 0
        _MaskTex("Mask (alpha gradient)", 2D) = "white" {}
        _MaskScale("Mask Scale XY", Vector) = (1, 1, 0, 0)
        _MaskPosition("Mask Position XY", Vector) = (0, 0, 0, 0)

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
            Name "UIBlendModes"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma shader_feature_local_fragment _BLENDMODE_NORMAL _BLENDMODE_MULTIPLY _BLENDMODE_COLOR_BURN _BLENDMODE_LIGHTEN _BLENDMODE_SCREEN _BLENDMODE_ADD _BLENDMODE_OVERLAY _BLENDMODE_SOFT_LIGHT _BLENDMODE_HARD_LIGHT _BLENDMODE_VIVID_LIGHT _BLENDMODE_DIVIDE _BLENDMODE_SUBTRACT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

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
                float2 maskUv : TEXCOORD2;
                float4 screenPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float4 _MaskScale;
                float4 _MaskPosition;
                float _UseMaskImage;
            CBUFFER_END

            float2 ComputeMaskUV(float2 quadUv)
            {
                float2 center = 0.5;
                float2 scale = max(_MaskScale.xy, 1e-4);
                return (quadUv - center) / scale + center + _MaskPosition.xy;
            }

            half SampleMaskAlpha(float2 quadUv)
            {
                float2 maskUv = ComputeMaskUV(quadUv);
                if (maskUv.x < 0.0 || maskUv.x > 1.0 || maskUv.y < 0.0 || maskUv.y > 1.0)
                    return 0.0h;
                return SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, maskUv).a;
            }

            float2 GetScreenUV(float4 screenPos)
            {
                float2 uv = screenPos.xy / max(screenPos.w, 1e-5);
                #if UNITY_UV_STARTS_AT_TOP
                if (_ProjectionParams.x > 0.0)
                    uv.y = 1.0 - uv.y;
                #endif
                return uv;
            }

            half3 BlendMultiply(half3 baseRgb, half3 blendRgb)
            {
                return baseRgb * blendRgb;
            }

            half3 BlendColorBurn(half3 baseRgb, half3 blendRgb)
            {
                half3 result;
                result.r = blendRgb.r <= 0.0h ? 0.0h : 1.0h - saturate((1.0h - baseRgb.r) / blendRgb.r);
                result.g = blendRgb.g <= 0.0h ? 0.0h : 1.0h - saturate((1.0h - baseRgb.g) / blendRgb.g);
                result.b = blendRgb.b <= 0.0h ? 0.0h : 1.0h - saturate((1.0h - baseRgb.b) / blendRgb.b);
                return result;
            }

            half3 BlendLighten(half3 baseRgb, half3 blendRgb)
            {
                return max(baseRgb, blendRgb);
            }

            half3 BlendScreen(half3 baseRgb, half3 blendRgb)
            {
                return 1.0h - (1.0h - baseRgb) * (1.0h - blendRgb);
            }

            half3 BlendAdd(half3 baseRgb, half3 blendRgb)
            {
                return saturate(baseRgb + blendRgb);
            }

            half OverlayChannel(half baseC, half blendC)
            {
                return baseC <= 0.5h
                    ? 2.0h * baseC * blendC
                    : 1.0h - 2.0h * (1.0h - baseC) * (1.0h - blendC);
            }

            half3 BlendOverlay(half3 baseRgb, half3 blendRgb)
            {
                return half3(
                    OverlayChannel(baseRgb.r, blendRgb.r),
                    OverlayChannel(baseRgb.g, blendRgb.g),
                    OverlayChannel(baseRgb.b, blendRgb.b));
            }

            half SoftLightChannel(half baseC, half blendC)
            {
                if (blendC <= 0.5h)
                    return baseC - (1.0h - 2.0h * blendC) * baseC * (1.0h - baseC);
                return baseC + (2.0h * blendC - 1.0h) * (sqrt(max(baseC, 0.0h)) - baseC);
            }

            half3 BlendSoftLight(half3 baseRgb, half3 blendRgb)
            {
                return half3(
                    SoftLightChannel(baseRgb.r, blendRgb.r),
                    SoftLightChannel(baseRgb.g, blendRgb.g),
                    SoftLightChannel(baseRgb.b, blendRgb.b));
            }

            half3 BlendHardLight(half3 baseRgb, half3 blendRgb)
            {
                return BlendOverlay(blendRgb, baseRgb);
            }

            half VividLightChannel(half baseC, half blendC)
            {
                if (blendC <= 0.5h)
                {
                    half burnBlend = 2.0h * blendC;
                    return burnBlend <= 0.0h ? 0.0h : 1.0h - saturate((1.0h - baseC) / burnBlend);
                }

                return saturate(baseC / max(2.0h * (1.0h - blendC), 1e-4h));
            }

            half3 BlendVividLight(half3 baseRgb, half3 blendRgb)
            {
                return half3(
                    VividLightChannel(baseRgb.r, blendRgb.r),
                    VividLightChannel(baseRgb.g, blendRgb.g),
                    VividLightChannel(baseRgb.b, blendRgb.b));
            }

            half3 BlendDivide(half3 baseRgb, half3 blendRgb)
            {
                return saturate(baseRgb / max(blendRgb, 1e-4h));
            }

            half3 BlendSubtract(half3 baseRgb, half3 blendRgb)
            {
                return max(baseRgb - blendRgb, 0.0h);
            }

            half3 ApplyBlendMode(half3 baseRgb, half3 blendRgb)
            {
                #if defined(_BLENDMODE_MULTIPLY)
                    return BlendMultiply(baseRgb, blendRgb);
                #elif defined(_BLENDMODE_COLOR_BURN)
                    return BlendColorBurn(baseRgb, blendRgb);
                #elif defined(_BLENDMODE_LIGHTEN)
                    return BlendLighten(baseRgb, blendRgb);
                #elif defined(_BLENDMODE_SCREEN)
                    return BlendScreen(baseRgb, blendRgb);
                #elif defined(_BLENDMODE_ADD)
                    return BlendAdd(baseRgb, blendRgb);
                #elif defined(_BLENDMODE_OVERLAY)
                    return BlendOverlay(baseRgb, blendRgb);
                #elif defined(_BLENDMODE_SOFT_LIGHT)
                    return BlendSoftLight(baseRgb, blendRgb);
                #elif defined(_BLENDMODE_HARD_LIGHT)
                    return BlendHardLight(baseRgb, blendRgb);
                #elif defined(_BLENDMODE_VIVID_LIGHT)
                    return BlendVividLight(baseRgb, blendRgb);
                #elif defined(_BLENDMODE_DIVIDE)
                    return BlendDivide(baseRgb, blendRgb);
                #elif defined(_BLENDMODE_SUBTRACT)
                    return BlendSubtract(baseRgb, blendRgb);
                #else
                    return blendRgb;
                #endif
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.maskUv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;

                if (_UseMaskImage > 0.5)
                    col.a *= SampleMaskAlpha(input.maskUv);

                if (col.a <= 0.0h)
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                float2 screenUV = GetScreenUV(input.screenPos);
                half3 backdrop = SampleSceneColor(screenUV).rgb;

                half3 fgRgb = col.rgb / max(col.a, 1e-4h);
                half3 blended = ApplyBlendMode(backdrop, fgRgb);
                half3 outRgb = blended * col.a + backdrop * (1.0h - col.a);

                return half4(outRgb, col.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
