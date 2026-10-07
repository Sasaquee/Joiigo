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

## Fase 7 · Aura · 2026-10-05

### D-060 · Forma da aura (resolve P-010)
- **Pergunta:** A aura vai ser o único jeito de ver HP, energia e estados. Que forma ela tem em volta do andarilho?
- **Opções:** Círculo aos pés · Chama na silhueta · Peças em órbita
- **Resposta literal:** "Círculo aos pés (Recomendado)"
- **Significa:** um círculo de latão com runas em cristal no chão, com luz subindo em volta do corpo.

### D-061 · HP na aura
- **Pergunta:** Como a aura mostra o HP, sem número nem barra?
- **Opções:** Força e tamanho · Cor · Arco que se fecha
- **Resposta literal:** "Força e tamanho (Recomendado)"
- **Significa:** HP cheio deixa a aura forte e larga; com pouco HP ela encolhe e apaga, e perto do zero falha como lâmpada. Não depende de cor.

### D-062 · Energia na aura
- **Pergunta:** E a energia, que paga as skills das cartas?
- **Opções:** Faíscas na aura · Lanterna do andarilho · Anel interno
- **Resposta literal:** "Faíscas na aura (Recomendado)"
- **Significa:** mais energia, mais faíscas subindo e mais rápidas; com energia cheia, o cristal das runas acende inteiro.

### D-063 · Estados na aura
- **Pergunta:** Como os estados aparecem na aura? (escudo, maldição cobrando vida, caído, bônus de dano depois de levar golpe)
- **Opções:** Cada um com um sinal · Só os fortes · Por cor
- **Resposta literal:** "Cada um com um sinal (Recomendado)"
- **Significa:** escudo = casca de cristal; maldição = fiapos violeta; caído = aura apagada, só brasa; bônus de dano = runas em brasa laranja. Cada sinal tem forma própria além da cor.

### D-064 · Sons da aura
- **Pergunta:** Que sons de aviso a aura faz?
- **Opções:** Batimento + chiado · Só batimento · Cristal desafinando
- **Resposta literal:** "Batimento + chiado (Recomendado)"
- **Significa:** batimento grave com HP baixo, que acelera perto do zero; chiado curto de vapor quando um golpe inimigo está vindo no jogador. Baixos; somem com HP cheio.

### D-065 · Paleta alternativa
- **Pergunta:** Como fica a paleta alternativa para daltônicos (ligada por tecla de debug no protótipo)?
- **Opções:** Azul e amarelo · Alto contraste · Três paletas
- **Resposta literal:** "Azul e amarelo (Recomendado)"
- **Significa:** ciano, violeta e laranja viram azul forte, amarelo e branco. A cor só reforça a forma.

### D-066 · Marca do seu personagem com a aura
- **Pergunta:** Com o círculo da aura aos pés de todos, como fica o anel branco que marca o seu personagem (D-010)?
- **Opções:** Anel por fora · Sua aura mais forte · Tirar o anel
- **Resposta literal:** "Anel por fora (Recomendado)"
- **Significa:** o anel branco continua, fino, por fora do círculo da aura, só na sua tela.

### D-067 · Aviso de energia cheia (resolve P-013)
- **Pergunta:** A energia hoje aparece como faíscas na aura (D-062) e o dono não percebeu quando estava cheia no playtest. O que fazemos?
- **Opções:** Pulso ao encher · Faíscas mais fortes · Barra discreta no chão · Manter como está
- **Resposta literal:** "Pulso ao encher (Recommended)"
- **Significa:** quando a energia chega ao máximo, a aura dá um pulso de luz e um som curto, uma vez. As faíscas continuam como em D-062. Fica para uma fase de ajuste da aura (arte e som só com o Opus, D-044).

### D-068 · O 1 do D20 mais teatral (resolve P-014)
- **Pergunta:** O dono tirou 1 (carta amaldiçoada + emboscada, D-048 e D-049) e não sentiu desastre. Como o 1 deve ser?
- **Opções:** Mais teatral · Emboscada maior · Carta amaldiçoada mais cara · Manter como está
- **Resposta literal:** "Mais teatral (Recommended)"
- **Significa:** a regra do 1 não muda (mesma carta amaldiçoada, mesmo número de inimigos da emboscada). Muda a apresentação: dado vermelho, tela escurece, som grave e os inimigos surgem de forma visível. Arte, efeito e som só com o Opus (D-044).

### D-069 · Reforçar o cristal no mundo (resolve P-015)
- **Pergunta:** O dono descreveu o mundo só como steampunk, sem perceber a magia (Pilar 4). Quer reforçar a magia?
- **Opções:** Reforçar o cristal · Magia só nas cartas e na aura · Mudar a paleta do mundo · Deixar para depois do coop
- **Resposta literal:** "Reforçar o cristal (Recommended)"
- **Significa:** mais brilho de cristal no mundo (veios luminosos nas máquinas, poeira mágica no ar, cristais das torres pulsando), sem trocar a cidade nem a paleta de D-039 a D-045. Arte e luz só com o Opus (D-044).

