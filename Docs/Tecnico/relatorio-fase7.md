# Relatório — Fase 7: Aura (D-060 a D-066)

## 1. O que foi construído

Até aqui, o HP, a energia e os estados não apareciam em lugar nenhum. Agora a **aura** mostra tudo, sem número nem barra:

- **Forma (D-060):** um círculo de latão aos pés de cada jogador, com 8 runas em cristal e uma luz que sobe em volta do corpo. O círculo gira devagar.
- **HP (D-061):** com HP cheio, a aura é forte e larga. Com pouco HP ela encolhe e escurece; abaixo de 20% falha como lâmpada.
- **Energia (D-062):** quanto mais energia, mais faíscas sobem, e mais rápido. Com energia cheia, as runas acendem inteiras.
- **Estados (D-063):**
  - escudo do Broquel = casca de cristal facetada;
  - carta amaldiçoada equipada = fiapos violeta;
  - caído = aura apagada, com brasas;
  - reforço da Mola de Recuo = runas e luz em brasa laranja.
- **Sons (D-064), só no seu personagem:**
  - batimento grave abaixo de 35% de HP, de 60 a 140 batidas por minuto conforme o HP cai;
  - chiado curto de vapor quando um inimigo a até 5,5 m prepara um golpe virado para você.
  - Os dois são sintetizados em código, sem arquivo de áudio.
- **Paleta alternativa (D-065):** a tecla **F8** troca ciano, violeta e laranja por azul forte, branco e amarelo. Como cada sinal tem forma própria, a cor só reforça.
- **Seu anel (D-066):** o anel branco do seu personagem foi refeito mais fino e fica por fora do círculo da aura.
- **Números na tela:** conferido, o jogo normal não mostra nenhum número de jogo. O único número visível é o título de tarô das cartas, que faz parte do desenho.

## 2. Arquivos

Novos:
- Core: `Core/Aura/AuraTypes.cs`, `Core/Aura/AuraMapper.cs`. O mapeamento estado → aura é C# puro.
- Jogo:
  - `Aura/PlayerAura.cs`: junta a entrada, suaviza e chama o Core.
  - `Aura/AuraVisual.cs`: o desenho.
  - `Aura/AuraAudio.cs`: os sons.
  - `Aura/AuraSettings.cs`: os números e a troca de paleta.
- Dados: `Data/Aura/AuraSettings.asset`.
- Arte: `Tools/Aura/aura_art.py` → `Art/Aura/` (círculo, runas, luz, faísca, fiapo, brasa e casca).
- Editor: `Editor/AuraBuilder.cs`, que liga a aura ao prefab do jogador e cuida da importação das texturas.
- Debug: `Debug/DevAuraTools.cs` (F8).
- Testes:
  - `Tests/EditMode/AuraCoreTests.cs` (27): HP cheio, médio, baixo e zero; com e sem cada estado; as duas paletas; entrada fora de faixa.
  - `Tests/PlayMode/AuraTests.cs` (7): HP cheio e baixo, batimento, escudo, caído, paleta, anel por fora e círculo com textura.
  - `Tests/PlayMode/Fase7Capturas.cs`: fotos, roda só quando pedido.
  - `Tests/PlayMode/PlayCapture.cs`: auxiliar de fotos, também usado pelo `Fase6Capturas`.

Alterados:
- `Cards/PlayerCards.cs`: o host publica os sinais de escudo e de reforço da Mola (`ShieldActive`, `HurtBonusActive`); `CursedEquipped` é lido por todos.
- `Editor/ArenaBuilder.cs`: chama o `AuraBuilder`.
- `Tools/Blender/build_props.py` → `Art/Models/AnelMarcador.fbx`: anel com raio 1,08 m e tubo de 2,2 cm.

## 3. Como testar

