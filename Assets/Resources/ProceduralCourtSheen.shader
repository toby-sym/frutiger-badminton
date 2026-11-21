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
                // Layer broad, stable reflections over a very fine moving lacquer grain.
                // Everything is analytic: there are no sampled image textures.
                float time = _Time.y * _FlowSpeed;
                float2 p = input.positionWS.xz;
                float field = .5 + .25 * sin(p.x * 1.35 + p.y * .38 + sin(p.y * .7) * .5)
                    + .25 * sin(p.y * .93 - p.x * .43);
                float grain = sin(p.x * _WaveScale * 2.1 + sin(p.y * 1.4) * .4)
                    * sin(p.y * _WaveScale * 1.45 + time * .35);
                float panelX = abs(frac((p.x + p.y * .21) * .34) - .5);
                float panelZ = abs(frac((p.y - p.x * .08) * .25) - .5);
                float frameReflection = max(smoothstep(.45, .495, panelX), smoothstep(.46, .495, panelZ));
                float2 glowPosition = float2((p.x + 1.1 + p.y * .20) * .24, (p.y - 1.7) * .085);
                float windowGlow = exp(-dot(glowPosition, glowPosition) * 1.7);
                float glint = saturate(windowGlow * (.48 + field * .52) + max(0, grain) * .035);
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirectionWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(saturate(1 - saturate(dot(normalWS, viewDirectionWS))), 3);
                float alpha = saturate(_Opacity * (.70 + glint * .25 + fresnel * _FresnelStrength));
                float3 color = lerp(_BaseColor.rgb * (.74 + .38 * field), _GlintColor.rgb,
                    saturate(glint * .68 + fresnel * .18));
                color *= 1 - frameReflection * .20;
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
