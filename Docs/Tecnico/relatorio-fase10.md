# Relatório — Fase 10: mais cartas e arcanos (D-088)

## 1. O que foi construído

O baralho passou de **14 para 36 cartas**, as 22 novas escolhidas pelo dono (D-088). Todas misturam máquina e magia (Pilar 4) e entram no sorteio do D20 pela raridade e pelos temas.

- **Skills (Espadas):** Giro de Engrenagem (golpe giratório em volta), Chicote de Corrente (corte estreito e comprido), Tempestade de Faíscas (descarga que salta por 5 inimigos), Martelo a Vapor (pancada pesada à frente).
- **Itens (Copas):** Vapor Condensado, Frasco de Faísca, Cálice Cheio, Tônico Fraco.
- **Passivas (Paus):** Passo de Pistão (+10% de velocidade), Coração de Caldeira (+30 de vida máxima), Pavio Curto (recarga -12%, golpe -4), Fornalha Faminta (+90% de energia por golpe, golpe -6).
- **Equipamentos (Ouros):** Luva de Cobre, Cristal de Fenda, Engrenagem Mestra, Braçadeira de Latão.
- **Arcanos maiores:** VII O Carro de Vapor (investida de 9 m), XIII A Ceifadora de Engrenagens (área de 7 m que cobra 15 de vida), XVII A Estrela de Cristal (descarga de 8 saltos), XI A Força (+14 de dano no golpe).
- **Amaldiçoadas:** Coroa de Rebites (+120% de energia por golpe, -30 de vida máxima) e Pacto de Cristal (+60% de dano no golpe, mas cada golpe que acerta tira 3 de vida).
- **4 modificadores novos no jogo:** velocidade de andar, vida máxima (mantém a fração da vida ao equipar e desequipar), dano percentual do golpe e custo de vida por golpe. A conta do golpe básico saiu do `PlayerCombat` para o Core (`BasicHitMath`) e continua multiplicando por cima da Mola de Recuo e da bênção do 20; o golpe nunca fica abaixo de 1.
- **22 faces novas** desenhadas por script em alta resolução (dourado sobre preto, ciano nas comuns e violeta nas amaldiçoadas).
- **Pool do D20:** o **1** agora sorteia entre 3 amaldiçoadas (Lâmina Sedenta, Coroa e Pacto) e o **20** entre 6 arcanos maiores (os 2 antigos e os 4 novos). Nenhuma faixa da tabela ficou sem carta.

## 2. Arquivos

**Novos**
- Core: `Core/Cards/BasicHitMath.cs`.
- Testes: `NovasCartasCoreTests` (EditMode), `NovasCartasTests` (PlayMode), `Fase10Capturas` (explícito).
- Arte: `Tools/Cards/novas/px_espadas.py`, `hd_espadas.py`, `px_copas_paus.py`, `hd_copas_paus.py`, `px_ouros_forca.py`, `hd_ouros_forca.py`, `LEIA-ME.md`; faces e máscaras de brilho em `Assets/_Game/Art/Cards/<id>.png` e `<id>_brilho.png` (22 cartas).
- Dados (gerados pelo construtor): 22 `CardData` e seus efeitos em `Data/Cards/`.

**Alterados:** `Core/Cards/Modifiers.cs`, `Core/Combat/HealthModel.cs`, `Combat/NetworkHealth.cs`, `Player/PlayerLife.cs`, `PlayerMotor.cs`, `PlayerCombat.cs`, `Cards/PlayerCards.cs`, `Cards/Effects/ArcSlashEffect.cs`, `Cards/CardVisuals.cs` (visual novo `slash_arc`), `Editor/CardAssetsBuilder.cs`, `Tools/Cards/card_pixel.py` e `card_hd.py` (gancho que carrega os módulos de `Tools/Cards/novas/`).

## 3. Como testar

1. `Game → Setup → Construir Arena` → Play.
2. **Shift+F3** dá todas as cartas (F3 dá uma); **Tab** abre as cartas; equipe nos espaços e teste as skills nos inimigos (F4 gera um inimigo). O cinto (Q, E, R) leva os itens de Copas.
3. Para ver o D20 sortear as novas: **F2** troca o próximo dado (normal, 1, 20, 10) e **Shift+F2** solta uma carta no chão.
4. Capturas: `Docs/Capturas/fase10/cartas_novas.png` (as 22 faces) e `f10_*.png` (as skills em uso).
5. Testes: EditMode **382/382**, PlayMode **215 passados, 0 falhas, 4 pulados** (as capturas e a medição de desempenho, explícitas).

