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
