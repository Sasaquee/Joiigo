---
name: delegar-dsh
description: Modo híbrido do Joiigo — o Claude orquestra e revisa, e delega trabalho mecânico (testes, código por contrato, docs, varreduras, rodar a Unity em batch) ao DeepSeek harness com modelos grátis/baratos via Tools/Agentes/dsh-tarefa.ps1. Use quando o dono pedir modo híbrido ou economia de uso do Claude.
---

# Delegar ao DeepSeek harness (modo híbrido)

Os modos de trabalho estão em `AGENTS.md` ("Modos de trabalho"). Aqui está o **híbrido**: você (Claude) orquestra, revisa e faz arte; o harness faz o trabalho mecânico e gasta cota grátis/barata, não a assinatura.

## O que delegar e o que não delegar

| Delegue ao harness | Faça você mesmo |
|---|---|
| Testes para código que já existe | **Arte, modelagem, shaders, efeitos, UI visual (D-044)** — nunca delegue |
| Código mecânico com contrato pronto (arquivos, assinaturas, dados) | Arquitetura, contratos, integração no `ArenaBuilder` |
| Documentação, rascunho de relatório | Perguntas de design ao dono e registro em `decisoes.md` |
| Varreduras ("liste…", "ache todos os…"), resumir logs | Decidir o que fazer com os achados |
| Rodar a Unity em batch e ler o resultado | Tarefa pequena que você termina mais rápido do que escreve o pedido |
| Segunda opinião de revisão | Revisão final e commit |

Regra prática: se escrever a tarefa leva mais tempo que fazê-la, faça você.

## Como delegar

1. Escreva a tarefa num arquivo temporário (`.dsh-saida/tarefa-<nome>.md`, pasta ignorada pelo git) com:
   - objetivo em 1–2 frases;
   - arquivos que pode criar/alterar (e os que não pode);
   - contrato (assinaturas, nomes, dados) quando for código;
   - critério de pronto (ex.: "testes EditMode passando, comando X");
   - o que devolver.
2. Rode em segundo plano (o harness é lento: 2–30 min):
   ```
   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Agentes/dsh-tarefa.ps1 -Papel <papel> -Tarefa .dsh-saida/tarefa-<nome>.md [-Modelo provedor/id] [-TimeoutMin 60]
   ```
   Papéis e modelos: `Tools/Agentes/papeis.json` (`programador`, `testador`, `revisor`, `documentador`, `batedor`, `unity`; `artista` é recusado).
3. **Uma tarefa do harness por vez** (o script recusa a segunda). Enquanto espera, siga com o seu trabalho em arquivos diferentes.
4. Revise **sempre**:
   - leia `.dsh-saida/<data>-<papel>/final.md` e `resumo.txt`;
   - leia o diff inteiro (`git diff`, arquivos novos);
   - confira as falhas típicas dos modelos baratos: decisão de design tomada sozinho ("provisório", "até o dono decidir"), número de jogo no código, troca de configuração do projeto para "contornar" erro (versão do .NET, asmdef), arquivos/pastas de lixo, testes que não testam nada;
   - rode os testes você mesmo quando a tarefa mexer em código.
5. Corrija o que for pouco; se estiver ruim, descarte (`git checkout`/apague) e faça você ou mande de novo com instruções melhores. Se falhar duas vezes, faça você.
6. O commit é seu, no fim da fase, como sempre.

## Problemas conhecidos

- Rodar a Unity pelo harness funciona com o GLM-5.3 (papel `unity`), mas modelos menores erram o comando (`-quit` junto com `-runTests`, duas Unity ao mesmo tempo) e já mataram processo do Unity Hub. Na dúvida, peça ao harness só o código e rode os testes você mesmo (36 s a 1 min).
- O `ToString`/`float.Parse` do C# usam a cultura do Windows (pt-BR, vírgula): confira testes de texto.
- Modelo que devolve "!!!!" ou resposta vazia: instabilidade da NVIDIA (comum no `kimi-k3`). Use o reserva do papel (`-Modelo`).
- GLM-5.3 pensa 15–25 min em decisões grandes: tarefas curtas e bem especificadas rendem mais.
- Cota: Gemini grátis ~20 pedidos/dia (3.8 Flash), OpenRouter grátis 50/dia sem créditos. NVIDIA não tem teto diário, mas ~40/min.
- Pasta com "Nível Obrigatório Baixo" (`icacls`): o script recusa; ver `Docs/Tecnico/agentes-e-modelos.md` §4.4.
