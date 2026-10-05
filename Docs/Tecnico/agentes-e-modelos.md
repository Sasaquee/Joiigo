# Agentes e modelos — quem faz o quê

> Escrito em 2026-10-04. O projeto passa a ser feito por mais de um harness: **Claude Code** (Opus 5.5 e Sonnet 5.5, em mais de uma conta) e **DeepSeek harness** com a **API da NVIDIA** (`build.nvidia.com`, endpoints gratuitos). As regras de trabalho são as mesmas para todos e estão em `AGENTS.md`.

## 1. Papéis

O trabalho é dividido por **papel**, não por modelo. Cada papel tem um modelo titular em cada harness e um reserva.

| Papel | O que faz | Claude Code | DeepSeek harness (NVIDIA) | Reserva NVIDIA |
|---|---|---|---|---|
| **Orquestrador** | Lê os docs, faz as perguntas ao dono, define contratos e arquitetura, divide as tarefas, integra, abre a Unity, depura, escreve o relatório da fase e faz o commit | Opus 5.5 (agente principal) | `kimi-k3` | `glm-5.3` |
| **Artista** | Modelagem, arte, shaders, efeitos, UI visual | Opus 5.5 (subagente `artista`) | **nenhum** (D-044: só o modelo mais competente; hoje o Opus) | — |
| **Programador** | Código C# mecânico a partir de um contrato já definido, ScriptableObjects, ferramentas de editor | Sonnet 5.5 (subagente `programador`) | `glm-5.3` | `deepseek-v4.1-flash` |
| **Testador** | Testes EditMode/PlayMode, teste de regressão para cada bug | Sonnet 5.5 (subagente `testador`) | `deepseek-v4.1-flash` | `glm-5.3` |
| **Revisor** | Revisão do diff antes do commit: bugs, regras do `AGENTS.md`, números fora de SO | Sonnet 5.5 (subagente `revisor`); Opus em mudança grande | **Nunca o mesmo modelo que escreveu o código**, e de preferência de outra empresa (ex.: código do `glm-5.3`, da Z.ai → revisão do `kimi-k3`, da Moonshot), porque um modelo tende a não ver os próprios erros | `nemotron-3-super-120b-a12b` |
| **Documentador** | `decisoes.md`, `estacionamento.md`, `arquitetura.md`, README, rascunho do relatório | Sonnet 5.5 (subagente `documentador`) | `deepseek-v4.1-flash` | `glm-5.3-flash` |
| **Batedor** | Busca rápida no repositório, levantamento de arquivos, rodar comando e resumir log | Sonnet 5.5 (ou o Explore embutido) | `nemotron-3.5-lightning-30b-a3b` | `laguna-xs-2.1` |
| **Leitor de contexto longo** | Ler o repo inteiro ou logs enormes de uma vez (1M de contexto) | Opus 5.5 | `nemotron-3-ultra-550b-a55b` | `nemotron-3-super-120b-a12b` |
| **Olho** (visão) | Comparar capturas da Unity com `arte-pixel.md`, conferir se algo aparece na tela | Opus 5.5 | `kimi-k3` | `glm-5.3-flash`, `muse-glimmer-30b` |

Regras que valem para todos os papéis:

- **Só o orquestrador fala com o dono** e só ele registra decisões. Os outros papéis devolvem dúvidas de design ao orquestrador.
- **Só o orquestrador abre a Unity** (uma instância por vez; compilar e testar é serial). Os outros escrevem código e o orquestrador integra.
- **Arte só no modelo mais competente disponível** (D-042, D-044): hoje o Claude Opus, em qualquer modo. Nunca Sonnet, nunca modelo do harness enquanto não for o melhor disponível.
- Quem escreve não revisa o próprio trabalho: a revisão sai de outro modelo, de preferência de outra empresa (o mesmo modelo tende a repetir os próprios erros).

## 2. Ordem de uma fase (orquestração)

