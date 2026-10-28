// Textured hard-surface shader for the Blender-made models (Art/blender): ships, cockpit, stations, rocks.
// Three textures per asset: albedo (sRGB), mask (R metal, G smoothness, B emission, A baked AO) and a
// tangent-space normal map. Lit by one sun (global _HexSunDirection / _HexSunColor) and a sky/ground
// ambient (_HexSkyColor / _HexGroundColor), all set by SpaceEnvironment and MapTheme; no real-time lights
// or shadows. A cheap GGX highlight, a sky reflection by smoothness, a fresnel rim and emission.
// Single pass instanced (multiview) and SRP-batcher compatible; no keywords.
Shader "HexWing/Hull"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        _MaskMap("Mask (R metal, G smoothness, B emission, A AO)", 2D) = "black" {}
        [Normal] _NormalMap("Normal", 2D) = "bump" {}
        [MainColor] _BaseColor("Tint", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor("Emission Tint", Color) = (1, 1, 1, 1)
        _EmissionStrength("Emission Strength", Range(0, 8)) = 2.5
        _NormalStrength("Normal Strength", Range(0, 2)) = 1
        _RimColor("Rim Color", Color) = (0.55, 0.72, 0.95, 1)
        _RimPower("Rim Power", Range(0.5, 8)) = 4
        _RimStrength("Rim Strength", Range(0, 2)) = 0.35
        _Ambient("Ambient Strength", Range(0, 2)) = 1
        _Flash("Hit Flash", Range(0, 1)) = 0
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

            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MaskMap);    SAMPLER(sampler_MaskMap);
            TEXTURE2D(_NormalMap);  SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                half _EmissionStrength;
                half _NormalStrength;
                half4 _RimColor;
                half _RimPower;
                half _RimStrength;
                half _Ambient;
                half _Flash;
            CBUFFER_END

            // Set by SpaceEnvironment (and MapTheme per map): toward the sun, its colour, the ambient.
            float4 _HexSunDirection;
            half4 _HexSunColor;
            half4 _HexSkyColor;
            half4 _HexGroundColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
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
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.tangentWS = half4(TransformObjectToWorldDir(v.tangentOS.xyz), v.tangentOS.w * GetOddNegativeScale());
                return o;
            }

            half3 SkyAt(half3 dir)
            {
                half t = saturate(dir.y * 0.5h + 0.5h);
                return lerp(_HexGroundColor.rgb, _HexSkyColor.rgb, t);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, i.uv);
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv), _NormalStrength);

                half3 nW = normalize(i.normalWS);
                half3 tW = normalize(i.tangentWS.xyz);
                half3 bW = cross(nW, tW) * i.tangentWS.w;
                half3 n = normalize(tW * nTS.x + bW * nTS.y + nW * nTS.z);

                float3 sun = _HexSunDirection.xyz;
                half3 l = dot(sun, sun) > 1e-4 ? (half3)normalize(sun) : half3(0.0h, 1.0h, 0.0h);
                half3 sunColor = dot(_HexSunColor.rgb, _HexSunColor.rgb) > 1e-4 ? _HexSunColor.rgb : half3(1.0h, 0.96h, 0.9h);
                half3 v = normalize((half3)GetWorldSpaceViewDir(i.positionWS));

                half metal = mask.r;
                half smooth = mask.g;
                half ao = mask.a;
                half rough = max(0.04h, 1.0h - smooth);
                half a2 = rough * rough * rough * rough;

                half3 diffuseColor = albedo.rgb * (1.0h - metal);
                half3 f0 = lerp(half3(0.04h, 0.04h, 0.04h), albedo.rgb, metal);
                half nl = saturate(dot(n, l));
                half nv = saturate(dot(n, v)) + 1e-4h;
                half3 h = normalize(l + v);
                half nh = saturate(dot(n, h));
                half lh = saturate(dot(l, h));

                // GGX distribution with a combined visibility/fresnel term (the cheap mobile form URP uses)
                half d = nh * nh * (a2 - 1.0h) + 1.0001h;
                half specTerm = a2 / (d * d * max(0.1h, lh * lh) * (rough * 4.0h + 2.0h));
                half3 direct = (diffuseColor + f0 * min(specTerm, 64.0h)) * sunColor * nl;

                // ambient: the sky/ground gradient for diffuse, and its reflection for the shine
                half3 fresnel = f0 + (max(smooth.xxx, f0) - f0) * pow(1.0h - nv, 5.0h);
                half3 envDiffuse = SkyAt(n) * diffuseColor;
                half3 envSpec = SkyAt(reflect(-v, n)) * fresnel * smooth;
                half3 ambient = (envDiffuse + envSpec) * ao * _Ambient;

                half rim = pow(1.0h - saturate(dot(n, v)), _RimPower) * _RimStrength;
                half3 color = direct + ambient + _RimColor.rgb * rim * ao;
                // the tint colours white lights only (the cockpit's light strips take the ship class colour);
                // saturated lights (red, green, blue buttons) keep their colour and follow only its brightness
                half hi = max(albedo.r, max(albedo.g, albedo.b));
                half sat = (hi - min(albedo.r, min(albedo.g, albedo.b))) / max(hi, 0.001h);
                half tintMax = max(_EmissionColor.r, max(_EmissionColor.g, _EmissionColor.b));
                half3 tint = lerp(_EmissionColor.rgb, tintMax.xxx, saturate(sat * 1.6h));
                color += albedo.rgb * mask.b * tint * _EmissionStrength;
                color = lerp(color, half3(1.0h, 0.85h, 0.7h), _Flash);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