1. Unity → `Game → Setup → Construir Arena` → Play.
2. O círculo de latão aparece aos seus pés, com o anel branco por fora.
3. Bata nos inimigos para ganhar energia: as faíscas aumentam e, com energia cheia, as runas acendem inteiras.
4. Deixe os inimigos baterem: a aura encolhe, escurece e, quase sem vida, falha e o batimento toca.
5. **Tab**: equipe o Broquel e use-o para ver a casca de cristal; equipe a Lâmina Sedenta para ver os fiapos violeta; equipe a Mola de Recuo e leve um golpe para ver as runas em brasa.
6. **F8** troca a paleta. Ao cair, só sobram brasas.

Testes: EditMode 165/165, PlayMode 66/66 (mais 2 de capturas, explícitos). Capturas: `Docs/Capturas/fase7/` (texturas e as oito situações).

## 4. Pilares

- **Pilar 1 (identidade descoberta):** a aura é a base para mostrar a identidade e a rota moral. Nesta fase ela só mostra HP, energia e estados, sem rótulo nem medidor (a cor e a forma por identidade ficam para quando houver rotas).
- **Pilar 2 (a sorte como emoção):** a fase não toca nele.
- **Pilar 3 (escolhas com peso cinza):** o custo de vida da carta amaldiçoada aparece sem número, nos fiapos violeta e no HP da aura.
- **Pilar 4 (fusão de tecnologia e magia):** latão e engrenagem com runas em cristal, na mesma peça.
- **"Mostrar pelo mundo" (§3.1):** HP, energia e estados aparecem só na aura; nenhum número de jogo na tela.

## 5. Números (todos em `Data/Aura/AuraSettings.asset`)

**Mapeamento:**
- raio com HP 0: 0,55
- força com HP 0: 0,2
- falha abaixo de 20% de HP
- batimento abaixo de 35% de HP, de 60 a 140 bpm
- faíscas com energia 0 a 30% da velocidade
- força de quem caiu: 0,08

**Visual:**
- círculo cheio: 0,9 m; anel branco: 1,08 m
- luz: 1,5 m
- faíscas: 16/s com energia cheia; fiapos: 5/s; brasas: 5/s
- casca: 1,05 m
- suavização: 6

**Sons:**
- batimento: volume 0,35; chiado: volume 0,28
- perigo: até 5,5 m, num cone de 35° à frente do inimigo
- intervalo mínimo entre chiados: 0,9 s

## 6. Perguntas e respostas

D-060 círculo aos pés, D-061 força e tamanho, D-062 faíscas na aura, D-063 cada estado com um sinal, D-064 batimento e chiado, D-065 azul e amarelo, D-066 anel por fora. Todas são a opção recomendada e resolvem a P-010. Ficam em `Docs/Design/decisoes.md`, seção "Fase 7 · Aura".

## 7. Pendentes

- Interpretação a confirmar: "carta amaldiçoada cobrando vida" foi lida como "carta amaldiçoada equipada num espaço de skill", porque ela cobra vida a cada uso.
- Aura dos outros jogadores no coop: cada cliente monta a aura de todos a partir do que já vem pela rede, mas o coop está guardado (D-018). Conferir quando ele voltar.
- O chiado de perigo não tem teste automático: ele depende de um inimigo preparando golpe na sua direção. Conferir em Play.
- A cor e a forma da aura por identidade e rota moral (§3.2) dependem de sistemas que ainda não existem. Fica para depois do protótipo.

## 8. Problemas conhecidos

- Bug achado nas capturas e corrigido: o círculo de latão não aparecia, porque o quadrado usado (`FxKit.Quad`) não tem UV. Agora a aura tem um quadrado próprio, com UV; teste `CirculoDeLatao_TemUvParaATextura`.
- Em 120 a 180 px, os glifos das runas viram pontos acesos; a leitura vem do anel e dos engastes.
- No piso claro do ponto de partida, a luz que sobe aparece menos do que no chão escuro da arena.
- Nas fotos de teste, o contorno do pós-processo passa por cima da UI (efeito só do método de captura).
