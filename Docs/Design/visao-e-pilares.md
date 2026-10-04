# Visão e Pilares de Design — RPG Steampunk Mágico (nome provisório: a definir)

> Versão 1.2 · 2026-10-03
> Construído por entrevista. Tudo marcado `[DECIDIDO]` veio de respostas do dono. Não há trechos `[PROPOSTA]` pendentes nesta versão.
> Este documento é a base de design. Toda spec de feature deve declarar quais pilares serve e responder à pergunta-teste de cada um.

---

## 1. Visão `[DECIDIDO · dono · 2026-10-03]`

**Em uma frase**
Um RPG de ação 2.5D cooperativo num mundo onde engrenagem e feitiço são a mesma coisa, em que cada jogador descobre quem seu personagem é por meio de dilemas sem resposta certa e da sorte de cartas de tarô.

**Fantasia central**
"Ninguém me disse quem eu era; eu fui descobrindo." O personagem começa genérico e se revela com o tempo, sem tela de classe e sem nome de classe.

**Tom**
Aventura com sombra: esperançosa e divertida, com momentos pesados trazidos pelos dilemas.

**Jogadores**
Solo e coop de 2 a 4 jogadores, com a mesma história nos dois modos. O coop existe desde o início do projeto, e cada amigo constrói a própria identidade. No solo, um mentor ocupa os papéis que no coop são dos outros jogadores.

**História principal**
É a consequência do dilema inicial (§4). Tudo nasce do momento em que a carta proibida tenta o grupo.

**O que faz voltar a jogar**
1. Descobrir quem meu personagem está virando.
2. Caçar cartas e sentir a sorte do dado.
3. Jogar com amigos.

**O que o jogo não é**
- Não explica em menus: sem tutoriais longos, popups e telas de números.
- Não tem build de guia: não dá para seguir uma build pronta da internet.

**Mundo**
Steampunk mágico em fusão: tecnologia e magia são uma coisa só.

**Referência**
A filosofia de progressão de classes do Ragnarok Online, com identidade própria. Nada de nomes, textos ou arte de obras existentes.

---

## 2. Pilares (em ordem de prioridade) `[DECIDIDO · dono · 2026-10-03]`

Em conflito entre pilares, vence o de número menor.

### Pilar 1 — Identidade descoberta

**O jogador deve sentir:** "Ninguém me disse quem eu era; eu fui descobrindo."

**Por isso fazemos:**
- Habilidades e passivas aparecem aos poucos, ligadas ao caminho que o jogador segue.
- A aura muda de cor e forma conforme a identidade emergente.
- NPCs, descrições de cartas e quests dão pistas sutis do que o personagem está virando.

**Nunca fazemos:**
- Tela de escolha de classe.
- Nome de classe na tela durante o jogo. **Única exceção:** a retrospectiva final revela um nome para quem o personagem se tornou, como coroação da descoberta.
- Caminho sem volta: o jogador nunca fica preso numa identidade que não está construindo.

**Pergunta-teste:** "Esta feature ajuda o jogador a descobrir quem ele está se tornando, sem dizer isso diretamente?"

**Ganha de:** todos os outros pilares. Em especial, ganha da Sorte: o dado nunca impede o jogador de seguir o caminho que está construindo, só muda a força com que ele chega.

---

### Pilar 2 — A sorte como emoção

**O jogador deve sentir:** "Cada carta no chão é uma aposta, e eu prendo a respiração quando o dado rola."

**Por isso fazemos:**
- Pegar uma carta do chão dispara um D20, no estilo Baldur's Gate.
- O D20 decide raridade, qualidade e quantidade de cartas e itens.
- O tema da carta vem do caminho do jogador somado ao lugar onde ela foi encontrada.
- No coop, quem pega a carta rola o dado, e o grupo vê a rolagem.
- O 1 e o 20 são críticos com efeito extra (§3.3).

**Nunca fazemos:**
- Rolagem escondida: o dado sempre aparece e rola na frente do jogador e do grupo.
- Modificar o dado: nada altera a rolagem. Dado puro.
- Deixar o dado escolher o tema: ele decide só qualidade e quantidade, nunca o que a carta é.
- Deixar o azar travar o caminho: um resultado ruim nunca impede seguir a identidade.

**Pergunta-teste:** "Este momento cria a tensão de uma aposta, sem tirar do jogador o controle sobre quem ele é?"

---

### Pilar 3 — Escolhas com peso cinza

