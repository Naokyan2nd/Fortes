Shader "OutGame/UI_VerticalCenterDarken"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ScanRevealHalf ("Scan Reveal Half (UV, matches ScanWave)", Range(0, 0.5)) = 0
        _ScanRectBottom ("Scan Rect Bottom Y (canvas 0-1)", Range(0, 1)) = 0
        _ScanRectTop ("Scan Rect Top Y (canvas 0-1)", Range(0, 1)) = 1
        _DarkenMultiplier ("Darken Multiplier", Range(0, 1)) = 0.45
        _DarkenSoftness ("Darken Edge Softness (UV)", Range(0.001, 0.25)) = 0.02
        _CanvasRect ("Canvas Bounds (xMin, yMin, xMax, yMax)", Vector) = (0, 0, 1, 1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _CullMode ("Cull Mode", Float) = 0
        _ColorMask ("Color Mask", Float) = 15
        _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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

        Cull [_CullMode]
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

            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

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
                float2 canvasWorldXY : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _ScanRevealHalf;
            float _ScanRectBottom;
            float _ScanRectTop;
            float _DarkenMultiplier;
            float _DarkenSoftness;
            float4 _CanvasRect;

            float GetCanvasNormalizedY(float2 position)
            {
                float height = max(_CanvasRect.w - _CanvasRect.y, 0.0001);
                return (position.y - _CanvasRect.y) / height;
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.canvasWorldXY = mul(unity_ObjectToWorld, v.vertex).xy;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half4 color = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;

                if (_ScanRevealHalf > 0.001)
                {
                    float normalizedY = GetCanvasNormalizedY(i.canvasWorldXY);
                    float scanHeight = max(_ScanRectTop - _ScanRectBottom, 0.0001);
                    float uvY = (normalizedY - _ScanRectBottom) / scanHeight;
                    float distOutsideReveal = max(abs(uvY - 0.5) - _ScanRevealHalf, 0.0);
                    float darkenT = saturate(distOutsideReveal / max(_DarkenSoftness, 0.0001));
                    color.rgb *= lerp(1.0, _DarkenMultiplier, darkenT);
                }

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
