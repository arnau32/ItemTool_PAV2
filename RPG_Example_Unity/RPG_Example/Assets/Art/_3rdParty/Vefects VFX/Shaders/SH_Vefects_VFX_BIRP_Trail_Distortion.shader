// Converted from BIRP (Amplify Shader Editor) to URP Unlit
// Original: Vefects/SH_Vefects_VFX_BIRP_Trail_Distortion
//
// Key changes from BIRP -> URP:
//   - GrabPass is NOT supported in URP. Replaced with the URP OpaqueTexture
//     mechanism: declare _CameraOpaqueTexture and sample it via screen-space UVs.
//     Requires "Opaque Texture" enabled in the URP Asset (Project Settings > Graphics).
//   - _CameraDepthTexture is still available in URP but must be declared via
//     TEXTURE2D_X / SAMPLE_TEXTURE2D_X and sampled with LinearEyeDepth().
//   - Replaced CG surface shader boilerplate (appdata_full, SurfaceOutput, etc.)
//     with a clean HLSL vertex/fragment pair.
//   - eyeDepth is now computed in the vertex shader using -TransformWorldToView().z
//     (equivalent to -UnityObjectToViewPos().z in BIRP).
//   - screenPos is passed as a Varyings float4 and normalised in the fragment shader.
//   - Removed ShadowCaster pass (distortion-only transparent effect, no shadow needed).
//   - Added "RenderPipeline" = "UniversalPipeline" SubShader tag.
//   - Added UniversalForward LightMode pass tag.

