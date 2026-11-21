Shader "Frutiger/Vertex Color"
{
    Properties { _Tint("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Front
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; };
            struct Varyings { float4 positionHCS : SV_POSITION; float4 color : COLOR; float3 directionOS : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Tint;
                output.directionOS = input.positionOS.xyz;
                return output;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float2 blend = f * f * (3 - 2 * f);
                float a = Hash(cell);
                float b = Hash(cell + float2(1, 0));
                float c = Hash(cell + float2(0, 1));
                float d = Hash(cell + float2(1, 1));
                return lerp(lerp(a, b, blend.x), lerp(c, d, blend.x), blend.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.directionOS);
                float2 cloudSpace = direction.xz / (.45 + abs(direction.y));
                float2 drift = float2(_Time.y * .001, 0);
                float2 p = cloudSpace * 4.3 + drift;
                float cloudField = Noise(p) * .57 + Noise(p * 2.05 + 13.7) * .28
                    + Noise(p * 4.1 + 29.3) * .15;
                float cloudShape = smoothstep(.42, .7, cloudField);
                float lowerSky = 1 - smoothstep(-.14, -.025, direction.y);
                float upperSky = smoothstep(.04, .16, direction.y) * (1 - smoothstep(.45, .7, direction.y));
                float opacity = saturate(lowerSky * (.25 + cloudShape * .45) + upperSky * cloudShape * .34);
                float3 cloudColor = lerp(float3(.62, .79, .93), float3(.98, 1, 1), cloudShape);
                return half4(lerp(input.color.rgb, cloudColor, opacity), input.color.a);
            }
            ENDHLSL
        }
    }
}
