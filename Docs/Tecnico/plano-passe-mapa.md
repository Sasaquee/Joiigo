# Plano técnico — Passe do mapa (D-073 a D-082)

Contrato dos agentes. Escopo vem de `Docs/Design/decisoes.md`: D-073 (a arena se abre para a cidade), D-074 (praça e avenidas), D-075 (cerca do dobro, raio ~50 m), D-076 e D-079 (prédios translúcidos, recorte com fantasma), D-077 (inimigos pelas bocas das ruas), D-080 (correm quando longe), D-081 (portões fecham o fim de cada rua), D-082 (carta onde morreu o último inimigo). D-078: quem escreve código usa Sonnet; só modelagem e arte usam Opus.

`G/` = `Assets/_Game/`. Ângulos contados a partir de +Z, no sentido horário (como `Polar()` no `ArenaBuilder`).

> **Ajustes feitos na integração (ver `relatorio-passe-mapa.md` §7):** o raio das praças menores passou de 7,5 para **9 m** (a torre do relógio de 7,2 m não deixava o agente passar) e o limite do jogador de 54,5 para **56 m**; os colliders de prédios, marcos, vedação e lampiões são marcados "Not Walkable" no bake; a checagem de visão dos drones usa a espessura do orbe. Onde este plano diz 7,5, vale 9.

## 0. Fatos que pesam

- O `Arena.unity` tem ~10,5 MB, dos quais ~7,8 MB são de 67 `ParticleSystem` (o `SmokeEmitter.Configure()` monta o sistema em edição e ele fica gravado na cena). Os `PrefabInstance` somam ~0,84 MB.
- Câmera (14 m, 50°, giro 30°, FOV 40°): fica a 9,0 m do foco no chão (deslocamento (-4,5; -7,8)) e a 11,7 m de altura. A borda de cima da tela toca o chão 11,3 m à frente do jogador; os cantos ~16 m (22 m em 21:9); atrás vê 4,7 m. Nunca vê nada a mais de ~25 m de si.
- `com.unity.ai.navigation` 2.0.14 já está no manifest e o `Game.Runtime.asmdef` já referencia `Unity.AI.Navigation`. Faltam a referência no `Game.Editor.asmdef` e no `Game.Tests.PlayMode.asmdef`.
- Render: Forward+, MSAA 4, SRP Batcher. O `PixelPost` lê profundidade e normais da pré-passada DepthNormals.
- O projeto não tem camadas próprias: a camada 8 e a rendering layer 8 estão livres.
- Raio dos inimigos até 0,9 m (constructo). Onda maior: 7 inimigos, mais 2 ou 3 da emboscada.
- Os portões atuais ficam no norte (-45°, 0°, +45°), longe da câmera, que olha para nor-nordeste; as avenidas correm na direção da vista.

## 1. Layout (números iguais em `MapLayoutSettings`, D-075)

`s` = distância do centro ao longo do eixo da avenida.

| Elemento | Valor | Coordenadas |
|---|---|---|
| Praça central (linha das fachadas) | raio 29 (anda até ~28,3) | centro (0,0) |
| Avenidas | -45°, 0°, +45°; 9 m entre fachadas; de s 26 a s 41 | boca na praça: (-20,5; 20,5), (0; 29), (20,5; 20,5) |
| Praças menores | centro em s 47, raio 7,5 | NO (-33,2; 33,2) · N (0; 47) · NE (33,2; 33,2) |
| Boca da rua | 6 m de largura, de s 53 a s 61 | — |
| `EnemySpawn` (raio 1,5) | s 57,5 | (-40,7; 40,7) · (0; 57,5) · (40,7; 40,7) |
| Portão-máquina, fechando a boca, virado para o centro (D-081) | s 60,6 | (-42,9; 42,9) · (0; 60,6) · (42,9; 42,9) |
| Limite | jogador ~54,5; boca até 60 | — |

