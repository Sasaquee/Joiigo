# Instruções para agentes (qualquer IA)

> Fonte única das regras do projeto para qualquer harness (Claude Code, DeepSeek harness com a API da NVIDIA ou outro). `CLAUDE.md` só importa este arquivo. Quem faz o quê está em `Docs/Tecnico/agentes-e-modelos.md`.

Projeto Unity 6.3 LTS (6000.3.25f1), protótipo de arena de um RPG 2.5D cooperativo. Antes de qualquer trabalho, leia:

1. `Docs/Design/visao-e-pilares.md` — autoridade sobre o jogo.
2. `Docs/Design/prompt-prototipo.md` — regras de trabalho, sistemas e fases.
3. `Docs/Design/decisoes.md` — decisões já tomadas e perguntas pendentes.
4. `Docs/Tecnico/arquitetura.md` — como o código está organizado.
5. `Docs/Tecnico/agentes-e-modelos.md` — papéis, modelos de cada harness e ordem de trabalho.

## Regras que não mudam

- **Design é decisão do dono.** Tudo que muda o que o jogador sente ou vê vira pergunta de múltipla escolha (1–2 frases de contexto, 2–4 opções com consequência, recomendação com motivo e pilar, no máximo 4 por rodada). Registrar a resposta literal em `decisoes.md`. Nunca decidir "provisoriamente".
- **Números de jogo só em ScriptableObjects** (`Assets/_Game/Data/`), listados no relatório da fase.
- **Escopo fechado por fase.** Ideias novas vão para `Docs/Design/estacionamento.md`.
- **Um commit por fase.** Push só quando pedido. Parar ao fim de cada fase com o relatório (§7 do prompt) e esperar OK.
- **Todo bug corrigido ganha um teste que falharia antes da correção.**

## Onde o projeto está

Fases 0–5 concluídas e **passe visual concluído** (D-039 a D-045, relatório em `Docs/Tecnico/relatorio-passe-visual.md`): 3D pixelado (`Camera/PixelCamera.cs`, `Art/Shaders/PixelPost.shader`, `Editor/PixelRenderSetup.cs`, paleta em `Editor/PixelPalette.cs`; regras em `Docs/Design/arte-pixel.md`), andarilho encapuzado, cartas em pixel art, efeitos, e a cidade steampunk em volta da arena (`Editor/CityBuilder.cs`, chamado pelo `ArenaBuilder`; kit em `Tools/Blender/build_city.py`). Testes: EditMode 138, PlayMode 55. **Fase 6 — carta no chão e D20: em andamento** (D-046 a D-052; regras, rede, arte e testes prontos; falta ver em Play, ajustar, perguntar dois pontos ao dono e o relatório — **leia `Docs/Tecnico/fase6-pendente.md` antes de continuar**). **Depois da Fase 6: passe de resolução** (D-053 — o dono acha o jogo pixelado demais; perguntar P-012 antes de mexer), e só então a Fase 7. O coop (Fase 3) está guardado: o jogo entra direto solo (D-018), F9 volta à tela de conexão. Abertas para quando o coop voltar: P-003, P-004.

## Modos de trabalho

O dono escolhe o modo a cada tarefa; todos seguem as mesmas regras deste arquivo.

| Modo | Quem orquestra | Quem executa | Quando |
|---|---|---|---|
| **Só Claude** | Claude Opus (Claude Code) | Opus e subagentes Sonnet (`.claude/agents/`) | Tarefas complexas, fases inteiras, qualquer coisa visual |
| **Só harness** | DeepSeek harness (app) | Modelos NVIDIA / OpenRouter / Gemini da skill `equipe-joiigo` | Tarefas simples e bem delimitadas, sem arte |
| **Híbrido** | Claude Opus | Claude delega o mecânico ao harness com `Tools/Agentes/dsh-tarefa.ps1` (skill `delegar-dsh`) e revisa tudo | Economizar o uso do Claude em fases grandes |

**Arte é exceção em todos os modos (D-044):** modelagem, arte, shaders, efeitos, UI visual e qualquer coisa que mude como o jogo parece são feitas **só pelo modelo mais competente disponível** — hoje o Claude Opus. No modo só-harness, tarefa visual não é feita: vira pendência para o Claude. O jogo precisa ser bonito.

## Equipe de agentes

