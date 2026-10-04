# Instruções para o agente

Projeto Unity 6.3 LTS (6000.3.25f1), protótipo de arena de um RPG 2.5D cooperativo. Antes de qualquer trabalho, leia:

1. `Docs/Design/visao-e-pilares.md` — autoridade sobre o jogo.
2. `Docs/Design/prompt-prototipo.md` — regras de trabalho, sistemas e fases.
3. `Docs/Design/decisoes.md` — decisões já tomadas e perguntas pendentes.
4. `Docs/Tecnico/arquitetura.md` — como o código está organizado.

## Regras que não mudam

- **Design é decisão do Thiago (dono).** Tudo que muda o que o jogador sente ou vê vira pergunta de múltipla escolha (1–2 frases de contexto, 2–4 opções com consequência, recomendação com motivo e pilar, no máximo 4 por rodada). Registrar a resposta literal em `decisoes.md`. Nunca decidir "provisoriamente".
- **Números de jogo só em ScriptableObjects** (`Assets/_Game/Data/`), listados no relatório da fase.
- **Escopo fechado por fase.** Ideias novas vão para `Docs/Design/estacionamento.md`.
- **Um commit por fase.** Push só quando pedido. Parar ao fim de cada fase com o relatório (§7 do prompt) e esperar OK.
- **Todo bug corrigido ganha um teste que falharia antes da correção.**

## Onde o projeto está

Fases 0–5 concluídas. **Passe visual em andamento (D-039 a D-042):** o estilo mudou para **3D pixelado** (render em ~640x360 com ampliação Point, contorno e luz em faixas — `Camera/PixelCamera.cs`, `Art/Shaders/PixelPost.shader`, `Editor/PixelRenderSetup.cs`, paleta chapada em `Editor/PixelPalette.cs`; regras em `Docs/Design/arte-pixel.md`). Já integrados: andarilho encapuzado (`Tools/Blender/build_character.py`), cartas em pixel art (`Tools/Cards/card_pixel.py`), efeitos de combate/skills/revelação, componentes de vida da cidade (`Arena/Life`). **Falta (ver `Docs/Tecnico/passe-visual-pendente.md`):** a cidade steampunk em volta da arena (`Tools/Blender/build_city.py` + `Editor/CityBuilder.cs`, ligar com `CityBuilder.Build(arena)` no ArenaBuilder), ajuste final de luz, rodar os testes e fechar o passe com relatório. Depois: **Fase 6 — carta no chão e D20** (perguntar P-008, P-009 e a tabela do D20 antes). O coop (Fase 3) está guardado: o jogo entra direto solo (D-018), F9 volta à tela de conexão. Abertas para quando o coop voltar: P-003, P-004.

Divisão de trabalho que o dono pediu: **modelagem e arte só por Opus** (pode usar vários agentes Opus em paralelo); código mecânico, docs, testes e revisão podem ir para Sonnet; o agente principal fica com arquitetura, contratos, integração e depuração. Só uma Unity abre o projeto por vez, então compilar e testar é serial.

## Armadilhas já conhecidas

- Arena, prefab do jogador, rede e UI são **gerados por código** (`Game → Setup → Construir Arena` / `ArenaBuilder`). Mudanças manuais na cena se perdem ao reconstruir; altere o construtor.
- Modelos vêm de `Tools/Blender/build_props.py` (Blender 5.2, `--background`). Não edite os FBX à mão.
- Namespaces: não crie `Game.Debug`, `Game.Editor`, `Game.Camera` nem tipos com nomes da Unity (ex.: `SessionState`) — colidem com `UnityEngine.Debug`, `UnityEditor.Editor`, `UnityEngine.Camera`, `UnityEditor.SessionState`.
- `CharacterController` ignora transform movido por fora; o `PlayerMotor` ressincroniza. Para teleportar, desligue e religue o controller.
- `InputActionAsset` é compartilhado entre instâncias; o `PlayerInputReader` clona por instância.
- `NetworkObject` criado por script fica com `GlobalObjectIdHash` 0; o `ArenaBuilder` chama o `OnValidate` (ver `EnsureNetworkObjectHash`).
- Em batchmode, `Start-Process -Wait` trava esperando o cliente de licença da Unity; espere só o processo principal (`$p.WaitForExit()`).
- Testes em batchmode só rodam com o editor fechado.
- Se a Unity sair com "Library/ArtifactDB is corrupted", apague `Library/ArtifactDB*` e `Library/Artifacts` (é cache) e rode de novo.
- Dentro de `Game.Core.*`, `Math` resolve para o namespace `Game.Core.Math`; use `System.Math` / `MathF`.
- No editor, objetos devolvidos de um passo anterior do build podem virar referência morta após reimportações; recarregue assets pelo caminho antes de ligar referências na cena.
- Faces das cartas: `Tools/Cards/card_art.py` (Python + Pillow, fontes do Windows) → `Assets/_Game/Art/Cards/<id>.png` (D-037). Cartas novas: CardData + entrada no script.
- Inimigos vêm de `Tools/Blender/build_enemies.py`: peças separadas sob um Empty (`Corpo`, `Parte_*`, `Cristal`, `Arma`), frente em -Y no Blender.
