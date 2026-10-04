# Agentes e modelos — quem faz o quê

> Escrito em 2026-10-04. O projeto passa a ser feito por mais de um harness: **Claude Code** (Opus 5.5 e Sonnet 5.5, em mais de uma conta) e **DeepSeek harness** com a **API da NVIDIA** (`build.nvidia.com`, endpoints gratuitos). As regras de trabalho são as mesmas para todos e estão em `AGENTS.md`.

## 1. Papéis

O trabalho é dividido por **papel**, não por modelo. Cada papel tem um modelo titular em cada harness e um reserva.

| Papel | O que faz | Claude Code | DeepSeek harness (NVIDIA) | Reserva NVIDIA |
|---|---|---|---|---|
| **Orquestrador** | Lê os docs, faz as perguntas ao Thiago, define contratos e arquitetura, divide as tarefas, integra, abre a Unity, depura, escreve o relatório da fase e faz o commit | Opus 5.5 (agente principal) | `kimi-k3` | `glm-5.3` |
| **Artista** | Modelagem (`Tools/Blender/*.py`), pixel art das cartas (`Tools/Cards/*.py`), shaders, paleta, efeitos, olhar capturas e corrigir | Opus 5.5 (subagente `artista`) | `kimi-k3`, **só depois que o Thiago responder P-011** (até lá, arte espera um Opus) | — (espera) |
| **Programador** | Código C# mecânico a partir de um contrato já definido, ScriptableObjects, ferramentas de editor | Sonnet 5.5 (subagente `programador`) | `glm-5.3` | `deepseek-v4.1-flash` |
| **Testador** | Testes EditMode/PlayMode, teste de regressão para cada bug | Sonnet 5.5 (subagente `testador`) | `deepseek-v4.1-flash` | `glm-5.3` |
| **Revisor** | Revisão do diff antes do commit: bugs, regras do `AGENTS.md`, números fora de SO | Sonnet 5.5 (subagente `revisor`); Opus em mudança grande | **Nunca o mesmo modelo que escreveu o código**, e de preferência de outra empresa (ex.: código do `glm-5.3`, da Z.ai → revisão do `kimi-k3`, da Moonshot), porque um modelo tende a não ver os próprios erros | `nemotron-3-super-120b-a12b` |
| **Documentador** | `decisoes.md`, `estacionamento.md`, `arquitetura.md`, README, rascunho do relatório | Sonnet 5.5 (subagente `documentador`) | `deepseek-v4.1-flash` | `glm-5.3-flash` |
| **Batedor** | Busca rápida no repositório, levantamento de arquivos, rodar comando e resumir log | Sonnet 5.5 (ou o Explore embutido) | `nemotron-3.5-lightning-30b-a3b` | `laguna-xs-2.1` |
| **Leitor de contexto longo** | Ler o repo inteiro ou logs enormes de uma vez (1M de contexto) | Opus 5.5 | `nemotron-3-ultra-550b-a55b` | `nemotron-3-super-120b-a12b` |
| **Olho** (visão) | Comparar capturas da Unity com `arte-pixel.md`, conferir se algo aparece na tela | Opus 5.5 | `kimi-k3` | `glm-5.3-flash`, `muse-glimmer-30b` |

Regras que valem para todos os papéis:

- **Só o orquestrador fala com o Thiago** e só ele registra decisões. Os outros papéis devolvem dúvidas de design ao orquestrador.
- **Só o orquestrador abre a Unity** (uma instância por vez; compilar e testar é serial). Os outros escrevem código e o orquestrador integra.
- **Arte só no tier mais alto** (D-042): no Claude, só Opus. Nunca Sonnet, nunca modelo pequeno.
- Quem escreve não revisa o próprio trabalho: a revisão sai de outro modelo, de preferência de outra empresa (o mesmo modelo tende a repetir os próprios erros).

## 2. Ordem de uma fase (orquestração)

