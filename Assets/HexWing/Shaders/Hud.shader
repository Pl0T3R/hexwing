// Holographic HUD glyphs (Cockpit/TargetingHud.cs): white glyphs with a soft glow from one atlas
// (Art/fx/make_fx.py, HUD_Glyphs.png), tinted, added on top of the view. Depth-tested, so the cockpit
// frame hides them like a projection on the glass. Single pass instanced (multiview); no keywords.
Shader "HexWing/Hud"
{
    Properties
    {
        [MainTexture] _MainTex("Glyph Atlas", 2D) = "black" {}
        [MainColor] _BaseColor("Tint", Color) = (0.37, 0.83, 1, 1)
        _Intensity("Intensity", Range(0, 4)) = 1.4
    }

    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.universal": "14.0"
        }
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
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
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
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
