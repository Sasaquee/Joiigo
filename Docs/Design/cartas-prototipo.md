# Cartas do protótipo — proposta

> Status: **aprovada (D-035, D-036)**. Os nomes, os efeitos e o mapeamento dos naipes são decisões de design. Os números de dano, custo e recarga são provisórios e ficam em ScriptableObjects.
> Regra do Pilar 4: cada efeito junta o mecânico e o arcano no próprio funcionamento, e não só na aparência. Todo dano declara a mistura mecânico/arcano (D-021).

## Naipes → tipo de carta (§3.3)

| Naipe do tarô | Tipo | Por quê |
|---|---|---|
| **Espadas** | Skill | ação, corte, decisão |
| **Copas** | Item consumível | o cálice é o frasco, usado e esvaziado |
| **Paus** | Passiva | o bastão é força e vontade contínuas |
| **Ouros** | Equipamento | moeda e matéria: o que se carrega |

As cartas vão de Ás a 10 e depois Pajem, Cavaleiro, Rainha e Rei. Os arcanos maiores usam numeral romano.

## Temas (tags)

Usados na Fase 6 para o tema da carta (lugar + caminho do jogador). **Nunca aparecem na tela.**
`vapor` · `engrenagem` · `faisca` (eletricidade arcana) · `cristal`

## Cartas

| # | Carta | Tipo | Efeito | Mistura | Temas |
|---|---|---|---|---|---|
| 1 | **Ás de Espadas · Pistão Rúnico** | Skill | Investida curta na direção do mouse. Empurra e fere quem estiver no caminho. Um pistão empurra, e a runa gravada nele é o que dá impulso. | 70% mec / 30% arc | engrenagem |
| 2 | **Três de Espadas · Sopro de Caldeira** | Skill | Cone de vapor superaquecido à frente por ~1 s, com dano contínuo. O vapor carrega runas que queimam o arcano. | 50 / 50 | vapor |
| 3 | **Cinco de Espadas · Arco Voltaico** | Skill | Projétil de faísca que salta para até 3 inimigos próximos. É uma descarga de bobina de cobre guiada pelo cristal. | 20 / 80 | faisca, cristal |
| 4 | **Sete de Espadas · Mina de Engrenagem** | Skill | Planta no chão uma armadilha de engrenagens com cristal. Ela explode ao contato, ou depois de 10 s. | 60 / 40 | engrenagem, cristal |
| 5 | **Pajem de Espadas · Broquel Cantante** | Skill | Escudo de latão à frente por 2 s. Bloqueia projéteis e devolve parte do impacto como pulso arcano. | 30 / 70 (pulso) | engrenagem, cristal |
| 6 | **Dois de Copas · Tônico de Óleo e Luz** | Item | Cura parte da vida e devolve energia. É óleo de máquina destilado com luz de cristal. | — | vapor |
| 7 | **Quatro de Copas · Granada de Cristal Rachado** | Item | Arremessa até o mouse. Explode em área quando o cristal racha. | 40 / 60 | cristal, faisca |
| 8 | **Ás de Paus · Caldeira Interna** | Passiva | Cada golpe básico que acerta gera mais energia: o golpe mecânico alimenta o arcano. | — | vapor |
| 9 | **Seis de Paus · Mola de Recuo** | Passiva | Depois de levar dano, o próximo golpe básico causa dano extra. A dor tensiona a mola. | mecânico | engrenagem |
| 10 | **Três de Ouros · Manopla Pistonada** | Equipamento | Golpe básico com mais alcance e mais dano mecânico. | +mec | engrenagem |
| 11 | **Rainha de Ouros · Lente Prismática** | Equipamento | Converte parte do golpe básico em arcano. É ótima contra o autômato e ruim contra o constructo (escolha de build). | +arc | cristal |
| 12 | **Oito de Espadas (amaldiçoada) · Lâmina Sedenta** | Skill | Corte em arco largo e forte que **cobra vida** a cada uso. Ocupa um espaço de skill. Remover a maldição tem custo, mas isso fica fora do protótipo (§3.3). | 50 / 50 | faisca |
| 13 | **XVI · A Chaminé Partida** *(arcano maior, teste)* | Skill | Explosão de vapor arcano em volta do jogador, com recarga longa. | 50 / 50 | vapor |
| 14 | **I · O Artífice** *(arcano maior, teste)* | Passiva | Todas as skills recarregam mais rápido. | — | engrenagem, cristal |

## Números provisórios (em `Data/Cards/*.asset`)

| Carta | Custo de energia | Recarga | Dano / valor |
|---|---|---|---|
| Pistão Rúnico | 20 | 4 s | 18, investida de 5 m |
| Sopro de Caldeira | 30 | 6 s | 30 por segundo, por 1 s |
| Arco Voltaico | 25 | 5 s | 22 por salto, 3 saltos |
| Mina de Engrenagem | 25 | 8 s | 45 em área de 2,5 m |
| Broquel Cantante | 20 | 7 s | pulso de 15 |
| Tônico de Óleo e Luz | — | uso único | +35 de vida, +40 de energia |
| Granada de Cristal Rachado | — | uso único | 40 em área de 3 m |
| Caldeira Interna | — | — | +50% de energia por golpe |
| Mola de Recuo | — | — | +60% no próximo golpe, por 3 s |
| Manopla Pistonada | — | — | +0,6 m de alcance, +6 de dano mecânico |
| Lente Prismática | — | — | +0,35 de parte arcana no golpe |
| Lâmina Sedenta | 0 | 3 s | 40 em arco de 160°, custa 8 de vida |
| A Chaminé Partida | 50 | 20 s | 60 em área de 5 m |
| O Artífice | — | — | recarga -25% |

Energia: máximo 100. Recupera 4 por segundo, +8 por golpe básico que acerta (D-030).
