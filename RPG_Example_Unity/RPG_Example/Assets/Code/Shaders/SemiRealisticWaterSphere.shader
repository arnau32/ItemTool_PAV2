Shader "Custom/SemiRealisticWaterSphere"
{
    Properties
    {
        // ── Base Colors ────────────────────────────────────────────────
        _ShallowColor       ("Shallow Color",           Color)  = (0.18, 0.62, 0.78, 0.80)
        _DeepColor          ("Deep Color",              Color)  = (0.02, 0.18, 0.42, 1.00)
        _FresnelColor       ("Fresnel / Rim Color",     Color)  = (0.55, 0.85, 1.00, 1.00)
        _SpecularColor      ("Specular Color",          Color)  = (1.00, 1.00, 1.00, 1.00)

        // ── Normal Maps ───────────────────────────────────────────────
        // Two layers scrolling at different angles break tiling and
        // produce the organic ripple feel of BotW/Genshin water.
        _NormalMapA         ("Normal Map A",            2D)     = "bump" {}
        _NormalMapB         ("Normal Map B",            2D)     = "bump" {}
        _NormalStrength     ("Normal Strength",         Range(0, 2))   = 0.75
        _NormalScaleA       ("Normal Scale A",          Range(0.1, 8)) = 1.5
        _NormalScaleB       ("Normal Scale B",          Range(0.1, 8)) = 0.9
        _NormalSpeedA       ("Normal Speed A (XY)",     Vector) = ( 0.04,  0.02, 0, 0)
        _NormalSpeedB       ("Normal Speed B (XY)",     Vector) = (-0.02,  0.05, 0, 0)

        // ── Specular / Smoothness ─────────────────────────────────────
        _Smoothness         ("Smoothness",              Range(0, 1))   = 0.90
        _SpecularStrength   ("Specular Strength",       Range(0, 1))   = 0.85

        // ── Fresnel ───────────────────────────────────────────────────
        _FresnelPower       ("Fresnel Power",           Range(0.5, 8)) = 3.0
        _FresnelStrength    ("Fresnel Strength",        Range(0, 1))   = 0.70

        // ── Depth color blend ─────────────────────────────────────────
        _DepthBlend         ("Depth Blend",             Range(0, 1))   = 0.60

        // ── Wave animation ────────────────────────────────────────────
        _WaveHeight         ("Wave Height",             Range(0, 0.06)) = 0.018
        _WaveSpeed          ("Wave Speed",              Range(0, 4))    = 1.2
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
        }

        Pass
        {
            Name "WaterForward"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ── Textures ──────────────────────────────────────────────
            TEXTURE2D(_NormalMapA); SAMPLER(sampler_NormalMapA);
            TEXTURE2D(_NormalMapB); SAMPLER(sampler_NormalMapB);

            // ── CBUFFER — required for SRP Batcher ────────────────────
            CBUFFER_START(UnityPerMaterial)
                half4  _ShallowColor;
                half4  _DeepColor;
                half4  _FresnelColor;
                half4  _SpecularColor;
                float4 _NormalMapA_ST;
                float4 _NormalMapB_ST;
                float  _NormalStrength;
                float  _NormalScaleA;
                float  _NormalScaleB;
                float4 _NormalSpeedA;
                float4 _NormalSpeedB;
                float  _Smoothness;
                float  _SpecularStrength;
                float  _FresnelPower;
                float  _FresnelStrength;
                float  _DepthBlend;
                float  _WaveHeight;
                float  _WaveSpeed;
            CBUFFER_END

            // ── Structs ───────────────────────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 tangentWS   : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float2 uv          : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── Helpers ───────────────────────────────────────────────

            // Whiteout blend — preserves curvature from both normal layers.
            float3 BlendNormals(float3 a, float3 b)
            {
                return normalize(float3(a.xy + b.xy, a.z * b.z));
            }

            // GGX specular (Trowbridge-Reitz).
            // Long-tailed highlight — more realistic than Blinn-Phong.
            float GGX(float NdotH, float roughness)
            {
                float a  = roughness * roughness;
                float a2 = a * a;
                float d  = NdotH * NdotH * (a2 - 1.0) + 1.0;
                return a2 / max(PI * d * d, 1e-4);
            }

            // ── Vertex Shader ─────────────────────────────────────────
            Varyings Vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 posOS  = IN.positionOS.xyz;
                float3 normOS = normalize(IN.normalOS);

                // Three sines with prime-ish ratios — no visible periodicity.
                float wave = sin(posOS.y * 5.1 + _Time.y * _WaveSpeed)
                           + sin(posOS.x * 4.3 + _Time.y * _WaveSpeed * 0.73 + 0.9)
                           + sin(posOS.z * 3.7 + _Time.y * _WaveSpeed * 1.27 + 1.7);
                posOS += normOS * (wave * _WaveHeight * 0.333);

                OUT.positionHCS = TransformObjectToHClip(posOS);
                OUT.positionWS  = TransformObjectToWorld(posOS);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.tangentWS   = TransformObjectToWorldDir(IN.tangentOS.xyz);
                OUT.bitangentWS = cross(OUT.normalWS, OUT.tangentWS)
                                * IN.tangentOS.w
                                * GetOddNegativeScale();
                OUT.uv = IN.uv;

                return OUT;
            }

            // ── Fragment Shader ───────────────────────────────────────
            half4 Frag(Varyings IN) : SV_Target
            {
                // ── Animated UVs ──────────────────────────────────────
                float2 uvA = IN.uv * _NormalScaleA + _NormalSpeedA.xy * _Time.y;
                float2 uvB = IN.uv * _NormalScaleB + _NormalSpeedB.xy * _Time.y;

                // ── Normal maps ───────────────────────────────────────
                float3 nA = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMapA, sampler_NormalMapA, uvA),
                    _NormalStrength);
                float3 nB = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMapB, sampler_NormalMapB, uvB),
                    _NormalStrength);
                float3 normalTS = BlendNormals(nA, nB);

                // Tangent → World space (TBN row-major multiply).
                float3x3 TBN = float3x3(
                    normalize(IN.tangentWS),
                    normalize(IN.bitangentWS),
                    normalize(IN.normalWS));
                float3 N = normalize(mul(normalTS, TBN));

                // ── Lighting vectors ──────────────────────────────────
                Light  mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float3 V = normalize(GetCameraPositionWS() - IN.positionWS);
                float3 H = normalize(L + V);

                float NdotL = saturate(dot(N, L));
                float NdotH = saturate(dot(N, H));
                float NdotV = saturate(dot(N, V));

                // ── Fresnel (Schlick, F0=0.02 for water IOR 1.33) ─────
                float fresnel = 0.02 + 0.98 * pow(1.0 - NdotV, _FresnelPower);
                fresnel      *= _FresnelStrength;

                // ── Shallow/deep color blend ───────────────────────────
                // NdotV~1 at center (facing camera) = shallow.
                // NdotV~0 at edges (grazing)        = deep.
                half4 waterColor = lerp(_ShallowColor, _DeepColor,
                                        saturate((1.0 - NdotV) * _DepthBlend));

                // ── Diffuse + ambient ─────────────────────────────────
                float diffuse = NdotL * 0.75 + 0.25;
                half3 color   = waterColor.rgb * diffuse * mainLight.color;

                // ── GGX Specular ──────────────────────────────────────
                float roughness = max(1.0 - _Smoothness, 0.02);
                float specVal   = GGX(NdotH, roughness);
                float specular  = saturate(specVal * roughness * 2.0)
                                * _SpecularStrength * NdotL;
                color          += _SpecularColor.rgb * specular * mainLight.color;

                // ── Fresnel rim — simulates sky reflection at edges ───
                color = lerp(color, _FresnelColor.rgb, fresnel);

                // ── Alpha — transparent center, opaque edges ──────────
                float alpha = lerp(_ShallowColor.a, _DeepColor.a,
                                   saturate((1.0 - NdotV) * _DepthBlend + fresnel * 0.3));

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
