Shader "Custom/UI_MapFog"
{
    Properties
    {
        [MainTexture] _BaseMap("Map Texture", 2D) = "white" {}
        _ExplorationMask("Exploration Mask", 2D) = "black" {}
        _FogColor("Fog Color", Color) = (0, 0, 0, 0.95)
        _ExploredTint("Explored Tint", Color) = (1, 1, 1, 1)
        _FogStrength("Fog Strength", Range(0, 1)) = 1

        [Toggle] _FullyDiscovered("Fully Discovered", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

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
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_ExplorationMask);
            SAMPLER(sampler_ExplorationMask);

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                half4 _ExploredTint;
                float _FogStrength;
                float _FullyDiscovered;
                float4 _BaseMap_ST;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 mapColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * IN.color;

                // 🚀 FAST PATH: mapa completamente descubierto
                if (_FullyDiscovered > 0.5)
                {
                    return mapColor * _ExploredTint;
                }

                // 🔻 Camino normal (con fog)
                half mask = SAMPLE_TEXTURE2D(_ExplorationMask, sampler_ExplorationMask, IN.uv).r;

                half4 fogged = lerp(mapColor, _FogColor, _FogStrength);
                half4 visibleMap = mapColor * _ExploredTint;

                return lerp(fogged, visibleMap, mask);
            }

            ENDHLSL
        }
    }
}