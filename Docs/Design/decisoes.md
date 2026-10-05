# Decisões de design

Registro das perguntas de design feitas ao dono durante o protótipo. A resposta aparece literal, como foi dada. O documento de autoridade é `visao-e-pilares.md`.

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

## Mudança de rumo + Fase 4 · 2026-10-04

### D-015 · Ordem de trabalho
- **Pergunta:** Em que ordem vamos avançar agora que o coop ficou de lado?
- **Opções:** Mecânicas + visual juntos · Ambientação primeiro · Mecânicas primeiro
- **Resposta literal:** "Mecânicas + visual juntos (Recomendado)"
- **Significa:** as Fases 4 a 7 seguem na ordem, e cada uma entrega o visual final do que toca. O coop fica guardado, sem ser removido.

### D-016 · Estilo visual
- **Pergunta:** Qual estilo visual a ambientação deve seguir?
- **Opções:** Estilizado low-poly · Toon com contorno · Semi-realista sombrio
- **Resposta literal:** "o semi realista sombrio ficaria bom, mas se tiver dificuldade para fazer por script abra o blender pelo meu pc e use meu pc para modelar ou alguma ferramenta q preferir"
- **Significa:** semi-realista sombrio. A ferramenta escolhida foi script no Blender com texturas PBR CC0 (Poly Haven) e pós-processamento na Unity.

### D-017 · Clima de luz
- **Pergunta:** Qual clima de luz a instalação deve ter?
- **Opções:** Noite arcana com fornalhas · Entardecer âmbar · Subsolo industrial
- **Resposta literal:** "Noite arcana com fornalhas (Recomendado)"
- **Significa:** ambiente escuro, com a luz vindo dos cristais (ciano) e das fornalhas e brasas (laranja), e vapor no ar.

### D-018 · Início do jogo com o coop de lado
- **Pergunta:** Com o coop de lado, o que acontece quando você dá Play ou abre o jogo?
- **Opções:** Entra direto, solo · Mantém a tela de conexão
- **Resposta literal:** "Entra direto, solo (Recomendado)"
- **Significa:** o jogo começa na arena com o jogador como host sozinho e a partida já iniciada. A tela de conexão só aparece com uma tecla de debug.

### D-019 · Texturas
- **Pergunta:** Posso baixar texturas PBR CC0 do Poly Haven?
- **Resposta literal:** "Pode baixar 1K (Recomendado)"

### D-020 · Ataque básico
- **Pergunta:** Qual é o ataque básico do personagem?
- **Opções:** Golpe curto em arco · Disparo leve · Os dois
- **Resposta literal:** "Golpe curto em arco (Recomendado)"
- **Significa:** golpe corpo a corpo num arco à frente, na direção do mouse. O ataque à distância fica para as cartas.

### D-021 · Tipo de dano
- **Pergunta:** Como funciona o tipo de dano?
- **Opções:** Mistura mecânico/arcano · Dois tipos separados · Tipo único
- **Resposta literal:** "Mistura mecânico/arcano (Recomendado)"
- **Significa:** todo ataque tem uma proporção mecânico/arcano, e cada inimigo resiste a uma parte. O constructo corta a parte arcana.

### D-022 · HP zero jogando sozinho
- **Pergunta:** Jogando sozinho, o que acontece quando o seu HP chega a zero?
- **Opções:** Cai e volta no spawn · Reinicia a arena
- **Resposta literal:** "Cai e volta no spawn (Recomendado)"
- **Significa:** é a mesma regra do coop (D-003): o jogador fica caído alguns segundos e, sem ninguém para levantar, volta no spawn.

### D-023 · Aparição dos inimigos
- **Pergunta:** Como os inimigos aparecem na arena depois da largada?
- **Opções:** Ondas com pausa · Fluxo contínuo · Só por debug
- **Resposta literal:** "Ondas com pausa (Recomendado)"

### D-024 · Aviso antes do ataque
- **Pergunta:** Os inimigos avisam antes de atacar?
- **Opções:** Aviso curto visual · Sem aviso
- **Resposta literal:** "Aviso curto visual (Recomendado)"
- **Significa:** o inimigo carrega por um instante antes de atacar, com o cristal brilhando e a peça recuando.

### D-025 · Ataque do drone
- **Pergunta:** Como é o ataque do drone à distância?
- **Opções:** Projétil lento esquivável · Raio instantâneo com mira
- **Resposta literal:** "Projétil lento esquivável (Recomendado)"

