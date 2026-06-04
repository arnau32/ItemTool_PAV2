// Converted from BIRP (Amplify Shader Editor v1.9.8) to URP Unlit
// Original: Vefects/SH_Vefects_VFX_Easy_Shockwave_Distortion
//
// Notable features of this shader:
//   - UV0 uses all 4 channels (xy = uvs, z = customOffset, w = distortionIntensity)
//   - UV1 uses all 4 channels (x = noiseMultiply, y = emissiveness, z = desaturation, w = erosion)
//   - GrabPass replaced with URP OpaqueTexture (_CameraOpaqueTexture)
//     Requires "Opaque Texture" ON in the URP Renderer asset.
//   - The distortion offset fed into the screen-space sample is driven by the
//     intensity mask multiplied by emissiveness, NOT by the full color chain like
//     the non-distortion variant. This preserves the original shader's behaviour
//     where only the scalar intensity drives the screen warp amount.
//
// BIRP -> URP changes:
//   - GrabPass{} + UNITY_SAMPLE_SCREENSPACE_TEXTURE(_GrabTexture) removed
//     -> replaced with SampleSceneColor() from DeclareOpaqueTexture.hlsl
//   - ASE_ComputeGrabScreenPos helper removed; ComputeScreenPos is used instead
//   - Surface shader replaced with explicit vert/frag HLSL
//   - CG includes replaced with URP Core.hlsl + DeclareDepthTexture.hlsl + DeclareOpaqueTexture.hlsl
//   - sampler2D / tex2D() replaced with TEXTURE2D / SAMPLE_TEXTURE2D macros
//   - All uniforms in CBUFFER_START(UnityPerMaterial) for SRP Batcher
//   - eyeDepth computed in vertex shader
//   - ShadowCaster pass removed

