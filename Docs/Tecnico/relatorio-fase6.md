# Relatório da Fase 6

## 1. O que foi construído, em linguagem simples
Na Fase 6, implementamos a mecânica de carta no chão e rolagem do D20. Quando uma onda termina, uma carta aparece flutuando no centro da arena. Ao pegar a carta com F, o D20 rola grande na tela e para em um número. O resultado determina a raridade, qualidade e quantidade da carta, com críticos no 1 (carta amaldiçoada e emboscada) e no 20 (chance de arcano maior ou carta única). A qualidade da carta afeta seus números e aparece na moldura. Todas as mudanças são feitas no host e replicadas para os clientes.

## 2. Arquivos criados e alterados
- `Assets/_Game/Core/Dice/*.cs` (regras puras de D20, tabela, sorteio)
- `Assets/_Game/Core/Cards/CardDraw.cs` (sorteio de carta)
- `Assets/_Game/Core/Cards/CardQuality.cs` (qualidade da carta)
- `Assets/_Game/Data/Dice/DiceSettings.asset` (tabela, peso do caminho, emboscada, tempos)
- `Assets/_Game/Data/Cards/CardsSettings.asset` (multiplicadores de qualidade)
- `Assets/_Game/Dice/CardDropService.cs` (rede: carta no chão, rolagem no host, entrega)
- `Assets/_Game/Dice/FloorCard.cs` (representação da carta no chão)
- `Assets/_Game/Enemies/WaveSpawner.cs` (evento WaveCleared para spawn de carta)
- `Assets/_Game/Cards/PlayerCards.cs` (qualidade por carta, entrega, melhoria, cópia)
- `Assets/_Game/Cards/CardEffect.cs` (potência nos efeitos e modificadores)
- `Assets/_Game/Cards/Effects/*.cs` (potência da qualidade nos efeitos)
- `Assets/_Game/Art/Models/Dice/D20.fbx` (modelo 3D do D20: latão, números em cristal, opostos somam 21)
- `Assets/_Game/Art/Cards/verso.png` (verso da carta: engrenagem dourada com cristal)
- `Assets/_Game/UI/DiceRollUi.cs` (tela do dado rolando e parando no número)
- `Assets/_Game/UI/CardView.cs` (moldura de qualidade: gasta = verdete, perfeita = ouro)
- `Assets/_Game/UI/CardRevealFx.cs` (revelação na melhoria)
- `Assets/_Game/Editor/CardDropBuilder.cs` (construtor chamado pelo ArenaBuilder)
- `Assets/_Game/Debug/DevDiceTools.cs` (debug: F2 força próximo dado, Shift+F2 põe carta no chão)
- `Assets/_Game/Tests/EditMode/DiceCoreTests.cs` (21 testes EditMode)
- `Assets/_Game/Tests/PlayMode/CardDropTests.cs` (5 testes PlayMode)
- `Assets/_Game/Tests/PlayMode/DiceFaceTests.cs` (as 20 faces do D20 param de frente e em pé; opostas somam 21)
- `Assets/_Game/Tests/PlayMode/Fase6Capturas.cs` (fotos do fluxo em Play, só quando pedido)

## 3. Como testar, passo a passo
1. Abra a Unity.
2. Vá em Game → Setup → Construir Arena.
3. Clique em Play para entrar na cena.
4. Espere terminar uma onda (inimigos aparecem em ondas com pausa).
5. Uma carta flutuante de pé aparece na arena.
6. Aproxime-se e pressione F para pegar a carta.
7. Observe o D20 rolando grande na tela e parando em um número.
8. Verifique o resultado na tabela: 1 dá carta amaldiçoada e emboscada, 20 dá arcano maior ou única.
9. Pressione Tab para ver a moldura de qualidade na tiragem.
10. Para testar debug: pressione F2 para ciclar o próximo dado (1, 20, 10, normal); pressione Shift+F2 para pôr uma carta no chão a qualquer momento.
11. Para rodar os testes, abra Window → General → Test Runner e execute EditMode e PlayMode.
12. Resultado esperado: EditMode 138/138 e PlayMode 59/59 (o `Fase6Capturas` é explícito e fica de fora).

## 4. Checagem dos pilares
- **Pilar 1 (identidade descoberta):** o tema da carta vem do caminho do jogador (as 2 tags mais usadas, com peso 2) e do lugar, nunca do dado. A carta acompanha quem o jogador está virando.
- **Pilar 2 (a sorte como emoção):** o D20 rola grande na tela, todos veem o mesmo número, e o 1 e o 20 mudam o momento (emboscada; arcano maior ou única). O resultado não tem modificador nenhum (teste em `DiceCoreTests`).
- **Pilar 3 (escolhas com peso cinza):** a carta repetida melhora a que já existe (D-051) ou vira cópia (D-052). A qualidade muda os números sem tornar a carta gasta inútil.
- **Pilar 4 (fusão de tecnologia e magia):** o D20 é de latão com números em cristal, e o verso é uma engrenagem dourada com cristal.

