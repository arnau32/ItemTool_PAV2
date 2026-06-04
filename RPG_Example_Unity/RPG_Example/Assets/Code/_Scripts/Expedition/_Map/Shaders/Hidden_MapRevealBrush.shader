Shader "Hidden/MapRevealBrush"
{
    Properties
    {
        _MainTex("Previous Mask", 2D) = "black" {}
        _RevealCenter("Reveal Center", Vector) = (0.5, 0.5, 0, 0)
        _RevealRadius("Reveal Radius", Float) = 0.05
        _RevealSoftness("Reveal Softness", Float) = 0.2
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _RevealCenter;
                float _RevealRadius;
                float _RevealSoftness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half previous = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).r;

                float dist = distance(IN.uv, _RevealCenter.xy);
                float innerRadius = _RevealRadius * (1.0 - _RevealSoftness);
                half reveal = 1.0 - smoothstep(innerRadius, _RevealRadius, dist);

                half result = max(previous, reveal);

                return half4(result, result, result, 1);
            }

            ENDHLSL
        }
    }
}