- Cada avenida tem ~12,3 m de fachada de cada lado; entre duas praças menores vizinhas sobram 21 m (dois prédios de costas, até 9 m de fundo; a Fábrica, com 10,2 m, não cabe ali).
- **Ficam:** plataforma de spawn (raio 18, sul), alavanca, alcova (-115°), caldeiras, fornalhas, postes, núcleo.
- **Muro baixo (`BuildBoundary`) sai**, com os veios de muro do `AmbienceBuilder`, as pontes "muro→fachada" e os lampiões encostados no muro.
- `Piso` atual (caixa de 62,4 m com collider) vira só visual (disco de 29,5 m).
- Portões (`GateActivation`): o código não muda; só a posição (fechando o fim de cada boca). A peça tem 5,6 m e fecha a rua de 6 m. O `BuildGateVeins` acha os portões pelo nome.
- **Colisão em três camadas:**
  1. Collider de cada prédio: um `BoxCollider` (corpo × fundo × beiral, 0,3 m à frente da fachada) num grupo `Cidade/Colisores` à parte, fora do transform espelhado `(-1,1,1)`. Camada `Cenario`.
  2. Paredes de vedação invisíveis atrás da fileira de prédios, 0,5 m para dentro do corpo, só fechando vãos (altura 6 m).
  3. Chão com collider só onde se anda: disco da praça, retângulos das avenidas e das bocas, discos das praças menores. Fora disso não há chão (não sobram ilhas de NavMesh).
- **Kit de prédios:** `FillSector` passa a receber um centro (praça e praças menores, com peças estreitas nos círculos de raio 7,5). Novo `FillEdge(linha, lado)` para os lados retos das avenidas e bocas, frente virada para o eixo. Becos na borda: só bloqueio visual (Caixotes ou `GradeBeco`).
- **Altura:** `AllowedHeight` / `VisibleRadius` saem. Prédio cuja planta toque o "envelope da câmera" (área andável deslocada (-4,5; -7,8) e alargada 1 m) fica com no máximo 11,7 − 1,5 ≈ 10,2 m. O resto pode ser alto (D-076).
- **Fundo da cena:** chão visual até r 85; a câmera alcança ~77. Anéis 3 e 4 de prédios (r 57 e ~68) cobrem o setor de -70° a +70°, para não aparecer chão vazio além das bocas.

## 2. Navegação dos inimigos

NavMesh só para calcular o caminho, **sem `NavMeshAgent`** (o `CharacterController` continua movendo).

- **Bake:** novo `G/Editor/ArenaNavMeshBuilder.cs` coloca um `NavMeshSurface` em `Arena/Navegacao` (Children, PhysicsColliders, camadas Default e Cenario) e roda `BuildNavMesh()`. Grava `G/Arena/ArenaNavMesh.asset` (apagando o anterior). Funciona em batchmode.
- Agente Humanoid com raio 0,9 em `ProjectSettings/NavMeshAreas.asset`.
- **`EnemyController`, só no host (`ServerThink`):**
  - Com linha de visão até o alvo e distância até 6 m: segue reto, como hoje (`Steer`).
  - Senão: segue as quinas do `NavMeshPath`. Recalcula a cada 0,4 s, ou quando o alvo andou mais de 1,5 m, ou quando o inimigo fica preso (andou menos de 0,3 m em 1,5 s). O `Steer` fica fora no modo caminho.
  - Recuo do drone (`Away`): `NavMesh.Raycast` antes; se bater, fica parado.
  - Drone sem linha de visão (`Linecast` na camada Cenario): o cérebro recebe distância `max(d, attackRange + 0,01)` e continua perseguindo, para não atirar através de prédio. O `EnemyBrain` (Core) não muda.
  - Sem NavMesh carregada: volta ao comportamento atual.
  - **D-080:** a mais de `farSprintDistance` (18 m) de todos os jogadores, a velocidade é multiplicada por `farSprintMultiplier` (2).
