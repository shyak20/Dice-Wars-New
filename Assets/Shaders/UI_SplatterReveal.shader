Shader "DiceGame/UI Splatter Reveal (URP)"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        [HDR] _Color("Tint", Color) = (1, 1, 1, 1)

        _RevealAmount("Reveal (0 hidden, 1 full)", Range(0, 1)) = 0
        _SplatterMaskOffset("Splatter mask UV offset (XY)", Vector) = (0, 0, 0, 0)
        _SplatterScale("Splatter scale", Float) = 24
        _EdgeSoftness("Edge softness", Range(0.001, 0.35)) = 0.06
        _Randomness("Randomness", Range(0, 1)) = 0.65
        _WarpStrength("Splash warp strength", Range(0, 0.6)) = 0.22

        [Header(Splatter noise)]
        [Toggle(USE_SPLATTER_NOISE_MAP)] _UseSplatterNoiseMap("Use splatter noise map", Float) = 0
        _SplatterNoiseMap("Splatter noise map", 2D) = "white" {}

        [HDR] _MaskOutlineColor("Mask outline color", Color) = (0.2, 0.55, 1, 1)
        _MaskOutlineWidth("Mask outline width (× edge softness)", Range(0.05, 6)) = 1.35

        [HideInInspector] _StencilComp("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask("Color Mask", Float) = 15
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

        Pass
        {
            Name "UISplatterReveal"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment USE_SPLATTER_NOISE_MAP

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

            TEXTURE2D(_SplatterNoiseMap);
            SAMPLER(sampler_SplatterNoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _SplatterNoiseMap_ST;
                half4 _Color;
                half4 _SplatterMaskOffset;
                half _RevealAmount;
                half _SplatterScale;
                half _EdgeSoftness;
                half _Randomness;
                half _WarpStrength;
                half4 _MaskOutlineColor;
                half _MaskOutlineWidth;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(234.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float2 Hash22(float2 p)
            {
                float n = Hash21(p);
                float m = Hash21(p + float2(19.19, 47.71));
                return float2(n, m) * 2.0 - 1.0;
            }

            float ValueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm3(float2 p)
            {
                float sum = 0.0;
                float amp = 0.52;
                float2 q = p;
                [unroll] for (int i = 0; i < 3; i++)
                {
                    sum += ValueNoise(q) * amp;
                    q *= 2.07;
                    amp *= 0.5;
                }
                return sum;
            }

            float2 DomainWarpSplash(float2 uv)
            {
                float s = saturate((float)_WarpStrength);
                float scale = max((float)_SplatterScale, 1e-5);
                float2 q = uv * (_SplatterScale * 0.35);
                float2 off = float2(Fbm3(q + float2(0.13, 1.71)), Fbm3(q + float2(6.91, 0.73)));
                return uv + (off - 0.48) * s * (6.0 / scale);
            }

            float MetaballDroplets(float2 uv, float density)
            {
                float2 g = uv * density;
                float2 id = floor(g);
                float2 gv = frac(g) - 0.5;

                float acc = 0.0;
                [unroll] for (int y = -1; y <= 1; y++)
                {
                    [unroll] for (int x = -1; x <= 1; x++)
                    {
                        float2 offs = float2((float)x, (float)y);
                        float2 cellId = id + offs;
                        float h = Hash21(cellId);
                        float2 jitter = Hash22(cellId + 19.417) * 0.43;
                        float2 p = gv - offs - jitter;

                        float rz = saturate(0.16 + h * h * 0.52);
                        float len = length(p);
                        float d = len / rz;
                        float blob = exp(-d * d * 3.4);
                        blob *= lerp(0.55, 1.05, saturate(h * h * h));
                        blob *= lerp(0.75, 1.35, saturate(1.05 - dot(p, p)));
                        acc += blob;
                    }
                }
                return saturate(acc);
            }

            float SplashPhaseField(float2 uv)
            {
                float2 uw = DomainWarpSplash(uv);

                float d1 = _SplatterScale;
                float dropletsPrimary = MetaballDroplets(uw + float2(0.11, -0.09), d1 * 1.03);
                float dropletsSecondary = MetaballDroplets(uw * 1.71 + float2(14.07, -7.71), d1 * 0.71);
                float dropletsAccent = MetaballDroplets(uw * -0.85 + float2(-3.2, 9.6), d1 * 1.38);

                float ripples = saturate(Fbm3(uw * d1 * 0.092 + float2(2.71, -5.3)) + 0.08);
                float mist = saturate(Fbm3(uw * d1 * 0.061 + float2(-8.81, 1.94)) + 0.06);

                float merged = saturate(
                    dropletsPrimary * 1.07 +
                    dropletsSecondary * 0.92 +
                    dropletsAccent * 0.62 +
                    ripples * 0.42 +
                    mist * 0.28 -
                    0.45);

                merged = saturate(pow(max(merged, 1e-4), lerp(0.95, 0.62, saturate(_Randomness))));

                float2 cellPick = uw * (_SplatterScale * 1.06);
                float cellRnd = Hash21(floor(cellPick) + float2(4.71, -2.19));
                float mixed = saturate(lerp(merged * 1.06, saturate(merged * 0.55 + cellRnd * 0.45), _Randomness));
                return saturate(mixed * 0.97 + 0.015);
            }

            float SplatterNoiseMapSample01(float2 uv)
            {
                float2 nuv = TRANSFORM_TEX(uv, _SplatterNoiseMap);
                float3 s = SAMPLE_TEXTURE2D(_SplatterNoiseMap, sampler_SplatterNoiseMap, nuv).rgb;
                float lum = saturate(dot(s, float3(0.33333333, 0.33333333, 0.33333333)));
                // Match procedural path end-cap so softness/band clamps behave similarly.
                return saturate(lum * 0.97 + 0.015);
            }

            float SplatterThreshold(float2 uv)
            {
#ifdef USE_SPLATTER_NOISE_MAP
                return SplatterNoiseMapSample01(uv);
#else
                return SplashPhaseField(uv);
#endif
            }

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

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                half srcAlpha = saturate(tex.a);

                half4 c = tex * input.color * _Color;

                half edge = max((half)0.0005, _EdgeSoftness);

                half hi = saturate((half)1 - edge * (half)3);
                half lo = saturate(edge * (half)3);
                float2 maskUv = (float2)input.uv + (float2)_SplatterMaskOffset.xy;
                half threshold = lerp(lo, hi, (half)SplatterThreshold(maskUv));

                half progress = saturate((half)_RevealAmount);
                half reveal = smoothstep(threshold - edge, threshold + edge, progress);

                // Frontier band: near |progress - threshold| within the soften ramp.
                half span = edge * (half)2 + (half)1e-5;
                half uRamp = saturate((progress - (threshold - edge)) / span);
                half rampCore = saturate(uRamp * ((half)1 - uRamp) * (half)4);
                half w = edge * (_MaskOutlineWidth) + (half)1e-4;
                half nearFront = saturate((half)1 - smoothstep((half)0, w, abs(progress - threshold)));
                half outlineMask = saturate(rampCore * nearFront);

                half outlineWeight = saturate(_MaskOutlineColor.a);
                half3 outlineRgb = _MaskOutlineColor.rgb * (outlineWeight * outlineMask * srcAlpha);
                half outlineA = outlineMask * outlineWeight * srcAlpha;

                c.rgb += outlineRgb;

                half baseA = c.a * reveal;
                c.a = saturate(baseA + outlineA);

                return c;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
