# Relatório — passe de resolução (D-053, D-056 a D-059)

## 1. O que foi construído

O dono achou o jogo pixelado demais ("o dado esta em baixissima resolução, o mesmo serve para o jogo em si e tudo mais"). O passe deixou tudo nítido:

- **Mundo:** antes era renderizado em 640x360 e ampliado 3x. Agora sai na resolução da tela, com bordas suavizadas (MSAA 4x). O contorno escuro continua, com espessura proporcional à tela (2 px em 1080p).
- **Luz:** as faixas continuam, agora com 24 degraus, sem pontilhado e com uma rampa curta entre um degrau e outro. Sem a rampa, a divisa serrilhava em resolução cheia.
- **D20:** antes era desenhado numa textura de 151 px e esticado. Agora é desenhado no tamanho real em que aparece (cerca de 450 px numa tela 1080p), com bordas suaves.
- **Cartas:** as 14 cartas e o verso foram refeitas em 672x1152 pelo artista (Opus), com o mesmo desenho, mais detalhe e bordas limpas. Agora são importadas com filtro suave e mipmaps.
- **Bug corrigido:** o quadrado preto atrás do D20 ao rolar. O buffer HDR do URP não tem canal alfa, e o pós-processo devolvia alfa 1. Agora a câmera do palco não usa HDR e o shader preserva a transparência.
- **Ajuste:** o brilho animado da carta amaldiçoada na UI passou a ser violeta (era ciano como as outras), como pede D-041.
- **Bug corrigido (achado nas capturas):** a revelação da carta nova só existia se a Arena fosse a primeira cena do jogo. Ao recarregar a Arena (F9 e entrar de novo), ela não aparecia mais. Agora é criada a cada cena carregada.

## 2. Arquivos

Novos:
- `Assets/_Game/Camera/ImageQualitySettings.cs` e `Assets/_Game/Data/Camera/ImageQualitySettings.asset`: todos os números de imagem.
- `Assets/_Game/Editor/ResolutionPreview.cs`: menu `Game/Debug/Comparar resoluções`, gera as comparações de `Docs/Capturas/resolucao/`.
- `Tools/Cards/card_hd.py`: as cartas em alta resolução. O `card_pixel.py` fica como histórico e ainda é usado pelo `card_hd.py`.
- Testes: `Tests/PlayMode/DiceRollUiTests.cs`, `Tests/PlayMode/DiceFaceTests.cs`, `Tests/PlayMode/Fase6Capturas.cs` (só tira fotos e roda só quando pedido) e `CartaEntregue_TocaARevelacao` em `Tests/PlayMode/CardDropTests.cs`.

Alterados:
- `Camera/PixelCamera.cs`: resolução do mundo vinda dos dados, snap de pixel só no modo pixelado, espessura do contorno para o shader.
- `Art/Shaders/PixelPost.shader`: contorno de espessura variável, rampa entre faixas (`_BandSoftness`), alfa preservado.
- `Editor/PixelRenderSetup.cs`: copia faixas, rampa e pontilhado para o material, e o MSAA para o URP.
- `Editor/ArenaBuilder.cs`: liga os dados ao `PixelCamera`.
- `UI/DiceRollUi.cs`: textura do tamanho real, suave, sem HDR; `Play()` público para os testes.
- `UI/CardRevealFx.cs`: criada a cada cena carregada.
- `UI/CardView.cs`: brilho violeta na carta amaldiçoada.
- `Editor/CardTextureImport.cs` e `Editor/CardDropBuilder.cs`: cartas e verso com filtro suave, mipmaps e anisotrópico 4.
- `Editor/SceneSnapshot.cs`: capturas em 1920x1080.
- `Art/Cards/*.png`: regeradas em 672x1152.
- Docs: `Docs/Design/arte-pixel.md`, `Docs/Tecnico/arquitetura.md` e três comentários que citavam 640x360.

## 3. Como testar

1. Unity → `Game → Setup → Construir Arena` → Play.
2. O mundo, o personagem e os inimigos devem aparecer nítidos, sem bloco de pixel, com contorno.
3. **Shift+F2** põe uma carta no chão, **F2** escolhe o próximo dado e **F** pega a carta. O D20 tem de rolar nítido, sem quadrado preto, e a carta tem de ser revelada depois.
4. **Tab:** as cartas na tiragem aparecem em alta resolução.
5. Para voltar ao visual pixelado e comparar: em `Data/Camera/ImageQualitySettings`, ponha `worldHeight` = 360 e rode `Game → Setup → Instalar Render Pixelado`.
6. Testes: EditMode 138/138, PlayMode 59/59. Os dois testes de bug (`DiceRollUiTests` e `CartaEntregue_TocaARevelacao`) foram rodados sem a correção e falharam como esperado.

Capturas: `Docs/Capturas/resolucao/` (comparações e cartas antes/depois) e `Docs/Capturas/fase6/` (o fluxo em Play, já em resolução cheia).

## 4. Pilares

- **Pilar 2 (a sorte como emoção):** o D20, o momento mais importante da tela, agora é nítido; números e cristais se leem durante a rolagem.
- **Pilar 4 (fusão de tecnologia e magia):** latão e cristal ficaram legíveis nos inimigos, no dado e nas cartas (reflexos no metal, facetas no cristal).
- Pilares 1 e 3: o passe não toca neles.

## 5. Números (todos em `Data/Camera/ImageQualitySettings.asset`)

`worldHeight` 0 (tela cheia) · `msaa` 4 · `lightBands` 24 · `bandSoftness` 0,25 · `dither` 0 · `outlinePixelsAt1080` 2.

## 6. Perguntas e respostas

D-053 (pedido do dono), D-056 tela cheia, D-057 faixas suaves, D-058 dado em tela cheia suave, D-059 redesenhar as cartas em alta. Tudo em `Docs/Design/decisoes.md`, seção "Passe de resolução".

## 7. Pendentes

- Algumas decisões de desenho do artista nas cartas, todas dentro de D-059:
  - fonte Copperplate Gothic Bold no lugar da fonte de pixel;
  - dourado do meio um pouco mais claro;
  - costura de equador na esfera do Arco Voltaico;
  - cristais de canto menores nos arcanos maiores.
- O chão da arena ficou liso: o pixelado escondia isso. Ganhar textura ou detalhe é decisão de arte para perguntar ao dono (D-039 pedia cores chapadas, sem textura fotográfica).

## 8. Problemas conhecidos

- No teste de capturas, a UI passa pelo pós-processo. Por isso aparece o contorno fraco de quem está atrás do dado. No jogo a UI fica por cima e isso não acontece.
- Os modelos do Blender foram feitos para 640x360 (formas grossas). Em resolução cheia continuam bons, mas algumas faces planas grandes ficam mais visíveis.