Shader "Vefects/SH_Vefects_VFX_URP_Easy_Shockwave_Distortion"
{
    Properties
    {
        [Space(33)][Header(Base Texture)][Space(13)]
        _BaseTexture                ("Base Texture",                    2D)      = "white" {}

        [Space(33)][Header(Noise)][Space(13)]
        _NoiseTexture               ("Noise Texture",                   2D)      = "white" {}
        _Noise01UVS                 ("Noise 01 UV S",                   Vector)  = (1,1,0,0)
        _Noise01UVP                 ("Noise 01 UV P",                   Vector)  = (0.1,0.3,0,0)
        _DistortionLerp             ("Distortion Lerp",                 Float)   = 1

        [Normal][Space(33)][Header(Noise Distortion)][Space(13)]
        _NoiseDistortionTexture     ("Noise Distortion Texture",        2D)      = "bump" {}
        _NoiseDistUVS               ("Noise Dist UV S",                 Vector)  = (1,1,0,0)
        _NoiseDistUVP               ("Noise Dist UV P",                 Vector)  = (-0.2,-0.1,0,0)
        _TileUVS                    ("Tile UV S",                       Vector)  = (1,1,0,0)
        _Tile02UVP                  ("Tile 02 UV P",                    Vector)  = (-0.2,-0.1,0,0)
        _Noise01Intensity           ("Noise 01 Intensity",              Float)   = -1
        _Noise02Intensity           ("Noise 02 Intensity",              Float)   = 1
        _CustomOffsetLerp           ("Custom Offset Lerp",              Float)   = 0
        _CustomOffsetBase           ("Custom Offset Base",              Float)   = 0

        [Space(33)][Header(Intensity Mask)][Space(13)]
        _IntensityMask              ("Intensity Mask",                  2D)      = "black" {}
        _IntensityMaskUVS           ("Intensity Mask UV S",             Vector)  = (1,1,0,0)
        _IntensityMaskPower         ("Intensity Mask Power",            Float)   = 1
        _IntensityMaskMultiply      ("Intensity Mask Multiply",         Float)   = 1

        [Space(33)][Header(Depth Fade)][Space(13)]
        _DepthFade                  ("Depth Fade",                      Float)   = 0
        _OpacityMultiply            ("Opacity Multiply",                Float)   = 1

        [Space(33)][Header(Opacity Cutout Mask)][Space(13)]
        _OpacityMask                ("Opacity Mask",                    2D)      = "white" {}
        _ErosionSmoothness          ("Erosion Smoothness",              Float)   = 1

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
            // The OpaqueTexture is resolved after all opaque objects are rendered.
            // This pass sits at Transparent+0 so _CameraOpaqueTexture is available.
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
            // _CameraOpaqueTexture and _CameraDepthTexture are declared by the
            // DeclareOpaqueTexture / DeclareDepthTexture includes. Do NOT redeclare.
            // ---------------------------------------------------------------------------
            TEXTURE2D(_BaseTexture);            SAMPLER(sampler_BaseTexture);
            TEXTURE2D(_NoiseTexture);           SAMPLER(sampler_NoiseTexture);
            TEXTURE2D(_NoiseDistortionTexture); SAMPLER(sampler_NoiseDistortionTexture);
            TEXTURE2D(_IntensityMask);          SAMPLER(sampler_IntensityMask);
            TEXTURE2D(_OpacityMask);            SAMPLER(sampler_OpacityMask);

            // ---------------------------------------------------------------------------
            // CBUFFER — SRP Batcher compatible
            // ---------------------------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseTexture_ST;
                float2 _Noise01UVS;
                float2 _Noise01UVP;
                float  _DistortionLerp;
                float2 _NoiseDistUVS;
                float2 _NoiseDistUVP;
                float2 _TileUVS;
                float2 _Tile02UVP;
                float  _Noise01Intensity;
                float  _Noise02Intensity;
                float  _CustomOffsetLerp;
                float  _CustomOffsetBase;
                float4 _IntensityMask_ST;
                float2 _IntensityMaskUVS;
                float  _IntensityMaskPower;
                float  _IntensityMaskMultiply;
                float  _DepthFade;
                float  _OpacityMultiply;
                float4 _OpacityMask_ST;
                float  _ErosionSmoothness;
            CBUFFER_END

            // ---------------------------------------------------------------------------
            // Vertex I/O
            // ---------------------------------------------------------------------------
            struct Attributes
            {
                float4 positionOS   : POSITION;
                // UV0: xy = base UVs | z = customOffset | w = distortionIntensity (per-vertex)
                float4 uv0          : TEXCOORD0;
                // UV1: x = noiseMultiply | y = emissiveness | z = desaturation | w = erosion
                float4 uv1          : TEXCOORD1;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 uv0          : TEXCOORD0;
                float4 uv1          : TEXCOORD1;
                float4 screenPos    : TEXCOORD2;
                float  eyeDepth     : TEXCOORD3;
                float4 vertexColor  : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);

                OUT.positionCS  = vpi.positionCS;
                OUT.uv0         = IN.uv0;
                OUT.uv1         = IN.uv1;
                OUT.screenPos   = ComputeScreenPos(vpi.positionCS);
                OUT.eyeDepth    = -TransformWorldToView(vpi.positionWS).z;
                OUT.vertexColor = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv               = IN.uv0.xy;

                // ------------------------------------------------------------------
                // Per-vertex data packed into UV channels
                // ------------------------------------------------------------------
                float customOffset      = IN.uv0.z;
                float distortionIntensity = IN.uv0.w;   // per-particle distortion scale
                float noiseMultiply     = IN.uv1.x;
                float emissiveness      = IN.uv1.y;
                // desaturation and erosion still read but only erosion is used here;
                // desaturation is unused in this variant (no color output, only distortion).
                float erosion           = IN.uv1.w;

                // ------------------------------------------------------------------
                // Screen-space UV for OpaqueTexture / depth sampling
                // ComputeScreenPos returns homogeneous coords — divide by .w to normalise.
                // ------------------------------------------------------------------
                float2 screenUV         = IN.screenPos.xy / IN.screenPos.w;

                // ------------------------------------------------------------------
                // Noise distortion warp (same pipeline as the non-distortion variant)
                // UnpackNormal handles DXT5nm / BC5 normal maps correctly.
                // ------------------------------------------------------------------
                float2 noiseDistUV      = uv * _NoiseDistUVS + _Time.y * _NoiseDistUVP;
                float2 noiseDistNormal  = UnpackNormal(
                    SAMPLE_TEXTURE2D(_NoiseDistortionTexture, sampler_NoiseDistortionTexture, noiseDistUV)
                ).xy;
                float2 warpOffset       = ((noiseDistNormal + (-0.5)) * 2.0) * _Noise02Intensity;

                float2 tileUV           = uv * _TileUVS + _Time.y * _Tile02UVP;
                float2 warpedUV         = warpOffset + tileUV;

                // ------------------------------------------------------------------
                // BaseTexture scalar — .g channel drives the distortion strength
                // (original: tex2D(_BaseTexture, warpedUV).g * emissiveness * intensityMask)
                // ------------------------------------------------------------------
                float  baseSample       = SAMPLE_TEXTURE2D(_BaseTexture, sampler_BaseTexture, warpedUV).g;

                // ------------------------------------------------------------------
                // Intensity Mask (identical to the non-distortion variant)
                // ------------------------------------------------------------------
                float2 noisePanUV       = uv * _Noise01UVS + _Time.y * _Noise01UVP;
                float  noiseVal         = SAMPLE_TEXTURE2D(_NoiseTexture, sampler_NoiseTexture, noisePanUV).r;
                float  noiseContrib     = noiseVal * (_Noise01Intensity * noiseMultiply);

                float  offsetFinal      = lerp(_CustomOffsetBase, customOffset, _CustomOffsetLerp);
                float2 maskUV           = noiseContrib + (uv.y * _IntensityMaskUVS + offsetFinal);
                float  maskRaw          = SAMPLE_TEXTURE2D(_IntensityMask, sampler_IntensityMask, maskUV).g;
                float  maskSmooth       = smoothstep(erosion, erosion + _ErosionSmoothness, maskRaw);
                float  intensityMask    = saturate(
                    saturate(pow(abs(maskSmooth), _IntensityMaskPower)) * _IntensityMaskMultiply
                );

                // ------------------------------------------------------------------
                // Distortion scalar
                // baseSample * emissiveness * intensityMask = how much screen-space warp
                // ------------------------------------------------------------------
                float  distortScalar    = (baseSample * emissiveness) * intensityMask;

                // ------------------------------------------------------------------
                // Opacity mask
                // ------------------------------------------------------------------
                float2 omUV             = IN.uv0.xy * _OpacityMask_ST.xy + _OpacityMask_ST.zw;
                float  opacityMask      = SAMPLE_TEXTURE2D(_OpacityMask, sampler_OpacityMask, omUV).g;

                // ------------------------------------------------------------------
                // Depth (soft particles) fade
                // ------------------------------------------------------------------
                float  sceneDepth       = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float  depthFade        = (_DepthFade > 0.0001)
                    ? saturate((sceneDepth - IN.eyeDepth) / _DepthFade)
                    : 1.0;

                // ------------------------------------------------------------------
                // Combined opacity (same chain as original)
                // saturate( saturate( saturate(distortScalar * opacityMask) * depthFade )
                //           * opacityMultiply )
                // ------------------------------------------------------------------
                float  combinedOpacity  = saturate(
                    saturate(
                        saturate(
                            saturate(distortScalar * opacityMask) * depthFade
                        ) * _OpacityMultiply
                    )
                );

                // ------------------------------------------------------------------
                // Screen-space distortion offset
                // lerp(0, combinedOpacity, DistortionLerp * distortionIntensity)
                // Then offset the screen UV to sample the opaque texture.
                //
                // In BIRP: _GrabTexture was sampled at (screenUV + lerpResult)
                // In URP:  SampleSceneColor() wraps _CameraOpaqueTexture the same way.
                // ------------------------------------------------------------------
                float2 distortOffset    = lerp(
                    float2(0.0, 0.0),
                    float2(combinedOpacity, combinedOpacity),
                    _DistortionLerp * distortionIntensity
                );

                half3  sceneColor       = SampleSceneColor(screenUV + distortOffset);

                // Alpha is vertex alpha (matches original: o.Alpha = i.vertexColor.a)
                return half4(sceneColor, IN.vertexColor.a);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