## 4. Pilares

- **Pilar 1 (identidade descoberta):** mais cartas por tema (vapor, engrenagem, faísca, cristal) dão mais caminhos para o jogador descobrir quem está virando.
- **Pilar 2 (a sorte como emoção):** o 20 e o 1 ganham variedade (6 arcanos maiores e 3 amaldiçoadas).
- **Pilar 3 (escolhas com peso):** passivas e equipamentos com custo (Pavio Curto, Fornalha Faminta, Coroa, Pacto) e mais cartas do que espaços obrigam a escolher.
- **Pilar 4 (tecnologia e magia):** cada efeito mistura a parte mecânica e a arcana; as faces mostram engrenagem e cristal na mesma peça.

## 5. Números provisórios (nos dados das cartas, `Data/Cards/*.asset`)

Os números de cada carta estão na tabela de `Docs/Design/cartas-prototipo.md` (seção "Cartas da Fase 10"). Quem escolheu a raridade, os temas e os números fui eu, a partir do que você aprovou; todos vão para sua revisão:

- Raridade: comuns (Giro, Vapor Condensado, Tônico Fraco, Passo de Pistão, Pavio Curto, Luva de Cobre); incomuns (Chicote, Tempestade, Frasco, Cálice Cheio, Coração, Fornalha, Cristal de Fenda, Engrenagem Mestra, Braçadeira, Coroa, Pacto); raras (Martelo, Carro, Ceifadora, Estrela); única (A Força).
- Alcance e abertura da Tempestade de Faíscas: copiados do Arco Voltaico (10 m e 30°); a tabela não dava.
- A qualidade da carta (gasta ×0,85, boa, perfeita ×1,15) multiplica também os bônus de vida máxima, custo de vida e as penalidades.

## 6. Perguntas e respostas

D-088 (Fase 10 e as 22 cartas, com as respostas literais). Em `Docs/Design/decisoes.md`.

## 7. Pendentes (perguntas para o dono)

- **Fonte do título das faces novas:** as 14 cartas originais usam a Copperplate Gothic Bold (`COPRGTB.TTF`), que **não está instalada neste PC**; as 22 novas saíram com a Georgia Bold, uma serifa diferente. A arte não muda. Para corrigir basta gerar as 22 num PC que tenha a fonte: `python Tools/Cards/card_hd.py giro_engrenagem chicote_corrente tempestade_faiscas martelo_vapor vapor_condensado frasco_faisca calice_cheio tonico_fraco passo_pistao coracao_caldeira pavio_curto fornalha_faminta luva_cobre cristal_fenda engrenagem_mestra bracadeira_latao carro_vapor ceifadora_engrenagens estrela_cristal forca coroa_rebites pacto_cristal` (não gerar o baralho inteiro aqui: as 14 antigas trocariam de fonte).
- **Luva de Cobre em dourado:** o baralho só tem a rampa de ouro, então a luva "de cobre" saiu dourada. Um cobre de verdade seria uma decisão de design.
- **Luva de Cobre e A Força são ambas um punho fechado.** Diferem pela silhueta (a Força tem a barra de ferro, os pistões e o símbolo do infinito); vale o seu olhar.
- **A Ceifadora de Engrenagens não é amaldiçoada** (só a Coroa e o Pacto são), embora cobre vida: a tabela marcou só essas duas.
- Teste humano das cartas novas (equilíbrio de dano, custo, recarga) e do coop com elas.

## 8. Problemas conhecidos

- O visual do **Giro de Engrenagem** é a explosão de vapor existente, não um giro de fato; o **Chicote** e o **Martelo** usam o visual novo `slash_arc` (arco com a abertura do efeito), que na captura funciona e fica bem largo no Martelo.
- A explosão da **Ceifadora** (raio 7 m) é um disco branco grande com engrenagens voando e cobre boa parte da tela por um instante.
- No gerador em pixel, o nome "A CEIFADORA DE ENGRENAGENS" passa da faixa; na alta resolução (a que a Unity usa) não.
- Os testes de capturas da Fase 10 dão as cartas por `ServerGiveCard`, então a revelação da carta nova aparece no centro das fotos.
