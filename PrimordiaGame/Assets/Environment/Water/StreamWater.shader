Shader "Custom/StreamWater"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.35, 0.75, 0.78, 0.65)
        _FlowTex ("Flow Texture (UV2)", 2D) = "white" {}
        _FlowSpeed ("Flow Speed (X,Y)", Vector) = (0.06, 0.03, 0, 0)
        _FlowStrength ("Flow Tint Strength", Range(0,1)) = 0.35
        _EdgeFoamPower ("Edge Highlight Power", Range(0.1, 8)) = 2.5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            TEXTURE2D(_FlowTex);
            SAMPLER(sampler_FlowTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _FlowSpeed;
                float _FlowStrength;
                float _EdgeFoamPower;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv0 = IN.uv0;
                OUT.uv1 = IN.uv1;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 flowUV = IN.uv1 + _FlowSpeed.xy * _Time.y;
                half flow = SAMPLE_TEXTURE2D(_FlowTex, sampler_FlowTex, flowUV).r;

                // second slower/offset sample for a bit of cheap depth-of-flow variation
                float2 flowUV2 = IN.uv1 * 1.7 - _FlowSpeed.xy * 0.6 * _Time.y + 0.33;
                half flow2 = SAMPLE_TEXTURE2D(_FlowTex, sampler_FlowTex, flowUV2).r;

                half flowMix = saturate(flow * 0.6 + flow2 * 0.4);

                half3 col = _BaseColor.rgb + (flowMix - 0.5) * _FlowStrength;

                // soft brightening near stream edges (uv0.x assumed 0..1 across width) to fake a thin foam/edge highlight
                half edge = pow(1.0 - saturate(abs(IN.uv0.x - 0.5) * 2.0), _EdgeFoamPower);
                col += edge * 0.15;

                half alpha = _BaseColor.a;
                return half4(saturate(col), alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