### D-026 · Morte do inimigo
- **Pergunta:** O que acontece com o inimigo quando ele morre?
- **Opções:** Desmonta e o cristal apaga · Explode em vapor · Some com brilho
- **Resposta literal:** "Desmonta e o cristal apaga (Recomendado)"
- **Significa:** as peças se soltam com faíscas e vapor, o cristal escurece e os restos somem depois de alguns segundos.

---

## Fase 5 · 2026-10-04

### D-027 · Tela de loadout
- **Pergunta:** Como o jogador vê e troca as cartas equipadas?
- **Opções:** Tela de tiragem · Só na mesa da alcova · Equipa ao pegar
- **Resposta literal:** "Tela de tirágem (Recomendado)"
- **Significa:** uma tecla abre as cartas dispostas como uma tiragem de tarô. O jogador arrasta as cartas entre os espaços e o jogo continua rodando por trás. Não há texto longo, só a ilustração e um nome curto.

### D-028 · Skills na tela durante o combate
- **Pergunta:** Como você sabe quais skills tem e quando voltam da recarga?
- **Opções:** 4 cartas pequenas no canto · Nada na tela · Ícones mínimos
- **Resposta literal:** "4 cartas pequenas no canto (Recomendado)"
- **Significa:** a recarga aparece como uma sombra que desce sobre a ilustração, sem número.

### D-029 · Cinto de consumíveis (resolve P-001 e P-002)
- **Pergunta:** Quantos espaços tem o cinto e quais teclas?
- **Opções:** 2 espaços, Q e R · 3 espaços, Q, R e F · 2 espaços, 5 e 6
- **Resposta literal:** "3 espaços Q, E, R"

### D-030 · Recuperação de energia (resolve P-007)
- **Pergunta:** Como a energia se recupera?
- **Opções:** Golpes carregam + lenta · Só com o tempo · Só por golpes
- **Resposta literal:** "Golpes carregam + lenta (Recomendado)"

### D-031 · Conflito da tecla E
- **Pergunta:** O E era "interagir" e passou a ser do cinto. Como resolvemos?
- **Opções:** Interagir vai para F · E faz os dois por contexto · Cinto com Q, R e F
- **Resposta literal:** "Interagir vai para F (Recomendado)"
- **Significa:** Q, E e R são só do cinto. F pega cartas e aciona a alavanca e a mesa.

### D-032 · Tecla da tiragem
- **Resposta literal:** "Tab (Recomendado)"

### D-033 · Cartas iniciais
- **Pergunta:** Com que cartas o personagem começa?
- **Opções:** Nenhuma, só o golpe · Uma skill neutra
- **Resposta literal:** "Nenhuma, só o golpe (Recomendado)"
- **Significa:** todas as cartas vêm do chão (Fase 6). Até lá, os testes usam a tecla de debug "dar carta".

### D-034 · Visual das cartas
- **Pergunta:** Como as cartas de tarô devem parecer?
- **Opções:** Ilustração 3D em moldura · Símbolo gráfico
- **Resposta literal:** "Ilustração 3D em moldura (Recomendado)"
- **Significa:** cada carta mostra um objeto modelado e renderizado no Blender, numa moldura de latão gasto, com numeral romano e nome curto.

### D-035 · Conteúdo das cartas
- **Pergunta:** Aprova o conjunto de 14 cartas de `Docs/Design/cartas-prototipo.md`?
- **Resposta literal:** "Aprovo como está (Recomendado)"

### D-036 · Naipes → tipos
- **Pergunta:** Espadas = skill, Copas = consumível, Paus = passiva, Ouros = equipamento?
- **Resposta literal:** "Aprovo (Recomendado)"

### D-037 · Novo estilo das cartas (substitui D-034)
- **Origem:** o dono mandou uma imagem de referência (cartas pretas com traço dourado) depois de ver a primeira versão em 3D.
- **Resposta literal:** "refaça o modelo das cartas nesse estilo, a carta da mto feia"
- **Significa:** faces 2D de traço dourado sobre preto, com moldura ornamentada (cantos com arcos e contas, faixas de título e nome), estrelas, constelações e raios atrás do objeto. O objeto de cada carta é desenhado em linha. Os cristais levam um toque ciano (violeta na amaldiçoada) como assinatura arcana (Pilar 4). Os arcanos maiores têm cristais nos cantos. Os símbolos da referência não foram copiados (§1: identidade própria). Gerado por `Tools/Cards/card_art.py`.

