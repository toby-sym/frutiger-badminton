Shader "Frutiger/Procedural Court Sheen"
{
    Properties
    {
        _BaseColor ("Aqua glaze", Color) = (0.18, 0.58, 0.68, 1)
        _GlintColor ("Moving glint", Color) = (0.76, 1, 1, 1)
        _Opacity ("Glaze opacity", Range(0, 1)) = 0.42
        _WaveScale ("Wave scale", Range(1, 24)) = 11
        _FlowSpeed ("Flow speed", Range(0, 1)) = 0.22
        _FresnelStrength ("View-angle sheen", Range(0, 1)) = 0.38
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _GlintColor;
                float _Opacity;
                float _WaveScale;
                float _FlowSpeed;
                float _FresnelStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float time = _Time.y * _FlowSpeed;
                float waveA = sin((input.uv.x * 1.15 + input.uv.y * .55) * _WaveScale - time
                    + sin(input.uv.y * 6.4 + time * .4) * .48);
                float waveB = sin((input.uv.x * .48 - input.uv.y * 1.2) * (_WaveScale * .68) + time * .72
                    + cos(input.uv.x * 4.6) * .32);
                float field = saturate(.5 + .25 * (waveA + waveB));
                float glint = smoothstep(.68, .94, field);
                float scintillation = .5 + .5 * sin(input.uv.x * 37 + input.uv.y * 23 + time * 1.6)
                    * sin(input.uv.y * 31 - input.uv.x * 18 - time * 1.1);
                glint *= lerp(.7, 1, scintillation);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirectionWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(saturate(1 - saturate(dot(normalWS, viewDirectionWS))), 3);
                float alpha = saturate(_Opacity * (.05 + glint * .4 + field * .2 + fresnel * _FresnelStrength));
                float3 color = lerp(_BaseColor.rgb, _GlintColor.rgb, saturate(glint * .9 + fresnel * .2));
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
