// Capa y capucha de Ilse: tela dorada pálida que ondea con el viento (desplazamiento de vértices
// tipo sin(tiempo) que crece hacia las puntas) y un pequeño "latigazo" (_Flick) al arrancar a caminar.
// Doble cara, con el mismo borde luminoso que el cuerpo. Compatible con Forward y Forward+.
Shader "Ascua/CapaViento"
{
    Properties
    {
        _BaseColor ("Color (alfa = opacidad)", Color) = (1, 0.89, 0.63, 0.9)
        _BaseMap ("Ruido (escala de grises)", 2D) = "white" {}
        _NoiseStrength ("Fuerza del ruido", Range(0, 1)) = 0.4
        [HDR] _EmissionColor ("Emisión", Color) = (0.25, 0.2, 0.1, 1)
        [HDR] _RimColor ("Color del borde", Color) = (1.2, 1.0, 0.6, 1)
        _RimPower ("Dureza del borde", Range(0.5, 8)) = 3
        _RimIntensity ("Intensidad del borde", Range(0, 5)) = 0.9
        _WindStrength ("Viento: amplitud (m)", Range(0, 0.3)) = 0.06
        _WindSpeed ("Viento: velocidad", Range(0, 8)) = 2.2
        _WindFrequency ("Viento: frecuencia espacial", Range(0, 10)) = 3
        _Flick ("Latigazo (lo anima el script, 0-1)", Range(0, 1)) = 0
        _DetailMap ("Trama de tela", 2D) = "white" {}
        _DetailStrength ("Fuerza de la trama", Range(0, 1)) = 0.45
        _DetailTiling ("Repetición de la trama", Float) = 6
        [HDR] _SeamColor ("Costura luminosa (bordes)", Color) = (1.6, 1.2, 0.6, 1)
        _SeamWidth ("Ancho de la costura", Range(0.005, 0.2)) = 0.045
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DetailMap);
            SAMPLER(sampler_DetailMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float _NoiseStrength;
                float4 _EmissionColor;
                float4 _RimColor;
                float _RimPower;
                float _RimIntensity;
                float _WindStrength;
                float _WindSpeed;
                float _WindFrequency;
                float _Flick;
                float4 _DetailMap_ST;
                float _DetailStrength;
                float _DetailTiling;
                float4 _SeamColor;
                float _SeamWidth;
            CBUFFER_END

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
                float2 rawUV : TEXCOORD4;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                // uv.y = 1 en el borde cosido (hombros), 0 en las puntas: las puntas se mueven más.
                float tip = pow(saturate(1.0 - input.uv.y), 1.5);
                float phase = dot(positionWS.xz, float2(_WindFrequency, _WindFrequency * 0.7));
                float wave = sin(_Time.y * _WindSpeed + phase) + 0.4 * sin(_Time.y * _WindSpeed * 2.3 + phase * 1.7);
                float flick = _Flick * sin(_Time.y * 14.0) * 0.12;
                float3 windDirection = normalize(float3(1.0, 0.15, 0.35));
                positionWS += windDirection * (wave * _WindStrength + flick) * tip;

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.rawUV = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS) * (isFrontFace ? 1.0 : -1.0);
                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                Light mainLight = GetMainLight();
                float3 lighting = mainLight.color * saturate(dot(normalWS, mainLight.direction)) + SampleSH(normalWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS);
                    lighting += light.color * light.distanceAttenuation * saturate(dot(normalWS, light.direction));
                LIGHT_LOOP_END

                float noise = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv * 3.0).r;
                float weave = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, input.rawUV * _DetailTiling).r;
                float3 albedo = _BaseColor.rgb * lerp(1.0, noise, _NoiseStrength) * lerp(1.0, weave, _DetailStrength);
                float rim = pow(1.0 - saturate(dot(normalWS, viewWS)), _RimPower);

                // Costura luminosa: bordes laterales y bajo de la capa (uv.x = 0/1, uv.y = 0).
                float edge = min(min(input.rawUV.x, 1.0 - input.rawUV.x), input.rawUV.y);
                float seam = 1.0 - smoothstep(_SeamWidth * 0.5, _SeamWidth, edge);

                float3 color = albedo * lighting + _EmissionColor.rgb + _RimColor.rgb * rim * _RimIntensity + _SeamColor.rgb * seam;
                color = MixFog(color, input.fogFactor);
                return half4(color, _BaseColor.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