```
1. Orquestrador   lê AGENTS.md + docs, levanta o estado (git log, passe pendente)
2. Orquestrador   faz as perguntas de design da fase ao dono → registra em decisoes.md
3. Orquestrador   escreve o plano curto: contratos (interfaces, SOs, nomes de arquivos) e lista de tarefas
4. Em paralelo:   Artista(s)    → modelos/arte por script (pode ser vários, um por peça)
                  Programador(es) → código por contrato (cada um em arquivos diferentes)
                  Testador      → testes a partir do contrato
5. Orquestrador   integra (ArenaBuilder/construtores), abre a Unity, compila, conserta
6. Testador       roda os testes (batchmode, editor fechado) e corrige o que for teste
7. Revisor        revisa o diff inteiro da fase
8. Documentador   atualiza docs e rascunha o relatório (§7 do prompt-prototipo)
9. Orquestrador   confere, faz o commit único da fase, para e espera o OK do dono
```

Paralelismo só no passo 4, e só com arquivos separados. Dois agentes nunca editam o mesmo arquivo ao mesmo tempo.

## 3. Trabalhando com mais de uma conta / harness

O limite de uso de uma conta pode acabar no meio de uma tarefa (aconteceu no passe visual). Para outro harness continuar sem perder nada:

1. **Um harness por vez no repositório.** Antes de começar, `git status` e `git log -3`: se há arquivos não commitados de outro agente, leia antes de mexer.
2. **Passagem de bastão:** ao parar no meio de uma fase, o agente escreve `Docs/Tecnico/<tarefa>-pendente.md` (modelo: `passe-visual-pendente.md`) com estado, o que falta e como terminar, e atualiza a seção "Onde o projeto está" do `AGENTS.md`. Código não compilado vai para `Docs/Tecnico/wip/*.txt` para não quebrar a Unity de quem pegar depois.
3. O commit continua sendo **um por fase**. Commit de passagem de bastão só se o dono pedir.
4. Quem assume lê o `-pendente.md` primeiro e apaga ele no commit que fecha a fase.

## 4. Configurar o DeepSeek harness com a NVIDIA

> **Máquina nova:** siga [`configurar-deepseek-harness.md`](configurar-deepseek-harness.md). Você pega e cola as chaves, o Claude aplica a configuração de referência (`Tools/Agentes/dsh-config/`) com `aplicar.ps1`.

A API da NVIDIA é compatível com OpenAI:

| Item | Valor |
|---|---|
| Base URL | `https://integrate.api.nvidia.com/v1` |
| Chave | variável de ambiente `NVIDIA_API_KEY` (gerada em build.nvidia.com → Get API Key). **Nunca no repositório** (`.env` está no `.gitignore`). |
| Instruções do projeto | `AGENTS.md` na raiz. Se o harness não ler `AGENTS.md` sozinho, aponte o arquivo de instruções/system prompt dele para ele. |
| Prompts dos papéis | `.claude/agents/<papel>.md` — o texto depois do cabeçalho `---` serve como prompt de papel em qualquer harness. |
| Skill da equipe | `.agents/skills/equipe-joiigo/SKILL.md` — o harness acha sozinho; diz qual modelo faz cada papel e como montar o `workflow` em lotes de 2. |
| Regras globais | `~/.dsh/AGENTS.md` (fora do repo) — limite da NVIDIA e segurança das chaves, valem em qualquer pasta. |

**Configuração do app desktop** (`~/.dsh/profiles/desktop/cordis.patch.yml`, backup em `~/.dsh/backups/`), feita em 2026-10-04:

