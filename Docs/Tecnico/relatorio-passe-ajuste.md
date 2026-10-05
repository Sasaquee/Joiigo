# Relatório — Passe de ajuste pós-playtest (D-067 a D-069)

Origem: sessão `Docs/Playtest/sessao-2026-10-05-1.md`. Três pontos do playtest viraram perguntas (P-013 a P-015) e foram respondidos em D-067, D-068 e D-069.

## 1. O que foi construído

- **D-067 · Pulso de energia cheia.** Quando a energia enche, a aura dá uma onda de luz que sai da borda do círculo de latão e um "ding" de cristal (só no seu personagem). As faíscas de D-062 não mudaram.
  - Dispara uma vez por enchida. Só rearma depois que a energia mostrada cai abaixo de 95%.
  - Não dispara ao nascer, caído nem ao levantar com a energia cheia.
  - Todos veem a onda (no coop mostra quem está carregado); o som é só do dono.
- **D-068 · O 1 do D20 mais teatral.** A regra do 1 **não mudou** (mesma carta amaldiçoada, mesmos sorteios, mesma emboscada de 2 a 3 inimigos no mesmo momento). Muda a apresentação, só no 1:
  - dado e luzes do palco em vermelho-sangue;
  - véu escuro com bordas vermelhas sobre o jogo, **abaixo da HUD** (a barra de cartas e a vida continuam legíveis);
  - baque grave no assentar do dado e ronco quando os inimigos surgem (sons sintetizados, com harmônicas para soar em fone e notebook);
  - cada inimigo da emboscada surge com rachadura vermelha no chão, coluna de luz, poeira e clarão (RPC `ShowAmbushRpc`, mostrado em todos os clientes).
- **D-069 · Reforçar o cristal no mundo.** Sem trocar a cidade nem a paleta de D-039 a D-045 e sem luzes novas (só emissão):
  - veios de cristal com uma crista de luz correndo, nos canos do muro, nas caldeiras, nos portões (apagados até a largada, D-013) e nas pontes de canos;
  - `CrystalPulse` faz pulsar o cristal das fornalhas, dos postes e das torres;
  - poeira mágica na rua atrás do muro, só no lado longe da câmera (arco de 210°), para nunca passar na frente da praça.

## 2. Arquivos

Novos:
- Core: `Core/Aura/AuraPulseTrigger.cs`, `Core/Aura/AuraSmoother.cs`, `Core/Ambience/CrystalWave.cs`.
- Jogo: `Dice/AmbushFx.cs`, `Dice/CriticalSounds.cs`, `Arena/CrystalPulse.cs`, `Arena/CrystalAmbienceSettings.cs`.
- Dados e arte gerados pelo construtor: `Data/Ambience/CrystalAmbienceSettings.asset`, `Art/Ambience/ParticulaPoeiraMagica.mat`.
- Testes: `Tests/EditMode/CrystalWaveTests.cs`, `Tests/PlayMode/CrystalAmbienceTests.cs`.

Alterados:
- Aura: `Aura/PlayerAura.cs`, `AuraVisual.cs`, `AuraAudio.cs`, `AuraSettings.cs`, `Core/Aura/AuraMapper.cs` (`FullEnergy` ganhou nome).
- Dado: `Dice/CardDropService.cs`, `DiceSettings.cs`, `UI/DiceRollUi.cs`, `Editor/CardDropBuilder.cs`.
- Mundo: `Editor/AmbienceBuilder.cs`, `Editor/CityBuilder.cs`.
- Testes: `AuraCoreTests`, `AuraTests`, `CardDropTests`, `DiceRollUiTests`.
- `Arena.unity` e `ArenaVolume.asset` foram regenerados pelo construtor.

## 3. Como testar

1. `Game → Setup → Construir Arena` → Play.
2. **Pulso:** use uma skill para gastar energia e espere encher; veja a onda e ouça o "ding". F7 troca a paleta.
3. **O 1:** F1 abre o overlay; **F2** troca o próximo dado até o 1; **Shift+F2** solta uma carta. Pegue a carta e veja o dado vermelho, o véu, o baque e as rachaduras dos inimigos.
4. **Cristal:** olhe os canos do muro, as caldeiras e os portões (puxe a alavanca para acender os portões).
5. Capturas desta fase: `Docs/Capturas/passe-ajuste/` (vistas da arena pela ferramenta de captura).
6. Testes: EditMode **186/186**, PlayMode **85 passados, 0 falhas, 2 pulados** (as 2 capturas explícitas).

## 4. Pilares

