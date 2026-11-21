Shader "Frutiger/Shuttle Feather"
{
    Properties
    {
        _BaseColor ("Pearl feather tint", Color) = (0.88, 0.99, 1, 1)
        _RimColor ("Aqua feather sheen", Color) = (0.28, 0.92, 1, 1)
        _RimPower ("Sheen falloff", Range(1, 6)) = 2.4
        _RimStrength ("Sheen strength", Range(0, 2)) = 0.7
        _EmissionColor ("Soft feather glow", Color) = (0.08, 0.18, 0.2, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
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
                float4 _RimColor;
                float _RimPower;
                float _RimStrength;
                float4 _EmissionColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color * _BaseColor;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirectionWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float facing = abs(dot(normalWS, viewDirectionWS));
                float rim = pow(saturate(1 - facing), _RimPower);
                float3 keyLight = normalize(float3(-.35, .82, -.44));
                float wrappedLight = saturate((dot(normalWS, keyLight) + .3) / 1.3);
                float3 pearl = input.color.rgb * (.68 + wrappedLight * .4);
                pearl += _RimColor.rgb * rim * _RimStrength;
                pearl += _EmissionColor.rgb;
                return half4(pearl, 1);
            }
            ENDHLSL
        }
    }
}