- **Peças:** `G/Enemies/EnemyPathFollower.cs`; `G/Core/AI/PathCursor.cs` (avanço de quina e regra de recálculo, puro); `EnemyNavigationSettings` (SO); o `EnemyPrefabBuilder` liga o SO nos prefabs.
- **Teste PlayMode `EnemyNavigationTests.Inimigo_SaiDaBocaEChegaAoJogadorContornandoPredios`:** leva o jogador ao centro da praça NE; afirma que o `Linecast` do spawn NO até o jogador bate em `Cenario` e que o caminho está `PathComplete`; gera um autômato no marcador NO (`Time.timeScale = 3`); a cada quadro `Physics.CheckCapsule` contra `Cenario` tem de dar falso; chega a `attackRange + 0,5` dentro de `comprimento / velocidade × 1,5 + 5 s`. Mais `Drone_NaoAtiraAtravesDePredio` e `Emboscada_NasceEmChaoAndavelAlcancavel`.

## 3. Spawn pelas bocas das ruas e carta

- Os 3 `ArenaMarker.EnemySpawn` saem do anel de 18,5 m e vão para s 57,5 (`SpawnInimigo1..3`). O `ArenaLayoutTests` (3 marcadores) segue valendo; entram novas verificações: r ≥ 50, sobre a NavMesh, caminho completo até a plataforma.
- `WaveSpawner`: rodízio das bocas, pulando a que tiver jogador vivo a menos de `mouthPlayerClearance` (12 m); se todas estiverem ocupadas, usa a mais longe. Posição sorteada em volta do marcador + `NavMesh.SamplePosition` (2 m). Fila com `spawnStagger` de 0,5 s por boca. O `WaveDirector` recebe `vivos + fila` (o Core não muda).
- `CardDropService.ServerAmbush`: continua em volta do jogador (D-049). Sai o `arenaInnerRadius = 23` (campo serializado fora de SO); entram `SamplePosition` e `CalculatePath` a partir do jogador, até 6 tentativas (±30°, raio × 0,7).
- **D-082:** a carta do fim da onda surge na posição de morte do último inimigo da onda (ponto andável mais próximo se necessário). Muda `OnWaveCleared`/`WaveSpawner` para informar a posição; `dropPoint` fixo deixa de ser usado.
- O F4 do `DevCombatTools` usa `SamplePosition`.
- `Ondas_PrimeiraOndaNasceDepoisDaLargada` e `FullLoopTests` devem continuar passando.

## 4. Translucidez (D-076, D-079)

Recorte em tela no próprio shader do prédio, com fantasma.

- **Shader `Game/LitVazado`:** cópia do `Lit.shader` do URP 17.3, mesmas passadas e mesmo `CBUFFER` (compatível com o SRP Batcher). Cada fragmento chama `ClipVazado(positionCS)` em `UniversalForward`, `DepthOnly` e `DepthNormals`. `ShadowCaster` sem recorte (a sombra continua).
- **Regra do recorte:** corta quando valem juntas: o renderer tem a rendering layer 8 (`Vazavel`, lida por `GetMeshRenderingLayer()`); o pixel está dentro do círculo de algum alvo (`_VazadoAlvos[12]`: posição na tela, profundidade, raio); a profundidade do pixel é menor que a do alvo menos 0,8 m. Tudo a menos de 3 m da câmera é cortado (rede de segurança).
- O `PixelPalette` troca o shader dos materiais da paleta; só as peças da cidade levam o bit (personagem, inimigos e máquinas não são cortados). `CrystalPulse`, `WindowFlicker` e `GateActivation` continuam achando o material.
- **Fantasma:** passada extra (`LightMode = VazadoFantasma`, transparente, sem escrita de profundidade, stencil para desenhar uma camada só) por um `RenderObjectsRendererFeature` em AfterRenderingTransparents instalado pelo `PixelRenderSetup`. Pinta a silhueta a ~25% só dentro do buraco.
- Com o PixelPost: a pré-passada de profundidade e normais já sai recortada (contorno em volta do jogador e na borda do buraco, sem linhas da silhueta do prédio atravessando o personagem). O fantasma só entra na cor.
- **CPU (`SeeThroughDriver`, na câmera, depois do `PixelCamera`):** a cada 0,25 s junta jogadores e inimigos vivos; a cada quadro faz `Linecast` da câmera a 3 alturas de cada alvo (0,4 / 1,2 / 1,9 m) na camada Cenario; só os alvos tapados entram, com o raio crescendo em 0,12 s e diminuindo em 0,3 s; publica por `Shader.SetGlobalVectorArray`. Cada cliente usa a própria câmera: nada novo na rede.
- Testes: EditMode `SeeThroughMathTests` (raio na tela inversamente proporcional à profundidade, fade, regra "dentro do buraco"); PlayMode `SeeThroughTests` (ponto andável tapado, jogador marcado como tapado, contagem 0 depois do fade); teste de material (toda peça da cidade tem o bit e `FindPass("DepthNormals") ≥ 0`); captura `[Explicit]`.

