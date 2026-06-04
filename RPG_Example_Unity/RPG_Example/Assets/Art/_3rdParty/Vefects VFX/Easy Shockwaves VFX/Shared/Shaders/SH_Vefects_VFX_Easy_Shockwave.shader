// Converted from BIRP (Amplify Shader Editor v1.9.8) to URP Unlit
// Original: Vefects/SH_Vefects_VFX_Easy_Shockwave
//
// Notable features of this shader:
//   - UV0 uses all 4 channels (xy = uvs, z = customOffset, w = chromaticAberration)
//   - UV1 uses all 4 channels (x = noiseMultiply, y = emissiveness, z = desaturation, w = erosion)
//   - Chromatic aberration: BaseTexture is sampled 3 times with +offset / center / -offset
//     and the R, G, B channels are recombined into a fake aberration effect
//   - Depth (soft particles) fade via _CameraDepthTexture
//
// BIRP -> URP changes:
//   - Surface shader replaced with explicit vert/frag HLSL
//   - CG includes replaced with URP Core.hlsl + DeclareDepthTexture.hlsl
//   - sampler2D / tex2D() replaced with TEXTURE2D / SAMPLE_TEXTURE2D macros
//   - All uniforms moved into CBUFFER_START(UnityPerMaterial) for SRP Batcher
//   - eyeDepth computed in vertex shader via -TransformWorldToView().z
//   - screenPos passed as float4 and normalised in fragment shader
//   - ShadowCaster pass removed (transparent VFX, no shadow casting needed)
//   - UnpackNormal equivalent is UnpackNormal() — still available in Core.hlsl

