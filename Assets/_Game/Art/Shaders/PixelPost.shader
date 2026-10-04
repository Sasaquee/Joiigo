// Pós-processo do 3D pixelado (D-039): roda na resolução baixa, antes da ampliação sem filtro.
// 1) Contorno escuro onde a profundidade ou a normal mudam de repente (silhuetas e quinas).
// 2) Cores em faixas (posterização) com pontilhado ordenado leve, para cara de paleta limitada.
Shader "Hidden/Game/PixelPost"
{
    Properties
    {
        _OutlineColor ("Cor do contorno", Color) = (0.05, 0.04, 0.05, 1)
        _OutlineStrength ("Força do contorno", Range(0, 1)) = 0.85
        _DepthThreshold ("Limiar de profundidade (m)", Float) = 0.35
        _NormalThreshold ("Limiar de normal", Range(0, 2)) = 0.45
        _InnerEdgeStrength ("Força das quinas internas", Range(0, 1)) = 0.35
        _Levels ("Faixas de luz", Float) = 8
        _Dither ("Pontilhado", Range(0, 1)) = 0.3
        _Saturation ("Saturação", Range(0, 2)) = 1.12
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "PixelPost"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            half4 _OutlineColor;
            half _OutlineStrength;
            float _DepthThreshold;
            half _NormalThreshold;
            half _InnerEdgeStrength;
            half _Levels;
            half _Dither;
            half _Saturation;

            static const float Bayer4[16] =
            {
                0.0 / 16, 8.0 / 16, 2.0 / 16, 10.0 / 16,
                12.0 / 16, 4.0 / 16, 14.0 / 16, 6.0 / 16,
                3.0 / 16, 11.0 / 16, 1.0 / 16, 9.0 / 16,
                15.0 / 16, 7.0 / 16, 13.0 / 16, 5.0 / 16
            };

            float EyeDepth(float2 uv)
            {
                return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 texel = 1.0 / _ScreenParams.xy;

                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).rgb;

                // Contorno externo: algum vizinho está bem mais perto (este pixel é o fundo atrás de uma borda).
                float d = EyeDepth(uv);
                float dl = EyeDepth(uv - float2(texel.x, 0));
                float dr = EyeDepth(uv + float2(texel.x, 0));
                float du = EyeDepth(uv + float2(0, texel.y));
                float dd = EyeDepth(uv - float2(0, texel.y));
                float threshold = _DepthThreshold * (1.0 + d * 0.04); // mais tolerante longe da câmera
                float nearer = max(max(d - dl, d - dr), max(d - du, d - dd));
                half outline = nearer > threshold ? 1.0 : 0.0;

                // Quinas internas: a normal muda (só se o vizinho não estiver atrás, para a linha não dobrar).
                half3 n = SampleSceneNormals(uv);
                half3 nl = SampleSceneNormals(uv - float2(texel.x, 0));
                half3 nu = SampleSceneNormals(uv + float2(0, texel.y));
                half normalEdge = (distance(n, nl) + distance(n, nu)) > _NormalThreshold ? 1.0 : 0.0;
                half inner = normalEdge * (nearer > -threshold ? 1.0 : 0.0) * _InnerEdgeStrength;

                // Saturação levemente mais alta: pixel art lê melhor com cor firme.
                half luma = dot(color, half3(0.299, 0.587, 0.114));
                color = lerp(luma.xxx, color, _Saturation);

                // Luz em faixas: quantiza só a luminosidade (em espaço gama), mantendo o matiz.
                // Arredondar canal por canal gerava pontos coloridos nos tons escuros.
                uint2 p = (uint2)(uv * _ScreenParams.xy) % 4;
                half bayer = (Bayer4[p.y * 4 + p.x] - 0.5) * _Dither;
                half levels = max(_Levels, 2.0);
                half lumaLin = max(dot(color, half3(0.2126, 0.7152, 0.0722)), 1e-4);
                half lumaGamma = pow(lumaLin, 1.0 / 2.2);
                half bandedGamma = floor(lumaGamma * levels + 0.5 + bayer) / levels;
                bandedGamma = max(bandedGamma, lumaGamma * 0.5); // nunca afunda um tom escuro até o preto
                half bandedLin = pow(bandedGamma, 2.2);
                color *= bandedLin / lumaLin;

                // Quina interna clareia de leve (realce de aresta); contorno externo escurece.
                color = lerp(color, color * 1.25 + 0.03, inner);
                color = lerp(color, _OutlineColor.rgb, outline * _OutlineStrength);
                return half4(max(color, 0), 1);
            }
            ENDHLSL
        }
    }
}
