# Instruções para o Claude Code

As regras do projeto ficam em `AGENTS.md` (fonte única, lida por qualquer harness). Não duplique conteúdo aqui.

@AGENTS.md

## Só no Claude Code

- Você (agente principal, Opus) é o **orquestrador** de `Docs/Tecnico/agentes-e-modelos.md`.
- Os papéis estão em `.claude/agents/`: `artista` (Opus) e `programador`, `testador`, `revisor`, `documentador` (Sonnet). Modelagem e arte (modelos, texturas, pixel art) só com o modelo mais competente (D-042, D-044, D-078): nunca Sonnet, nunca o harness. Quem escreve código, inclusive de efeito, shader e UI, pode ser Sonnet (D-078).
- Modo híbrido: skill `delegar-dsh` (delegar o mecânico ao DeepSeek harness e revisar tudo antes do commit).