- Pilar 2 (a sorte como emoção): o 1 passa a ser memorável.
- Pilar 4 (tecnologia e magia): cristal vivo nas máquinas, na mesma peça do latão.
- "Mostrar pelo mundo": a energia cheia passa a ser percebida sem número na tela.

## 5. Números novos

**`AuraSettings` (D-067)**

| Campo | Valor |
|---|---|
| `fullPulseDuration` | 0,65 s |
| `fullPulseEndRadius` | 2,4 m |
| `fullPulseBand` | 0,14 do raio |
| `fullPulseGlow` | 1,8 |
| `fullPulseEchoGlow` | 0,45 |
| `fullPulseOutlineAlpha` | 0,45 |
| `fullPulseRuneBoost` | 1,5 |
| `fullPulseRuneTime` | 0,4 s |
| `fullChimeVolume` | 0,3 |
| `fullPulseRearmBelow` | 0,95 |

**`DiceSettings` (D-068)**

| Campo | Valor |
|---|---|
| `criticalTintStart` | 0,45 da rolagem |
| `veilOpacity` | 0,55 |
| `vignetteOpacity` | 0,85 |
| `veilHold` | 1,5 s |
| `veilFadeOut` | 0,9 s |
| `veilAmbushMargin` | 0,5 s |
| `criticalPulseTime` | 0,5 s |
| `criticalThudVolume` | 0,9 |
| `ambushRumbleVolume` | 0,8 |
| `ambushFxRadius` | 1,4 m |
| `ambushFxDuration` | 1,8 s |
| `ambushFxPillarHeight` | 3 m |
| `ambushFxPillarTime` | 0,55 s |
| `ambushFxFlashIntensity` | 8 |
| `ambushFxFlashTime` | 0,45 s |

**`CrystalAmbienceSettings` (D-069)**

| Grupo | Padrão |
|---|---|
| Veios | brilho 0,45 a 1,1 · 0,22 pulsos/s · 14 m entre cristas (crista a ~3 m/s) |
| Cristais de máquina | brilho 0,75 a 1,15 · 0,3 pulsos/s |
| Torres | brilho 0,6 a 1,3 · 0,18 pulsos/s · luz ±30% |
| Poeira | 120 partículas · 12/s · anel 27,8 a 31 m · altura 2 m ± 0,9 · vida 8 a 12 s · arco 210° · 12% violeta · 10% dourado |

Os tamanhos e as cores internas dos efeitos ficaram como constantes comentadas no código, como o `AuraVisual` já fazia.

## 6. Perguntas e respostas

D-067 (pulso ao encher), D-068 (o 1 mais teatral) e D-069 (reforçar o cristal), resolvem P-013, P-014 e P-015. Em `Docs/Design/decisoes.md`.

## 7. Pendentes (perguntas para o dono)

- **Violeta na poeira:** 12% da poeira é violeta, mas na aura o violeta é o sinal de maldição (D-063). Manter ou pôr `dustVioletShare` em 0?
- **Aviso do 1 antes do dado parar:** o véu e o vermelho começam aos 45% da rolagem. Manter o aviso crescente ou tudo de uma vez quando o dado para (`criticalTintStart` = 1)?
- **Tranco do dado ao bater no 1:** quer também um tranco visual? Não estava no pedido.
- **Torres e pontes quase invisíveis:** em simulação com a câmera real, os cristais das torres da cidade e as pontes de canos aparecem em 0% das posições do jogador. O que se vê do D-069 são os veios do muro, das caldeiras e dos portões, mais as fornalhas e os postes. "Cristais das torres pulsando" deve valer também para os postes arcanos da praça (luz pulsando perto do combate)?
- Perguntas que esperam o coop voltar: P-003, P-004 e P-009.
- A pergunta 1 e a final do playtest (o que a aura mostrava) não foram respondidas; a carta que "mudou o jeito de jogar" não foi dita.

## 8. Problemas conhecidos

- **Custo do pulso dos veios:** cerca de 150 renderers com `MaterialPropertyBlock` por quadro saem do SRP Batcher. Não foi medido no Profiler.
- **Atraso do pulso de energia:** vem da suavização da aura (de 0,3 a 0,9 s depois de a energia encher).
- **Gastar menos de 5% da energia e voltar a encher não pulsa** (histerese). Hoje a carta mais barata custa 20%.
- **`AmbienceBuilder`:** a geometria do muro, da caldeira e do portão é copiada do `ArenaBuilder` e do `build_props.py`. Os testes de geometria avisam se alguém mudar lá.
- **Nada foi visto em Play por mim:** as capturas são de vistas paradas; a onda, o 1 e a poeira em movimento dependem do seu olhar.
- `.vsconfig` (arquivo criado pela Unity) fica fora do commit.