**O jogador deve sentir:** "Não havia resposta certa, e eu carrego o que escolhi."

**Por isso fazemos:**
- Um dilema inicial marcante, com uma carta proibida, abre uma de três rotas: Ordem, Poder ou Libertação (§4).
- Os dilemas existem em dois níveis: do grupo e pessoais.
- A rota pode mudar depois, mas com custo (§3.4).
- A aura reflete o peso moral das escolhas.

**Nunca fazemos:**
- Lado obviamente certo: nenhum dilema com resposta "de herói" clara.
- Escolha sem consequência: toda decisão importante deixa marca no mundo.
- Mudar de rota de graça: trocar de caminho sempre tem custo.
- Rótulo moral na tela: nunca "Você é Mau" ou um medidor de bem e mal. Só a aura e o mundo comunicam.

**Pergunta-teste:** "Esta decisão tem custo real para os dois lados, e o mundo vai lembrar dela?"

---

### Pilar 4 — Fusão de tecnologia e magia

**O jogador deve sentir:** "Engrenagens e feitiços são a mesma coisa aqui, e isso me fascina."

**Por isso fazemos:**
- Habilidades e cartas misturam o mecânico com o arcano no efeito, e não só na aparência.
- Inimigos, NPCs e cenários mostram a fusão no dia a dia do mundo.

**Nunca fazemos:**
- Steampunk só cosmético: engrenagens não são decoração; a mistura aparece nas mecânicas.
- Magia genérica: nada de magia de fantasia padrão sem traço mecânico.
- Copiar propriedade intelectual existente: visual e mundo com identidade própria.

**Pergunta-teste:** "Aqui, máquina e magia funcionam como uma coisa só, ou estão apenas lado a lado?"

---

## 3. Sistemas decididos `[DECIDIDO · dono · 2026-10-03]`

### 3.1 Comunicação e números
- **Mostrar pelo mundo** é a regra de comunicação (classificado como detalhe, não pilar): mundo, NPCs, descrições e aura no lugar de menus.
- **Quase nenhum número na tela.**
- **XP e nível existem, sem número.** O progresso aparece por quatro sinais:
  - a aura fica mais intensa (brilho, tamanho, detalhe);
  - o visual do personagem muda (roupas, marcas, efeitos);
  - o mundo reage (NPCs e inimigos tratam o personagem de outro jeito);
  - um momento de virada visual e sonoro a cada nível.
- **Afinidades** existem internamente e só aparecem no fim.
- **Retrospectiva final** mostra: a linha do tempo das escolhas que mais moldaram o personagem, as afinidades em número, o nome revelado (exceção do Pilar 1) e, no coop, como as identidades do grupo se completaram.

### 3.2 Aura
- Comunica **HP, energia, estados, rota moral e identidade emergente**.
- Acessibilidade: **sons** sutis (perigo, HP baixo) e **paleta alternativa** para daltonismo.
- No caso do traidor, a aura muda aos poucos e dá **pistas sutis** (§4).

### 3.3 Cartas de tarô
- Todas as cartas são de tarô e seguem a **estrutura real do tarô**.
- **Arcanos menores:** os 4 naipes são os tipos de carta:
  - **Skill:** habilidade.
  - **Item:** consumível, usado e some (poção, granada).
  - **Passiva:** efeito permanente enquanto equipada.
  - **Equipamento:** fica equipado.
- **Arcanos maiores:** **22, como no tarô**, cada um com versão própria do mundo. Metade pode sair do chão com um 20; a outra metade vem só de eventos da história. A carta proibida é um dos arcanos da história.
- **Obtenção:** cartas no chão. Ao pegar, rola-se um D20 puro, que decide raridade, qualidade e quantidade. O tema vem do caminho do jogador somado ao lugar.
- **Críticos:**
  - **1:** vem uma carta amaldiçoada (efeito negativo, ocupa espaço ou cobra ao usar) e o resultado atrai perigo (inimigos ou evento ruim). A maldição pode ser removida **com custo**, por um NPC, ritual ou máquina.
  - **20:** chance de carta única ou arcano maior e, no coop, um **efeito temporário** para o grupo.
- O tarô **não** tem papel na identidade do personagem; é a fonte das cartas.

### 3.4 Rotas e mudança de rota
- Três rotas, definidas por valores: **Ordem · Poder · Libertação**.
- A rota pode mudar, com três custos ao mesmo tempo:
  - **marca permanente na aura** com um traço da rota abandonada;
  - **perda ou enfraquecimento das cartas** ligadas à rota antiga;
  - **relações:** NPCs e facções da rota antiga passam a desconfiar ou hostilizar.