## 5. Números provisórios usados e onde mudar
Aprovados pelo dono em D-055. Continuam fáceis de mudar:
- Qualidade: gasta ×0,85, boa ×1,0, perfeita ×1,15 (`Data/Cards/CardsSettings.asset`).
- Tabela do D20: faixas 1–20 com raridade, qualidade e extras; a comum extra do 19 é boa (D-054) (`Data/Dice/DiceSettings.asset`).
- Peso do caminho no tema: 2 (`DiceSettings`, `pathBias`).
- Emboscada: 2–3 inimigos a 5 m (`DiceSettings`, `ambushMin`/`ambushMax`/`ambushRadius`).
- Rolagem: 2,2 s; a carta chega 0,6 s depois (`DiceSettings`, `rollDuration`/`grantDelay`).

## 6. Perguntas de design feitas, respostas e onde foram registradas
- D-046: Como a carta aparece no chão da arena antes de ser pega? Opções: Flutuando de pé · Caída no chão · Relicário mecânico. Resposta literal: "Flutuando de pé (Recomendado)". Registrada em `Docs/Design/decisoes.md`.
- D-047: Como o D20 rola na frente do jogador ao pegar a carta? Opções: Dado grande na tela · Dado no mundo · Dado sobre a cabeça. Resposta literal: "Dado grande na tela (Recomendado)". Registrada em `Docs/Design/decisoes.md`.
- D-048: Como a tabela do D20 decide raridade, qualidade e quantidade? Opções: Três eixos simples · Sem qualidade por enquanto · Quero ajustar a tabela. Resposta literal: "Três eixos simples (Recomendado)". Registrada em `Docs/Design/decisoes.md`.
- D-049: No 1, além da carta amaldiçoada, que perigo o resultado atrai? Opções: Emboscada · Onda antecipada · A carta morde. Resposta literal: "Emboscada (Recomendado)". Registrada em `Docs/Design/decisoes.md`.
- D-050: De onde surgem as cartas no chão da arena? Opções: Fim de cada onda · Inimigos derrubam · Pontos fixos da arena. Resposta literal: "Fim de cada onda (Recomendado)". Registrada em `Docs/Design/decisoes.md`.
- D-051: Se o dado sortear uma carta que o jogador já tem, o que acontece? Opções: Sorteia outra · Melhora a que já tem · Pode ter cópias. Resposta literal: "Melhora a que já tem". Registrada em `Docs/Design/decisoes.md`.
- D-052: E se a que ele tem já for perfeita? Opções: Sorteia outra · Nada acontece · Cópia extra. Resposta literal: "Cópia extra". Registrada em `Docs/Design/decisoes.md`.
- D-054: No 19, além da incomum perfeita, vem uma comum extra. Em que qualidade ela vem? Opções: Boa · Perfeita · Gasta. Resposta literal: "Boa (Recomendado)". Registrada em `Docs/Design/decisoes.md`.
- D-055: Os números provisórios podem ficar assim? Qualidade gasta ×0,85 / boa ×1,0 / perfeita ×1,15; tema puxado pelas 2 tags mais usadas, com peso 2; emboscada de 2–3 inimigos a 5 m; dado rola 2,2 s e a carta chega 0,6 s depois. Opções: Aprovar como estão · Qualidade mais forte (×0,75 / ×1,25) · Dado mais rápido (1,5 s). Resposta literal: "Aprovar como estão (Recomendado)". Registrada em `Docs/Design/decisoes.md`.
- P-009: Efeito do 20 no coop. Resposta literal: "Decidir quando o coop voltar (Recomendado)". Registrada em `Docs/Design/decisoes.md`.

## 7. Pendentes e estacionamento
- Visto em Play pelas capturas automáticas (`Docs/Capturas/fase6/`, teste `Fase6Capturas`):
  - a carta flutua girando, com o brilho ciano;
  - o D20 para no número certo, de frente e em pé (o `DiceFaceTests` confere as 20 faces);
  - o 1 chama a emboscada;
  - a tiragem mostra as cartas novas.
- Bug achado nas capturas e corrigido: a revelação da carta nova não aparecia se a Arena fosse recarregada (teste `CartaEntregue_TocaARevelacao`).
- Depois desta fase entrou o passe de resolução (D-053, D-056 a D-059, `relatorio-passe-resolucao.md`), e as capturas já mostram o jogo em resolução cheia.
- P-009 (efeito do 20 no coop) fica para quando o coop voltar.
- Nada novo foi para o estacionamento.

## 8. Problemas conhecidos
- No coop (guardado), a revelação de uma carta nova com qualidade diferente de "boa" pode tocar duas vezes nos clientes (inventário e qualidade chegam em ordens diferentes); há uma trava de 2,5 s em `CardRevealFx`.
- O modo de jogo solo já entra com a partida iniciada (D-018), então as ondas e as cartas no chão começam sozinhas.
- O log de batchmode mostra um `NullReferenceException` do pacote Multiplayer Play Mode — não é do jogo.