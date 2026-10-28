// Glow around laser bolts (Weapons/LaserSystem.cs): a quad stretched along the bolt that turns around the
// bolt's axis to face the camera, textured with a soft round glow (FX_Glow) and added on top. The
// instance matrix gives the bolt's centre, direction (its z axis), length (z scale) and glow width
// (x scale). Single pass instanced (multiview); no keywords.
Shader "HexWing/Beam"
{
    Properties
    {
        [MainTexture] _MainTex("Glow", 2D) = "black" {}
        [MainColor] _BaseColor("Tint", Color) = (0.37, 0.95, 1, 1)
        _Intensity("Intensity", Range(0, 8)) = 1.6
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

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _BaseColor;
                half _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 center = TransformObjectToWorld(float3(0, 0, 0));
                float3 zAxis = float3(UNITY_MATRIX_M[0].z, UNITY_MATRIX_M[1].z, UNITY_MATRIX_M[2].z);
                float3 xAxis = float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x);
                float len = length(zAxis);
                float width = length(xAxis);
                float3 axis = zAxis / max(len, 1e-5);
                float3 toCam = _WorldSpaceCameraPos - center;
                float3 side = cross(axis, toCam);
                side = dot(side, side) > 1e-8 ? normalize(side) : float3(1, 0, 0);
                float3 world = center + axis * (v.positionOS.z * len) + side * (v.positionOS.x * width);
                o.positionCS = TransformWorldToHClip(world);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half g = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).r;
                return half4(_BaseColor.rgb * g * _Intensity, 0.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
