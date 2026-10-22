// Criaturas de sombra: silueta oscura, UN color de acento en el borde (Fresnel) y emisión suave.
// Mismo lenguaje que el resto del elenco (formas simples, sin texturas detalladas).
//  _FlashAmount: parpadeo al recibir luz (sin barra de vida).
//  _Dissolve: 0 = entera, 1 = deshecha en luz (borde ámbar que avanza con ruido).
//  _Reveal: 1 = visible; valores bajos = "velada" (trama de puntos, casi invisible).
//  _CrackStrength: grietas ámbar que aparecen al revelarse (solo la Sombra velada).
// La oscuridad responde a la luz: bajo el Farol se ven sus volúmenes; lejos, es solo un contorno.
Shader "Ascua/SombraCriatura"
{
    Properties
    {
        _BaseColor ("Color del cuerpo", Color) = (0.06, 0.07, 0.15, 1)
        [HDR] _AccentColor ("Acento (borde y brillo interior)", Color) = (0.9, 0.65, 2.2, 1)
        _RimPower ("Dureza del borde", Range(0.5, 8)) = 2.2
        _RimIntensity ("Intensidad del borde", Range(0, 4)) = 1.4
        _InnerGlow ("Brillo interior", Range(0, 1)) = 0.12
        _NoiseMap ("Ruido (disolución y grietas)", 2D) = "white" {}
        _NoiseScale ("Escala del ruido", Float) = 1.6
        [HDR] _FlashColor ("Color del parpadeo", Color) = (2.2, 1.8, 1.2, 1)
        _FlashAmount ("Parpadeo", Range(0, 1)) = 0
        _Dissolve ("Disolución", Range(0, 1)) = 0
        [HDR] _DissolveEdge ("Borde de disolución", Color) = (4, 2.6, 1.2, 1)
        _Reveal ("Revelada", Range(0, 1)) = 1
        _CrackStrength ("Grietas al revelarse", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _AccentColor;
            float _RimPower;
            float _RimIntensity;
            float _InnerGlow;
            float4 _NoiseMap_ST;
            float _NoiseScale;
            float4 _FlashColor;
            float _FlashAmount;
            float _Dissolve;
            float4 _DissolveEdge;
            float _Reveal;
            float _CrackStrength;
        CBUFFER_END

        TEXTURE2D(_NoiseMap);
        SAMPLER(sampler_NoiseMap);

        // Ruido en espacio de objeto (proyección doble para que no dependa de las UV de cada primitiva).
        float SampleNoise(float3 positionOS)
        {
            float a = SAMPLE_TEXTURE2D_LOD(_NoiseMap, sampler_NoiseMap, positionOS.xy * _NoiseScale, 0).r;
            float b = SAMPLE_TEXTURE2D_LOD(_NoiseMap, sampler_NoiseMap, positionOS.zy * _NoiseScale + 0.37, 0).r;
            return (a + b) * 0.5;
        }

        // Trama ordenada 4x4: transparencia "de puntos" sin ordenar objetos transparentes.
        float Bayer4(float2 pixel)
        {
            const float m[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            int2 p = int2(fmod(pixel, 4.0));
            return (m[p.y * 4 + p.x] + 0.5) / 16.0;
        }

        // Descarta los píxeles disueltos o velados (común a todos los pases).
        void ClipShadow(float noise, float2 pixel)
        {
            clip(noise - _Dissolve * 1.05);
            clip(lerp(0.0, 1.0, _Reveal) - Bayer4(pixel) * 0.999);
        }
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
            #pragma multi_compile _ _FORWARD_PLUS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float noise = SampleNoise(input.positionOS);
                ClipShadow(noise, input.positionCS.xy);

                float3 normalWS = normalize(input.normalWS);
                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // Cuerpo oscuro que solo "existe" donde llega la luz.
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 lighting = mainLight.color * saturate(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation + SampleSH(normalWS) * 0.5;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS);
                    lighting += light.color * light.distanceAttenuation * saturate(dot(normalWS, light.direction));
                LIGHT_LOOP_END

                float rim = pow(1.0 - saturate(dot(normalWS, viewWS)), _RimPower);
                float pulse = 0.8 + 0.2 * sin(_Time.y * 2.0 + noise * 6.0);
                float3 color = _BaseColor.rgb * lighting;
                color += _AccentColor.rgb * (rim * _RimIntensity + _InnerGlow * noise * pulse);

                // Grietas ámbar de la Sombra velada al revelarse.
                float cracks = smoothstep(0.47, 0.5, noise) * smoothstep(0.53, 0.5, noise);
                color += _DissolveEdge.rgb * cracks * _CrackStrength * saturate(_Reveal * 1.2 - 0.2);

                // Parpadeo al recibir luz y borde luminoso al disolverse.
                color += _FlashColor.rgb * _FlashAmount;
                float edge = 1.0 - smoothstep(_Dissolve * 1.05, _Dissolve * 1.05 + 0.08, noise);
                color += _DissolveEdge.rgb * edge * step(0.001, _Dissolve);

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

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            Varyings vertShadow(Attributes input)
            {
                Varyings output;
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
                output.positionCS = positionCS;
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 fragShadow(Varyings input) : SV_Target
            {
                clip(SampleNoise(input.positionOS) - _Dissolve * 1.05);
                clip(_Reveal - 0.5);
                return 0;
            }
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

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            Varyings vertDepth(float4 positionOS : POSITION)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(positionOS.xyz);
                output.positionOS = positionOS.xyz;
                return output;
            }

            half fragDepth(Varyings input) : SV_Target
            {
                ClipShadow(SampleNoise(input.positionOS), input.positionCS.xy);
                return 0;
            }
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
                float3 positionOS : TEXCOORD1;
            };

            Varyings vertNormals(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 fragNormals(Varyings input) : SV_Target
            {
                ClipShadow(SampleNoise(input.positionOS), input.positionCS.xy);
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