### 3.5 Combate
Meio-termo: tempo real, com a build pesando tanto quanto o reflexo.

### 3.6 Grupo e indivíduo
- O coop valoriza identidades diferentes que se completam (classificado como detalhe, não pilar).
- Um jogador pode tomar uma decisão que o grupo não quer. Nesse caso **o grupo se divide**: a decisão coletiva continua valendo, e a ação solo cria uma consequência paralela que respinga em todos.

---

## 4. O dilema inicial `[DECIDIDO · dono · 2026-10-03]`

Serve aos Pilares 3 (escolhas cinza), 1 (identidade), 2 (a carta é uma aposta de poder) e 4 (a carta é um objeto do mundo).

### 4.1 O evento
- **Quando:** depois de um ato curto de 15 a 30 minutos, para que o jogador já se importe com pessoas e lugares.
- **O que é:** uma promessa de poder combinada com uma lealdade rompida. O poder é a **carta proibida**, um arcano maior.
- **O que a carta dá:** **conhecimento**. Quem a aceita passa a ver segredos do mundo que os outros não veem: pistas, caminhos e verdades.
- **O dilema nunca é anunciado como "escolha de rota"** (Pilar 1). O jogador sente o peso; a rota se revela com o tempo.

### 4.2 As rotas a partir da carta
- **Aceitar** a carta leva à rota **Poder**.
- **Recusar** leva a **Ordem** ou **Libertação**, conforme a reação à carta:
  - entregá-la ou guardá-la para uma autoridade leva a **Ordem**;
  - tentar destruí-la ou fugir com o segredo leva a **Libertação**.

### 4.3 No coop
- A carta faz uma oferta **secreta** a cada jogador. Quem aceitar trai. É escolha, não papel sorteado.
- **A carta se divide:** todos que aceitarem recebem parte do conhecimento e viram **cúmplices**.
- Os cúmplices **se descobrem aos poucos:** a carta revela uns aos outros com o tempo, como pista.
- **Se ninguém aceitar:** não há traição, e **o mundo perde o equilíbrio da fusão**: máquina e magia começam a se rejeitar numa parte do mundo, uma tragédia que o conhecimento da carta poderia ter evitado.
- Em segredo, os traidores cumprem **missões da carta**, objetivos que só eles veem: **buscar segredos** e **tomar algo** (carta, item ou recompensa) antes dos outros.
- **Se o traidor ignorar as missões, a carta cobra:** o conhecimento enfraquece e a aura fica mais marcada.
- **O traidor pode contar ao grupo o que vê,** mas saber demais é uma pista contra ele.
- **O grupo descobre os traidores pelas pistas acumuladas** (aura, contradições, rastros). Não há evento fixo de revelação.
- **Traidor desmascarado antes do fim:** o grupo decide o destino dele (perdoar, punir ou expulsar). É um novo dilema cinza.
- **Se ninguém descobrir até o fim, os cúmplices se traem:** no clímax, cada um escolhe, **em segredo e ao mesmo tempo**, se trai os outros para ficar com tudo, como num dilema do prisioneiro. É a questão do egoísmo.
- **Proteção da amizade real:** nenhuma opção de desligar e nenhum aviso. A surpresa faz parte da experiência.

### 4.4 No solo
- A carta tenta **o jogador e o mentor**. Quem aceitar trai, e pode ser o próprio jogador.
- **O mentor também pode recusar.** A decisão dele fica **escondida**: o jogador só descobre pelas pistas, como no coop.
- Se ninguém aceitar, não há traição, e vale a mesma consequência do coop: o mundo perde o equilíbrio da fusão.

### 4.5 Ligação com os pilares
- A rota pode mudar depois, com os custos do §3.4. Isso vale para traidores, cúmplices e para quem recusou.
- A aura dá pistas sem rotular (Pilares 1 e 3).

---

## 5. Perguntas em aberto

As perguntas de design abertas na versão 1.1 foram respondidas e incorporadas nas seções acima. Restam:

1. **Nome do jogo.**
2. **A frase de visão diz "cooperativo"**, e o jogo também tem modo solo. Foi aprovada assim; vale reavaliar quando o nome do jogo for definido.
3. **Solo:** se o jogador e o mentor aceitarem a carta, eles viram cúmplices como no coop?
4. **Missões da carta:** até onde elas podem prejudicar o grupo?

