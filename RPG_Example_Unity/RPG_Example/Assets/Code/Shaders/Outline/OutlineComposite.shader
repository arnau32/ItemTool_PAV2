Shader "Custom/Outline/Composite"
{
    Properties
    {
        _OutlineColor ("Color", Color) = (1, 0.8, 0.2, 1)
        _OutlineWidth ("Width", Float) = 3
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Composite"

            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite

            // Core.hlsl must come first — it defines TEXTURE2D_X and sampler types
            // that Blit.hlsl depends on internally.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _OutlineColor;
            float  _OutlineWidth;

            half4 FragComposite(Varyings IN) : SV_Target
            {
                float2 uv    = IN.texcoord;
                // _ScreenParams.xy = (width, height); mask RT matches camera RT size
                float2 texel = 1.0 / _ScreenParams.xy;
                float  w     = _OutlineWidth;

                float orig = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).r;

                // Cross-pattern dilation — 4 samples, O(1) per pixel
                float dilated = orig;
                dilated = max(dilated, SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( w,  0) * texel).r);
                dilated = max(dilated, SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-w,  0) * texel).r);
                dilated = max(dilated, SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( 0,  w) * texel).r);
                dilated = max(dilated, SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( 0, -w) * texel).r);

                // Border = dilated silhouette minus original fill
                float border = saturate(dilated - orig);
                return half4(_OutlineColor.rgb, border * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}