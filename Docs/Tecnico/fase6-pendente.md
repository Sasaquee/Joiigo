# Fase 6 — carta no chão e D20: o que falta

> Escrito em 2026-10-04 21:05 a pedido do dono, para trocar de conta no meio da fase. Este commit é de passagem de bastão; o commit que fecha a fase apaga este arquivo.

## Como continuar (qualquer conta ou harness)

1. Abra o Claude Code (ou outro harness, em acesso total) nesta pasta.
2. Peça: **"leia Docs/Tecnico/fase6-pendente.md e continue a Fase 6 no modo híbrido"**.
3. O agente lê `AGENTS.md` (regras), este arquivo (estado) e `Docs/Design/decisoes.md` D-046 a D-052 (o que o dono decidiu).

## Decisões do dono desta fase

D-046 carta flutuando de pé · D-047 dado grande na tela · D-048 tabela de três eixos · D-049 emboscada no 1 · D-050 carta no fim de cada onda · D-051 repetida melhora a qualidade · D-052 repetida de perfeita vira cópia · P-009 (efeito do 20 no coop) fica para quando o coop voltar.

## Estado

| Item | Situação | Onde |
|---|---|---|
| Regras puras: D20, tabela, sorteio do tema e da carta, qualidade | ✅ | `Core/Dice/*.cs`, `Core/Cards/CardDraw.cs`, `Core/Cards/CardQuality.cs` |
| Números provisórios (tabela, multiplicadores de qualidade, peso do caminho, emboscada, tempos) | ✅ em dados | `Data/Dice/DiceSettings.asset`, `Data/Cards/CardsSettings.asset` |
| Rede: carta no chão no fim da onda, rolagem no host, todos veem, entrega depois da rolagem, emboscada | ✅ | `Dice/CardDropService.cs`, `Dice/FloorCard.cs`, `Enemies/WaveSpawner.cs` (evento `WaveCleared`) |
| Qualidade por carta (rede), entrega nova/melhoria/cópia, potência nos 9 efeitos e nos modificadores | ✅ | `Cards/PlayerCards.cs`, `Cards/CardEffect.cs` (`ICardUser.Potency`), `Cards/Effects/*.cs` |
| Visual: D20 3D (latão, números em cristal, opostos somam 21) | ✅ modelo | `Tools/Blender/build_d20.py` → `Art/Models/Dice/D20.fbx` |
| Visual: verso da carta (engrenagem dourada com cristal) | ✅ arte | `Tools/Cards/card_pixel.py verso` → `Art/Cards/verso.png` |
| Visual: tela do dado rolando e parando no número | ✅ código | `UI/DiceRollUi.cs` |
| Visual: moldura de qualidade (gasta = verdete, perfeita = ouro) e revelação na melhoria | ✅ código | `UI/CardView.cs`, `UI/CardRevealFx.cs` |
| Construtor | ✅ | `Editor/CardDropBuilder.cs`, chamado pelo `ArenaBuilder` |
| Debug: F2 força o próximo dado (normal → 1 → 20 → 10), Shift+F2 põe carta no chão | ✅ | `Debug/DevDiceTools.cs` |
| Testes | ✅ EditMode 138/138, PlayMode 55/55 | `Tests/EditMode/DiceCoreTests.cs` (21), `Tests/PlayMode/CardDropTests.cs` (4) |
| **Conferir o visual em Play e ajustar** | ❌ falta | ver abaixo |
| **Relatório da fase (§7) e capturas** | ❌ falta | `Docs/Tecnico/relatorio-fase6.md`, `Docs/Capturas/fase6/` |

## O que falta

1. **Ver em Play e ajustar (arte = Claude Opus, D-044).** Nada disso foi visto rodando ainda:
   - Unity → Game → Setup → Construir Arena → Play.
   - **Shift+F2** põe uma carta à frente do jogador; **F2** escolhe o próximo dado (1, 20, 10 ou normal); **F** perto da carta pega.
   - Conferir: a carta flutuando e girando com o brilho ciano subindo (D-046); o D20 rolando grande no centro e **parando com o número certo de frente e em pé** (D-047 — a orientação vem dos Empties `Centro_N`/`Topo_N` do FBX; se parar torto, o erro está em `DiceRollUi.BuildFaceTable`); a revelação da carta depois do dado; a moldura de qualidade na tiragem (Tab) e na barra; no 1, a emboscada em volta.
   - Pontos de atenção: tamanho/luz do dado na tela (`DiceRollUi`, luzes do palco), se o verso lê bem a 14 m (`CardDropBuilder.CardWidth/Height`), se o brilho ciano não estoura.
2. **Perguntar ao dono** (uma rodada, com recomendação):
   - Qualidade das comuns extras do 19: está **boa** (`DiceSettings`, faixa 19, `extraQuality`) — o dono não definiu.
   - Aprovação dos números provisórios: qualidade gasta ×0,85 / boa ×1,0 / perfeita ×1,15; peso do caminho 2 (top 2 tags); emboscada 2–3 inimigos a 5 m; rolagem 2,2 s + 0,6 s até a carta.
3. **Relatório §7** (pode delegar o rascunho ao harness, papel `documentador`, e revisar) com capturas em `Docs/Capturas/fase6/`.
4. **Commit único que fecha a fase** (apaga este arquivo), push, e parar esperando o OK do dono.
5. **Próxima etapa: passe de resolução** (D-053), não a Fase 7. O dono acha o jogo pixelado demais. Comece pela pergunta P-012 (`decisoes.md`), mostrando capturas lado a lado: 640x360 (hoje), 960x540, 1280x720 e a resolução da tela. Pontos que a etapa toca:
   - `Camera/PixelCamera.cs` (`targetHeight` = 360; levar o número para um ScriptableObject) e `Editor/PixelRenderSetup.cs` (`renderScale` da prévia no editor);
   - `Art/Shaders/PixelPost.shader` (espessura do contorno e faixas de luz, que dependem da resolução);
   - cartas (`Tools/Cards/card_pixel.py`), verso e D20 (`UI/DiceRollUi.cs`, textura do palco em filtro Point);
   - texturas e modelos do Blender, se ficarem pobres em mais resolução (arte = Claude Opus, D-044);
   - `Docs/Design/arte-pixel.md` atualizado com o que o dono decidir.

## Problemas conhecidos

- No coop (guardado), a revelação de uma carta nova com qualidade diferente de "boa" pode tocar duas vezes nos clientes (inventário e qualidade chegam em ordens diferentes); há uma trava de 2,5 s em `CardRevealFx`, testar quando o coop voltar.
- O modo de jogo solo já entra com a partida iniciada (D-018), então as ondas e as cartas no chão começam sozinhas.
- O log de batchmode mostra um `NullReferenceException` do pacote Multiplayer Play Mode — não é do jogo.

## Como o trabalho foi dividido nesta sessão (modo híbrido)

| Quem | Modelo | Fez |
|---|---|---|
| Harness — programador | GLM-5.3 (NVIDIA) | Implementou as regras puras a partir dos contratos (42 min, compilou e conferiu fora da Unity; código bom) |
| Harness — testador | Nemotron 3 Super (NVIDIA) | 21 testes EditMode (8 min); deixou 3 linhas-placeholder que não compilavam — corrigidas na revisão |
| Claude Opus | — | Contratos, rede, entrega, potência, construtor, debug, testes PlayMode, toda a arte (D20, verso, tela do dado, molduras), revisão e integração |
