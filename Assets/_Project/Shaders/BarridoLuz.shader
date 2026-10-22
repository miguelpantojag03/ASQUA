// Barrido de luz del Farol: un arco aditivo (no un arma física) que se dibuja de un lado a otro.
// UV.x recorre el arco (0 = inicio, 1 = final); UV.y va del borde interior (0) al exterior (1).
//  _Progress: posición de la "cabeza" del barrido (0-1). Detrás deja una estela que se apaga.
//  _Direction: 1 = izquierda a derecha, -1 = al revés.  _Fade: desvanecido global.
//  _Uniform: 1 = intensidad pareja a lo largo (haz recto del ataque cargado).
Shader "Ascua/BarridoLuz"
{
    Properties
    {
        [HDR] _Color ("Color de la luz", Color) = (3.2, 2.2, 1.1, 1)
        [HDR] _CoreColor ("Color del filo", Color) = (5, 4.4, 3.4, 1)
        _Progress ("Progreso del barrido", Range(0, 1)) = 1
        _Fade ("Desvanecido", Range(0, 1)) = 1
        _Direction ("Sentido (1 / -1)", Float) = 1
        _TrailLength ("Longitud de la estela", Range(0.05, 1)) = 0.55
        _Uniform ("Intensidad uniforme (haz)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _CoreColor;
                float _Progress;
                float _Fade;
                float _Direction;
                float _TrailLength;
                float _Uniform;
            CBUFFER_END

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

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            // Ruido barato para que la luz tenga "vetas" (sin texturas).
            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float u = _Direction >= 0 ? input.uv.x : 1.0 - input.uv.x;
                float v = input.uv.y;

                // Estela: brilla en la cabeza y se apaga hacia atrás; no hay luz por delante de la cabeza.
                float head = _Progress;
                float trail = smoothstep(head - _TrailLength, head, u) * step(u, head + 0.02);
                float along = lerp(trail, smoothstep(0.0, 0.08, u) * smoothstep(1.0, 0.85, u), _Uniform);

                // Perfil transversal: más intenso hacia el borde exterior, como una hoja de luz.
                float across = pow(sin(v * 3.14159), 2.0) * lerp(0.6, 1.0, v);
                float edge = smoothstep(0.75, 0.95, v) * smoothstep(1.0, 0.92, v);

                float streaks = lerp(0.75, 1.0, Hash(float2(floor(u * 40.0), floor(v * 6.0))));
                float intensity = along * across * streaks * _Fade;

                float3 color = _Color.rgb * intensity + _CoreColor.rgb * edge * along * _Fade;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
