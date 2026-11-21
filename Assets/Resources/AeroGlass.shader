Shader "Frutiger/Aero Glass"
{
    Properties
    {
        _Tint ("Glass tint", Color) = (0.52, 0.87, 1, 1)
        _Opacity ("Center opacity", Range(0, 1)) = 0.04
        _RimOpacity ("Edge opacity", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Opacity;
                float _RimOpacity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                float rim = pow(1 - saturate(dot(normal, view)), 2.4);
                float highlight = pow(saturate(dot(normal, normalize(view + float3(-.4, .8, -.35)))), 80);
                float alpha = saturate(_Opacity + rim * _RimOpacity + highlight * .55);
                return half4(lerp(_Tint.rgb, 1, saturate(highlight + rim * .55)), alpha);
            }
            ENDHLSL
        }
    }
}
