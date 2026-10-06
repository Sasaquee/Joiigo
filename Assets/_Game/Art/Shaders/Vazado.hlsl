// Recorte "vazado" dos prédios da cidade (D-076, D-079; Docs/Tecnico/plano-passe-mapa.md §4).
// Incluído pelo Game/LitVazado depois do LitInput.hlsl (precisa do Core.hlsl do URP).
//
// Contrato com o SeeThroughDriver (todas globais, passadas por Shader.SetGlobal*):
//   _VazadoContagem  quantos alvos valem (0..12). Com 0 e _VazadoPerto = 0 o shader é igual ao Lit do URP.
//   _VazadoAlvos[i]  x,y = posição do alvo em UV de tela (0..1, y para cima, como o viewport da câmera)
//                    z   = raio do buraco como fração da ALTURA da tela
//                    w   = profundidade do alvo em metros (eye depth da câmera)
//   _VazadoMargem    metros: só corta o pixel se a profundidade dele for menor que w - margem
//   _VazadoPerto     metros: tudo mais perto que isto é cortado (rede de segurança contra a câmera dentro do prédio)
//   _VazadoFantasma  0..1: opacidade da silhueta fantasma dentro do buraco
// Só renderers com a rendering layer 8 "Vazavel" (MapLayers.VazavelRenderingLayerMask) são cortados.
#ifndef GAME_VAZADO_INCLUDED
#define GAME_VAZADO_INCLUDED

#define VAZADO_MAX_ALVOS 12
// Igual a Game.Arena.MapLayers.VazavelRenderingLayerMask (1 << 8).
#define VAZADO_LAYER_MASK (1u << 8)

// Globais fora de CBUFFER de propósito: não são propriedades do material e o SRP Batcher não se importa com elas.
float _VazadoContagem;
float4 _VazadoAlvos[VAZADO_MAX_ALVOS];
float _VazadoMargem;
float _VazadoPerto;
float _VazadoFantasma;

bool VazadoRendererVazavel()
{
    return (GetMeshRenderingLayer() & VAZADO_LAYER_MASK) != 0u;
}

// Profundidade linear do pixel (metros). positionCS.z do fragmento é o valor cru do buffer de profundidade.
float VazadoEyeDepth(float4 positionCS)
{
    return LinearEyeDepth(positionCS.z, _ZBufferParams);
}

// UV de tela com y para cima (igual ao viewport da câmera). Em D3D/Vulkan/Metal a coordenada y do fragmento começa
// no topo, e o URP renderiza em textura com a projeção invertida (_ProjectionParams.x < 0): nesse caso y já está certo.
float2 VazadoScreenUV(float4 positionCS)
{
    float2 uv = positionCS.xy / GetScaledScreenParams().xy;
#if UNITY_UV_STARTS_AT_TOP
    if (_ProjectionParams.x > 0.0)
        uv.y = 1.0 - uv.y;
#endif
    return uv;
}

// Pixel dentro do círculo de algum alvo e mais perto da câmera que ele (menos a margem).
bool VazadoNoBuraco(float4 positionCS)
{
    int count = (int)(_VazadoContagem + 0.5);
    if (count <= 0)
        return false;

    float2 size = GetScaledScreenParams().xy;
    float2 uv = VazadoScreenUV(positionCS);
    float depth = VazadoEyeDepth(positionCS);
    float aspect = size.x / size.y; // o raio é fração da altura: o x entra multiplicado para o buraco ser círculo

    [loop]
    for (int i = 0; i < VAZADO_MAX_ALVOS; i++)
    {
        if (i >= count)
            break;
        float4 alvo = _VazadoAlvos[i];
        float2 d = (uv - alvo.xy) * float2(aspect, 1.0);
        if (dot(d, d) < alvo.z * alvo.z && depth < alvo.w - _VazadoMargem)
            return true;
    }
    return false;
}

// Descarta o fragmento se ele cai no buraco (ou perto demais da câmera). Só vale em renderers vazáveis.
void ClipVazado(float4 positionCS)
{
    if (_VazadoContagem < 0.5 && _VazadoPerto <= 0.0)
        return; // nada a fazer: igual ao Lit

    if (!VazadoRendererVazavel())
        return;

    bool corta = VazadoEyeDepth(positionCS) < _VazadoPerto || VazadoNoBuraco(positionCS);
    clip(corta ? -1.0 : 1.0);
}

#endif
