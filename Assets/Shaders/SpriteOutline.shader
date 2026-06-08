Shader "DiceGame/Sprite Outline (URP)"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        [HDR] _Color("Tint", Color) = (1, 1, 1, 1)

        [Header(Outline)]
        [HDR] _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth("Outline Width (pixels)", Range(0, 16)) = 2
        _AlphaThreshold("Sprite Alpha Threshold", Range(0, 1)) = 0.05
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

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "SpriteOutline"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                half4 _Color;
                half4 _OutlineColor;
                half _OutlineWidth;
                half _AlphaThreshold;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half SampleSpriteAlpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            /// <summary>Max alpha in a square ring up to <paramref name="widthPixels"/> texels from center.</summary>
            half SampleOutlineSupport(float2 uv, half widthPixels)
            {
                if (widthPixels <= 0.001h)
                    return 0;

                float2 texel = _MainTex_TexelSize.xy;
                half maxAlpha = 0;
                int radius = (int)ceil(widthPixels);

                [loop]
                for (int y = -radius; y <= radius; y++)
                {
                    [loop]
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (x == 0 && y == 0)
                            continue;

                        half dist = length(half2(x, y));
                        if (dist > widthPixels)
                            continue;

                        float2 offset = float2(x, y) * texel;
                        maxAlpha = max(maxAlpha, SampleSpriteAlpha(uv + offset));
                    }
                }

                return maxAlpha;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 tint = input.color * _Color;
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 sprite = tex * tint;
                half centerAlpha = sprite.a;

                if (centerAlpha >= _AlphaThreshold)
                    return sprite;

                half neighborAlpha = SampleOutlineSupport(input.uv, _OutlineWidth);
                if (neighborAlpha < _AlphaThreshold)
                    return half4(0, 0, 0, 0);

                half4 outline = _OutlineColor;
                outline.a *= tint.a * saturate(neighborAlpha);
                return outline;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