## 5. Câmera

- `CameraSettings` (14 m, 50°, 30°, 40°) não muda. O limite de altura passa a ser o envelope da seção 1; a oclusão passa a ser resolvida pelo vazado. Sem trava da câmera: o chão até r 85 cobre o alcance de ~77.
- Core `CameraFootprint` (EditMode) calcula os cantos da vista para qualquer foco, em 16:9 e 21:9. PlayMode `CameraCoverageTests`: para focos numa grade de 2 m sobre a área andável, todos os cantos ficam dentro de r ≤ 80; cada canto fora da área andável tem um collider `Cenario` a até 8 m.
- `SceneSnapshot` ganha vistas nas avenidas e nas praças menores.

## 6. Desempenho

1. `SmokeEmitter` monta o `ParticleSystem` só em tempo de execução (o `Configure` guarda tipo e intensidade; `cullingMode = Pause`). A cena cai de ~10,5 para ~3 MB. Teste EditMode: `Arena.unity` < 6 MB.
2. `MaxPointLights` de 22 para ~34 (espaçamento mínimo 7 m, sem sombra, prioridade por "perto da área andável e do lado norte").
3. `MaxSmokeEmitters` de 56 para ~90, com o culling acima.
4. Static batching e SRP Batcher continuam; colliders são caixas estáticas.
5. `ArenaBuilder.ConfigureModelImports` reimporta `Models/City`, que o `CityBuilder` reimporta de novo: excluir a subpasta.

## 7. Cristal e ambientação (D-069)

- Torre do relógio no centro da praça N; pilões arcanos no centro das praças NO e NE (base da torre 7,2 m, sobra um anel andável de ~3,9 m). O `CrystalPulse` de torre e as luzes já existem.
- Trilhos de cristal: o `TrilhoCobre` do núcleo continua por cada avenida até o portão, com veio `CrystalPulse` e deslocamento igual à distância ao núcleo; apagado até a largada (D-013, D-081); na alavanca a crista corre da praça aos portões. Fica no chão, então aparece em quase toda vista. Vai no `AmbienceBuilder`.
- Lampiões de cristal ao longo das avenidas (~8 m), com collider cápsula; pontes de canos sobre as avenidas (≥ 6 m de altura) com veio e o bit de vazado.
- Poeira mágica: o anel da "rua do anel" sai; entram emissores por avenida (caixa 9 × 2 × 10,5 m) e por praça menor (círculo de 7,5 m), nunca sobre o disco de combate (r < 26). Violeta a 3% (D-070).
- `CrystalAmbienceTests`: reescrever os 4 testes que dependem do muro; teste novo: em cada foco (spawn, centro, meio de cada avenida, cada praça menor), pelo menos 2 renderers de `CrystalPulse` dentro do frustum da câmera real.

## 8. Pacotes de trabalho

Modelo (D-078): quem escreve código = Sonnet; só modelagem de arte (Blender) = Opus.