Shader "Vefects/SH_Vefects_VFX_URP_Easy_Shockwave"
{
    Properties
    {
        [Space(33)][Header(Base Texture)][Space(13)]
        _BaseTexture                ("Base Texture",                    2D)      = "white" {}

        [Space(33)][Header(Noise)][Space(13)]
        _NoiseTexture               ("Noise Texture",                   2D)      = "white" {}
        _Noise01UVS                 ("Noise 01 UV S",                   Vector)  = (1,1,0,0)
        _Noise01UVP                 ("Noise 01 UV P",                   Vector)  = (0.1,0.3,0,0)

        [Normal][Space(33)][Header(Noise Distortion)][Space(13)]
        _NoiseDistortionTexture     ("Noise Distortion Texture",        2D)      = "white" {}
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

            // ---------------------------------------------------------------------------
            // Textures & Samplers
            // ---------------------------------------------------------------------------
            TEXTURE2D(_BaseTexture);            SAMPLER(sampler_BaseTexture);
            TEXTURE2D(_NoiseTexture);           SAMPLER(sampler_NoiseTexture);
            TEXTURE2D(_NoiseDistortionTexture); SAMPLER(sampler_NoiseDistortionTexture);
            TEXTURE2D(_IntensityMask);          SAMPLER(sampler_IntensityMask);
            TEXTURE2D(_OpacityMask);            SAMPLER(sampler_OpacityMask);

            // ---------------------------------------------------------------------------
            // CBUFFER — SRP Batcher compatible
            // All per-material properties must live here.
            // ---------------------------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseTexture_ST;
                float2 _Noise01UVS;
                float2 _Noise01UVP;
                float4 _NoiseDistortionTexture_ST;
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
                // UV0: xy = base UVs | z = customOffset | w = chromaticAberration amount
                float4 uv0          : TEXCOORD0;
                // UV1: x = noiseMultiply | y = emissiveness | z = desaturation | w = erosion
                float4 uv1          : TEXCOORD1;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 uv0          : TEXCOORD0;    // xy = uvs, z = customOffset, w = chromAberr
                float4 uv1          : TEXCOORD1;    // x = noiseMul, y = emissive, z = desat, w = eros
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
                // Eye depth: positive linear distance from camera
                OUT.eyeDepth    = -TransformWorldToView(vpi.positionWS).z;
                OUT.vertexColor = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv           = IN.uv0.xy;

                // ------------------------------------------------------------------
                // Per-vertex data packed into UV channels
                // ------------------------------------------------------------------
                float customOffset      = IN.uv0.z;
                float chromaticAberr    = IN.uv0.w;
                float noiseMultiply     = IN.uv1.x;
                float emissiveness      = IN.uv1.y;
                float desaturation      = saturate(IN.uv1.z);
                float erosion           = IN.uv1.w;

                // ------------------------------------------------------------------
                // Noise distortion — unpacks normal map to get a 2D warp vector
                // ((normalXY + (-0.5)) * 2.0) * Noise02Intensity
                // This is the manual equivalent of ASE's ConstantBiasScale node.
                // ------------------------------------------------------------------
                float2 noiseDistUV      = uv * _NoiseDistUVS + _Time.y * _NoiseDistUVP;
                float2 noiseDistNormal  = UnpackNormal(
                    SAMPLE_TEXTURE2D(_NoiseDistortionTexture, sampler_NoiseDistortionTexture, noiseDistUV)
                ).xy;
                // Remap [0,1] normal XY to [-1,1] offset, scale by intensity
                float2 warpedOffset     = ((noiseDistNormal + (-0.5)) * 2.0) * _Noise02Intensity;

                // Tile panner — base UVs for BaseTexture sampling
                float2 tileUV           = uv * _TileUVS + _Time.y * _Tile02UVP;
                float2 warpedUV         = warpedOffset + tileUV;

                // ------------------------------------------------------------------
                // Chromatic aberration
                // Sample BaseTexture three times:
                //   R channel: warpedUV + aberration scalar
                //   G channel: warpedUV (center)
                //   B channel: warpedUV - aberration scalar
                // The scalar is chromaticAberration * 0.001 (from original: uv.w * 0.001)
                // ------------------------------------------------------------------
                float  aberrScalar  = chromaticAberr * 0.001;
                float  sampleR      = SAMPLE_TEXTURE2D(_BaseTexture, sampler_BaseTexture,
                                          warpedUV + aberrScalar).r;
                float  sampleG      = SAMPLE_TEXTURE2D(_BaseTexture, sampler_BaseTexture,
                                          warpedUV).g;
                float  sampleB      = SAMPLE_TEXTURE2D(_BaseTexture, sampler_BaseTexture,
                                          warpedUV - aberrScalar).b;
                float3 colorRGB     = float3(sampleR, sampleG, sampleB);

                // ------------------------------------------------------------------
                // Desaturation (vertex-driven, packed in UV1.z)
                // lerp(color, dot(color, luminance), desaturation)
                // ------------------------------------------------------------------
                float  luminance    = dot(colorRGB, float3(0.299, 0.587, 0.114));
                float3 desatColor   = lerp(colorRGB, luminance.xxx, desaturation);

                // ------------------------------------------------------------------
                // Intensity Mask
                // Noise pan -> sample noise -> multiply by (Noise01Intensity * noiseMultiply)
                // -> add to (uv.y * IntensityMaskUVS + customOffset) -> sample mask
                // ------------------------------------------------------------------
                float2 noisePanUV   = uv * _Noise01UVS + _Time.y * _Noise01UVP;
                float  noiseVal     = SAMPLE_TEXTURE2D(_NoiseTexture, sampler_NoiseTexture, noisePanUV).r;
                float  noiseContrib = noiseVal * (_Noise01Intensity * noiseMultiply);

                // customOffset: lerp between material base and per-vertex value
                float  offsetFinal  = lerp(_CustomOffsetBase, customOffset, _CustomOffsetLerp);

                // The intensity mask UV is 1D along Y, matching original: uv.y * _IntensityMaskUVS
                float2 maskUV       = noiseContrib + (uv.y * _IntensityMaskUVS + offsetFinal);
                float  maskRaw      = SAMPLE_TEXTURE2D(_IntensityMask, sampler_IntensityMask, maskUV).g;
                float  maskSmooth   = smoothstep(erosion, erosion + _ErosionSmoothness, maskRaw);
                float  intensityMask = saturate(
                    saturate(pow(abs(maskSmooth), _IntensityMaskPower)) * _IntensityMaskMultiply
                );

                // ------------------------------------------------------------------
                // Emission = vertexColor * (desatColor * emissiveness * intensityMask)
                // ------------------------------------------------------------------
                float3 emission     = IN.vertexColor.rgb * (desatColor * emissiveness * intensityMask);

                // ------------------------------------------------------------------
                // Depth (soft particles) fade
                // ------------------------------------------------------------------
                float2 screenUV     = IN.screenPos.xy / IN.screenPos.w;
                float  sceneDepth   = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float  depthFade    = (_DepthFade > 0.0001)
                    ? saturate((sceneDepth - IN.eyeDepth) / _DepthFade)
                    : 1.0;

                // ------------------------------------------------------------------
                // Opacity mask
                // ------------------------------------------------------------------
                float2 omUV         = IN.uv0.xy * _OpacityMask_ST.xy + _OpacityMask_ST.zw;
                float  opacityMask  = SAMPLE_TEXTURE2D(_OpacityMask, sampler_OpacityMask, omUV).g;

                // ------------------------------------------------------------------
                // Final alpha chain (mirrors original saturate nesting)
                // saturate( saturate( saturate(intensityMask * opacityMask) * depthFade )
                //           * opacityMultiply ) * vertexAlpha
                // ------------------------------------------------------------------
                float  finalAlpha   = saturate(
                    saturate(
                        saturate(
                            saturate(intensityMask * opacityMask) * depthFade
                        ) * _OpacityMultiply
                    ) * IN.vertexColor.a
                );

                return half4(emission, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
