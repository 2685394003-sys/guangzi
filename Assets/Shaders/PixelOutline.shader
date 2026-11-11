Shader "Guangzi/PixelOutline"
{
    Properties
    {
        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.04, 0.04, 0.06, 1)
        _DarkenLines ("Line Darkening", Range(0, 1)) = 0.6

        [Header(Edge Detection)]
        _DepthThreshold ("Depth Threshold", Range(0.001, 0.5)) = 0.03
        _NormalThreshold ("Normal Threshold", Range(0.01, 2)) = 0.35
        _Thickness ("Thickness (texels)", Range(1, 4)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "PixelOutline"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _OutlineColor;
            float _DarkenLines;
            float _DepthThreshold;
            float _NormalThreshold;
            float _Thickness;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 texel = _BlitTexture_TexelSize.xy * _Thickness;

                // ---------- 深度边 ----------
                // 关键：深度差要除以中心深度做归一化。
                // 不归一化的话，同样 0.02 的差值在近处是轮廓、在远处就是噪声，
                // 一张图里会同时出现"近处漏线、远处满屏线"。
                float depthC = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float depthL = LinearEyeDepth(SampleSceneDepth(uv - float2(texel.x, 0.0)), _ZBufferParams);
                float depthR = LinearEyeDepth(SampleSceneDepth(uv + float2(texel.x, 0.0)), _ZBufferParams);
                float depthU = LinearEyeDepth(SampleSceneDepth(uv + float2(0.0, texel.y)), _ZBufferParams);
                float depthD = LinearEyeDepth(SampleSceneDepth(uv - float2(0.0, texel.y)), _ZBufferParams);

                float depthDiff = max(
                    max(abs(depthC - depthL), abs(depthC - depthR)),
                    max(abs(depthC - depthU), abs(depthC - depthD)));
                depthDiff /= max(depthC, 0.0001);

                float depthEdge = step(_DepthThreshold, depthDiff);

                // ---------- 法线边 ----------
                // 取四邻域里"最不像"的那个：1 - min(dot)，这样任意一侧断开都能抓到。
                float3 nC = SampleSceneNormals(uv);
                float3 nL = SampleSceneNormals(uv - float2(texel.x, 0.0));
                float3 nR = SampleSceneNormals(uv + float2(texel.x, 0.0));
                float3 nU = SampleSceneNormals(uv + float2(0.0, texel.y));
                float3 nD = SampleSceneNormals(uv - float2(0.0, texel.y));

                float nDiff = 1.0 - min(
                    min(dot(nC, nL), dot(nC, nR)),
                    min(dot(nC, nU), dot(nC, nD)));

                float normalEdge = step(_NormalThreshold, nDiff);

                float edge = saturate(max(depthEdge, normalEdge));

                // ---------- 合成 ----------
                half4 col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

                // 亮暗线调制：亮处线浅一点、暗处线实一点，
                // 全用死黑会在暗部糊成一块，看不出这是线
                float luma = dot(col.rgb, float3(0.299, 0.587, 0.114));
                float3 lineColor = lerp(_OutlineColor.rgb, _OutlineColor.rgb * 1.8, luma * _DarkenLines);

                col.rgb = lerp(col.rgb, lineColor, edge * _OutlineColor.a);
                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
