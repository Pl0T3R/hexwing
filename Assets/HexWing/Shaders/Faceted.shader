// Faceted surfaces for the cockpit and ships: flat facets with a two-tone lit/shadow split from one sun
// direction (global _HexSunDirection, set by SpaceEnvironment), a soft fresnel rim, a highlight, optional
// emission, and the mesh's vertex color as cheap baked shading. No real-time lights or shadows.
// Single pass instanced (multiview) and SRP-batcher compatible.
Shader "HexWing/Faceted"
{
    Properties
    {
        [MainColor] _BaseColor("Lit Color", Color) = (0.4, 0.43, 0.48, 1)
        _ShadowColor("Shadow Color", Color) = (0.12, 0.13, 0.16, 1)
        _RimColor("Rim Color", Color) = (0.55, 0.75, 0.95, 1)
        _RimPower("Rim Power", Range(0.5, 8)) = 3
        _RimStrength("Rim Strength", Range(0, 2)) = 0.25
        _HighlightColor("Highlight Color", Color) = (1, 1, 1, 1)
        _Gloss("Gloss", Range(4, 256)) = 32
        _SpecStrength("Highlight Strength", Range(0, 2)) = 0.3
        _Ambient("Ambient (fills the shadow side)", Range(0, 1)) = 0.25
        _Emission("Emission", Range(0, 1)) = 0
    }

    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.universal": "14.0"
        }
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadowColor;
                half4 _RimColor;
                half _RimPower;
                half _RimStrength;
                half4 _HighlightColor;
                half _Gloss;
                half _SpecStrength;
                half _Ambient;
                half _Emission;
            CBUFFER_END

            // Set by SpaceEnvironment: direction toward the sun, world space.
            float4 _HexSunDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half3 n = normalize(i.normalWS);
                float3 sun = _HexSunDirection.xyz;
                half3 l = dot(sun, sun) > 1e-4 ? (half3)normalize(sun) : half3(0.0h, 1.0h, 0.0h);
                half3 viewDir = normalize((half3)GetWorldSpaceViewDir(i.positionWS));

                // two-tone: a soft step between shadow and lit color keeps every facet readable
                half wrap = saturate(dot(n, l) * 0.5h + 0.5h);
                half lit = smoothstep(0.3h, 0.7h, wrap);
                half3 shadow = lerp(_ShadowColor.rgb, _BaseColor.rgb, _Ambient);
                half3 color = lerp(shadow, _BaseColor.rgb, lit) * i.color.rgb;

                half3 h = normalize(l + viewDir);
                half spec = pow(saturate(dot(n, h)), _Gloss) * _SpecStrength * lit;
                half rim = pow(1.0h - saturate(dot(n, viewDir)), _RimPower) * _RimStrength;
                color += _HighlightColor.rgb * spec + _RimColor.rgb * rim + _BaseColor.rgb * _Emission;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