| Item | Valor | Por quê |
|---|---|---|
| Modelo padrão | `z-ai/glm-5.3`, raciocínio `medium` | Seria o `kimi-k3`, mas ele estava instável em 2026-10-04 (ver §4.4); em `high` o GLM levava 10–25 min por passo |
| Permissão | **acesso total** na interface do app | O modo seguro marca a pasta com integridade baixa e quebra build/testes depois (§4.4) |
| `input` de cada modelo | `[text, image]` em `kimi-k3`, `deepseek-v4.1-flash`, `glm-5.3-flash`, `muse-glimmer-30b`; `[text]` nos outros | Sem isso o harness recusa imagem ao modelo |
| `streamIdleTimeoutMs` (provedor `nvidia`) | 120000 (padrão 300000) | Desistir de modelo mudo em 2 min |
| `retryPolicy` | `normal`, 2 tentativas, espera de 15 s a 60 s | Padrão era 5 tentativas; com 5 min cada, um modelo travado prendia a sessão por 30 min |
| `subagent.maxActiveSubagents` | 2 (padrão 8) | Limite da §4.2 |
| `workflow-ptc.maxConcurrentAgents` | 2 (padrão 0 = sem limite) | Foi o que deixou disparar 11 agentes de uma vez |
| Compactação de resultados de ferramentas | já vem ligada (corta acima de 8 KB) | — |

Plugins externos: nenhum instalado. Ver §4.3.

IDs dos modelos na API (conferidos na linha `model` do código de exemplo de cada página de build.nvidia.com em 2026-10-04 — atenção: o ID nem sempre é igual à URL, ex.: a página `z-ai/glm-5-3` usa o ID `z-ai/glm-5.3`):

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
// Por agente: um subagente por papel — no máximo 2 rodando ao mesmo tempo (§4.2)
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

O prompt de cada papel é o texto de `.claude/agents/<papel>.md` (a parte depois do cabeçalho `---`). As perguntas ao dono, a Unity e o commit ficam com o orquestrador (o próprio agente principal do harness, em `kimi-k3`), fora das fases. Exemplos que o harness der com modelos que não estão nesta lista (ex.: `deepseek-r1`, `llama-3.3-70b`) não valem para este projeto.

### 4.2 Limite da conta gratuita da NVIDIA (regra obrigatória)

**O que é o limite** (pesquisado em 2026-10-04):
- A conta gratuita tem cerca de **40 requisições por minuto por chave**. O limite é **compartilhado** por todos os modelos, agentes e sessões que usam a mesma `NVIDIA_API_KEY`.
- A NVIDIA diz que o limite também **varia por modelo e pelo tráfego do momento**. Num modelo lotado, a requisição pode ficar parada sem resposta em vez de voltar erro 429.
- No plano gratuito **não há como aumentar** o limite.
- Cada passo de um agente (pensar → chamar ferramenta → ler o resultado) é **uma requisição**. Uma tarefa agêntica comum gasta de 30 a 60 requisições.

**O que já aconteceu:** um teste disparou 11 agentes ao mesmo tempo. Dez responderam em 1 minuto, e o `deepseek-v4.1-flash` ficou sem resposta 5 minutos por tentativa, de 5 tentativas possíveis. A sessão principal ficou parada esperando.

**Regras (valem para todo agente rodando pela API da NVIDIA):**

1. **No máximo 3 agentes ao mesmo tempo** na mesma chave, contando o agente principal. No `workflow`, dispare os `agent()` em lotes de até 2 subagentes e espere o lote terminar antes do próximo.
2. **Uma sessão do harness por vez** com a chave da NVIDIA. Duas janelas abertas trabalhando dividem os mesmos 40 por minuto.
3. **Teto de 30 requisições por minuto** (25% de folga). Prefira passos que façam mais de uma coisa (ler vários arquivos numa chamada, comandos agrupados) a muitos passos pequenos.
4. **Nunca testar todos os modelos em paralelo.** Os 11 IDs já foram conferidos na API em 2026-10-04. Se precisar testar de novo, faça um modelo por vez, uma vez só.
5. **Desistir cedo de modelo travado.** Sem nenhuma resposta em **2 minutos**, ou com **2 erros seguidos** (429, timeout, servidor): cancele, aguarde 60 s e passe a tarefa ao **reserva** da tabela da §1. Não espere as 5 tentativas de 5 minutos do harness.
6. **Erro 429:** pare tudo por **60 s** (a janela é por minuto) antes de qualquer nova chamada. Não insista em loop.
7. **Esperar job em segundo plano:** use espera curta (até 2 min por consulta). Se o job não andou entre duas consultas, aplique a regra 5.
8. Arte não roda no harness (D-044): vira pendência para o Claude e o resto da fase continua.

