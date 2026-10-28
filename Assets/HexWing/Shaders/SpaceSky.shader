// Space skybox: the nebula and galactic band from a lat-long texture rendered in Blender (Art/blender/
// models/sky.py; without it, a faint procedural two-colour nebula), a procedural star field, the sun's
// glow, and one planet with a lat-long surface texture, a lit side, an atmosphere rim and optional rings
// (with the planet's shadow on them). Everything is a function of the view direction, so the planet is
// "infinitely" far away (no depth-precision problems with huge meshes). Stars are antialiased with
// fwidth so they don't shimmer. Textures are read at mip 0 with the shader's own lat-long mapping
// (centre = +z, right = +x), so there is no seam. Sun direction: global _HexSunDirection.
Shader "HexWing/SpaceSky"
{
    Properties
    {
        _StarDensity("Star Cells per Cube Face", Float) = 180
        _StarChance("Share of Cells with a Star", Range(0, 1)) = 0.16
        _StarBrightness("Star Brightness", Range(0, 4)) = 1.4
        _NebulaColorA("Nebula Color A", Color) = (0.10, 0.05, 0.22, 1)
        _NebulaColorB("Nebula Color B", Color) = (0.02, 0.12, 0.20, 1)
        _NebulaStrength("Nebula Strength", Range(0, 1)) = 0.6
        _SpaceColor("Deep Space Color", Color) = (0.005, 0.007, 0.015, 1)
        _SunColor("Sun Color", Color) = (1, 0.92, 0.78, 1)
        _SunSize("Sun Glow Sharpness", Float) = 900
        _PlanetDirection("Planet Direction", Vector) = (-0.35, -0.25, 0.9, 0)
        _PlanetRadius("Planet Angular Radius (deg)", Range(1, 60)) = 22
        _PlanetColorA("Planet Color A", Color) = (0.16, 0.36, 0.55, 1)
        _PlanetColorB("Planet Color B", Color) = (0.55, 0.62, 0.68, 1)
        _AtmosphereColor("Atmosphere Color", Color) = (0.45, 0.75, 1, 1)
        _NebulaMap("Nebula (lat-long)", 2D) = "black" {}
        _NebulaMapAmount("Nebula Map Amount", Range(0, 2)) = 1
        _PlanetMap("Planet Surface (lat-long)", 2D) = "white" {}
        _PlanetMapAmount("Planet Map Amount", Range(0, 1)) = 1
        _PlanetSpin("Planet Map Turn (0..1)", Float) = 0
        _RingNormal("Ring Plane Normal", Vector) = (0.25, 1, 0.15, 0)
        _RingInner("Ring Inner Radius (planet radii)", Float) = 1.45
        _RingOuter("Ring Outer Radius (planet radii)", Float) = 2.35
        _RingColor("Ring Color", Color) = (0.85, 0.72, 0.52, 1)
        _RingStrength("Ring Strength (0 = no rings)", Range(0, 1)) = 0
    }

    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.universal": "14.0"
        }
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _StarDensity;
                float _StarChance;
                half _StarBrightness;
                half4 _NebulaColorA;
                half4 _NebulaColorB;
                half _NebulaStrength;
                half4 _SpaceColor;
                half4 _SunColor;
                float _SunSize;
                float4 _PlanetDirection;
                float _PlanetRadius;
                half4 _PlanetColorA;
                half4 _PlanetColorB;
                half4 _AtmosphereColor;
                half _NebulaMapAmount;
                half _PlanetMapAmount;
                float _PlanetSpin;
                float4 _RingNormal;
                float _RingInner;
                float _RingOuter;
                half4 _RingColor;
                half _RingStrength;
            CBUFFER_END

            TEXTURE2D(_NebulaMap);  SAMPLER(sampler_NebulaMap);
            TEXTURE2D(_PlanetMap);  SAMPLER(sampler_PlanetMap);

            // lat-long coordinates of a direction: centre = +z, right = +x, top = +y
            float2 LatLong(float3 d)
            {
                return float2(0.5 + atan2(d.x, d.z) * (0.5 / PI), 0.5 + asin(clamp(d.y, -1.0, 1.0)) * (1.0 / PI));
            }

            // distance along a ray from the origin to a sphere (center c, radius r); negative = no hit
            float SphereHit(float3 o, float3 d, float3 c, float r)
            {
                float3 oc = o - c;
                float b = dot(oc, d);
                float disc = b * b - (dot(oc, oc) - r * r);
                return disc < 0.0 ? -1.0 : -b - sqrt(disc);
            }

            float4 _HexSunDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
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
                o.direction = v.positionOS.xyz;
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Stars: each cube face is a grid of cells; some cells hold one star at a random spot.
            half Stars(float3 d)
            {
                float3 a = abs(d);
                float2 uv;
                float face;
                if (a.x >= a.y && a.x >= a.z) { uv = d.yz / a.x; face = d.x > 0 ? 0.0 : 1.0; }
                else if (a.y >= a.z)        { uv = d.xz / a.y; face = d.y > 0 ? 2.0 : 3.0; }
                else                        { uv = d.xy / a.z; face = d.z > 0 ? 4.0 : 5.0; }
                float2 grid = uv * _StarDensity;
                float2 cell = floor(grid) + face * 97.13;
                float h = Hash(cell);
                float2 spot = float2(Hash(cell + 13.7), Hash(cell + 29.1)) * 0.7 + 0.15;
                float dist = length(frac(grid) - spot);
                float aa = max(fwidth(grid.x), 1e-4);
                float size = lerp(0.04, 0.12, Hash(cell + 7.3));
                float shape = 1.0 - smoothstep(size - aa, size + aa, dist);
                return (half)(shape * step(1.0 - _StarChance, h) * lerp(0.25, 1.0, Hash(cell + 3.1)));
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 d = normalize(i.direction);
                float3 sun = dot(_HexSunDirection.xyz, _HexSunDirection.xyz) > 1e-4 ? normalize(_HexSunDirection.xyz) : float3(0, 1, 0);

                // nebula: a few slow waves give large soft clouds of two colours
                float n1 = sin(d.x * 2.1 + d.y * 3.3) * sin(d.z * 2.7 - d.y * 1.4);
                float n2 = sin(d.y * 4.2 + d.z * 1.7 + 1.3) * sin(d.x * 3.1 + 0.7);
                half3 color = _SpaceColor.rgb;
                half waves = (half)(1.0 - saturate(_NebulaMapAmount));
                color += _NebulaColorA.rgb * (half)(saturate(n1) * _NebulaStrength) * waves;
                color += _NebulaColorB.rgb * (half)(saturate(n2) * _NebulaStrength) * waves;
                color += SAMPLE_TEXTURE2D_LOD(_NebulaMap, sampler_NebulaMap, LatLong(d), 0).rgb * _NebulaMapAmount;
                color += (half3)Stars(d) * _StarBrightness;

                // sun: a bright core and a wide soft glow
                float s = saturate(dot(d, sun));
                color += _SunColor.rgb * (half)(pow(s, _SunSize) * 6.0 + pow(s, 24.0) * 0.12);

                // planet: a sphere seen from far away, lit from the sun direction, with an atmosphere rim
                float3 p = normalize(_PlanetDirection.xyz);
                float3 right = normalize(cross(abs(p.y) > 0.99 ? float3(1, 0, 0) : float3(0, 1, 0), p));
                float3 up = cross(p, right);
                float facing = dot(d, p);
                float tanR = tan(radians(_PlanetRadius));
                bool onPlanet = false;
                if (facing > 0.0)
                {
                    float2 q = float2(dot(d, right), dot(d, up)) / (facing * tanR);
                    float r2 = dot(q, q);
                    if (r2 < 1.0)
                    {
                        float3 normal = normalize(q.x * right + q.y * up - sqrt(1.0 - r2) * p);
                        float light = saturate(dot(normal, sun) * 0.9 + 0.1);
                        float bands = sin(dot(normal, up) * 9.0 + sin(dot(normal, right) * 3.0)) * 0.5 + 0.5;
                        half3 surface = lerp(_PlanetColorA.rgb, _PlanetColorB.rgb, (half)bands);
                        float3 local = float3(dot(normal, right), dot(normal, up), dot(normal, -p));
                        float2 puv = LatLong(local) + float2(_PlanetSpin, 0.0);
                        half3 mapped = SAMPLE_TEXTURE2D_LOD(_PlanetMap, sampler_PlanetMap, puv, 0).rgb;
                        surface = lerp(surface, mapped, _PlanetMapAmount);
                        onPlanet = true;
                        half rim = (half)pow(r2, 4.0);
                        color = surface * (half)light + _AtmosphereColor.rgb * rim * (half)(light + 0.15);
                    }
                    else
                    {
                        // a thin halo just outside the disc on the sunlit side
                        float halo = saturate(1.0 - (sqrt(r2) - 1.0) * 14.0);
                        float sunSide = saturate(dot(normalize(q.x * right + q.y * up), sun) * 0.7 + 0.4);
                        color += _AtmosphereColor.rgb * (half)(halo * halo * 0.5 * sunSide);
                    }
                }

                // rings: a disc in a plane through the planet's centre (the planet sits at distance 1 with
                // radius tanR), hidden behind the planet, in the planet's shadow where it is behind it
                if (_RingStrength > 0.0)
                {
                    float3 c = p;
                    float3 rn = normalize(_RingNormal.xyz);
                    float dn = dot(d, rn);
                    if (abs(dn) > 1e-4)
                    {
                        float t = dot(c, rn) / dn;
                        float3 x = d * t;
                        float rr = length(x - c) / tanR;
                        if (t > 0.0 && rr > _RingInner && rr < _RingOuter)
                        {
                            float hit = SphereHit(0.0, d, c, tanR);
                            bool hidden = onPlanet && hit > 0.0 && hit < t;
                            if (!hidden)
                            {
                                float k = (rr - _RingInner) / (_RingOuter - _RingInner);
                                float bandsR = 0.55 + 0.25 * sin(rr * 41.0) + 0.2 * sin(rr * 113.0 + 1.3);
                                float gap = smoothstep(0.0, 0.03, abs(k - 0.62));                     // one dark division
                                float edge = smoothstep(0.0, 0.06, k) * smoothstep(1.0, 0.85, k);
                                float alpha = saturate(bandsR) * gap * edge * _RingStrength;
                                float shadowed = SphereHit(x, sun, c, tanR) > 0.0 ? 0.15 : 1.0;
                                float lit = (0.35 + 0.65 * abs(dot(rn, sun))) * shadowed;
                                color = lerp(color, _RingColor.rgb * (half)lit, (half)alpha);
                            }
                        }
                    }
                }
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