Detalhes em `Docs/Tecnico/agentes-e-modelos.md`. Resumo:

- **Orquestrador** (Claude: Opus · harness: `z-ai/glm-5.3` enquanto o `kimi-k3` estiver instável): arquitetura, contratos, perguntas ao dono, integração, Unity, depuração, relatório e commit. Só ele fala com o dono e só ele abre a Unity.
- **Artista** — D-044: só o modelo mais competente disponível (hoje Opus, vários em paralelo se quiser — D-042).
- **Programador / Testador / Revisor / Documentador / Batedor** (Claude: Sonnet · harness: `Tools/Agentes/papeis.json`): código mecânico, testes, revisão, docs e varreduras. A revisão sai de um modelo diferente do que escreveu.
- Prompts dos papéis em `.claude/agents/*.md` (servem para qualquer harness).
- O harness roda sempre em **acesso total**; o modo seguro marca a pasta e quebra build/testes (`agentes-e-modelos.md` §4.4). Ele **nunca faz commit** no modo híbrido.
- **API gratuita da NVIDIA (~40 requisições/min por chave, compartilhadas):** no máximo **3 agentes ao mesmo tempo** (subagentes em lotes de 2), uma sessão do harness por vez, nunca testar todos os modelos em paralelo. Modelo sem resposta em 2 min ou com 2 erros seguidos → cancele, espere 60 s e use o reserva. Regras completas em `Docs/Tecnico/agentes-e-modelos.md` §4.2.
- Só uma Unity abre o projeto por vez, então compilar e testar é serial. Só um harness trabalha no repositório por vez; ao parar no meio, deixe `Docs/Tecnico/<tarefa>-pendente.md` (passagem de bastão).

## Armadilhas já conhecidas

- Arena, prefab do jogador, rede e UI são **gerados por código** (`Game → Setup → Construir Arena` / `ArenaBuilder`). Mudanças manuais na cena se perdem ao reconstruir; altere o construtor.
- Modelos vêm de `Tools/Blender/build_props.py` (Blender 5.2, `--background`). Não edite os FBX à mão.
- Namespaces: não crie `Game.Debug`, `Game.Editor`, `Game.Camera` nem tipos com nomes da Unity (ex.: `SessionState`) — colidem com `UnityEngine.Debug`, `UnityEditor.Editor`, `UnityEngine.Camera`, `UnityEditor.SessionState`.
- `CharacterController` ignora transform movido por fora; o `PlayerMotor` ressincroniza. Para teleportar, desligue e religue o controller.
- `InputActionAsset` é compartilhado entre instâncias; o `PlayerInputReader` clona por instância.
- `NetworkObject` criado por script fica com `GlobalObjectIdHash` 0; o `ArenaBuilder` chama o `OnValidate` (ver `EnsureNetworkObjectHash`).
- Em batchmode, `Start-Process -Wait` trava esperando o cliente de licença da Unity; espere só o processo principal (`$p.WaitForExit()`).
- Testes em batchmode só rodam com o editor fechado. Use `-runTests -testPlatform EditMode -testResults <xml>` **sem `-quit`** (com `-quit` a Unity fecha antes de rodar os testes) e não abra uma segunda Unity enquanto a primeira roda.
- **Nunca mate processos da Unity, do Unity Hub ou do harness.** `unity.exe serve` e `Unity.Licensing.Client` são do Hub, não são o editor. Se achar que o editor está aberto, pare e avise.
- Se a Unity sair com "Library/ArtifactDB is corrupted", apague `Library/ArtifactDB*` e `Library/Artifacts` (é cache) e rode de novo.
- Dentro de `Game.Core.*`, `Math` resolve para o namespace `Game.Core.Math`; use `System.Math` / `MathF`.
- No editor, objetos devolvidos de um passo anterior do build podem virar referência morta após reimportações; recarregue assets pelo caminho antes de ligar referências na cena.
- Faces das cartas: `Tools/Cards/card_art.py` (Python + Pillow, fontes do Windows) → `Assets/_Game/Art/Cards/<id>.png` (D-037). Cartas novas: CardData + entrada no script.
- Inimigos vêm de `Tools/Blender/build_enemies.py`: peças separadas sob um Empty (`Corpo`, `Parte_*`, `Cristal`, `Arma`), frente em -Y no Blender.
