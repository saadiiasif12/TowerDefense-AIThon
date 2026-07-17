// Cheap URP dissolve for enemy deaths: unlit textured, UV-space value noise (stable under
// skinned animation), alpha-clip burn with an emissive ember edge. One pass, mobile-friendly.
Shader "RoyalSiege/Dissolve"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _Dissolve("Dissolve", Range(0,1)) = 0
        _EdgeWidth("Edge Width", Range(0.005,0.25)) = 0.09
        _EdgeColor("Edge Color", Color) = (2.6,1.5,0.45,1)
        _NoiseScale("Noise Scale", Float) = 9
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Cull Off

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Dissolve;
                half _EdgeWidth;
                half4 _EdgeColor;
                float _NoiseScale;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float n = vnoise(input.uv * _NoiseScale) * 0.65 + vnoise(input.uv * _NoiseScale * 3.1) * 0.35;
                // Remap so _Dissolve==1 fully clips even where noise==1.
                float cutoff = _Dissolve * (1.0 + _EdgeWidth * 2.0);
                float burn = n - cutoff;
                clip(burn + _EdgeWidth);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half edge = 1.0 - saturate(burn / _EdgeWidth); // 0 inside → 1 at the burn front
                return half4(lerp(albedo.rgb, _EdgeColor.rgb, edge * edge), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
