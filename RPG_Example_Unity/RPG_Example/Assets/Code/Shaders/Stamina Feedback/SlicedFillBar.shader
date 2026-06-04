Shader "Custom/UI/SlicedFillBar"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FillAmount ("Fill Amount", Range(0, 1)) = 1
        _InnerFillAmount ("Inner Fill Amount", Range(0, 1)) = 0

        [Header(Slicing)]
        _BorderLeft ("Border Left", Float) = 0.1
        _BorderRight ("Border Right", Float) = 0.1
        _BorderTop ("Border Top", Float) = 0.1
        _BorderBottom ("Border Bottom", Float) = 0.1

        [Header(Fill Settings)]
        [Enum(Horizontal,0,Vertical,1,Radial90,2,Radial180,3,Radial360,4)] _FillType("Fill Type", Float) = 0
        [Enum(Left,0,Right,1,Bottom,2,Top,3)] _FillOrigin("Fill Origin", Float) = 0
        [Toggle] _Clockwise("Clockwise", Float) = 1

        [Header(Render Settings)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull [_Cull]
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _FillAmount;
            float _InnerFillAmount;
            float _FillType;
            float _FillOrigin;
            float _Clockwise;

            float _BorderLeft;
            float _BorderRight;
            float _BorderTop;
            float _BorderBottom;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            // Calculate fill alpha for horizontal fill with 9-slice support and inner fill
            float CalculateHorizontalFill(float2 uv, float fillAmount, float innerFill, float origin, float borderL,
        float borderR)
            {
                float uvX = uv.x;

                // Fill from Left (0)
                if (origin < 0.5)
                {
                    // Left border region
                    if (uvX <= borderL)
                    {
                        float outerVisible = step(uvX, fillAmount);
                        float innerVisible = step(uvX, innerFill);
                        return outerVisible * (1.0 - innerVisible);
                    }
                    // Center region
                    else if (uvX <= 1.0 - borderR)
                    {
                        float centerUV = (uvX - borderL) / (1.0 - borderL - borderR);
                        float centerFill = saturate((fillAmount - borderL) / (1.0 - borderL - borderR));
                        float centerInner = saturate((innerFill - borderL) / (1.0 - borderL - borderR));

                        // Show pixels between innerFill and fillAmount
                        float outerMask = step(centerUV, centerFill);
                        float innerMask = step(centerUV, centerInner);
                        return outerMask * (1.0 - innerMask);
                    }
                    // Right border region
                    else
                    {
                        float outerVisible = step(1.0 - borderR, fillAmount);
                        float innerVisible = step(1.0 - borderR, innerFill);
                        return outerVisible * (1.0 - innerVisible);
                    }
                }
                // Fill from Right (1)
                else
                {
                    // Right border region
                    if (uvX >= 1.0 - borderR)
                    {
                        float posInFill = uvX;
                        float outerVisible = step(posInFill, fillAmount);
                        float innerVisible = step(posInFill, innerFill);
                        return outerVisible * (1.0 - innerVisible);
                    }
                    // Center region
                    else if (uvX >= borderL)
                    {
                        float centerUV = (uvX - borderL) / (1.0 - borderL - borderR);
                        float centerFill = saturate((fillAmount - borderR) / (1.0 - borderL - borderR));
                        float centerInner = saturate((innerFill - borderR) / (1.0 - borderL - borderR));

                        float outerMask = step(1.0 - centerUV, centerFill);
                        float innerMask = step(1.0 - centerUV, centerInner);
                        return outerMask * (1.0 - innerMask);
                    }
                    // Left border region
                    else
                    {
                        float leftProgress = uvX / borderL;
                        float posInFill = 1.0 - leftProgress * borderL;
                        float outerVisible = step(posInFill, fillAmount);
                        float innerVisible = step(posInFill, innerFill);
                        return outerVisible * (1.0 - innerVisible);
                    }
                }
            }

            // Calculate fill alpha for vertical fill with 9-slice support and inner fill
            float CalculateVerticalFill(float2 uv, float fillAmount, float innerFill, float origin, float borderB,
                                        float borderT)
            {
                float uvY = uv.y;

                // Fill from Bottom (2)
                if (origin < 2.5)
                {
                    // Bottom border region
                    if (uvY <= borderB)
                    {
                        return step(0.001, fillAmount) * (1.0 - step(uvY / borderB, innerFill));
                    }
                    // Center region
                    else if (uvY <= 1.0 - borderT)
                    {
                        float centerUV = (uvY - borderB) / (1.0 - borderB - borderT);
                        float centerFill = saturate((fillAmount - borderB) / (1.0 - borderB - borderT));
                        float centerInner = saturate((innerFill - borderB) / (1.0 - borderB - borderT));

                        float outerMask = step(centerUV, centerFill);
                        float innerMask = step(centerUV, centerInner);
                        return outerMask * (1.0 - innerMask);
                    }
                    // Top border region
                    else
                    {
                        return step(1.0 - borderT, fillAmount) * (1.0 - step(1.0 - borderT, innerFill));
                    }
                }
                // Fill from Top (3)
                else
                {
                    // Top border region
                    if (uvY >= 1.0 - borderT)
                    {
                        float topUV = (uvY - (1.0 - borderT)) / borderT;
                        return step(0.001, fillAmount) * (1.0 - step(1.0 - topUV, innerFill));
                    }
                    // Center region
                    else if (uvY >= borderB)
                    {
                        float centerUV = (uvY - borderB) / (1.0 - borderB - borderT);
                        float centerFill = saturate((fillAmount - borderT) / (1.0 - borderB - borderT));
                        float centerInner = saturate((innerFill - borderT) / (1.0 - borderB - borderT));

                        float outerMask = step(1.0 - centerUV, centerFill);
                        float innerMask = step(1.0 - centerUV, centerInner);
                        return outerMask * (1.0 - innerMask);
                    }
                    // Bottom border region
                    else
                    {
                        float bottomUV = uvY / borderB;
                        return step(borderB, fillAmount) * (1.0 - step(bottomUV, innerFill));
                    }
                }
            }

            // Calculate radial fill alpha
            float CalculateRadialFill(float2 uv, float fillAmount, float innerFill, float fillType, float origin,
float clockwise)
            {
                float2 centered = uv - 0.5;
                float angle = atan2(centered.y, centered.x);

                // Normalize angle to 0-1 range
                angle = angle / (2.0 * 3.14159265) + 0.5;

                // Adjust for origin and clockwise
                float originOffset = 0.0;

                // Radial 360 (4) - origin values: Bottom=0, Right=1, Top=2, Left=3
                if (fillType > 3.5)
                {
                    if (origin < 0.5) originOffset = 0.25; // Bottom
                    else if (origin < 1.5) originOffset = 0.0; // Right
                    else if (origin < 2.5) originOffset = 0.75; // Top
                    else originOffset = 0.5; // Left
                }
                // Radial 180 (3) - origin values: Bottom=0, Left=1, Top=2, Right=3
                else if (fillType > 2.5)
                {
                    if (origin < 0.5) originOffset = 0.25; // Bottom
                    else if (origin < 1.5) originOffset = 0.5; // Left
                    else if (origin < 2.5) originOffset = 0.75; // Top
                    else originOffset = 0.0; // Right

                    fillAmount *= 0.5; // 180 degrees = half circle
                    innerFill *= 0.5;
                }
                // Radial 90 (2) - origin values: BottomLeft=0, TopLeft=1, TopRight=2, BottomRight=3
                else if (fillType > 1.5)
                {
                    if (origin < 0.5) originOffset = 0.375; // BottomLeft
                    else if (origin < 1.5) originOffset = 0.625; // TopLeft
                    else if (origin < 2.5) originOffset = 0.875; // TopRight
                    else originOffset = 0.125; // BottomRight

                    fillAmount *= 0.25; // 90 degrees = quarter circle
                    innerFill *= 0.25;
                }

                angle = frac(angle - originOffset);

                // Clockwise vs Counter-clockwise
                if (clockwise < 0.5)
                {
                    angle = 1.0 - angle;
                }

                // Create ring: visible between innerFill and fillAmount
                float outerMask = step(angle, fillAmount);
                float innerMask = step(angle, innerFill);

                return outerMask * (1.0 - innerMask);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Sample texture
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // Apply clipping
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                float alpha = 1.0;

                // Horizontal Fill (0)
                if (_FillType < 0.5)
                {
                    alpha = CalculateHorizontalFill(IN.texcoord, _FillAmount, _InnerFillAmount, _FillOrigin,
       _BorderLeft, _BorderRight);
                }
                // Vertical Fill (1)
                else if (_FillType < 1.5)
                {
                    alpha = CalculateVerticalFill(IN.texcoord, _FillAmount, _InnerFillAmount, _FillOrigin,
                 _BorderBottom, _BorderTop);
                }
                // Radial 90 (2), Radial 180 (3), Radial 360 (4)
                else
                {
                    alpha = CalculateRadialFill(IN.texcoord, _FillAmount, _InnerFillAmount, _FillType, _FillOrigin,
   _Clockwise);
                }

                color.a *= alpha;

                // Discard fully transparent pixels (optimization)
                clip(color.a - 0.001);

                return color;
            }
            ENDCG
        }
    }
}