Shader "Vefects/SH_Vefects_VFX_URP_Trail_Distortion"
{
    Properties
    {
        _DistortionIntensity        ("Distortion Intensity",            Float)   = 1
        _NoiseMultiply              ("Noise Multiply",                  Float)   = 1
        _Erosion                    ("Erosion",                         Float)   = 0
        _ErosionSmoothness          ("Erosion Smoothness",              Float)   = 1
        _OpacityMultiply            ("Opacity Multiply",                Float)   = 1

        [Space(33)][Header(Noise)][Space(13)]
        _NoiseTexture               ("Noise Texture",                   2D)      = "white" {}
        _Noise01UVS                 ("Noise 01 UV S",                   Vector)  = (1,1,0,0)
        _Noise01UVP                 ("Noise 01 UV P",                   Vector)  = (0.1,0.3,0,0)
        _DistortionLerp             ("Distortion Lerp",                 Float)   = 1
        _Noise01Intensity           ("Noise 01 Intensity",              Float)   = -1

        [Space(33)][Header(Intensity Mask)][Space(13)]
        _IntensityMask              ("Intensity Mask",                  2D)      = "black" {}
        _IntensityMaskUVS           ("Intensity Mask UV S",             Vector)  = (1,1,0,0)
        _IntensityMaskPanSpeed      ("Intensity Mask Pan Speed",        Vector)  = (0,0,0,0)
        _IntensityMaskOffset        ("Intensity Mask Offset",           Vector)  = (0,0,0,0)
        _IntensityMaskPower         ("Intensity Mask Power",            Float)   = 1
        _IntensityMaskMultiply      ("Intensity Mask Multiply",         Float)   = 1

        [Space(33)][Header(Depth Fade)][Space(13)]
        _DepthFade                  ("Depth Fade",                      Float)   = 0

        [Space(33)][Header(Camera Depth Fade)][Space(13)]
        _CameraDepthFadeLength      ("Camera Depth Fade Length",        Float)   = 3
        _CameraDepthFadeOffset      ("Camera Depth Fade Offset",        Float)   = 0.1

        [Space(33)][Header(Opacity Cutout Mask)][Space(13)]
        _OpacityMask                ("Opacity Mask",                    2D)      = "white" {}
        _OpacityMaskPower           ("Opacity Mask Power",              Float)   = 1
        _OpacityMaskMultiply        ("Opacity Mask Multiply",           Float)   = 1

        [Space(33)][Header(AR)][Space(13)]
        _Cull                       ("Cull",                            Float)   = 0
        _Src                        ("Src",                             Float)   = 5
        _Dst                        ("Dst",                             Float)   = 10
        _ZWrite                     ("ZWrite",                          Float)   = 0
        _ZTest                      ("ZTest",                           Float)   = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType"        = "Transparent"
            "Queue"             = "Transparent+0"
            "RenderPipeline"    = "UniversalPipeline"
            "IgnoreProjector"   = "True"
            // OpaqueTexture is read after the opaque queue is rendered.
            // This pass sits in Transparent+0 so _CameraOpaqueTexture is ready.
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
            #pragma target   3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            // ---------------------------------------------------------------------------
            // Textures & Samplers
            // ---------------------------------------------------------------------------
            TEXTURE2D(_NoiseTexture);    SAMPLER(sampler_NoiseTexture);
            TEXTURE2D(_IntensityMask);   SAMPLER(sampler_IntensityMask);
            TEXTURE2D(_OpacityMask);     SAMPLER(sampler_OpacityMask);

            // _CameraOpaqueTexture and _CameraDepthTexture are declared by the
            // DeclareOpaqueTexture.hlsl / DeclareDepthTexture.hlsl includes above.
            // Do NOT redeclare them here.

            // ---------------------------------------------------------------------------
            // CBUFFER — SRP Batcher compatible
            // ---------------------------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                float  _DistortionIntensity;
                float  _NoiseMultiply;
                float  _Erosion;
                float  _ErosionSmoothness;
                float  _OpacityMultiply;

                float2 _Noise01UVS;
                float2 _Noise01UVP;
                float  _DistortionLerp;
                float  _Noise01Intensity;

                float4 _IntensityMask_ST;
                float2 _IntensityMaskUVS;
                float2 _IntensityMaskPanSpeed;
                float2 _IntensityMaskOffset;
                float  _IntensityMaskPower;
                float  _IntensityMaskMultiply;

                float  _DepthFade;
                float  _CameraDepthFadeLength;
                float  _CameraDepthFadeOffset;

                float4 _OpacityMask_ST;
                float  _OpacityMaskPower;
                float  _OpacityMaskMultiply;
            CBUFFER_END

            // ---------------------------------------------------------------------------
            // Vertex input / output
            // ---------------------------------------------------------------------------
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float2 uv2          : TEXCOORD1;   // second UV channel (intensity mask)
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float2 uv2          : TEXCOORD1;
                float4 screenPos    : TEXCOORD2;   // raw clip-space for screen UVs
                float  eyeDepth     : TEXCOORD3;   // linear eye depth for camera fade
                float4 vertexColor  : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);

                OUT.positionCS  = vertexInput.positionCS;
                OUT.uv          = IN.uv;
                OUT.uv2         = IN.uv2;
                OUT.screenPos   = ComputeScreenPos(vertexInput.positionCS);

                // Eye depth: distance from camera in view space (positive value)
                // Equivalent to -UnityObjectToViewPos(v.vertex).z in BIRP.
                OUT.eyeDepth    = -TransformWorldToView(vertexInput.positionWS).z;

                OUT.vertexColor = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // ------------------------------------------------------------------
                // Screen-space UV (normalised) — replaces ASE_ComputeGrabScreenPos
                // In URP we use the positionCS.xy / w trick via ComputeScreenPos.
                // ------------------------------------------------------------------
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                // ------------------------------------------------------------------
                // Noise texture pan — drives intensity mask distortion
                // ------------------------------------------------------------------
                float2 noisePanUV   = IN.uv * _Noise01UVS + _Time.y * _Noise01UVP;
                float  noiseSample  = SAMPLE_TEXTURE2D(_NoiseTexture, sampler_NoiseTexture, noisePanUV).r;
                float  noiseOffset  = noiseSample * (_Noise01Intensity * _NoiseMultiply);

                // ------------------------------------------------------------------
                // Intensity mask
                // UV2 * scale + offset + pan, then add noise offset before sampling.
                // ------------------------------------------------------------------
                float2 imBaseUV     = IN.uv2 * _IntensityMaskUVS + _IntensityMaskOffset;
                float2 imUV         = imBaseUV + _Time.y * _IntensityMaskPanSpeed + noiseOffset;
                float  imRaw        = SAMPLE_TEXTURE2D(_IntensityMask, sampler_IntensityMask, imUV).g;
                float  imSmooth     = smoothstep(_Erosion, _Erosion + _ErosionSmoothness, imRaw);
                float  intensityMask = saturate(saturate(pow(abs(imSmooth), _IntensityMaskPower))
                                                * _IntensityMaskMultiply);

                // ------------------------------------------------------------------
                // Opacity mask (UV channel 0 with ST transform)
                // ------------------------------------------------------------------
                float2 omUV         = IN.uv * _OpacityMask_ST.xy + _OpacityMask_ST.zw;
                float  omRaw        = SAMPLE_TEXTURE2D(_OpacityMask, sampler_OpacityMask, omUV).g;
                float  opacityMask  = saturate(saturate(pow(abs(omRaw), _OpacityMaskPower))
                                                * _OpacityMaskMultiply);

                // ------------------------------------------------------------------
                // Camera depth fade
                // (_ProjectionParams.y is the near clip plane in URP as in BIRP)
                // ------------------------------------------------------------------
                float cameraFade    = (_ProjectionParams.y >= 0)
                    ? (IN.eyeDepth - _ProjectionParams.y - _CameraDepthFadeOffset) / _CameraDepthFadeLength
                    : 0.0;
                cameraFade          = saturate(cameraFade);

                // ------------------------------------------------------------------
                // Depth (soft particles) fade
                // SampleSceneDepth returns raw depth; LinearEyeDepth converts it.
                // ------------------------------------------------------------------
                float2 depthScreenUV = screenUV;
                float  sceneRawDepth = SampleSceneDepth(depthScreenUV);
                float  sceneDepth    = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                // Avoid division by zero when _DepthFade == 0
                float  depthFade     = (_DepthFade > 0.0001)
                    ? saturate((sceneDepth - IN.eyeDepth) / _DepthFade)
                    : 1.0;

                // ------------------------------------------------------------------
                // Combined opacity mask chain:
                //   intensityMask * opacityMask -> camera fade -> depth fade -> multiply
                // Matches the saturate chain in the original surf shader.
                // ------------------------------------------------------------------
                float  combinedOpacity = saturate(
                    saturate(
                        saturate(
                            saturate(intensityMask * opacityMask) * cameraFade
                        ) * depthFade
                    ) * _OpacityMultiply
                );

                // ------------------------------------------------------------------
                // Screen-space distortion
                // lerp(0, combinedOpacity, distortionLerp * distortionIntensity)
                // added to base screen UV to offset the opaque texture sample.
                // ------------------------------------------------------------------
                float2 distortOffset = lerp(
                    float2(0, 0),
                    float2(combinedOpacity, combinedOpacity),
                    _DistortionLerp * _DistortionIntensity
                );

                float2 sampledUV    = screenUV + distortOffset;

                // ------------------------------------------------------------------
                // Sample the opaque (grab) texture — URP replacement for GrabPass.
                // SampleSceneColor is defined in DeclareOpaqueTexture.hlsl and wraps
                // _CameraOpaqueTexture, which must be enabled in the URP asset.
                // ------------------------------------------------------------------
                half3  sceneColor   = SampleSceneColor(sampledUV);

                // Alpha is vertex alpha (same as original o.Alpha = i.vertexColor.a)
                float  finalAlpha   = IN.vertexColor.a;

                return half4(sceneColor, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
