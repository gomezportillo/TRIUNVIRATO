Shader "TRIUNVIRATO/Sprite Outline URP"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _SwapTex ("Optional Color Swap", 2D) = "transparent" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlinePixels ("Outline Width (source pixels)", Range(0,4)) = 1
        [HideInInspector] _SpriteUvMin ("Sprite UV Min", Vector) = (0,0,0,0)
        [HideInInspector] _SpriteUvMax ("Sprite UV Max", Vector) = (0,0,0,0)
        [HideInInspector] _SpriteUvValid ("Sprite UV Valid", Float) = 0
        [HideInInspector] _SpriteLocalMin ("Sprite Local Min", Vector) = (0,0,0,0)
        [HideInInspector] _SpriteLocalMax ("Sprite Local Max", Vector) = (0,0,0,0)
        [HideInInspector] _OutlineLocalWidth ("Outline Local Width", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "CanUseSpriteAtlas"="True"
        }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

        TEXTURE2D(_SwapTex);
        SAMPLER(sampler_SwapTex);
        float4 _MainTex_TexelSize;

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half4 _OutlineColor;
            float _OutlinePixels;
            float4 _SpriteUvMin;
            float4 _SpriteUvMax;
            float _SpriteUvValid;
            float4 _SpriteLocalMin;
            float4 _SpriteLocalMax;
            float _OutlineLocalWidth;
        CBUFFER_END

        struct Attributes
        {
            COMMON_2D_INPUTS
            half4 color : COLOR;
            UNITY_SKINNED_VERTEX_INPUTS
        };

        struct Varyings
        {
            COMMON_2D_OUTPUTS
            half4 color : COLOR;
        };

        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

        Varyings Vert(Attributes input)
        {
            UNITY_SKINNED_VERTEX_COMPUTE(input);
            SetUpSpriteInstanceProperties();
            input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

            // SpriteRenderer normally clips the mesh exactly to the sprite rectangle.
            // Expand that rectangle and its UVs together so the outline has room outside.
            if (_SpriteUvValid > 0.5 && _OutlinePixels > 0.01 && _OutlineLocalWidth > 0.0)
            {
                float2 fromCenter = input.positionOS.xy - ((_SpriteLocalMin.xy + _SpriteLocalMax.xy) * 0.5);
                float2 direction = sign(fromCenter);
                input.positionOS.xy += direction * _OutlineLocalWidth;
                float2 uvDirection = sign(input.uv - ((_SpriteUvMin.xy + _SpriteUvMax.xy) * 0.5));
                input.uv += uvDirection * _MainTex_TexelSize.xy * _OutlinePixels;
            }

            Varyings output = CommonUnlitVertex(input);
            output.color = input.color * _Color * unity_SpriteColor;
            return output;
        }

        half SampleNeighborAlpha(float2 uv)
        {
            if (_SpriteUvValid > 0.5)
            {
                if (any(uv < _SpriteUvMin.xy) || any(uv > _SpriteUvMax.xy))
                    return 0;
            }
            else if (any(uv < 0) || any(uv > 1))
            {
                return 0;
            }

            // Explicit LOD avoids implicit-derivative sampling inside the outline loops,
            // which makes Unity's DX11 shader compiler try to unroll a huge loop.
            return SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).a;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            if (source.a > 0.01)
            {
                half4 swapColor = SAMPLE_TEXTURE2D(_SwapTex, sampler_SwapTex, float2(source.r, 0));
                half4 baseColor = lerp(source, swapColor, swapColor.a);
                return half4(baseColor.rgb * input.color.rgb, source.a * input.color.a);
            }

            if (_OutlinePixels <= 0.01)
                return 0;

            float2 texel = _MainTex_TexelSize.xy;
            int radius = max(1, (int)ceil(_OutlinePixels));
            half neighborAlpha = 0;
            // Keep the loop bounds constant. DX11 cannot safely unroll a loop whose
            // bounds depend on the material property, and was expanding it to hundreds
            // of iterations. The material clamps the supported radius to four pixels.
            [unroll]
            for (int y = -4; y <= 4; y++)
            {
                [unroll]
                for (int x = -4; x <= 4; x++)
                {
                    if (x == 0 && y == 0)
                        continue;
                    if (max(abs(x), abs(y)) > radius)
                        continue;
                    neighborAlpha = max(neighborAlpha, SampleNeighborAlpha(input.uv + float2(x, y) * texel));
                }
            }

            if (neighborAlpha <= 0.01)
                return 0;
            return half4(_OutlineColor.rgb * input.color.rgb, _OutlineColor.a * input.color.a);
        }
        ENDHLSL

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            ENDHLSL
        }

        Pass
        {
            Name "SRPDefaultUnlit"
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            ENDHLSL
        }
    }
}
