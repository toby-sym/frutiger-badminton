Shader "Frutiger/Player Lacquer Glaze"
{
    Properties
    {
        _Tint ("Highlight tint", Color) = (0.76, 0.95, 1, 1)
        _Opacity ("Base opacity", Range(0, 1)) = 0
        _RimOpacity ("Rim opacity", Range(0, 1)) = 0.27
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
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 normalOS : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.normalOS = input.normalOS;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 localNormal = normalize(input.normalOS);
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                float rim = pow(1 - saturate(dot(normal, view)), 2.4);
                float highlight = pow(saturate(dot(normal, normalize(view + float3(-.4, .8, -.35)))), 36);
                float stripe = exp(-pow((localNormal.x + .32) * 7, 2))
                    * smoothstep(.1, .65, abs(localNormal.z)) * (1 - smoothstep(.6, .95, abs(localNormal.y)));
                float alpha = saturate(_Opacity + rim * _RimOpacity + highlight * .42 + stripe * .28);
                return half4(lerp(_Tint.rgb, 1, saturate(highlight + stripe * .55 + rim * .45)), alpha);
            }
            ENDHLSL
        }
    }
}
