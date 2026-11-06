Shader "Hidden/TRIUNVIRATO CRT Fullscreen"
{
    Properties { _Fisheye ("Fisheye Strength", Range(0, 0.2)) = 0.025 _Vignette ("Vignette Strength", Range(0, 1)) = 0.25 _ChromaticAberration ("Chromatic Aberration", Range(0, 0.02)) = 0.002 _CRTEnabled ("CRT Enabled", Float) = 1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "CRT Fullscreen"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float _Fisheye, _Vignette, _ChromaticAberration, _CRTEnabled;
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                if (_CRTEnabled < 0.5)
                    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float2 centered = uv * 2.0 - 1.0;
                float r2 = dot(centered, centered);
                float2 warped = centered * (1.0 + _Fisheye * r2) * 0.5 + 0.5;
                float2 shift = centered * (_ChromaticAberration * r2);
                half red = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, warped + shift).r;
                half green = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, warped).g;
                half blue = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, warped - shift).b;
                half vignette = 1.0 - _Vignette * smoothstep(0.25, 1.5, r2);
                return half4(half3(red, green, blue) * vignette, 1);
            }
            ENDHLSL
        }
    }
}
