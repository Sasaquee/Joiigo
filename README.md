# Joiigo — protótipo de arena (RPG Steampunk Mágico)

RPG de ação 2.5D cooperativo (2 a 4 jogadores em LAN) em Unity 6. Este repositório é a **arena de protótipo**: a base onde as mecânicas são testadas antes do mundo do jogo.

## Estado

| Fase | O que entrega | Estado |
|---|---|---|
| 0 | Análise, plano, primeiras decisões | ✅ |
| 1 | Setup (pacotes, assemblies, input, cena) | ✅ |
| 2 | Personagem, câmera 2.5D, arena com modelos do Blender | ✅ |
| 3 | Coop LAN (Hospedar / Entrar com IP, previsão local, largada por alavanca) | ✅ código e testes · ⏳ **falta playtest humano** |
| 4 | Combate (golpe em arco, dano mecânico/arcano), 3 inimigos com IA no host, ondas, morte que desmonta; ambientação semi-realista noturna (texturas CC0, fornalhas, vapor) | ✅ |
| 5 | Cartas de tarô (14, arte dourada), loadout por tiragem (Tab), skills 1–4, cinto Q/E/R, energia, efeitos mecânico-arcanos | ✅ |
| 6 | Carta no chão e D20 | próxima |
| 7–8 | Aura, ciclo completo e playtest | — |

## Abrir o projeto

1. Instale o **Unity 6.3 LTS — 6000.3.25f1** (pelo Unity Hub). Outra versão pode reimportar e mudar arquivos.
2. No Hub: **Add → Add project from disk** e escolha esta pasta. A primeira abertura demora (gera a `Library/`).
3. Abra `Assets/_Game/Arena/Arena.unity` e dê **Play**. O jogo entra direto, solo (D-018). **F9** abre a tela de conexão (coop). **F3** dá a próxima carta, **Shift+F3** dá todas (debug); **Tab** abre a tiragem.

Blender só é necessário para editar modelos (ver `Docs/Tecnico/arquitetura.md`).

## Testar o coop

Guia completo em [`Docs/Tecnico/rede-lan.md`](Docs/Tecnico/rede-lan.md). Resumo: **Window → Multiplayer → Multiplayer Playmode**, ative os Players 2–4, dê Play, clique **Hospedar** na janela principal e **Entrar** (IP `127.0.0.1`) nas outras. O host dá a largada puxando a alavanca no fundo do spawn com **E**.

## Documentos

| Arquivo | Para quê |
|---|---|
| [`Docs/Design/visao-e-pilares.md`](Docs/Design/visao-e-pilares.md) | Documento de design. Autoridade sobre o que o jogo é. |
| [`Docs/Design/prompt-prototipo.md`](Docs/Design/prompt-prototipo.md) | A tarefa do protótipo: regras de trabalho, sistemas, testes e fases. |
| [`Docs/Design/decisoes.md`](Docs/Design/decisoes.md) | Todas as decisões de design já tomadas (D-001…) e as perguntas pendentes. |
| [`Docs/Design/estacionamento.md`](Docs/Design/estacionamento.md) | Ideias fora do escopo atual. |
| [`Docs/Tecnico/arquitetura.md`](Docs/Tecnico/arquitetura.md) | Pastas, assemblies, rede, Blender, ferramentas de editor. |
| [`Docs/Tecnico/versoes.md`](Docs/Tecnico/versoes.md) | Versões exatas da Unity e dos pacotes. |
| `Docs/Capturas/` | Capturas de cada fase. |

## Testes

Unity: **Window → General → Test Runner** → Run All (EditMode e PlayMode). Na Fase 5: 101 EditMode + 48 PlayMode passando.
