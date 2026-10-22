// Vecinos de Vesper (Mina, Teo): tela opaca con trama, borde de luz (Fresnel / Rim Light) y emisión
// de acento. Recibe y proyecta sombras en tiempo real y participa del SSAO (pase DepthNormals).
// Iluminación: luz principal con sombras en cascada + luces adicionales (Forward y Forward+) + ambiente.
Shader "Ascua/PersonajeTela"
{
    Properties
    {
        _BaseColor ("Color base", Color) = (0.36, 0.39, 0.47, 1)
        _BaseMap ("Ruido (escala de grises)", 2D) = "white" {}
        _NoiseStrength ("Fuerza del ruido", Range(0, 1)) = 0.3
        _DetailMap ("Trama de tela", 2D) = "white" {}
        _DetailStrength ("Fuerza de la trama", Range(0, 1)) = 0.4
        _DetailTiling ("Repetición de la trama", Float) = 6
        [HDR] _EmissionColor ("Emisión (acentos)", Color) = (0, 0, 0, 1)
        [HDR] _RimColor ("Color del borde", Color) = (0.45, 0.55, 0.95, 1)
        _RimPower ("Dureza del borde", Range(0.5, 8)) = 3
        _RimIntensity ("Intensidad del borde", Range(0, 3)) = 0.55
        _Wrap ("Luz envolvente (suaviza la sombra propia)", Range(0, 1)) = 0.35
        _Ambient ("Luz ambiente", Range(0, 3)) = 1.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _BaseMap_ST;
            float _NoiseStrength;
            float4 _DetailMap_ST;
            float _DetailStrength;
            float _DetailTiling;
            float4 _EmissionColor;
            float4 _RimColor;
            float _RimPower;
            float _RimIntensity;
            float _Wrap;
            float _Ambient;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _FORWARD_PLUS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DetailMap);
            SAMPLER(sampler_DetailMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // Lambert "envolvente": la tela no tiene terminador duro, la luz se cuela un poco.
            float WrapLambert(float3 n, float3 l)
            {
                return saturate((dot(n, l) + _Wrap) / (1.0 + _Wrap));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);

                // Oclusión ambiental (SSAO) si está activa.
                float indirectAO = 1.0;
                float directAO = 1.0;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(screenUV);
                    indirectAO = ao.indirectAmbientOcclusion;
                    directAO = ao.directAmbientOcclusion;
                #endif

                // Luz principal con sombras en cascada.
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 lighting = mainLight.color * WrapLambert(normalWS, mainLight.direction) * mainLight.shadowAttenuation * directAO;
                lighting += SampleSH(normalWS) * indirectAO * _Ambient;

                // Luces adicionales (Farol, ventanas, farolas) con sus sombras, en Forward y Forward+.
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = screenUV;
                half4 shadowMask = half4(1, 1, 1, 1);
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                    lighting += light.color * light.distanceAttenuation * light.shadowAttenuation * WrapLambert(normalWS, light.direction);
                LIGHT_LOOP_END

                float noise = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).r;
                float weave = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, input.uv * _DetailTiling).r;
                float3 albedo = _BaseColor.rgb * lerp(1.0, noise, _NoiseStrength) * lerp(1.0, weave, _DetailStrength);

                // Fresnel suave: separa la silueta de la noche sin volverla translúcida.
                float rim = pow(1.0 - saturate(dot(normalWS, viewWS)), _RimPower);

                float3 color = albedo * lighting + _EmissionColor.rgb + _RimColor.rgb * rim * _RimIntensity;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
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
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 vertShadow(Attributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirection = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 fragShadow() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth

            float4 vertDepth(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half fragDepth() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertNormals
            #pragma fragment fragNormals

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings vertNormals(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 fragNormals(Varyings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
