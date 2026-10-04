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

Dependências: `Core` ← `Runtime` ← `Debug` / `Editor`. O `Core` não conhece ninguém.

## Namespaces

- `Game.Core.*` no Core e `Game.<Sistema>` no Runtime (ex.: `Game.Player`).
- O assembly `Game.Debug` usa o namespace **`Game.DevTools`**, o `Game.Editor` usa **`Game.EditorTools`** e a pasta `Camera/` usa **`Game.Cameras`**. Os nomes `Game.Debug`, `Game.Editor` e `Game.Camera` esconderiam `UnityEngine.Debug`, `UnityEditor.Editor` e `UnityEngine.Camera` dentro de qualquer código em `Game.*`.

## Rede

- Host-cliente com autoridade do host (Netcode for GameObjects + Unity Transport, LAN por IP direto).
- O cliente envia intenções, e o host valida, aplica e replica.
- D20, dano, vida e cartas existem só no host.

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
| Game → Debug → Capturar vistas da Arena | Salva PNGs pela câmera do jogo (`-snapshotDir <pasta>`) | `Game.EditorTools.SceneSnapshot.CaptureArena` |

A arena é gerada pelo `ArenaBuilder`. Mudanças feitas à mão na cena se perdem ao rodar o construtor de novo.
