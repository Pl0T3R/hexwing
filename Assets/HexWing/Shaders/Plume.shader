// Engine plumes of the Blender-made ships (the "<Name>_Glow" meshes: open cones behind the nozzles).
// Added on top, bright where the cone faces the eye and fading to nothing at its silhouette, so it reads
// as a soft volume of light rather than a solid cone. Tinted per faction; _Flicker adds a slight pulse.
// Single pass instanced (multiview); no keywords.
Shader "HexWing/Plume"
{
    Properties
    {
        [MainColor] _BaseColor("Color", Color) = (1, 0.3, 0.15, 1)
        _Intensity("Intensity", Range(0, 8)) = 2.2
        _Softness("Edge Softness", Range(0.5, 6)) = 2
        _Flicker("Flicker", Range(0, 1)) = 0.12
    }

    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.universal": "14.0"
        }
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Intensity;
                half _Softness;
                half _Flicker;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
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
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 n = normalize(i.normalWS);
                half3 v = normalize((half3)GetWorldSpaceViewDir(i.positionWS));
                half facing = pow(abs(dot(n, v)), _Softness);
                half pulse = 1.0h - _Flicker * (0.5h + 0.5h * sin(_Time.y * 37.0 + i.positionWS.x * 3.0 + i.positionWS.z * 2.0));
                return half4(_BaseColor.rgb * facing * _Intensity * pulse, 0.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
