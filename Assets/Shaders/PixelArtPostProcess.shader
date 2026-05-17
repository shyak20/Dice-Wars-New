Shader "Hidden/DiceGame/PixelArtPostProcess"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        // URP camera color (Blitter API).
        Pass
        {
            Name "PixelArtBlit"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPixelateBlit

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _PixelSize;
            float _ColorLevels;

            float4 FragPixelateBlit(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenSize = _ScreenParams.xy;
                float pixel = max(_PixelSize, 1.0);
                float2 pixelGrid = screenSize / pixel;
                float2 snappedUv = (floor(input.texcoord * pixelGrid) + 0.5) / pixelGrid;
                float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, snappedUv);

                if (_ColorLevels > 0.5)
                {
                    float levels = _ColorLevels;
                    color.rgb = floor(color.rgb * levels + 1e-4) / levels;
                }

                return color;
            }
            ENDHLSL
        }

        // Screen Space Overlay UI — end-of-frame blit from back buffer (_MainTex).
        Pass
        {
            Name "PixelArtScreen"

            HLSLPROGRAM
            #pragma vertex VertScreen
            #pragma fragment FragPixelateScreen

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _BlitScaleBias;
            float _PixelSize;
            float _ColorLevels;

            Varyings VertScreen(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _BlitScaleBias.xy + _BlitScaleBias.zw;
                return output;
            }

            float4 FragPixelateScreen(Varyings input) : SV_Target
            {
                float2 screenSize = _ScreenParams.xy;
                float pixel = max(_PixelSize, 1.0);
                float2 pixelGrid = screenSize / pixel;
                float2 snappedUv = (floor(input.uv * pixelGrid) + 0.5) / pixelGrid;
                float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_PointClamp, snappedUv);

                if (_ColorLevels > 0.5)
                {
                    float levels = _ColorLevels;
                    color.rgb = floor(color.rgb * levels + 1e-4) / levels;
                }

                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ScreenCopy"

            HLSLPROGRAM
            #pragma vertex VertScreen
            #pragma fragment FragCopyScreen

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _BlitScaleBias;

            Varyings VertScreen(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _BlitScaleBias.xy + _BlitScaleBias.zw;
                return output;
            }

            float4 FragCopyScreen(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
