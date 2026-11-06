Shader "UI/TRIUNVIRATO CRT Scanlines"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Line Tint and Opacity", Color) = (0,0,0,0.10)
        _LineSpacing ("Line Spacing (pixels)", Range(2, 16)) = 6
        _LineWidth ("Line Width (pixels)", Range(1, 3)) = 1
        _LineScrollSpeed ("Line Scroll Speed (pixels/sec)", Range(-100, 100)) = 8
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 _Color;
            float _LineSpacing, _LineWidth, _LineScrollSpeed;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float pixelY = floor(i.uv.y * _ScreenParams.y + _Time.y * _LineScrollSpeed);
                float phase = fmod(pixelY, max(_LineSpacing, 1.0));
                float scanline = 1.0 - step(_LineWidth, phase);
                return fixed4(_Color.rgb, _Color.a * scanline);
            }
            ENDHLSL
        }
    }
}
