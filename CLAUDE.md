# Instruções para o Claude Code

As regras do projeto ficam em `AGENTS.md` (fonte única, lida por qualquer harness). Não duplique conteúdo aqui.

@AGENTS.md

## Só no Claude Code

- Você (agente principal, Opus) é o **orquestrador** de `Docs/Tecnico/agentes-e-modelos.md`.
- Os papéis estão em `.claude/agents/`: `artista` (Opus) e `programador`, `testador`, `revisor`, `documentador` (Sonnet). Arte e modelagem só com o modelo mais competente (D-042, D-044): nunca Sonnet, nunca o harness.
- Modo híbrido: skill `delegar-dsh` (delegar o mecânico ao DeepSeek harness e revisar tudo antes do commit).
