// Cuerpo de Ilse-recuerdo: translúcido, con ruido sutil y borde luminoso (Fresnel / Rim Light).
// Iluminación simple (luz principal + luces adicionales + ambiente), compatible con Forward y Forward+.
Shader "Ascua/RecuerdoTranslucido"
{
    Properties
    {
        _BaseColor ("Color base (alfa = opacidad)", Color) = (0.75, 0.84, 1, 0.5)
        _BaseMap ("Ruido (escala de grises)", 2D) = "white" {}
        _NoiseStrength ("Fuerza del ruido", Range(0, 1)) = 0.35
        [HDR] _EmissionColor ("Emisión interior", Color) = (0.2, 0.26, 0.45, 1)
        [HDR] _RimColor ("Color del borde", Color) = (0.65, 0.78, 1.2, 1)
        _RimPower ("Dureza del borde", Range(0.5, 8)) = 2.6
        _RimIntensity ("Intensidad del borde", Range(0, 5)) = 1.6
        _RimOpacity ("Opacidad extra en el borde", Range(0, 1)) = 0.35
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
            Cull Back

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

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float _NoiseStrength;
                float4 _EmissionColor;
                float4 _RimColor;
                float _RimPower;
                float _RimIntensity;
                float _RimOpacity;
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

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // Luz principal + ambiente.
                Light mainLight = GetMainLight();
                float3 lighting = mainLight.color * saturate(dot(normalWS, mainLight.direction)) + SampleSH(normalWS);

                // Luces adicionales (el Farol), en Forward y Forward+.
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS);
                    lighting += light.color * light.distanceAttenuation * saturate(dot(normalWS, light.direction));
                LIGHT_LOOP_END

                float noise = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).r;
                float3 albedo = _BaseColor.rgb * lerp(1.0, noise, _NoiseStrength);

                // Fresnel: el borde de la silueta brilla, como un recuerdo que se deshace.
                float rim = pow(1.0 - saturate(dot(normalWS, viewWS)), _RimPower);

                float3 color = albedo * lighting + _EmissionColor.rgb + _RimColor.rgb * rim * _RimIntensity;
                float alpha = saturate(_BaseColor.a + rim * _RimOpacity);

                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