```
1. Orquestrador   lê AGENTS.md + docs, levanta o estado (git log, passe pendente)
2. Orquestrador   faz as perguntas de design da fase ao Thiago → registra em decisoes.md
3. Orquestrador   escreve o plano curto: contratos (interfaces, SOs, nomes de arquivos) e lista de tarefas
4. Em paralelo:   Artista(s)    → modelos/arte por script (pode ser vários, um por peça)
                  Programador(es) → código por contrato (cada um em arquivos diferentes)
                  Testador      → testes a partir do contrato
5. Orquestrador   integra (ArenaBuilder/construtores), abre a Unity, compila, conserta
6. Testador       roda os testes (batchmode, editor fechado) e corrige o que for teste
7. Revisor        revisa o diff inteiro da fase
8. Documentador   atualiza docs e rascunha o relatório (§7 do prompt-prototipo)
9. Orquestrador   confere, faz o commit único da fase, para e espera o OK do Thiago
```

Paralelismo só no passo 4, e só com arquivos separados. Dois agentes nunca editam o mesmo arquivo ao mesmo tempo.

## 3. Trabalhando com mais de uma conta / harness

O limite de uso de uma conta pode acabar no meio de uma tarefa (aconteceu no passe visual). Para outro harness continuar sem perder nada:

1. **Um harness por vez no repositório.** Antes de começar, `git status` e `git log -3`: se há arquivos não commitados de outro agente, leia antes de mexer.
2. **Passagem de bastão:** ao parar no meio de uma fase, o agente escreve `Docs/Tecnico/<tarefa>-pendente.md` (modelo: `passe-visual-pendente.md`) com estado, o que falta e como terminar, e atualiza a seção "Onde o projeto está" do `AGENTS.md`. Código não compilado vai para `Docs/Tecnico/wip/*.txt` para não quebrar a Unity de quem pegar depois.
3. O commit continua sendo **um por fase**. Commit de passagem de bastão só se o Thiago pedir.
4. Quem assume lê o `-pendente.md` primeiro e apaga ele no commit que fecha a fase.

## 4. Configurar o DeepSeek harness com a NVIDIA

A API da NVIDIA é compatível com OpenAI:

| Item | Valor |
|---|---|
| Base URL | `https://integrate.api.nvidia.com/v1` |
| Chave | variável de ambiente `NVIDIA_API_KEY` (gerada em build.nvidia.com → Get API Key). **Nunca no repositório** (`.env` está no `.gitignore`). |
| Instruções do projeto | `AGENTS.md` na raiz. Se o harness não ler `AGENTS.md` sozinho, aponte o arquivo de instruções/system prompt dele para ele. |
| Prompts dos papéis | `.claude/agents/<papel>.md` — o texto depois do cabeçalho `---` serve como prompt de papel em qualquer harness. |

IDs dos modelos na API (conferidos nas páginas de build.nvidia.com em 2026-10-04 — atenção: o ID nem sempre é igual à URL, ex.: a página `z-ai/glm-5-3` usa o ID `z-ai/glm-5.3`):