### D-038 · Custo de vida da carta amaldiçoada
- **Pergunta:** A Lâmina Sedenta cobra 8 de vida a cada uso. O que acontece se o jogador usar com pouca vida?
- **Opções:** Pode derrubar · Não deixa usar · Deixa com 1 de vida
- **Resposta literal:** "Pode derrubar (Recomendado)"

---

## Passe visual · 2026-10-04

### D-039 · Estilo pixelado (substitui o semi-realista de D-016)
- **Pergunta:** Qual "pixelado" você quer?
- **Opções:** 3D pixelado · HD-2D (sprites em cenário 3D) · 2D pixel art puro
- **Resposta literal:** "3D pixelado (Recomendado)"
- **Significa:** o jogo continua 3D com a câmera diagonal, renderizado em baixa resolução com pixels nítidos, contorno, paleta limitada e luz em faixas. A interface continua nítida.

### D-040 · Personagem
- **Pergunta:** Como deve ser o personagem?
- **Opções:** Andarilho encapuzado · Operário da instalação · Autômato vazio
- **Resposta literal:** "Andarilho encapuzado (Recomendado)"
- **Significa:** manto gasto, capuz, máscara simples de latão, bolsas e cinto de ferramentas. É neutro e igual para todos (Pilar 1).

### D-041 · Cartas em pixel art
- **Pergunta:** As cartas devem seguir o pixel também?
- **Opções:** Pixel art dourada · Traço fino mais elaborado
- **Resposta literal:** "Pixel art dourada (Recomendado)"
- **Significa:** o estilo dourado sobre preto de D-037, redesenhado em pixel art mais elaborada, com brilho animado no cristal.

### D-042 · Efeitos e ambientação
- **Pergunta:** Onde você quer mais efeito primeiro?
- **Resposta literal:** "faça em tudo pai, principalmente na ambientação, deixe mais vivo, parecendo uma cidade steampunk com magia, não é para nenhum agente do sonnet modelar, se quiser criar agentes do opus para modelar varios elementos e peças ao mesmo tempo fique a vontade"
- **Significa:** efeitos de combate, de skills, de ambiente e de revelação de carta. A arena passa a ser uma praça cercada por uma cidade steampunk-mágica viva (prédios, chaminés, canos, vapor, luzes, movimento no céu). Modelagem só por agentes Opus; código pode ser Sonnet.

## Equipe de agentes · 2026-10-04

### D-043 · Arte no DeepSeek harness (resolve P-011)
- **Pergunta:** No DeepSeek harness (API NVIDIA), o `kimi-k3` pode modelar e fazer arte no lugar do Opus?
- **Resposta literal:** "ah e ja libera na p011 ser esse kimi k3, lembra q o modelo tem que ser competente para tal"
- **Significa:** modelagem e arte podem ser feitas pelo Opus (Claude Code) ou pelo `moonshotai/kimi-k3` (DeepSeek harness). Só modelos desse nível: nenhum modelo menor ou reserva faz arte (sem Sonnet, sem `glm-5.3`, sem flash). Se o `kimi-k3` estiver indisponível, a arte espera.

### D-044 · Modos de trabalho e arte sempre no melhor modelo (substitui D-043)
- **Pedido:** usar o Claude junto com o DeepSeek harness, de forma flexível.
- **Resposta literal:** "boa faz isso, só n deixa apenas isso, as vezes vou usar apenas o claude, e as vezes tera tarefas mais simples, onde eu possa usar somente o harness enfim deixa bem flexivel, com a unica exceção de deixar a llm mais competente de todas para fazer artes, modelagem, graficos, e etc, pois o jogo precisa ser bonito"
- **Significa:** três modos (só Claude, só harness, híbrido), à escolha do dono em cada tarefa. Arte, modelagem, gráficos, shaders, efeitos e UI visual ficam sempre com o modelo mais competente disponível (hoje o Claude Opus), em qualquer modo. A liberação do `kimi-k3` para arte (D-043) deixa de valer enquanto ele não for o melhor disponível.

### D-045 · Luz da arena com a cidade
- **Pergunta:** Qual luz a arena deve ter? (antes, mais sombrio e azul × depois, mais legível; imagem em `Docs/Capturas/passe-visual/luz_antes_depois.png`)
- **Opções:** Depois, mais legível · Antes, mais sombrio · Meio-termo
- **Resposta literal:** "Depois, mais legível (Recomendado)"
- **Significa:** mantém a noite arcana com fornalhas (D-017), com o chão, os postes e a cidade legíveis; janelas em âmbar e cristais em ciano, sem estourar em branco.