Novas perguntas vão surgir no desenvolvimento. Elas são feitas ao dono, nunca decididas pelo agente (§7.3).

---

## 6. Impacto no prompt original do protótipo

As decisões de hoje mudam vários pontos do documento inicial para a Unity. Isso deve ser revisto antes de qualquer desenvolvimento:

| No prompt original | Com os pilares |
|---|---|
| Jogo implicitamente solo | Solo e coop de 2 a 4 desde o início, com impacto em arquitetura, rede e save |
| Lista de classes com nome (Arcanista, Engenheiro, Tecnomante…) | Sem nome de classe visível (Pilar 1). Nomes podem existir só internamente |
| Cartas obtidas por quest e level up | Cartas do chão, com D20 visível e críticos (Pilar 2) |
| Tipos de carta: skill, equipamento, passiva, quest/utilidade | Tarô real: naipes = skill, item consumível, passiva, equipamento; arcanos maiores raros |
| HUD com HP, mana, XP e level | Aura com som e paleta alternativa; XP e nível sem número |
| NPC de introdução e quests simples | Um ato curto que termina no dilema da carta proibida, com oferta secreta e cúmplices |
| Escolha de quest influencia afinidade | Rotas Ordem, Poder e Libertação, com custos para mudar |
| Afinidades só internas | Internas durante o jogo, reveladas em retrospectiva no fim |
| Loadout com slots fixos | Continua compatível |
| Mensagens como "Seu conhecimento sobre máquinas aumentou" | Rever: pode ser explícita demais para o Pilar 1 |

---

## 7. Primeiro protótipo: a arena `[DECIDIDO · dono · 2026-10-03]`

### 7.1 Objetivo
Uma **arena de testes** para ir decidindo mecânicas e interações. Não é o mundo do jogo: é a base sobre a qual o jogo será construído.

### 7.2 O que o protótipo prova
- **Coop local/LAN** jogável desde o protótipo (2 a 4 jogadores).
- **Carta + D20:** pegar carta no chão, ver o dado rolar, receber carta de tarô, equipar e usar.
- **Aura:** HP, energia e estados comunicados pela aura, com som e paleta alternativa.
- **Combate** de meio-termo, com inimigos simples.

**Fica de fora por enquanto:** mundo, história, ato curto, dilema da carta proibida, quests, NPCs narrativos, rotas e retrospectiva.

### 7.3 Regras de trabalho do protótipo
- Os pilares e as decisões deste documento valem acima de qualquer sugestão técnica.
- **Design é decisão do dono.** Qualquer dúvida sobre sensação, experiência, regra de jogo ou escopo é perguntada a ele, com opções e uma recomendação; nunca decidida pelo agente.
- Números de balanceamento do protótipo são provisórios, ficam em dados (ScriptableObjects) e são listados para aprovação.

---

## 8. Registro da entrevista (2026-10-03)

Respostas do dono em ordem, para consulta.