| Modelo | ID | Por que está na equipe |
|---|---|---|
| Kimi K3 | `moonshotai/kimi-k3` | ~2,8T MoE multimodal, feito para código de horizonte longo e uso de ferramentas. O mais forte da lista → orquestrador e artista. |
| GLM-5.3 | `z-ai/glm-5.3` | 753B MoE com raciocínio e tool calling. Melhor programador de C# da lista depois do Kimi. |
| DeepSeek V4.1 Flash | `deepseek-ai/deepseek-v4.1-flash` | 552B MoE com 8B ativos, barato e rápido, multimodal. Testes, docs, tarefas repetitivas. |
| GLM-5.3 Flash | `z-ai/glm-5.3-flash` | Multimodal, 18B ativos. Reserva de visão e docs. |
| Nemotron 3 Ultra | `nvidia/nemotron-3-ultra-550b-a55b` | 1M de contexto. Ler muita coisa de uma vez. |
| Nemotron 3 Super | `nvidia/nemotron-3-super-120b-a12b` | 1M de contexto, mais leve. Reserva de revisão e contexto longo. |
| Nemotron 3.5 Lightning | `nvidia/nemotron-3.5-lightning-30b-a3b` | O mais rápido para tarefas agênticas curtas. Batedor. |
| Laguna XS 2.1 | `poolside/laguna-xs-2.1` | 33B MoE para código e terminal. Reserva do batedor. |
| Muse Glimmer 30B | `meta/muse-glimmer-30b` | Multimodal com raciocínio. Reserva do olho. |
| Gemma 4 31B / gpt-oss-20b | `google/gemma-4-31b-it`, `openai/gpt-oss-20b` | Últimos reservas para tarefas simples de texto. |

Fora da equipe (não servem ao projeto ou não têm endpoint gratuito): modelos de fala (ASR/TTS), tradução, OCR, segurança/guardrails, biologia/química, clima, direção autônoma, embeddings e os geradores de imagem/vídeo (`cosmos3-nano`, FLUX, Qwen-Image, SD 3.5 — a arte do jogo é feita por script, D-037/D-039, e geradores marcam a imagem com marca d'água).

### 4.1 Orquestração no DeepSeek harness (tool `workflow`)

O harness tem um tool `workflow` que aceita `provider` e `model` por agente e por fase (informação dada pelo próprio harness). A equipe da §1 fica assim:

```js
// Por agente: um subagente por papel
await agent(promptArtista,     { provider: 'nvidia', model: 'moonshotai/kimi-k3' })            // só após P-011
await agent(promptProgramador, { provider: 'nvidia', model: 'z-ai/glm-5.3' })
await agent(promptTestador,    { provider: 'nvidia', model: 'deepseek-ai/deepseek-v4.1-flash' })
await agent(promptBatedor,     { provider: 'nvidia', model: 'nvidia/nemotron-3.5-lightning-30b-a3b' })

// Por fase: a ordem da §2
phases: [
  { title: 'planejar',  model: 'moonshotai/kimi-k3' },                  // orquestrador: contratos e tarefas
  { title: 'codar',     model: 'z-ai/glm-5.3' },                        // programador
  { title: 'testar',    model: 'deepseek-ai/deepseek-v4.1-flash' },     // testador
  { title: 'revisar',   model: 'moonshotai/kimi-k3' },                  // revisor ≠ quem codou
  { title: 'documentar', model: 'deepseek-ai/deepseek-v4.1-flash' },    // documentador
]
```

O prompt de cada papel é o texto de `.claude/agents/<papel>.md` (a parte depois do cabeçalho `---`). As perguntas ao Thiago, a Unity e o commit ficam com o orquestrador (o próprio agente principal do harness, em `kimi-k3`), fora das fases. Exemplos que o harness der com modelos que não estão nesta lista (ex.: `deepseek-r1`, `llama-3.3-70b`) não valem para este projeto.

Os endpoints gratuitos têm limite de requisições por minuto. Se um modelo começar a recusar por limite, passe a tarefa para o reserva da tabela da §1 em vez de insistir.

## 5. Claude Code

- O agente principal é Opus 5.5 e faz o papel de orquestrador.
- Os papéis estão em `.claude/agents/` (`artista` em Opus; `programador`, `testador`, `revisor`, `documentador` em Sonnet). O orquestrador chama pelo nome; vários `artista` em paralelo é permitido (D-042).
- Com mais de uma conta Claude, cada conta é só mais um harness: vale a §3.

## 6. Pendências

- **P-011** (em `decisoes.md`): no DeepSeek harness, o `kimi-k3` pode modelar e fazer arte no lugar do Opus? Até a resposta, arte nova no DeepSeek harness espera um Opus.