---

## Fase 6 · 2026-10-04

### D-046 · Carta no chão (resolve P-008)
- **Pergunta:** Como a carta aparece no chão da arena antes de ser pega (com F)?
- **Opções:** Flutuando de pé · Caída no chão · Relicário mecânico
- **Resposta literal:** "Flutuando de pé (Recomendado)"
- **Significa:** a carta, de verso dourado, flutua baixa e gira devagar, com um brilho ciano subindo. O verso esconde o que ela é.

### D-047 · Rolagem do D20
- **Pergunta:** Como o D20 rola na frente do jogador ao pegar a carta?
- **Opções:** Dado grande na tela · Dado no mundo · Dado sobre a cabeça
- **Resposta literal:** "Dado grande na tela (Recomendado)"
- **Significa:** um D20 de latão e cristal, em 3D pixelado, rola grande no centro da tela por ~2 s e para no número, com o jogo seguindo por trás. No coop, todos veem o mesmo dado.

### D-048 · Tabela do D20
- **Pergunta:** Como a tabela do D20 decide raridade, qualidade e quantidade?
- **Opções:** Três eixos simples · Sem qualidade por enquanto · Quero ajustar a tabela
- **Resposta literal:** "Três eixos simples (Recomendado)"
- **Significa:** 1: amaldiçoada + perigo · 2–6: comum gasta · 7–11: comum boa · 12–15: incomum boa · 16–18: incomum perfeita · 19: incomum perfeita + 1 comum · 20: arcano maior ou única. A qualidade (gasta, boa, perfeita) muda um pouco os números da carta e aparece só na moldura. As faixas e os números ficam em dados (provisórios).

### D-049 · Perigo do crítico 1
- **Pergunta:** No 1, além da carta amaldiçoada, que perigo o resultado atrai?
- **Opções:** Emboscada · Onda antecipada · A carta morde
- **Resposta literal:** "Emboscada (Recomendado)"
- **Significa:** 2 ou 3 inimigos surgem em volta do jogador na hora.

### D-050 · Origem das cartas na arena
- **Pergunta:** De onde surgem as cartas no chão da arena?
- **Opções:** Fim de cada onda · Inimigos derrubam · Pontos fixos da arena
- **Resposta literal:** "Fim de cada onda (Recomendado)"
- **Significa:** quando uma onda termina, uma carta aparece flutuando no centro da arena.

### D-051 · Carta repetida
- **Pergunta:** Se o dado sortear uma carta que o jogador já tem, o que acontece?
- **Opções:** Sorteia outra · Melhora a que já tem · Pode ter cópias
- **Resposta literal:** "Melhora a que já tem"
- **Significa:** a repetida sobe a qualidade da carta que ele já tem (gasta → boa → perfeita).

### D-052 · Repetida de carta já perfeita
- **Pergunta:** E se a que ele tem já for perfeita?
- **Opções:** Sorteia outra · Nada acontece · Cópia extra
- **Resposta literal:** "Cópia extra"
- **Significa:** ganha uma segunda cópia da carta perfeita (por exemplo, duas granadas no cinto).

### D-054 · Qualidade da comum extra do 19
- **Pergunta:** No 19, além da incomum perfeita, vem uma comum extra. Em que qualidade ela vem?
- **Opções:** Boa · Perfeita · Gasta
- **Resposta literal:** "Boa (Recomendado)"
- **Significa:** a comum extra do 19 vem boa, como na faixa 7–11 (`DiceSettings`, faixa 19, `extraQuality`).

### D-055 · Números provisórios da Fase 6
- **Pergunta:** Os números provisórios podem ficar assim? Qualidade gasta ×0,85 / boa ×1,0 / perfeita ×1,15; tema puxado pelas 2 tags mais usadas, com peso 2; emboscada de 2–3 inimigos a 5 m; dado rola 2,2 s e a carta chega 0,6 s depois.
- **Opções:** Aprovar como estão · Qualidade mais forte (×0,75 / ×1,25) · Dado mais rápido (1,5 s)
- **Resposta literal:** "Aprovar como estão (Recomendado)"
- **Significa:** os números ficam como estão em `CardsSettings` e `DiceSettings` e podem ser ajustados depois de jogar.