### D-073 · A arena se abre para a cidade (resolve P-016)
- **Contexto:** o dono respondeu, na pergunta sobre onde reforçar o cristal, que "a ambientação da arena podia ser mais para dentro da cidade, deixar o mapa mais rico, não só um círculo". Três perguntas de alcance:
  1. O que muda para o jogador? Opções: Só o cenário ao redor · A arena se abre para ruas · Várias áreas ligadas. **Resposta literal:** "A arena se abre para ruas".
  2. Forma do mapa. Opções: Círculo com obstáculos · Contorno irregular · Só o visual da borda. **Resposta literal (Outro):** "a cidade e a arena, e o limite sao predios que nao deixam voce passar".
  3. Quando? Opções: Antes de mais playtest · Depois de mais playtest · Fase própria do mapa. **Resposta literal:** "Antes de mais playtest".
- **Significa:** o mapa deixa de ser um círculo fechado por um muro baixo. Parte da cidade vira área jogável (ruas e praças ligadas à arena); o limite do mapa são **prédios que bloqueiam a passagem** do jogador e dos inimigos. Será feito **antes de mais playtest**, como um passe do mapa (pedido do dono, como o passe de resolução em D-053). Forma, tamanho, origem dos inimigos e câmera são decididos nas perguntas do passe. Isso estende o que o protótipo previa ("uma arena de testes, não o mundo", `prompt-prototipo.md` §2) por decisão do dono. O cristal nos postes da praça (opção da pergunta original) fica em espera.

### D-074 · Forma do mapa: praça e avenidas
- **Pergunta:** Como a arena se abre para a cidade? Hoje é uma praça redonda de ~26 m, com 3 portões e uma plataforma de spawn.
- **Opções:** Praça e avenidas · Quarteirões em grade · Uma rua longa
- **Resposta literal:** "Praça e avenidas (Recommended)"
- **Significa:** a praça atual continua como o coração do mapa. As avenidas que saem dos 3 portões viram ruas jogáveis até praças menores, com prédios dos dois lados que bloqueiam a passagem (D-073). Reaproveita a cidade e a plataforma de spawn.

### D-075 · Tamanho da área jogável: cerca do dobro
- **Pergunta:** Qual o tamanho da área jogável? Hoje o raio é de ~26 m.
- **Opções:** Cerca do dobro · Um pouco maior (~35 m) · Bem maior (~80 m)
- **Resposta literal:** "Cerca do dobro (Recommended)"
- **Significa:** raio de uns 50 m, com a praça no centro e as avenidas e praças menores até o limite de prédios. O número exato fica em dados do passe.

### D-076 · Prédios altos ficam translúcidos
- **Pergunta:** Os prédios são o limite do mapa e a câmera é diagonal; como tratar prédios altos perto do jogador?
- **Opções:** Ficam translúcidos · Altura limitada · Fachada some
- **Resposta literal:** "Ficam translúcidos (Recommended)"
- **Significa:** os prédios podem ser altos; quando um tapa o jogador ou um inimigo da visão da câmera, ele fica transparente. A regra de "nenhum prédio pode tapar a praça" (passe visual) deixa de ser por altura e passa a ser por translucidez.

### D-077 · Inimigos entram pelas bocas das ruas
- **Pergunta:** De onde vêm os inimigos agora que a cidade é jogável? Hoje nascem num anel de 18,5 m dentro da praça.
- **Opções:** Pelas bocas das ruas · Dos portões e das ruas · Perto do jogador, fora da visão
- **Resposta literal:** "Pelas bocas das ruas (Recommended)"
- **Significa:** os inimigos entram pelo fim de cada rua e atravessam a cidade até o jogador. Exige que eles achem caminho entre os prédios (navegação, decisão técnica do orquestrador). A emboscada do 1 (D-049) continua em volta do jogador.

### D-079 · Prédio translúcido: recorte com fantasma
- **Pergunta:** Como o prédio fica quando tapa o jogador ou um inimigo (D-076)?
- **Opções:** Recorte com fantasma · Só o recorte · Prédio inteiro a 30%
- **Resposta literal:** "Recorte com fantasma (Recommended)"
- **Significa:** abre-se um círculo no prédio em volta de quem está atrás, com a silhueta do prédio fraca (~25%) por cima e contorno na borda do buraco. Mantém o estilo de contorno do jogo.

