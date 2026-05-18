Shader "DiceWars/Dissolve Lit (URP)"
{
    Properties
    {
        [Header(Surface)]
        _BaseMap("Base Map", 2D) = "white" {}
        [HDR] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        _Metallic("Metallic", Range(0, 1)) = 0

        [Header(Dissolve)]
        _DissolveAmount("Dissolve (0 visible, 1 hidden)", Range(0, 1)) = 0
        _EdgeSoftness("Edge Softness", Range(0.001, 0.35)) = 0.06
        _DissolveEdgeWidth("Dissolve Edge Width", Range(0.05, 6)) = 1.35
        [HDR] _DissolveEdgeColor("Dissolve Edge Color", Color) = (1, 0.45, 0.1, 1)
        _DissolveEdgeIntensity("Dissolve Edge Intensity", Range(0, 4)) = 2

        [Header(Noise)]
        [Toggle(USE_DISSOLVE_NOISE_TEXTURE)] _UseDissolveNoiseTexture("Use Noise Texture", Float) = 0
        _NoiseMap("Noise Map", 2D) = "white" {}
        _NoiseScaleOffset("Noise Scale (XY) Offset (ZW)", Vector) = (1, 1, 0, 0)
        _ProceduralNoiseScale("Procedural Noise Scale", Float) = 2.5
        _ProceduralRandomness("Procedural Randomness", Range(0, 1)) = 0.65
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitVert
            #pragma fragment LitFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma shader_feature_local_fragment USE_DISSOLVE_NOISE_TEXTURE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "DissolveCommon.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Smoothness;
                half _Metallic;
                float4 _NoiseMap_ST;
                float4 _NoiseScaleOffset;
                half _DissolveAmount;
                half _EdgeSoftness;
                half _DissolveEdgeWidth;
                half4 _DissolveEdgeColor;
                half _DissolveEdgeIntensity;
                half _ProceduralNoiseScale;
                half _ProceduralRandomness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float SampleDissolveNoiseValue(float3 positionOS, float2 uv)
            {
                #if defined(USE_DISSOLVE_NOISE_TEXTURE)
                    float2 nuv = uv * _NoiseScaleOffset.xy + _NoiseScaleOffset.zw;
                    nuv = TRANSFORM_TEX(nuv, _NoiseMap);
                    float lum = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, nuv).r;
                    return saturate(lum * 0.97 + 0.015);
                #else
                    return DissolveProceduralField(positionOS, uv, _ProceduralNoiseScale, _ProceduralRandomness);
                #endif
            }

            Varyings LitVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = normInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = GetShadowCoord(posInputs);
                return output;
            }

            half4 LitFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float dissolveNoise = SampleDissolveNoiseValue(input.positionOS, input.uv);

                half3 dissolveEdgeRgb;
                ApplyDissolveEdgeGlow(
                    dissolveNoise,
                    _DissolveAmount,
                    _EdgeSoftness,
                    _DissolveEdgeWidth,
                    _DissolveEdgeColor,
                    _DissolveEdgeIntensity,
                    dissolveEdgeRgb);

                half4 albedoSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 albedo = albedoSample.rgb;
                half3 normalWS = normalize(input.normalWS);

                InputData inputData;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = input.shadowCoord;
                inputData.fogCoord = 0;
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData;
                surfaceData.albedo = albedo;
                surfaceData.metallic = _Metallic;
                surfaceData.specular = half3(0.04, 0.04, 0.04);
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.emission = dissolveEdgeRgb;
                surfaceData.occlusion = 1;
                surfaceData.alpha = albedoSample.a;
                surfaceData.clearCoatMask = 0;
                surfaceData.clearCoatSmoothness = 1;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb += dissolveEdgeRgb;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local_fragment USE_DISSOLVE_NOISE_TEXTURE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "DissolveCommon.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseMap_ST;
                float4 _NoiseScaleOffset;
                half _DissolveAmount;
                half _ProceduralNoiseScale;
                half _ProceduralRandomness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float SampleDissolveNoiseValue(float3 positionOS, float2 uv)
            {
                #if defined(USE_DISSOLVE_NOISE_TEXTURE)
                    float2 nuv = uv * _NoiseScaleOffset.xy + _NoiseScaleOffset.zw;
                    nuv = TRANSFORM_TEX(nuv, _NoiseMap);
                    float lum = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, nuv).r;
                    return saturate(lum * 0.97 + 0.015);
                #else
                    return DissolveProceduralField(positionOS, uv, _ProceduralNoiseScale, _ProceduralRandomness);
                #endif
            }

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                positionCS = ApplyShadowClamping(positionCS);
                return positionCS;
            }

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = GetShadowPositionHClip(input);
                output.positionOS = input.positionOS.xyz;
                output.uv = input.uv;
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                float dissolveNoise = SampleDissolveNoiseValue(input.positionOS, input.uv);
                clip(dissolveNoise - _DissolveAmount + 1e-5);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment USE_DISSOLVE_NOISE_TEXTURE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DissolveCommon.hlsl"

            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseMap_ST;
                float4 _NoiseScaleOffset;
                half _DissolveAmount;
                half _ProceduralNoiseScale;
                half _ProceduralRandomness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float SampleDissolveNoiseValue(float3 positionOS, float2 uv)
            {
                #if defined(USE_DISSOLVE_NOISE_TEXTURE)
                    float2 nuv = uv * _NoiseScaleOffset.xy + _NoiseScaleOffset.zw;
                    nuv = TRANSFORM_TEX(nuv, _NoiseMap);
                    float lum = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, nuv).r;
                    return saturate(lum * 0.97 + 0.015);
                #else
                    return DissolveProceduralField(positionOS, uv, _ProceduralNoiseScale, _ProceduralRandomness);
                #endif
            }

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.uv = input.uv;
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                float dissolveNoise = SampleDissolveNoiseValue(input.positionOS, input.uv);
                clip(dissolveNoise - _DissolveAmount + 1e-5);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
