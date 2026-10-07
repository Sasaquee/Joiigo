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

## Cartas da Fase 10 (D-088): 22 novas, ids 15 a 36

Todas aprovadas pelo dono (D-088). Raridade, temas e números são provisórios e vão para o relatório da fase. Os 4 modificadores novos (`MoveSpeed`, `MaxHealth`, `BasicDamageMultiplier`, `LifeCostPerHit`) pedem código; o resto usa os efeitos que já existem. Os rótulos e nomes são os do desenho da face (`Tools/Cards/card_pixel.py`, lista `CARDS`).

| # | id | Rótulo · Nome | Tipo | Raridade | Temas | Efeito e números provisórios |
|---|---|---|---|---|---|---|
| 15 | `giro_engrenagem` | II DE ESPADAS · GIRO DE ENGRENAGEM | Skill | Comum | engrenagem | Golpe giratório em volta (`AreaBurst`): raio 2,5 m, dano 28, 30% arcano; energia 20, recarga 5 s |
| 16 | `chicote_corrente` | IV DE ESPADAS · CHICOTE DE CORRENTE | Skill | Incomum | engrenagem, faisca | Corte estreito e comprido (`ArcSlash`): alcance 6 m, meia-abertura 20°, dano 30, 20% arcano; energia 20, recarga 4 s |
| 17 | `tempestade_faiscas` | IX DE ESPADAS · TEMPESTADE DE FAÍSCAS | Skill | Incomum | faisca, cristal | Descarga que salta (`ChainProjectile`): 5 saltos, dano 14 por salto, 90% arcano; energia 35, recarga 8 s |
| 18 | `martelo_vapor` | X DE ESPADAS · MARTELO A VAPOR | Skill | Rara | vapor, engrenagem | Pancada pesada à frente (`ArcSlash`): alcance 3,5 m, meia-abertura 45°, dano 70, 50% arcano; energia 40, recarga 10 s |
| 19 | `vapor_condensado` | VI DE COPAS · VAPOR CONDENSADO | Item | Comum | vapor | `Heal`: +15 vida, +50 energia |
| 20 | `frasco_faisca` | VIII DE COPAS · FRASCO DE FAÍSCA | Item | Incomum | faisca | `ThrowArea`: dano 25 em raio 2 m, 80% arcano, alcance 9 m |
| 21 | `calice_cheio` | X DE COPAS · CÁLICE CHEIO | Item | Incomum | cristal, vapor | `Heal`: +70 vida, +20 energia |
| 22 | `tonico_fraco` | PAJEM DE COPAS · TÔNICO FRACO | Item | Comum | vapor | `Heal`: +15 vida, +15 energia |
| 23 | `passo_pistao` | III DE PAUS · PASSO DE PISTÃO | Passiva | Comum | engrenagem | `MoveSpeed` +0,10 (10% mais rápido) |
| 24 | `coracao_caldeira` | VIII DE PAUS · CORAÇÃO DE CALDEIRA | Passiva | Incomum | vapor | `MaxHealth` +30 |
| 25 | `pavio_curto` | II DE PAUS · PAVIO CURTO | Passiva | Comum | faisca | `CooldownChange` -0,12 e `BasicDamage` -4 |
| 26 | `fornalha_faminta` | V DE PAUS · FORNALHA FAMINTA | Passiva | Incomum | vapor, faisca | `EnergyOnHitBonus` +0,9 e `BasicDamage` -6 |
| 27 | `luva_cobre` | II DE OUROS · LUVA DE COBRE | Equipamento | Comum | engrenagem | `BasicDamage` +10 e `BasicRange` -0,3 |
| 28 | `cristal_fenda` | IX DE OUROS · CRISTAL DE FENDA | Equipamento | Incomum | cristal | `BasicArcaneShift` +0,6 e `BasicDamage` +4 |
| 29 | `engrenagem_mestra` | REI DE OUROS · ENGRENAGEM MESTRA | Equipamento | Incomum | engrenagem, cristal | `CooldownChange` -0,10 e `BasicDamage` +4 |
| 30 | `bracadeira_latao` | V DE OUROS · BRAÇADEIRA DE LATÃO | Equipamento | Incomum | engrenagem, vapor | `BasicRange` +0,9, `EnergyOnHitBonus` +0,25 e `BasicDamage` -3 |
| 31 | `carro_vapor` | VII · O CARRO DE VAPOR | Skill (arcano maior) | Rara | vapor, engrenagem | Investida longuíssima (`Dash`): 9 m, dano 55, 30% arcano, largura 1,8 m; energia 45, recarga 15 s |
| 32 | `ceifadora_engrenagens` | XIII · A CEIFADORA DE ENGRENAGENS | Skill (arcano maior) | Rara | engrenagem, faisca | Ceifa em área enorme (`AreaBurst`): raio 7 m, dano 90, 50% arcano; energia 60, recarga 25 s, **custa 15 de vida** |
| 33 | `estrela_cristal` | XVII · A ESTRELA DE CRISTAL | Skill (arcano maior) | Rara | cristal, faisca | Descarga que salta (`ChainProjectile`): 8 saltos, dano 25 por salto, 95% arcano, alcance entre saltos 6 m; energia 50, recarga 18 s |
| 34 | `forca` | XI · A FORÇA | Passiva (arcano maior) | Única | engrenagem, vapor | `BasicDamage` +14 |
| 35 | `coroa_rebites` | VIII DE OUROS (amaldiçoada) · COROA DE REBITES | Equipamento | Incomum | engrenagem, faisca | **Amaldiçoada.** `EnergyOnHitBonus` +1,2 e `MaxHealth` -30 |
| 36 | `pacto_cristal` | VII DE PAUS (amaldiçoada) · PACTO DE CRISTAL | Passiva | Incomum | cristal | **Amaldiçoada.** `BasicDamageMultiplier` +0,6 e `LifeCostPerHit` 3 (cada golpe que acerta tira 3 da vida de quem usa; se a vida chegar a zero, vale a queda normal, como na Lâmina Sedenta) |

Modificadores novos: `MoveSpeed` (fração somada à velocidade de andar), `MaxHealth` (soma à vida máxima; a vida atual mantém a fração), `BasicDamageMultiplier` (fração somada ao multiplicador do dano do golpe básico) e `LifeCostPerHit` (vida perdida por golpe básico que acerta). O dano do golpe básico nunca fica abaixo de 1.

## Números provisórios das cartas da Fase 1 a 9 (em `Data/Cards/*.asset`)

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
