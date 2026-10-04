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
- O assembly `Game.Debug` usa o namespace **`Game.DevTools`** e o `Game.Editor` usa **`Game.EditorTools`**. Um namespace `Game.Debug` esconderia `UnityEngine.Debug`, e `Game.Editor` esconderia `UnityEditor.Editor`, dentro de qualquer código em `Game.*`.

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
