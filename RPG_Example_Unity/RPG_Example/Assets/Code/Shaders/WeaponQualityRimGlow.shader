// Inverted-hull outline for weapon quality indication.
// Uses smooth normals baked into the TANGENT channel (xyz) by WeaponSmoothNormalBaker.
// This eliminates the hard-edge gap artifacts caused by split/duplicated vertex normals.
//
// Setup per weapon prefab:
//   1. Run Assets > Weapon Quality > Bake Smooth Normals on the weapon mesh.
//   2. Create a child GameObject "QualityGlow" (inactive by default).
//   3. Add MeshFilter → assign the generated *_SmoothNormals mesh.
//   4. Add MeshRenderer → assign a material using this shader.
//   5. Assign that child to WeaponInstance._qualityVfxObject.
Shader "Custom/WeaponQualityRimGlow"
{
    Properties
    {
        _QualityColor   ("Quality Color",   Color)         = (0,0,0,0)
        _OutlineWidth   ("Outline Width",   Range(0,0.05)) = 0.008
        _GlowIntensity  ("Glow Intensity",  Range(0,1))    = 0.9
        _PulseSpeed     ("Pulse Speed",     Range(0,10))   = 1.5
        _PulseAmplitude ("Pulse Amplitude", Range(0,1))    = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent+1"
        }
        LOD 100

        Pass
        {
            Name "QualityGlow"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend  SrcAlpha OneMinusSrcAlpha   // alpha blend — rim replaces background at its hue
            ZWrite Off
            ZTest  LEqual
            Cull   Front     // inverted hull: only back faces rendered

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _QualityColor;
                float  _OutlineWidth;
                float  _GlowIntensity;
                float  _PulseSpeed;
                float  _PulseAmplitude;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 tangentOS  : TANGENT;   // xyz = baked smooth normal (WeaponSmoothNormalBaker)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // Smooth normal from tangents avoids hard-edge split vertex divergence.
                float3 smoothNormalWS = TransformObjectToWorldNormal(IN.tangentOS.xyz);
                float3 posWS          = TransformObjectToWorld(IN.positionOS.xyz);
                posWS                += normalize(smoothNormalWS) * _OutlineWidth;
                OUT.positionCS        = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float pulse = 1.0 + _PulseAmplitude * sin(_Time.y * _PulseSpeed);
                // saturate keeps every channel <= 1.0 — prevents tone-mapper hue shift at high intensity
                half3 col = saturate(_QualityColor.rgb * (_GlowIntensity * pulse));
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