1. **Classe:** "não seleciona a classe… vamos seguindo os caminhos que o jogo vai disponibilizando… no início, umas três rotas… uma ação inicial muito impactante… do bem, do mal… o jogo vai revelando a classe com o passar do tempo, mas não tem um nome fixo para a classe."
2. **Cartas:** "uma carta de tarot onde através dela pegamos skills, itens e passivas."
3. **Tecnologia e magia:** fusão.
4. **Jogadores:** coop pequeno. Diante do conflito com "single player": coop desde o início.
5. **Dilema:** cinza.
6. **Rota:** pode mudar com custo.
7. **Tarô e identidade:** o tarô é só a fonte de cartas.
8. **Dilema no coop:** os dois níveis, grupo e pessoal.
9. **Obter cartas:** "igual Baldur's Gate… pegar a carta do chão… rodar um D20… decide a raridade, a qualidade… a quantidade."
10. **Números:** "quase nada, ele só vê HP e coisas assim através de aura."
11. **Combate:** meio-termo.
12. **O que a carta é:** junção do caminho do jogador com o lugar.
13. **Modificadores do dado:** dado puro.
14. **Dado no coop:** quem pega, rola.
15. **Aura:** energia e estados, rota moral e identidade emergente.
16. **Tom:** aventura com sombra.
17. **História:** consequência do dilema.
18. **Motivação:** descobrir quem viro, caçar cartas e sorte, jogar com amigos.
19. **Não é:** não explica em menus, não tem build de guia.
20. **Pilares:** identidade (pilar), mostrar pelo mundo (detalhe), escolhas cinza (pilar), sorte (pilar), grupo (detalhe), fusão (pilar).
21. **Prioridade:** 1 Identidade, 2 Sorte, 3 Escolhas cinza, 4 Fusão. Identidade vence Sorte.
22. **Frases e proibições:** como registradas em cada pilar.
23. **Evento do dilema:** junção de promessa de poder e lealdade rompida.
24. **Momento:** depois de um ato curto.
25. **Rotas:** valores; trio Ordem · Poder · Libertação.
26. **Grupo x indivíduo:** "pode ter dilemas de grupo e pessoal onde um pode tomar uma decisão que o grupo não quer, afetando o todo." Resolução: o grupo se divide.
27. **Quem trai:** "se for em coop, um dos players; se for single player, um mentor."
28. **Poder prometido:** uma carta proibida.
29. **Modos:** solo e coop.
30. **Virar traidor no coop:** escolha secreta.
31. **Ninguém aceita:** não há traição.
32. **Revelação:** mais tarde.
33. **Aura x segredo:** pistas sutis.
34. **Vários aceitam:** inicialmente "só o primeiro"; **substituído** no item 44.
35. **Rotas e a carta:** aceitar é Poder.
36. **Dilema no solo:** a carta tenta o jogador e o mentor.
37. **Ordem x Libertação:** como reage à carta.
38. **Traidor em segredo:** missões da carta.
39. **O que a carta dá:** conhecimento.
40. **Proteção da amizade:** nada; a surpresa é parte da experiência.
41. **Descoberta do traidor:** pistas acumuladas.
42. **Ignorar missões:** a carta cobra.
43. **Compartilhar o que vê:** pode, mas arrisca.
44. **Vários aceitam (revisão):** "ela se divide, lembra, então tem cúmplices sempre." Confirmado: a carta se divide; substitui o item 34.
45. **Cúmplices:** descobrem-se aos poucos.
46. **Ninguém descobre:** "uma questão de egoísmo." Esclarecido: os cúmplices se traem no fim.
47. **Recusa total:** o mundo perde algo.
48. **Mentor no solo:** pode recusar.
49. **D20:** 1 é falha crítica, 20 é crítico.
50. **Tarô:** estrutura real do tarô.
51. **Naipes:** tipos de carta.
52. **Arcanos maiores:** do chão com 20 e de eventos da história.
53. **Efeitos críticos:** 1 traz carta amaldiçoada e atrai perigo; 20 traz carta única e bônus ao grupo.
54. **Tamanho do coop:** 2 a 4.
55. **Custo de mudar rota:** marca na aura, perder cartas, relações.
56. **XP e nível:** existem, sem número.
57. **Afinidades em número:** só no fim.
58. **Aura acessível:** som e paleta alternativa.
59. **Frase de visão:** aprovada.
60. **Perguntas-teste:** as quatro aprovadas.
61. **"Por isso fazemos" dos Pilares 1 e 4:** aprovados.
62. **Item x equipamento:** item é consumível.
63. **Mentor no solo:** decisão escondida, descoberta por pistas.
64. **Missões da carta:** buscar segredos e tomar algo.
65. **Tragédia da recusa:** o equilíbrio da fusão.
66. **Clímax dos cúmplices:** secreto e simultâneo.
67. **Traidor desmascarado:** o grupo decide o destino.
68. **Sinais de progresso:** aura mais intensa, visual do personagem, reação do mundo, momento de virada.
69. **Retrospectiva:** comparação no grupo, nome revelado, linha do tempo, afinidades em número.
70. **Arcanos maiores:** 22, como no tarô.
71. **Nome no fim x Pilar 1:** exceção só no fim.
72. **Carta amaldiçoada:** sai com custo.
73. **Bônus do 20 no coop:** efeito temporário.
74. **Divisão dos arcanos:** metade do chão, metade da história.
75. **Coop no protótipo:** coop local/LAN.
76. **O que o protótipo prova:** carta + D20, aura, combate; "tenta criar uma arena no início para ir decidindo as mecânicas e interações, não precisa desenvolver o mundo ainda, vamos construir a base do jogo primeiro."
77. **Regras de desenvolvimento:** "esquece o Véu, vamos focar somente o desenvolvimento via Unity."
78. **Projeto Unity:** não existe ainda.