Esses limites não valem para o Claude Code (outro provedor, com limites próprios da conta).

### 4.3 Plugins externos

Auditados em 2026-10-04, nenhum instalado:

- Os plugins "oficiais" `dsh-external/*` citados em listas da internet **não existem** (a organização tem 0 repositórios públicos). Buscas por eles levam a cópias de desconhecidos e até a um repositório-isca ("hacks-para-krunker"). Não instale plugin de lista "awesome" sem abrir o código.
- `CheshireJCat/blender` (MIT, 39 estrelas): código limpo (sem rede, sem script de instalação, só roda o Blender com `--disable-autoexec`), mas declara compatibilidade com o harness 0.1.x e o instalado é 0.2.0-rc.2, então o harness recusa sem exceção manual. O projeto já gera modelos por `Tools/Blender/*.py`, então ele não é necessário.
- O que os plugins de "plan-execute" e "contexto" prometiam já vem no harness: `workflow` com modelo por agente, subagentes e compactação.

Antes de instalar qualquer plugin: ler o código, procurar rede/`child_process`/scripts de instalação, conferir a compatibilidade de versão, e instalar primeiro num perfil de teste (`dsh plugin --profile <teste> add <pacote>`), nunca no `desktop`.

### 4.4 Teste de capacidade (2026-10-04)

O teste foi feito em três partes, sempre com o harness em **acesso total** (`danger-full-access`):
1. **Mini projeto** com as mesmas regras do Joiigo (`Documents/Codes/dsh-teste-d20`): D20 em C# com NUnit, dado modelado no Blender, um bug plantado e uma pergunta de design em aberto.
2. **Unity em batchmode no próprio Joiigo**, só rodando os testes, sem alterar nada.
3. **Arte só por texto:** o Pilão Arcano da cidade, descrito em palavras, sem imagem de referência.

**Resultados**

| Parte | Resultado |
|---|---|
| Unity no Joiigo | ✅ Leu o `AGENTS.md`, respeitou as armadilhas (batchmode com `-PassThru` + `WaitForExit`), conferiu que o editor estava fechado sem matar nada e rodou os testes: **101/101 passando**, sem erro de compilação, em 9 min no total. Não alterou nenhum arquivo do projeto. |
| Código C# | ✅ O GLM-5.3 escreveu código limpo e correto e corrigiu o bug plantado. Os testes (Nemotron 3 Super + GLM) chegaram a **20/20** depois que o orquestrador achou e mandou corrigir os erros deles. |
| Orquestração | ✅ Leu regras e skill sozinho, respeitou o limite de agentes, cancelou e trocou modelos travados, e desfez estragos de subagentes (troca do .NET, pastas de lixo). Um revisor de outro modelo revisou o diff. |
| Arte | ⚠️ O dado D20 ficou bom (script do Kimi K3, render corrigido pelo orquestrador). O Pilão só por texto não saiu: o Kimi K3 devolvia lixo, o Nemotron 3 Super entregou geometria quebrada e o GLM-5.3 passou 25 min pensando sem escrever. |
| Fim da fase | ❌ Não chegou ao relatório nem ao commit em ~2 h. |

**Estado dos modelos na NVIDIA gratuita (mudou ao longo do dia):**
- `z-ai/glm-5.3` responde, mas pensa de 15 a 25 min a cada decisão grande, mesmo em `medium`.
- `nvidia/nemotron-3-super-120b-a12b` é rápido e bom para testes e revisão.
- `moonshotai/kimi-k3` devolve "!!!!" na maioria das chamadas; ajustes de compatibilidade não resolveram.
- `deepseek-ai/deepseek-v4.1-flash` e `z-ai/glm-5.3-flash` travam.
- `moonshotai/kimi-k2.6`, `mistralai/mistral-large-2-instruct` e `nvidia/llama-3.1-nemotron-ultra-253b-v1` aparecem na lista da API, mas devolvem 404.
- A chave `DEEPSEEK_API_KEY` que está no harness é inválida.

