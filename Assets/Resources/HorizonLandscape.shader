Shader "Frutiger/Horizon Landscape"
{
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 landscapePos : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.landscapePos = input.positionOS.xy;
                return output;
            }
            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            float SoftNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(cell), Hash21(cell + float2(1, 0)), f.x),
                            lerp(Hash21(cell + float2(0, 1)), Hash21(cell + 1), f.x), f.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Broad, quiet variation suggests sunlit meadow slopes through the haze.
                float2 p = input.landscapePos;
                float relief = SoftNoise(p * float2(.13, .24)) * .7
                             + SoftNoise(p * float2(.37, .48)) * .3;
                half4 color = input.color;
                color.rgb *= lerp(.88, 1.12, relief);
                return color;
            }
            ENDHLSL
        }
    }
}
