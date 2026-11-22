Shader "Frutiger/Player Crystal"
{
    Properties
    {
        _Tint ("Crystal tint", Color) = (0.15, 0.8, 0.85, 1)
        _Opacity ("Inner tint", Range(0, 1)) = 0.3
        _RimOpacity ("Glass rim", Range(0, 1)) = 0.42
        _Reflectivity ("Window reflections", Range(0, 1)) = 0.24
        _ZWrite ("Depth write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        // Outer shells and racket details depth-test against each other. Inner gel
        // layers disable depth writes so their color can blend through the shell.
        ZWrite [_ZWrite]
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
                float _Reflectivity;
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
                float facing = saturate(dot(normal, view));
                float rim = pow(1 - facing, 1.8);
                float3 reflectedKey = normalize(view + float3(-.38, .8, -.3));
                float broadGloss = pow(saturate(dot(normal, reflectedKey)), 12);
                float specular = pow(saturate(dot(normal, reflectedKey)), 58);
                float stripePosition = (localNormal.x + .3) * 7;
                float stripe = exp(-stripePosition * stripePosition)
                    * smoothstep(.12, .7, abs(localNormal.z)) * (1 - smoothstep(.6, .98, abs(localNormal.y)));
                float3 reflection = reflect(-view, normal);
                float2 gridPosition = float2(atan2(reflection.z, reflection.x) * .159155, asin(clamp(reflection.y, -1, 1)) * .31831);
                float2 grid = abs(frac(gridPosition * float2(12, 7)) - .5);
                float2 filterWidth = max(fwidth(grid), float2(.01, .01));
                float2 frameLine = smoothstep(float2(.48, .48) - filterWidth, float2(.48, .48) + filterWidth, grid);
                float frame = max(frameLine.x, frameLine.y) * facing;
                float liquidRibbon = pow(saturate(.5 + .5 * sin(input.positionWS.y * 11 + input.positionWS.x * 7 - _Time.y * 1.8)), 18)
                    * smoothstep(.15, .72, abs(localNormal.z));
                float shine = saturate(broadGloss * .42 + specular * .8 + stripe * .35 + rim * .25 + liquidRibbon * .36);
                half3 color = lerp(_Tint.rgb * (.7 + .28 * facing), half3(1, 1, 1), shine);
                color *= 1 - frame * _Reflectivity * .24;
                color = lerp(color, _Tint.rgb, liquidRibbon * .12);
                float alpha = saturate(_Opacity + rim * _RimOpacity + specular * .42 + broadGloss * .08
                    + stripe * .23 + frame * _Reflectivity * .1 + liquidRibbon * .08);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