**Falhas de julgamento observadas** (o Claude ou o dono precisam revisar):
- Aplicou a recomendação de uma pergunta pendente "até o dono decidir", ou seja, decidiu provisoriamente. Nem o revisor pegou.
- Passou a arte para modelos fora da regra D-043 depois que o Kimi falhou.
- Escreveu errado o nome de um modelo (`muse-glimmer-30b` sem `meta/`) e depois atribuiu a falha a "congestionamento".
- No modo headless, encerra o turno para "esperar notificação", o que mata os jobs. No app desktop esse problema não existe.

**Armadilha de ambiente:** o modo seguro (`workspace-write`) marca a pasta do projeto com integridade baixa, e a marca fica. Depois disso, `dotnet test` e builds falham com "Acesso negado" mesmo em acesso total. Use sempre acesso total no Joiigo. Se a marca aparecer (`icacls <pasta>` mostra "Nível Obrigatório Baixo"), copie o projeto para uma pasta nova.

**Conclusão:** o harness **consegue trabalhar no Joiigo**: lê as regras, compila e roda os testes na Unity, escreve código bom e coordena subagentes. Hoje ele é **lento** (horas para uma fase que o Claude faz em minutos) e **não é confiável para arte**. Use para tarefas bem delimitadas (testes, revisão, docs, funções de C# com contrato pronto), sempre com revisão final do Claude ou do dono antes do commit. Arte continua com o Opus até o Kimi K3 estabilizar.

### 4.5 Modos de trabalho (D-044)

| Modo | Como usar |
|---|---|
| **Só Claude** | Abra o Claude Code no projeto e peça. Ele orquestra com Opus e usa os subagentes de `.claude/agents/`. |
| **Só harness** | Abra o DeepSeek harness no projeto (acesso total) e peça. Ele segue `AGENTS.md` e a skill `equipe-joiigo`. Tarefa visual não é feita: vira pendência para o Claude. |
| **Híbrido** | Peça ao Claude "modo híbrido". Ele delega o mecânico com `Tools/Agentes/dsh-tarefa.ps1` (skill `.claude/skills/delegar-dsh`), revisa o diff e faz o commit. Economiza a assinatura do Claude; o harness gasta cota grátis. |

`Tools/Agentes/dsh-tarefa.ps1` também pode ser usado à mão:

```
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Agentes/dsh-tarefa.ps1 -Papel testador -Tarefa minha-tarefa.md
```

Ele escolhe o modelo por papel (`Tools/Agentes/papeis.json`, editável), usa a configuração do app, roda em acesso total, recusa arte, recusa uma segunda tarefa simultânea e guarda tudo em `.dsh-saida/` (ignorada pelo git).

**Provedores configurados no app** (2026-10-04): NVIDIA (grátis, sem teto diário, instável), Google AI Studio (grátis, ~20 pedidos/dia no Gemini 3.8 Flash; Pro sem cota grátis) e OpenRouter (grátis 50 pedidos/dia sem créditos, 1.000/dia com US$ 10 de créditos; modelos pagos já cadastrados: DeepSeek V4 Pro, DeepSeek V4 Flash, Kimi K3, Gemini 3.8 Flash). O plano Google AI Plus do dono não dá cota de API.

## 5. Claude Code

- O agente principal é Opus 5.5 e faz o papel de orquestrador.
- Os papéis estão em `.claude/agents/` (`artista` em Opus; `programador`, `testador`, `revisor`, `documentador` em Sonnet). O orquestrador chama pelo nome; vários `artista` em paralelo é permitido (D-042).
- Com mais de uma conta Claude, cada conta é só mais um harness: vale a §3.

## 6. Pendências

Nenhuma. (P-011 resolvida em D-043, depois substituída por D-044.)
