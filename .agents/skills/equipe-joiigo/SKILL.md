---
name: equipe-joiigo
description: Como dividir uma tarefa do Joiigo entre os papéis (orquestrador, artista, programador, testador, revisor, documentador) com os modelos certos da API NVIDIA e o limite de concorrência da conta gratuita.
whenToUse: Ao planejar ou executar uma fase ou tarefa com mais de um agente (subagent ou workflow) no DeepSeek harness.
---

# Equipe do Joiigo no DeepSeek harness

Fonte completa: `Docs/Tecnico/agentes-e-modelos.md`. Regras do projeto: `AGENTS.md`.

## Papéis → modelo (provider `nvidia`)

| Papel | Modelo | Reserva | Prompt do papel |
|---|---|---|---|
| Orquestrador (você, sessão principal) | `moonshotai/kimi-k3` | `z-ai/glm-5.3` | — |
| Artista | `moonshotai/kimi-k3` | **nenhuma**: espera (D-043) | `.claude/agents/artista.md` |
| Programador | `z-ai/glm-5.3` | `deepseek-ai/deepseek-v4.1-flash` | `.claude/agents/programador.md` |
| Testador | `deepseek-ai/deepseek-v4.1-flash` | `z-ai/glm-5.3` | `.claude/agents/testador.md` |
| Revisor | modelo diferente de quem escreveu (código do GLM → `moonshotai/kimi-k3`) | `nvidia/nemotron-3-super-120b-a12b` | `.claude/agents/revisor.md` |
| Documentador | `deepseek-ai/deepseek-v4.1-flash` | `z-ai/glm-5.3-flash` | `.claude/agents/documentador.md` |
| Batedor | `nvidia/nemotron-3.5-lightning-30b-a3b` | `poolside/laguna-xs-2.1` | — |

O prompt do papel é o texto do arquivo depois do cabeçalho `---`. Leia o arquivo e passe o texto no início do prompt do subagente, seguido da tarefa e do contrato.

Só modelos com imagem (`kimi-k3`, `deepseek-v4.1-flash`, `glm-5.3-flash`, `muse-glimmer-30b`) podem olhar capturas e renders.

## Limite da NVIDIA (obrigatório)

- No máximo **2 subagentes ao mesmo tempo** (com você, 3). Em `workflow`, use `parallel()` com no máximo 2 itens e encadeie lotes.
- Modelo sem resposta em 2 min ou com 2 erros seguidos → cancele, espere 60 s, use o reserva.
- Nunca dispare um agente por modelo "para testar".

## Avisos práticos (teste de 2026-10-04)

- Use raciocínio `medium`: em `high`, o GLM leva de 10 a 25 min por passo na conta gratuita.
- Se o titular e o reserva de um papel estiverem fora, use um modelo que respondeu agora (ex.: `nvidia/nemotron-3.5-lightning-30b-a3b`, `nvidia/nemotron-3-super-120b-a12b`) e revise o código dele com cuidado. A arte continua só com o `kimi-k3`.
- No modo seguro do Windows, o `dotnet build` precisa de `-m:1 -nr:false -p:UseSharedCompilation=false`, e o `dotnet test` e a Unity em batchmode podem travar. Se travar, não insista: registre no relatório e peça ao dono para rodar.

## Ordem de uma fase

1. Você lê `AGENTS.md` e os docs, escreve o contrato (arquivos, interfaces, dados) e a lista de tarefas.
2. Dúvida de design → **não decida**: registre como pergunta pendente em `Docs/Design/decisoes.md` e siga com o que não depende dela.
3. Lote 1 (paralelo, 2): artista + programador, em arquivos diferentes.
4. Lote 2: testador (escreve e roda os testes) → corrige com o programador se falhar.
5. Revisor (modelo diferente de quem escreveu) revisa o diff; você aplica ou devolve.
6. Documentador atualiza docs e rascunha o relatório.
7. Você confere, roda tudo de novo e faz o commit único da fase (push só se pedirem).

## Modelo de workflow

```js
// lote 1: arte e código em paralelo (2 agentes)
const [arte, codigo] = await parallel([
  () => agent(promptArtista + tarefaArte, { provider: 'nvidia', model: 'moonshotai/kimi-k3' }),
  () => agent(promptProgramador + tarefaCodigo, { provider: 'nvidia', model: 'z-ai/glm-5.3' }),
])
// lote 2: testes
const testes = await agent(promptTestador + tarefaTestes, { provider: 'nvidia', model: 'deepseek-ai/deepseek-v4.1-flash' })
// lote 3: revisão por outro modelo
const revisao = await agent(promptRevisor + 'Revise o diff atual.', { provider: 'nvidia', model: 'moonshotai/kimi-k3' })
return { arte, codigo, testes, revisao }
```
