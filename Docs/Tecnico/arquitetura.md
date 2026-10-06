# Arquitetura do protótipo

## Pastas (`Assets/_Game/`)

| Pasta | Conteúdo | Assembly |
|---|---|---|
| `Core/` | Regras em C# puro: D20, sorteio de tema, dano, loadout, mapeamento da aura | `Game.Core` (`noEngineReferences`) |
| `Net/`, `Player/`, `Camera/`, `Combat/`, `Enemies/`, `Cards/`, `Dice/`, `Aura/`, `Arena/`, `UI/` | MonoBehaviours, Netcode e apresentação | `Game.Runtime` |
| `Debug/` | Overlay e ferramentas de teste | `Game.Debug` (só com `UNITY_EDITOR \|\| DEVELOPMENT_BUILD`) |
| `Editor/` | Ferramentas de editor | `Game.Editor` |
| `Tests/EditMode/`, `Tests/PlayMode/` | Testes | `Game.Tests.EditMode`, `Game.Tests.PlayMode` |
| `Data/` | ScriptableObjects (cartas, efeitos, inimigos, tabela do D20, números) | — |
| `Audio/`, `Art/` | Placeholders | — |

O render do jogo usa `PixelCamera` e `PixelPost`, com configurações em `ImageQualitySettings` (D-056 a D-059). Esse ScriptableObject define `worldHeight` (0 = resolução da tela), `msaa` (4), `lightBands` (24), `bandSoftness` (0,25), `dither` (0) e `outlinePixelsAt1080` (2). O `PixelRenderSetup` copia faixas, rampa e pontilhado para o material `PixelPost.mat` e o MSAA para o asset do URP; o `PixelCamera` passa a espessura do contorno ao shader (`_OutlineWidth`, global). O mundo é renderizado na resolução da tela por padrão, com o snap de pixel aplicado somente quando `worldHeight` reduz a contagem de linhas.

A aura (Fase 7, D-060 a D-066) segue a mesma divisão:
- **Core:** `Core/Aura/AuraMapper` transforma HP, energia e sinais (`AuraInput`) nos parâmetros visuais (`AuraState`): raio, força, falha, faíscas, runas cheias, sinais, batimento e as cores da paleta normal ou alternativa.
- **Jogo:** `Aura/PlayerAura`, no jogador, junta a entrada do que já vem pela rede:
  - vida (`NetworkHealth`);
  - energia, carta amaldiçoada equipada e, publicados pelo host, os sinais de escudo e de reforço da Mola (`PlayerCards`);
  - estado de caído (`PlayerLife`).
  
  O `PlayerAura` suaviza esses valores e entrega o `AuraState` ao `AuraVisual` (círculo de latão, runas, luz, faíscas, fiapos, brasas e casca do escudo) e ao `AuraAudio` (batimento e chiado, só no seu personagem, sintetizados em código).
- **Números:** `Data/Aura/AuraSettings.asset`.
- **Texturas:** geradas por `Tools/Aura/aura_art.py`, ficam em `Art/Aura/`.
- **Montagem:** o `AuraBuilder` liga tudo ao prefab do jogador.
- **Paleta para daltônicos:** F7 (`Debug/DevAuraTools`).

Passe de ajuste pós-playtest (D-067 a D-069, relatório em `relatorio-passe-ajuste.md`):
- **Pulso de energia cheia:** `Core/Aura/AuraPulseTrigger` decide o quadro do pulso (uma vez por enchida, com rearme abaixo de `fullPulseRearmBelow`; não dispara ao nascer, caído nem ao levantar). `Core/Aura/AuraSmoother` suaviza HP e energia e só começa depois que o jogador entra na rede. O `PlayerAura` liga os dois ao `AuraVisual.Pulse` (onda, todos veem) e ao `AuraAudio.PlayFullChime` (só o dono).
- **O 1 do D20:** o `DiceRollUi` deixa o dado vermelho e põe um véu abaixo da HUD só no 1; `Dice/CriticalSounds` sintetiza o baque e o ronco; o `CardDropService` manda `ShowAmbushRpc` com as posições e o `Dice/AmbushFx` mostra o surgimento em todos. A regra do 1 e o momento da emboscada não mudaram.
- **Cristal no mundo:** `Core/Ambience/CrystalWave` (onda pura, testada em EditMode), `Arena/CrystalPulse` aplica o pulso e a crista de luz por `MaterialPropertyBlock`, `Arena/CrystalAmbienceSettings` guarda os números (`Data/Ambience/`); os veios e a poeira nascem no `AmbienceBuilder` e no `CityBuilder`.

