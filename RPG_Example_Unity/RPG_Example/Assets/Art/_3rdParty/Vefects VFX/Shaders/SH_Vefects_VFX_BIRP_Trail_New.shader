// Converted from BIRP (Amplify Shader Editor) to URP Unlit
// Original: Vefects/SH_Vefects_VFX_BIRP_Trail_New
//
// Key changes from BIRP -> URP:
//   - Replaced CG surface shader paradigm with HLSL vertex/fragment pipeline
//   - Replaced #include "UnityCG.cginc" / "UnityPBSLighting.cginc" with URP Core.hlsl / ShaderVariables.hlsl
//   - Replaced sampler2D + tex2D() with TEXTURE2D / SAMPLER / SAMPLE_TEXTURE2D (URP macro set)
//   - Replaced _Time.y with _Time.y (still valid, but now sourced from UnityInput.hlsl via Core.hlsl)
//   - Removed ShadowCaster pass (transparent VFX trails don't cast shadows)
//   - Added UniversalForward LightMode tag required by URP render queue
//   - Added "RenderPipeline" = "UniversalPipeline" SubShader tag so URP picks this SubShader
//   - Kept all blend/zwrite/ztest/cull as material-driven properties (same as original)

Shader "Vefects/SH_Vefects_VFX_URP_Trail_New"
{
    Properties
    {
        _EmissiveIntensity          ("Emissive Intensity",              Float)   = 1
        _AlphaAffectsOpacity        ("Alpha Affects Opacity",           Float)   = 1
        _OverallSpeed               ("Overall Speed",                   Float)   = 1

        [Space(33)][Header(Color)][Space(13)]
        _Color                      ("Color",                           2D)      = "white" {}
        _ColorUVScale               ("Color UV Scale",                  Vector)  = (1,1,0,0)
        _ColorPanSpeed              ("Color Pan Speed",                 Vector)  = (0,0,0,0)
        _Color01                    ("Color 01",                        Color)   = (1,1,1,0)
        _Color02                    ("Color 02",                        Color)   = (1,1,1,0)
        _ColorSmoothstep            ("Color Smoothstep",                Float)   = 0
        _ColorSmoothstepSmoothness  ("Color Smoothstep Smoothness",     Float)   = 1

        [Space(33)][Header(Distortion)][Space(13)]
        _Distortion                 ("Distortion",                      2D)      = "white" {}
        _DistortionUVScale          ("Distortion UV Scale",             Vector)  = (1,1,0,0)
        _DistortionPanSpeed         ("Distortion Pan Speed",            Vector)  = (0,0,0,0)
        _DistortionAmount           ("Distortion Amount",               Float)   = 0.1

        [Space(33)][Header(Erosion)][Space(13)]
        _Erosion                    ("Erosion",                         2D)      = "white" {}
        _ErosionUVScale             ("Erosion UV Scale",                Vector)  = (1,1,0,0)
        _ErosionPanSpeed            ("Erosion Pan Speed",               Vector)  = (0,0,0,0)
        _ErosionSmoothstep          ("Erosion Smoothstep",              Float)   = 0
        _ErosionSmoothstepSmoothness("Erosion Smoothstep Smoothness",   Float)   = 1

        [Space(33)][Header(Mask)][Space(13)]
        _Mask                       ("Mask",                            2D)      = "white" {}
        _MaskUVScale                ("Mask UV Scale",                   Vector)  = (1,1,0,0)
        _MaskPanSpeed               ("Mask Pan Speed",                  Vector)  = (0,0,0,0)
        _MaskDistortionIntensity    ("Mask Distortion Intensity",       Float)   = 1
        _MaskSmoothstep             ("Mask Smoothstep",                 Float)   = 0
        _MaskSmoothstepSmoothness   ("Mask Smoothstep Smoothness",      Float)   = 1

        [Space(33)][Header(AR)][Space(13)]
        _Cull                       ("Cull",                            Float)   = 2
        _Src                        ("Src",                             Float)   = 5
        _Dst                        ("Dst",                             Float)   = 10
        _ZWrite                     ("ZWrite",                          Float)   = 0
        _ZTest                      ("ZTest",                           Float)   = 2
    }

    SubShader
    {
        // "RenderPipeline" tag is mandatory so URP selects this SubShader
        // and doesn't fall back to the BIRP one if both are present.
        Tags
        {
            "RenderType"        = "Transparent"
            "Queue"             = "Transparent+0"
            "RenderPipeline"    = "UniversalPipeline"
            "IgnoreProjector"   = "True"
        }

        Cull   [_Cull]
        ZWrite [_ZWrite]
        ZTest  [_ZTest]
        Blend  [_Src] [_Dst]

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            // URP core — provides UNITY_MATRIX_MVP, _Time, sampler macros, etc.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ---------------------------------------------------------------------------
            // Textures & Samplers
            // URP uses TEXTURE2D / SAMPLER macros instead of sampler2D to stay
            // compatible with DX11 SRV split (separate texture + sampler objects).
            // ---------------------------------------------------------------------------
            TEXTURE2D(_Color);      SAMPLER(sampler_Color);
            TEXTURE2D(_Distortion); SAMPLER(sampler_Distortion);
            TEXTURE2D(_Erosion);    SAMPLER(sampler_Erosion);
            TEXTURE2D(_Mask);       SAMPLER(sampler_Mask);

            // ---------------------------------------------------------------------------
            // CBUFFER — must be declared inside a ConstantBuffer block in URP
            // so the SRP Batcher can batch draw calls efficiently.
            // ---------------------------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                float  _EmissiveIntensity;
                float  _AlphaAffectsOpacity;
                float  _OverallSpeed;

                float4 _Color_ST;
                float2 _ColorUVScale;
                float2 _ColorPanSpeed;
                float4 _Color01;
                float4 _Color02;
                float  _ColorSmoothstep;
                float  _ColorSmoothstepSmoothness;

                float4 _Distortion_ST;
                float2 _DistortionUVScale;
                float2 _DistortionPanSpeed;
                float  _DistortionAmount;

                float4 _Erosion_ST;
                float2 _ErosionUVScale;
                float2 _ErosionPanSpeed;
                float  _ErosionSmoothstep;
                float  _ErosionSmoothstepSmoothness;

                float4 _Mask_ST;
                float2 _MaskUVScale;
                float2 _MaskPanSpeed;
                float  _MaskDistortionIntensity;
                float  _MaskSmoothstep;
                float  _MaskSmoothstepSmoothness;
            CBUFFER_END

            // ---------------------------------------------------------------------------
            // Vertex input / output
            // ---------------------------------------------------------------------------
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;         // vertex color (alpha = trail fade)
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 vertexColor  : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // TransformObjectToHClip is the URP equivalent of UnityObjectToClipPos
                OUT.positionCS  = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                OUT.vertexColor = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // ------------------------------------------------------------------
                // Global time — same formula as original surf shader
                // ------------------------------------------------------------------
                float globalSpeed = _OverallSpeed * _Time.y;

                // ------------------------------------------------------------------
                // Distortion pass
                // Pans a distortion texture and extracts a scalar warp value.
                // ------------------------------------------------------------------
                float2 distortionUV = uv * _DistortionUVScale + globalSpeed * _DistortionPanSpeed;
                float  distortion   = SAMPLE_TEXTURE2D(_Distortion, sampler_Distortion, distortionUV).g
                                      * _DistortionAmount;

                // ------------------------------------------------------------------
                // Mask pass
                // The original shader offsets UVs by (0.28, 0) before scaling/panning.
                // This is preserved verbatim.
                // ------------------------------------------------------------------
                float2 maskBaseUV   = (uv + float2(0.28, 0.0)) * _MaskUVScale;
                float2 maskUV       = maskBaseUV + globalSpeed * _MaskPanSpeed
                                      + (distortion * _MaskDistortionIntensity);
                float  maskRaw      = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, maskUV).g;
                float  maskValue    = smoothstep(_MaskSmoothstep,
                                                 _MaskSmoothstep + _MaskSmoothstepSmoothness,
                                                 maskRaw);

                // ------------------------------------------------------------------
                // Erosion pass
                // ------------------------------------------------------------------
                float2 erosionUV    = uv * _ErosionUVScale + globalSpeed * _ErosionPanSpeed + distortion;
                float  erosionRaw   = SAMPLE_TEXTURE2D(_Erosion, sampler_Erosion, erosionUV).g;
                float  erosionValue = saturate(smoothstep(_ErosionSmoothstep,
                                                          _ErosionSmoothstep + _ErosionSmoothstepSmoothness,
                                                          erosionRaw));

                // ------------------------------------------------------------------
                // Combined alpha mask
                // Original logic:
                //   saturate( saturate(mask) - saturate(erosion - vertexAlpha) )
                // ------------------------------------------------------------------
                float vertexAlpha   = IN.vertexColor.a;
                float combinedMask  = saturate(
                    saturate(maskValue) - saturate(erosionValue - vertexAlpha)
                );

                // ------------------------------------------------------------------
                // Color pass
                // ------------------------------------------------------------------
                float2 colorUV      = uv * _ColorUVScale + globalSpeed * _ColorPanSpeed;
                float  colorBlend   = saturate(smoothstep(_ColorSmoothstep,
                                                          _ColorSmoothstep + _ColorSmoothstepSmoothness,
                                                          SAMPLE_TEXTURE2D(_Color, sampler_Color, colorUV).g));
                float4 finalColor   = lerp(_Color01, _Color02, colorBlend);

                // ------------------------------------------------------------------
                // Emission
                // vertex RGB * combinedMask * colorRGB * emissive intensity
                // ------------------------------------------------------------------
                float3 emission     = IN.vertexColor.rgb * combinedMask * finalColor.rgb * _EmissiveIntensity;

                // ------------------------------------------------------------------
                // Alpha
                // lerp between raw mask and (mask * vertexAlpha) controlled by
                // _AlphaAffectsOpacity. Matches original lerp/saturate chain.
                // ------------------------------------------------------------------
                float  maskSat      = saturate(combinedMask);
                float  alphaFull    = saturate(maskSat * vertexAlpha);
                float  finalAlpha   = lerp(maskSat, alphaFull, saturate(_AlphaAffectsOpacity));

                return half4(emission, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