### P-009 · Efeito do 20 no coop
- **Resposta literal:** "Decidir quando o coop voltar (Recomendado)"
- **Significa:** por enquanto o 20 dá só o arcano maior ou a carta única; o efeito para o grupo continua pendente.

## Roteiro · 2026-10-04

### D-053 · Etapa de resolução
- **Pedido do dono (literal):** "adicione uma etapa no desenvovilmento do gamer para melhorar a resolução do jogo em tudo, ta muito pixelado"
- **Significa:** entra no roteiro um **passe de resolução** logo depois de fechar a Fase 6 e antes da Fase 7. Ele revê a nitidez de tudo: mundo 3D, personagem, inimigos, efeitos, cartas, D20 e interface. **Quanto** sobe a resolução e o que muda no estilo de D-039 ainda não foi decidido (P-012, perguntar no início da etapa com imagens de comparação).

## Passe de resolução · 2026-10-05

Pedido do dono antes das perguntas: "vamos focar em melhorar a qualidade de imagem do jogo, por exemplo o dado esta em baixissima resolução, o mesmo serve para o jogo em si e tudo mais". Comparações em `Docs/Capturas/resolucao/`.

### D-056 · Resolução do mundo (resolve P-012; muda D-039)
- **Pergunta:** Em quanto o mundo 3D deve ser desenhado? (hoje 640x360 ampliado 3x)
- **Opções:** Tela cheia · 1280x720 · 960x540
- **Resposta literal:** "Tela cheia (Recomendado)"
- **Significa:** o mundo é renderizado na resolução da tela, sem ampliar pixel. Continua 3D com contorno; deixa de ser "3D pixelado".

### D-057 · Luz em faixas
- **Pergunta:** A luz em faixas e o pontilhado continuam?
- **Opções:** Faixas suaves · Manter como está · Tirar as faixas
- **Resposta literal:** "Faixas suaves (Recomendado)"
- **Significa:** a luz continua em degraus, mas com mais degraus e sem o pontilhado.

### D-058 · D20 na tela
- **Pergunta:** O D20 grande na tela fica como?
- **Opções:** Tela cheia, suave · Acompanha o mundo
- **Resposta literal:** "Tela cheia, suave (Recomendado)"
- **Significa:** o dado é desenhado no tamanho real em que aparece, com bordas suaves.

### D-059 · Cartas em alta resolução (muda D-041)
- **Pergunta:** As cartas são pixel art de 112x192 ampliada. O que fazemos?
- **Opções:** Redesenhar em alta · Manter pixel art · Voltar ao dourado HD
- **Resposta literal:** "Redesenhar em alta (Recomendado)"
- **Significa:** mesmo desenho e composição de cada carta (e do verso), refeitos com detalhe em resolução alta e bordas limpas. Arte só pelo Opus (D-044).

---

## Perguntas pendentes (feitas na fase em que travarem)

| Id | Pergunta | Fase |
|---|---|---|
| ~~P-001~~ | Slots do cinto | resolvida em D-029 |
| ~~P-002~~ | Teclas do cinto | resolvida em D-029, D-031, D-032 |
| P-003 | Como o aliado levanta quem caiu (chegar perto, segurar uma tecla, tempo) | quando o coop voltar |
| P-004 | O que acontece se todos os jogadores caírem | quando o coop voltar |
| ~~P-005~~ | Tempo caído: número provisório 5 s em `CombatSettings` (para aprovar) | resolvida como número |
| P-006 | Nomes finais dos 3 inimigos (comportamentos decididos em D-023 a D-026; ids internos provisórios: automato, drone, constructo) | antes do conteúdo final |
| ~~P-007~~ | Recuperação da energia | resolvida em D-030 |
| ~~P-008~~ | Como a carta do chão se mostra no mundo | resolvida em D-046 |
| P-009 | Efeito temporário do 20 no coop | quando o coop voltar |
| P-010 | Forma e linguagem visual da aura | 7 |
| ~~P-012~~ | Passe de resolução (D-053): para quanto sobe o mundo (hoje 640x360: 960x540, 1280x720 ou resolução da tela); se o contorno, a luz em faixas e a paleta de D-039 ficam; se as cartas em pixel art (D-041) ganham versão em mais resolução | resolvida em D-056 a D-059 |
| ~~P-011~~ | Arte com `kimi-k3` no DeepSeek harness | resolvida em D-043 |