### D-080 · Inimigos correm quando estão longe
- **Pergunta:** As bocas das ruas ficam a 60-75 m do jogador (um autômato leva ~20 s e um constructo ~35 s para chegar). O que fazemos?
- **Opções:** Correm quando estão longe · Velocidade normal · Bocas mais perto
- **Resposta literal:** "Correm quando estão longe (Recommended)"
- **Significa:** a mais de ~18 m de todos os jogadores, os inimigos andam o dobro da velocidade; perto, velocidade normal. Os dois números (distância e multiplicador) ficam em dados.

### D-081 · Portões-máquina fecham o fim de cada rua
- **Pergunta:** Hoje os 3 portões ficam no fundo da praça. No mapa novo, o que são?
- **Opções:** Fecham o fim de cada rua · Arcos de entrada das avenidas · Tirar os portões
- **Resposta literal:** "Fecham o fim de cada rua (Recommended)"
- **Significa:** os portões vão para o fim de cada boca de rua; a largada os acende e o trilho de cristal corre da praça até eles; os inimigos saem deles (D-013, D-077).

### D-082 · A carta do fim da onda cai onde morreu o último inimigo
- **Pergunta:** A carta do fim da onda (D-050) aparece no centro; agora o centro pode estar a 50 m de onde o jogador lutou. Onde ela aparece?
- **Opções:** Onde caiu o último inimigo · Perto do jogador mais próximo · Centro da praça, como hoje
- **Resposta literal:** "Onde caiu o último inimigo (Recommended)"
- **Significa:** a carta surge no lugar da luta (Pilar 2). Se esse lugar não for andável ou alcançável, usa o ponto andável mais próximo. Substitui o centro fixo de D-050.

### D-083 · Levantar um aliado: segurar E por perto (resolve P-003)
- **Pergunta:** Quando um jogador cai no coop (D-003), como um aliado o levanta?
- **Opções:** Segurar E por perto · Só chegar perto · Gastar uma carta
- **Resposta literal:** "Segurar E por perto (Recommended)"
- **Significa:** o aliado chega perto e segura **E** por ~2 s; a aura de quem caiu mostra o progresso (sem número) e o aliado fica parado, exposto, enquanto levanta. O tempo de segurar e a distância ficam em dados (provisórios). O que vale quando ninguém vem continua sendo D-003 (volta no spawn).

### D-084 · Todos caídos: a partida recomeça e as cartas se perdem (resolve P-004)
- **Pergunta:** O que acontece se todos os jogadores do grupo estiverem caídos ao mesmo tempo? (e, em seguida, o que quer dizer "perder as cartas" e como fica o solo)
- **Opções (1):** Todos voltam no spawn · A partida recomeça · Quem caiu por último decide. **Resposta literal (Outro):** "recomeca e perde das cartas"
- **Opções (2), "perde as cartas":** Todas as cartas · Só as do inventário · Uma parte sorteada. **Resposta literal:** "Todas as cartas (Recommended)"
- **Opções (3), solo:** Igual ao coop · Solo levanta sozinho · Solo perde menos. **Resposta literal:** "Igual ao coop (Recommended)"
- **Significa:** quando todos os jogadores estão caídos (no solo, o único jogador), a partida **recomeça**: a arena zera, a onda volta à 1 e **todas as cartas somem** (inventário e equipadas) de todos. Vale igual no solo e no coop. Quando só alguns caem, vale D-003 e D-083. Como a queda total é mostrada e se o jogo recomeça sozinho ou pede a alavanca de novo ficam nas perguntas da Fase 9.

### D-086 · A queda total escurece a tela e volta
- **Pergunta:** Como a queda total e a perda das cartas aparecem na tela, sem texto?
- **Opções:** Escurece e volta · Cartas viram cinza · Corte seco
- **Resposta literal:** "Escurece e volta (Recommended)"
- **Significa:** com todos caídos a tela escurece devagar (~2 s, com um som grave); a arena zera no escuro e todos acordam no spawn. Sem texto. O tempo e o volume do som ficam em dados (provisórios).

### D-087 · Depois da queda total, o coop espera a alavanca
- **Pergunta:** Depois de recomeçar, a partida volta direto ou espera a alavanca de novo (D-013)?
- **Opções:** Espera a alavanca no coop · Volta direto à onda 1
- **Resposta literal:** "Espera a alavanca no coop (Recommended)"
- **Significa:** no coop todos acordam na sala de espera (portões apagados) e o host puxa a alavanca de novo. No solo a largada continua automática (D-018).