| Pacote | Quem | Arquivos (só dele) | Depende de |
|---|---|---|---|
| P0 Contrato | orquestrador + programador | `ProjectSettings/TagManager.asset` (camada 8 `Cenario`, rendering layer 8 `Vazavel`), `NavMeshAreas.asset` (raio 0,9), `Game.Editor.asmdef` e `Game.Tests.PlayMode.asmdef` (+`Unity.AI.Navigation`) | — |
| P1 Layout | programador | `G/Core/Map/MapLayout.cs`, `G/Arena/MapLayoutSettings.cs`, `G/Tests/EditMode/MapLayoutTests.cs` | P0 |
| P2 Estrutura | programador | `G/Editor/ArenaBuilder.cs`, novos `G/Editor/WalkableBuilder.cs` e `ArenaNavMeshBuilder.cs`, `ArenaLayoutTests.cs` | P1 |
| P3 Cidade | programador | `G/Editor/CityBuilder.cs` | P1 |
| P4 Kit (opcional) | **modelagem (Opus)** | `Tools/Blender/build_city.py` (GradeBeco, esquina com duas fachadas) | — |
| P5 Navegação | programador | `EnemyController.cs`, `EnemyPathFollower.cs`, `EnemyNavigationSettings.cs`, `Core/AI/PathCursor.cs`, `EnemyPrefabBuilder.cs`, testes | P0 |
| P6 Spawn e carta | programador | `WaveSpawner.cs`, `WaveSettings.cs`, `CardDropService.cs`, `DiceSettings.cs`, `DevCombatTools.cs`, testes | P1, P2 |
| P7 Shader | programador | `Art/Shaders/LitVazado.shader` + `Vazado.hlsl`, `PixelPalette.cs`, `PixelRenderSetup.cs` | P0 |
| P8 Driver | programador | `Camera/SeeThroughDriver.cs`, `SeeThroughSettings.cs`, `Core/View/SeeThroughMath.cs`, testes | P0 |
| P9 Ambiente | programador | `AmbienceBuilder.cs`, `CrystalAmbienceSettings.cs`, `CrystalAmbienceTests.cs` | P1 |
| P10 Desempenho | programador | `Arena/Life/SmokeEmitter.cs`, teste de tamanho da cena | — |
| P11 Docs e capturas | documentador | `SceneSnapshot.cs`, capturas, `arquitetura.md`, relatório | todos |

Ordem: P0 e P1; depois P2, P3, P5, P7, P8, P9, P10 em paralelo; P6 depois de P2. Integração pelo orquestrador: uma linha no `ArenaBuilder.SetupCamera` para o driver e uma para o bake.

Unity, sempre em série, editor fechado, sem `-quit` para testes: U1 compilar + EditMode depois do P1; U2 compilar + `ArenaBuilder.Build` + EditMode + PlayMode; U3 capturas; U4 rodada final, relatório e um commit.

## 9. Números novos (todos em ScriptableObject)

- `MapLayoutSettings`: valores da seção 1; altura da vedação 6 m; margem do envelope 1,5 m.
- `EnemyNavigationSettings`: recálculo 0,4 s / 1,5 m, alcance de quina 0,5 m, reto até 6 m, amostra 2 m, preso 1,5 s / 0,3 m, `farSprintDistance` 18 m, `farSprintMultiplier` 2 (D-080).
- `SeeThroughSettings`: 12 alvos, raio do jogador 1,8 m, raio do inimigo 1,4 m, margem de profundidade 0,8, recorte perto 3 m, fade 0,12 / 0,3 s, fantasma 0,25, alturas de checagem.
- `WaveSettings`: `mouthPlayerClearance` 12, `spawnStagger` 0,5.
- `DiceSettings`: `ambushNavSampleRadius` 2,5, `ambushTries` 6.
- `CrystalAmbienceSettings`: poeira por avenida e por praça.

Testes novos: ~+20 EditMode (`MapLayout`, `PathCursor`, `SeeThroughMath`, `CameraFootprint`, tamanho da cena) e ~+10 PlayMode (navegação, spawn pelas bocas, emboscada, vazado, cobertura da câmera, cristal visível, materiais).

## 10. Riscos

- A cópia do `Lit.shader` precisa ser refeita quando o URP for atualizado.
- O MSAA deixa serrilhada a borda do recorte.
- Inimigos levam ~20 a 40 s da boca até a praça (por isso D-080).
- Quatro testes de cristal caem junto com o muro.
- A primeira compilação de shader e o bake deixam o batchmode mais lento.
- `Arena.unity` e `ArenaNavMesh.asset` mexem no git a cada reconstrução.
