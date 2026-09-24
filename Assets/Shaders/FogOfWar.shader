// Туман войны: проход по всему кадру (Gameplay/FogOfWarFeature.cs).
//
// По глубине точки восстанавливается, где она в мире, и по карте тумана
// (Gameplay/FogOfWar.cs) решается, как её показать:
//   R — видно сейчас,
//   G — разведано.
// Не разведано — чёрное; разведано, но не видно — серое и приглушённое;
// видно — как есть. Небо не трогаем: это не карта.
Shader "Hidden/Sinbinder/FogOfWar"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "FogOfWar"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_FogOfWarTex);
            SAMPLER(sampler_FogOfWarTex);

            // xy — угол карты в мире (x, z), zw — её размер.
            float4 _FogOfWarRect;

            // Сколько света остаётся разведанному, но невидимому,
            // и насколько оно теряет цвет.
            #define FOG_GREY_LIGHT 0.42
            #define FOG_GREY_COLOUR 0.35

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float raw = SampleSceneDepth(uv);

                #if UNITY_REVERSED_Z
                    if (raw <= 0.0) return color;
                    float depth = raw;
                #else
                    if (raw >= 1.0) return color;
                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, raw);
                #endif

                float3 world = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float2 fogUV = (world.xz - _FogOfWarRect.xy) / _FogOfWarRect.zw;

                half seen = 0.0;
                half known = 0.0;
                if (all(fogUV >= 0.0) && all(fogUV <= 1.0))
                {
                    half2 fog = SAMPLE_TEXTURE2D(_FogOfWarTex, sampler_FogOfWarTex, fogUV).rg;
                    seen = fog.r;
                    known = fog.g;
                }

                half luma = dot(color.rgb, half3(0.299, 0.587, 0.114));
                half3 grey = lerp(luma.xxx, color.rgb, FOG_GREY_COLOUR) * FOG_GREY_LIGHT;

                half3 shown = lerp(half3(0.0, 0.0, 0.0), grey, known);
                shown = lerp(shown, color.rgb, seen);

                return half4(shown, color.a);
            }
            ENDHLSL
        }
    }
}
