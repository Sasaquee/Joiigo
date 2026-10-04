# Decisões de design

Registro das perguntas de design feitas ao Thiago durante o protótipo. A resposta aparece literal, como foi dada. O documento de autoridade é `visao-e-pilares.md`.

---

## Fase 0 · 2026-10-03

### D-001 · Itens consumíveis no loadout
- **Pergunta:** Onde ficam os itens consumíveis (naipe Item: poção, granada) no loadout e como são usados?
- **Opções:** Cinto próprio · Dividem os slots de skill · Uso imediato ao pegar · Mochila sem limite fixo
- **Resposta literal:** "Cinto próprio (Recomendado)"
- **Significa:** os consumíveis ficam numa faixa separada de slots (exemplo da pergunta: 2), cada um com uma tecla própria. Os 4 slots de skill continuam livres.
- **Ainda em aberto:** o número exato de slots do cinto e as teclas (P-001, P-002).

### D-002 · Mira do ataque básico
- **Pergunta:** Para onde vai o ataque básico?
- **Opções:** Mira pelo mouse · Direção do movimento · Alvo automático
- **Resposta literal:** "Mira pelo mouse (Recomendado)"
- **Significa:** o ataque vai para onde o cursor aponta, independente da direção em que o personagem anda.

### D-003 · HP zero no coop
- **Pergunta:** O que acontece quando um jogador chega a zero de HP no coop?
- **Opções:** Cai e é levantado · Volta num ponto · Observa até o fim
- **Resposta literal:** "Cai e é levantado (Recomendado)"
- **Significa:** o jogador fica caído por um tempo e um aliado pode levantá-lo ao chegar perto. Se ninguém vier, ele volta no spawn.
- **Ainda em aberto:** como o aliado levanta, o que acontece se todos caírem e o tempo de queda (P-003 a P-005).

### D-004 · Câmera inicial
- **Pergunta:** Com que câmera inicial você quer testar?
- **Opções:** Média · Próxima · Alta, tática
- **Resposta literal:** "Média (Recomendado)"
- **Significa:** valores iniciais de cerca de 14 m de distância, 50° de inclinação e giro diagonal de 30°. Todos ficam ajustáveis no Inspector, e cada jogador tem a própria câmera.

---

## Fase 2 · 2026-10-03

### D-005 · Para onde o personagem fica virado
- **Pergunta:** Para onde o personagem fica virado enquanto anda?
- **Opções:** Sempre para o mouse · Para o movimento · Movimento, mira ao atacar
- **Resposta literal:** "Sempre para o mouse (Recomendado)"
- **Significa:** o corpo gira sempre para o cursor, e dá para andar de costas ou de lado.

### D-006 · Referência do WASD
- **Pergunta:** O W anda para onde?
- **Opções:** Relativo à tela · Relativo ao mundo
- **Resposta literal:** "Relativo à tela (Recomendado)"
- **Significa:** W sobe na tela e D vai para a direita da tela, seja qual for o giro da câmera.

### D-007 · Resposta do movimento
- **Pergunta:** Como o movimento responde ao teclado?
- **Opções:** Quase instantâneo · Com inércia leve · Totalmente instantâneo
- **Resposta literal:** "Quase instantâneo (Recomendado)"
- **Significa:** o personagem arranca e para quase na hora, com aceleração curta. Os valores ficam em `MovementSettings`.

### D-008 · Organização da arena
- **Pergunta:** Como as áreas da arena se organizam?
- **Opções:** Anel em volta do centro · Corredor em sequência · Pátio com zonas nos cantos
- **Resposta literal:** "Anel em volta do centro (Recomendado)"
- **Significa:** uma plataforma circular de combate no centro, com a entrada dos jogadores de um lado, três portões-máquina de inimigos na borda e uma alcova lateral para testar cartas.

---

## Fase 3 · 2026-10-04

### D-009 · Resposta do movimento no cliente
- **Pergunta:** Na sua tela, o seu personagem responde na hora ou com um pequeno atraso?
- **Opções:** Previsão local · Autoridade pura
- **Resposta literal:** "Previsão local (Recomendado)"
- **Significa:** o cliente move o próprio personagem na hora, o host continua sendo a fonte da verdade e corrige quando há diferença.

### D-010 · Quem é quem antes da aura
- **Pergunta:** Até a aura chegar, como cada um sabe quem é quem?
- **Opções:** Marca só no seu · Cor por jogador · Nenhuma marca
- **Resposta literal:** "Marca só no seu (Recomendado)"
- **Significa:** um anel discreto no chão sob o próprio personagem, visível só na própria tela. Não existe cor por jogador.

### D-011 · Entrada no meio da partida
- **Pergunta:** Alguém pode entrar no meio da partida?
- **Opções:** A qualquer momento · Só antes de começar
- **Resposta literal:** "Só antes de começar"
- **Significa:** existe um momento de largada. Depois dele, ninguém novo entra (exceção em D-014).

### D-012 · IP na tela do host
- **Pergunta:** A tela de Hospedar mostra o IP do host?
- **Opções:** Mostra o IP · Não mostra
- **Resposta literal:** "Mostra o IP (Recomendado)"
- **Significa:** depois de hospedar, o IP local aparece pequeno num canto.

### D-013 · Sala de espera e largada
- **Pergunta:** Como funciona a espera antes de começar e quem dá a largada?
- **Opções:** Sala no mundo, alavanca · Sala no mundo, botão na tela · Tela de sala
- **Resposta literal:** "Sala no mundo, alavanca (Recomendado)"
- **Significa:** quem entra já aparece no spawn e pode andar. Os portões-máquina ficam parados e apagados. O host puxa uma alavanca-máquina no spawn com E, e os portões acordam.

### D-014 · Reconexão depois da largada
- **Pergunta:** Se um jogador cai da conexão depois da largada, ele pode voltar?
- **Opções:** Pode voltar · Não pode
- **Resposta literal:** "Pode voltar (Recomendado)"
- **Significa:** quem já estava na partida entra de novo pelo IP e volta no spawn. Alguém novo continua barrado.

---

## Perguntas pendentes (feitas na fase em que travarem)

| Id | Pergunta | Fase |
|---|---|---|
| P-001 | Quantos slots tem o cinto de consumíveis | 5 |
| P-002 | Teclas do cinto e dos demais atalhos fora do prompt | 5 |
| P-003 | Como o aliado levanta quem caiu (chegar perto, segurar uma tecla, tempo) | 4 |
| P-004 | O que acontece se todos os jogadores caírem | 4 |
| P-005 | Quanto tempo o jogador fica caído antes de voltar ao spawn | 4 (número, em SO) |
| P-006 | Nomes e comportamentos dos 3 inimigos | 4 |
| P-007 | Como a energia se recupera | 5 |
| P-008 | Como a carta do chão se mostra no mundo antes de ser pega | 6 |
| P-009 | Efeito temporário do 20 no coop | 6 |
| P-010 | Forma e linguagem visual da aura | 7 |
