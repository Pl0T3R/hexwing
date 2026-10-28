// Camera-facing effect sprites drawn instanced (Visuals/Effects.cs): explosion flipbooks (fire that
// turns into smoke) and soft glows (impacts, muzzle flashes). The texture is premultiplied: RGB is the
// light added, alpha how much of what is behind is hidden (smoke), so one blend mode (One,
// OneMinusSrcAlpha) does both fire and smoke. Per instance: _Age (0..1 through the flipbook) and _Seed
// (a random turn of the sprite). The sprite faces the camera around its centre; its size is the
// instance matrix's scale. Single pass instanced (multiview); no keywords.
Shader "HexWing/Sprite"
{
    Properties
    {
        [MainTexture] _MainTex("Flipbook (premultiplied)", 2D) = "black" {}
        _Columns("Columns", Float) = 8
        _Rows("Rows", Float) = 8
        [HDR] _Tint("Tint", Color) = (1, 1, 1, 1)
        _Intensity("Light Intensity", Range(0, 8)) = 1.5
        _Opacity("Smoke Opacity", Range(0, 1)) = 1
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
            Blend One OneMinusSrcAlpha
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
                float _Columns;
                float _Rows;
                half4 _Tint;
                half _Intensity;
                half _Opacity;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _Age)
                UNITY_DEFINE_INSTANCED_PROP(float, _Seed)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                half blend : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float age = saturate(UNITY_ACCESS_INSTANCED_PROP(Props, _Age));
                float seed = UNITY_ACCESS_INSTANCED_PROP(Props, _Seed);

                // billboard: face the camera from the sprite's centre, turned by the seed
                float3 center = TransformObjectToWorld(float3(0, 0, 0));
                float scale = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                float3 toCam = normalize(_WorldSpaceCameraPos - center);
                float3 right = normalize(cross(abs(toCam.y) > 0.98 ? float3(1, 0, 0) : float3(0, 1, 0), toCam));
                float3 up = cross(toCam, right);
                float a = seed * 6.2831853;
                float s, c;
                sincos(a, s, c);
                float2 p = v.positionOS.xy;
                p = float2(p.x * c - p.y * s, p.x * s + p.y * c);
                float3 world = center + (right * p.x + up * p.y) * scale;
                o.positionCS = TransformWorldToHClip(world);

                // two neighbouring flipbook frames and the blend between them
                float frames = _Columns * _Rows;
                float f = age * (frames - 1.0);
                float f0 = floor(f);
                float f1 = min(f0 + 1.0, frames - 1.0);
                float2 cell = float2(1.0 / _Columns, 1.0 / _Rows);
                float2 uv = v.uv * cell;
                o.uv0 = uv + float2(fmod(f0, _Columns), _Rows - 1.0 - floor(f0 / _Columns)) * cell;
                o.uv1 = uv + float2(fmod(f1, _Columns), _Rows - 1.0 - floor(f1 / _Columns)) * cell;
                o.blend = (half)(f - f0);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv0);
                half4 b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv1);
                half4 t = lerp(a, b, i.blend);
                return half4(t.rgb * _Tint.rgb * _Intensity, t.a * _Opacity);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
