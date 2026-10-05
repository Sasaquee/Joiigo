# Relatório — Fase 8: ciclo completo e playtest

## 1. O que foi construído

- **Ciclo completo testado de ponta a ponta.** O teste `FullLoopTests` faz no host o fluxo inteiro do protótipo:
  1. largada;
  2. a primeira onda nasce e o golpe do jogador acerta um inimigo;
  3. a onda acaba e uma carta aparece no chão;
  4. pegar a carta rola o D20, e a carta chega depois da rolagem;
  5. a carta é equipada no espaço do tipo dela;
  6. a carta é usada (skill ou consumível).
- **Debug finalizado (§4.7).** Todas as teclas do mapa "Debug" agora funcionam (só no editor e em development build):

| Tecla | O que faz | Onde |
|---|---|---|
| F1 | Overlay: HP, energia, último dado, caminho, paleta, ondas e a lista de teclas | `Debug/DevOverlay.cs` (novo) |
| F2 / Shift+F2 | Próximo dado (normal → 1 → 20 → 10) / carta no chão | `Debug/DevDiceTools.cs` |
| F3 / Shift+F3 | Dar carta / todas | `Debug/DevCardTools.cs` |
| F4 | Gerar inimigo a 6 m à frente, em ciclo pelos tipos | `Debug/DevCombatTools.cs` (novo) |
| F5 / F6 | Dano de 25% em si / cura total | `Debug/DevCombatTools.cs` (novo) |
| F7 | Paleta da aura (normal ↔ daltônicos) | `Debug/DevAuraTools.cs` (passou de F8 para F7, a tecla que o mapa de debug já documentava) |
| F9 | Tela de conexão (coop) | `Debug/DevConnectionToggle.cs` |

- **Roteiro de playtest:** `Docs/Playtest/roteiro.md` cobre a preparação (solo e coop), o que dizer a quem joga, o que observar em cada sistema, as perguntas do fim, a checagem dos pilares e onde registrar.

## 2. Arquivos

- Novos:
  - `Debug/DevOverlay.cs`, `Debug/DevCombatTools.cs`;
  - `Tests/PlayMode/FullLoopTests.cs`;
  - `Docs/Playtest/roteiro.md`;
  - este relatório.
- Alterados: `Debug/DevAuraTools.cs` (F7); `Docs/Tecnico/relatorio-fase7.md` e `arquitetura.md` (F7).

## 3. Como testar

1. `Game → Setup → Construir Arena` → Play.
2. Aperte F1 e confira os números; teste F4, F5 e F6.
3. Jogue uma onda inteira: carta → dado → Tab → equipar → usar.
4. Testes: EditMode 165/165, PlayMode 67/67 (mais 2 de capturas, explícitos).
5. **Playtest humano:** seguir `Docs/Playtest/roteiro.md`.

## 4. Pilares

O ciclo completo junta os quatro: a carta temática vem do caminho (1), o D20 visível para todos (2), carta amaldiçoada e escolhas de equipar (3), latão e cristal (4). O roteiro mede cada um com quem joga (tabela "Checagem dos pilares").

## 5. Números

Nenhum número de jogo novo. Os de debug (6 m e 25%) ficam como constantes comentadas no `DevCombatTools`, porque são da ferramenta, não do jogo.

## 6. Perguntas e respostas

Nenhuma pergunta de design nesta fase.

## 7. Pendentes

- **Playtest humano** (solo e coop LAN) com o roteiro. É a parte da fase que só pessoas podem fazer. O coop também tem o playtest da Fase 3 pendente.
- Perguntas que esperam o coop voltar: P-003, P-004 e P-009.
- Depois do playtest: registrar as sessões em `Docs/Playtest/` e transformar o que pedir decisão em perguntas ao dono.

## 8. Problemas conhecidos

- O caminho no overlay só aparece no host (é ele quem registra as tags).
- O `FullLoopTests` aceita qualquer carta que o dado entregar. Para passiva e equipamento, o teste confere que a carta foi equipada, mas não confere o efeito delas; esses efeitos têm testes próprios em `CardUseTests`.
