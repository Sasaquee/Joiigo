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

Fases 0–4 concluídas. O coop (Fase 3) está guardado: o jogo entra direto solo como host (D-018), e F9 (debug) volta à tela de conexão. Desde a Fase 4, cada fase entrega mecânica + visual final (D-015), no estilo semi-realista sombrio (D-016) com noite arcana e fornalhas (D-017). A próxima é a **Fase 5 — cartas e loadout**; antes de codar, perguntar P-001, P-002 e P-007 de `decisoes.md`. Ainda abertas para quando o coop voltar: P-003 (como levantar aliado) e P-004 (todos caídos).

Divisão de trabalho que o dono pediu: tarefas mecânicas, docs, testes e revisão vão para subagentes Sonnet em paralelo; o agente principal fica com arquitetura, contratos do Core, modelagem no Blender, integração e depuração. Só uma Unity abre o projeto por vez, então compilar e testar é serial.

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
- Inimigos vêm de `Tools/Blender/build_enemies.py`: peças separadas sob um Empty (`Corpo`, `Parte_*`, `Cristal`, `Arma`), frente em -Y no Blender.
