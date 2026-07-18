// RoyalSiege stylized outline (18-Jul art pass): inverted-hull second material slot.
// Appended as an EXTRA material on the same renderer, so the mesh is drawn twice with
// one skinning pass — the cheapest reliable outline on mobile URP (no post FX, no copy
// renderers). Cull Front + world-space normal push = consistent width on every model
// regardless of its import scale.
Shader "RoyalSiege/Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.05, 0.04, 0.06, 1)
        _OutlineWidth ("Outline Width (world)", Range(0, 0.12)) = 0.05
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }

        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                positionWS += normalWS * _OutlineWidth; // world-space push: scale-independent width
                output.positionHCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