Passe do mapa (D-073 a D-082, relatório em `relatorio-passe-mapa.md`, contrato em `plano-passe-mapa.md`):
- **Geometria do mapa no Core:** `Core/Map/MapLayout` (praça, avenidas, praças menores, bocas, spawns, portões, andável, região, ponto andável mais próximo e altura máxima de prédio pelo envelope da câmera) e `MapBoundary` (segmentos da vedação). Números em `Data/Map/MapLayoutSettings.asset`. Camadas em `Arena/MapLayers.cs` (camada 8 `Cenario`; rendering layer 8 `Vazavel`).
- **Construtores:** o `ArenaBuilder` monta o chão andável e a vedação (`Editor/WalkableBuilder`), os portões e marcadores nas bocas, chama o `CityBuilder` (que usa o `CityPlanner` para posicionar os prédios e seus colliders) e o `AmbienceBuilder`, e assa a NavMesh por último (`Editor/ArenaNavMeshBuilder`, grupos de colliders marcados "Not Walkable").
- **Inimigos:** `Enemies/EnemyPathFollower` calcula o caminho pela NavMesh só no host (sem `NavMeshAgent`); `Core/AI/PathCursor` e `SpawnMouths` são a lógica pura (quinas, recálculo, rodízio das bocas, fila); `WaveSpawner` nasce pelas bocas; o `CardDropService` põe a carta onde caiu o último inimigo.
- **Prédios translúcidos:** `Camera/SeeThroughDriver` publica os alvos tapados pelas globais `_Vazado*`; o shader `Art/Shaders/LitVazado` recorta o prédio (rendering layer 8) e uma passada `VazadoFantasma` desenha a silhueta fraca no buraco, instalada como feature do renderer pelo `PixelRenderSetup`.
- **Cena leve:** `Arena/Life/SmokeEmitter` e `AmbienceParticles` montam o `ParticleSystem` só em jogo.

Dependências: `Core` ← `Runtime` ← `Debug` / `Editor`. O `Core` não conhece ninguém.

## Namespaces

- `Game.Core.*` no Core e `Game.<Sistema>` no Runtime (ex.: `Game.Player`).
- O assembly `Game.Debug` usa o namespace **`Game.DevTools`**, o `Game.Editor` usa **`Game.EditorTools`** e a pasta `Camera/` usa **`Game.Cameras`**. Os nomes `Game.Debug`, `Game.Editor` e `Game.Camera` esconderiam `UnityEngine.Debug`, `UnityEditor.Editor` e `UnityEngine.Camera` dentro de qualquer código em `Game.*`.

## Rede

- Host-cliente com autoridade do host (Netcode for GameObjects + Unity Transport, LAN por IP direto).
- O cliente envia intenções, e o host valida, aplica e replica.
- D20, dano, vida e cartas existem só no host.

## Rede (Fase 3)

Como testar: `Docs/Tecnico/rede-lan.md`.

- `NetSession`: liga o `NetworkManager` às regras do jogo (hospedar, entrar por IP, aprovar conexões, vaga de spawn).
- `NetworkPlayer`: jogador em rede; o dono prevê o movimento e envia intenções, o host aplica e publica o estado.
- `MatchState`: estado compartilhado da partida (largada), só o host escreve. Foi nomeada assim para não colidir com `UnityEditor.SessionState`.
- `StartLever`: alavanca-máquina do spawn; só o host puxa (D-013).
- `GateActivation`: portão-máquina parado e apagado até a largada.
- `ConnectionScreen`: tela de Hospedar/Entrar, mensagens de status e IP do host.
- `SessionRoster` (Core): regras de quem entra e quem volta, por token (D-011, D-014).
- `ReconciliationBuffer` (Core): previsão local do movimento e correção pelo estado do host (D-009).

## Regras de código

- Conteúdo por dados: cartas, efeitos, inimigos e a tabela do D20 são ScriptableObjects.
- Números de balanceamento ficam só em ScriptableObjects, nunca no código.
- A aleatoriedade é injetada no Core (`seed` fixa nos testes).
- Sem singletons, exceto o `NetworkManager`.

## Input

`Assets/_Game/Player/Input/GameControls.inputactions`

| Mapa | Ação | Tecla |
|---|---|---|
| Player | Move | WASD |
| Player | Aim | posição do mouse |
| Player | Attack | botão esquerdo |
| Player | Skill1–4 | 1–4 |
| Player | Interact (pegar carta) | E |
| Debug | ToggleOverlay, ForceNextDice, GiveCard, SpawnEnemy, DamageSelf, HealSelf, ToggleAuraPalette | F1–F7 |

O mapa `Debug` só é lido pelo assembly `Game.Debug`, que não existe em build de jogo. As teclas do cinto de consumíveis e os demais atalhos são perguntas de design pendentes (P-002).

## Arte e Blender

- Os modelos placeholder são gerados por script: `Tools/Blender/build_props.py`.
  ```
  "E:/Program Files/Blender/blender.exe" --background --factory-startup --python Tools/Blender/build_props.py
  ```
- Saídas: `Art/Blender/*.blend` (fonte editável, fora de `Assets/`) e `Assets/_Game/Art/Models/*.fbx` (o que a Unity usa). Por isso só quem edita modelos precisa do Blender.
- Os materiais do Blender têm os mesmos nomes dos materiais em `Assets/_Game/Art/Materials`, e a importação remapeia pelo nome.
- Os modelos são simétricos de frente e de trás, e a rotação e a escala ficam embutidas na malha.

## Ferramentas de editor

| Menu | O que faz | Batchmode |
|---|---|---|
| Game → Setup → Criar cena Arena | Cria a cena e registra no Build Settings | `Game.EditorTools.ArenaSceneSetup.CreateArenaScene` |
| Game → Setup → Construir Arena | Reconstrói arena, prefab do jogador, materiais e settings | `Game.EditorTools.ArenaBuilder.Build` |
| Game → Build → Development Build (Windows) | Gera `Builds/Dev/Joiigo.exe` (`-buildPath <pasta>`) | `Game.EditorTools.DevBuild.BuildWindows` |
| Game → Debug → Capturar vistas da Arena | Salva PNGs pela câmera do jogo (`-snapshotDir <pasta>`) | `Game.EditorTools.SceneSnapshot.CaptureArena` |

A arena é gerada pelo `ArenaBuilder`. Mudanças feitas à mão na cena se perdem ao rodar o construtor de novo.