### D-085 · O 20 do D20 no coop dá bênção de dano ao grupo (resolve P-009)
- **Pergunta:** No coop, o 20 do D20 dá um efeito temporário para o grupo todo (visão §4.5 do prompt). Qual?
- **Opções:** Bênção de dano · Cura e energia · Escudo de cristal
- **Resposta literal:** "Bêpção de dano (Recommended)"
- **Significa:** por ~30 s todos os jogadores causam mais dano, com um brilho dourado nas auras. Duração e bônus ficam em dados (provisórios). Vale só no coop (no solo o 20 continua só a carta, D-048).

### D-078 · Só a modelagem exige o Opus; quem escreve código pode ser Sonnet (ajusta D-044)
- **Pedido (2026-10-05):** depois de o passe de ajuste usar três agentes Opus para escrever efeitos em código.
- **Resposta literal:** "os agentes de escrita apenas nao precisa ser opus e sim sonnet, agora os de modelagem ai sim precisa"
- **Significa:** agentes que escrevem código (inclusive código de efeito, shader, UI e construtor de cena) rodam em **Sonnet**. O **Opus fica para a modelagem e a arte** (modelos do Blender, texturas e pixel art por script, a composição visual de algo novo). A parte do D-044 sobre o harness continua: ele não faz modelagem nem arte. O orquestrador deve conferir o resultado visual (capturas) de qualquer trabalho de código visual feito em Sonnet.

### D-070 · Violeta na poeira mágica
- **Pergunta:** 12% da poeira mágica (D-069) estava violeta, mas na aura o violeta é o sinal de maldição (D-063). O que fazemos?
- **Opções:** Tirar o violeta · Manter 12% · Violeta só mais raro (3%)
- **Resposta literal:** "Violeta só mais raro (3%)"
- **Significa:** `dustVioletShare` = 0,03 em `CrystalAmbienceSettings`. Um toque de cor, com pouco risco de confundir com a maldição.

### D-071 · Quando o aviso do 1 começa
- **Pergunta:** O vermelho e o véu do 1 (D-068) começavam aos 45% da rolagem, antes de o dado parar. Quando o aviso deve começar?
- **Opções:** Crescente, como está · Tudo quando o dado para · Mais tarde (70%)
- **Resposta literal:** "Mais tarde (70%)"
- **Significa:** `criticalTintStart` = 0,7 em `DiceSettings`: o dado só avermelha e a tela só escurece depois de 70% da rolagem, perto de o dado parar.

### D-072 · Sem tranco do dado no 1
- **Pergunta:** Quer que o D20 dê um tranco visual (tremor ou quique) ao bater no 1?
- **Opções:** Sim, um tranco curto · Não · Só no dado, sem tela
- **Resposta literal:** "Não (Recommended)"
- **Significa:** nada muda: o dado continua assentando suave como nos outros números; o baque de som e o véu bastam.

---

## Perguntas pendentes (feitas na fase em que travarem)

| Id | Pergunta | Fase |
|---|---|---|
| ~~P-001~~ | Slots do cinto | resolvida em D-029 |
| ~~P-002~~ | Teclas do cinto | resolvida em D-029, D-031, D-032 |
| ~~P-003~~ | Como o aliado levanta quem caiu | resolvida em D-083 |
| ~~P-004~~ | O que acontece se todos os jogadores caírem | resolvida em D-084 |
| ~~P-005~~ | Tempo caído: número provisório 5 s em `CombatSettings` (para aprovar) | resolvida como número |
| P-006 | Nomes finais dos 3 inimigos (comportamentos decididos em D-023 a D-026; ids internos provisórios: automato, drone, constructo) | antes do conteúdo final |
| ~~P-007~~ | Recuperação da energia | resolvida em D-030 |
| ~~P-008~~ | Como a carta do chão se mostra no mundo | resolvida em D-046 |
| ~~P-009~~ | Efeito temporário do 20 no coop | resolvida em D-085 |
| ~~P-010~~ | Forma e linguagem visual da aura | resolvida em D-060 a D-066 |
| ~~P-012~~ | Passe de resolução (D-053): para quanto sobe o mundo (hoje 640x360: 960x540, 1280x720 ou resolução da tela); se o contorno, a luz em faixas e a paleta de D-039 ficam; se as cartas em pixel art (D-041) ganham versão em mais resolução | resolvida em D-056 a D-059 |
| ~~P-011~~ | Arte com `kimi-k3` no DeepSeek harness | resolvida em D-043 |
| ~~P-013~~ | Energia pouco notada no playtest de 2026-10-05 | resolvida em D-067 |
| ~~P-014~~ | Peso do 1 no D20 (o dono tirou 1 e não sentiu desastre) | resolvida em D-068 |
| ~~P-015~~ | Magia pouco visível no mundo (Pilar 4) | resolvida em D-069 |
| ~~P-016~~ | Cristal das torres fora da câmera e mapa pobre ("mais para dentro da cidade, não só um círculo") | resolvida em D-073 (alcance); detalhes no passe do mapa |
