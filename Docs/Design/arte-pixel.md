# Bíblia de arte — 3D com contorno (D-039 a D-042, D-056 a D-059)

## Como o jogo é desenhado

- **Tudo é 3D**, renderizado na resolução da tela (D-056), com bordas suavizadas (MSAA 4x). Por cima entram contorno escuro proporcional à tela (2 px em 1080p) e luz em faixas suaves, com rampa curta entre uma faixa e outra e sem pontilhado (D-057). Os números ficam em `Assets/_Game/Data/Camera/ImageQualitySettings.asset`; o modo pixelado de antes (640x360 ampliado sem suavizar) volta pondo `worldHeight` = 360.
- O D20 da tela aparece no tamanho real, com bordas suaves (D-058). As cartas são arte em alta resolução, 672x1152 (D-059, `Tools/Cards/card_hd.py`).
- A UI fica fora disso, em resolução cheia.
- Câmera de jogo: ~14 m de distância, 50° de inclinação, FOV 40°. Nessa câmera, **1 pixel ≈ 1 cm em 1080p**:
  - detalhes com menos de **2 cm** somem ou cintilam, então não modele parafusos minúsculos;
  - prefira formas **grossas, chanfradas e com silhueta clara** (não é mais limite técnico).
- **Sem texturas fotográficas.** As cores vêm dos materiais, que são **cores chapadas** desta paleta. O detalhe vem da forma e da luz.
- Pilar 4: a máquina e a magia aparecem na **mesma peça**. Exemplos: cristal embutido no cano, engrenagem com núcleo arcano, janela que brilha em ciano porque a casa é movida a cristal.

## Paleta (nomes exatos dos materiais no Blender)

A Unity troca cada material pelo de mesmo nome em `Assets/_Game/Art/Materials`. Use **só estes nomes**. Se precisar de um novo, avise o líder.

| Nome | Cor | Uso |
|---|---|---|
| `FerroEscuro` | #2B2A30 | estruturas, máquinas |
| `FerroMedio` | #4A4752 | chapas, detalhes de metal |
| `Cobre` | #B5653A | canos, caldeiras |
| `CobreOxidado` | #4F8C7A | cobre velho (verdete), telhados de cobre |
| `Latao` | #C9A04A | molduras, engrenagens, adornos |
| `LataoEscuro` | #7A5F2C | latão sujo, sombra de adorno |
| `Pedra` | #6A625A | paredes, calçamento |
| `PedraEscura` | #3A3634 | base, sombra, fundações |
| `Tijolo` | #7A3E2E | prédios |
| `Madeira` | #6B4630 | portas, vigas, caixotes |
| `Telhado` | #3B3F52 | telhas de ardósia |
| `Tecido` | #4E4A44 | manto do andarilho |
| `TecidoEscuro` | #2F2C2A | forro, sombra de tecido |
| `Couro` | #6E4A32 | bolsas, cintos, botas |
| `Bandeira` | #7C2F3A | estandartes, toldos |
| `JanelaQuente` | #FFC070 emissivo | janelas com luz de lampião |
| `CristalArcano` | #6FF0FF emissivo | cristais, energia arcana |
| `BrasaFornalha` | #FF7A20 emissivo | fogo, brasas |
| `PersonagemNeutro` | #8C8A86 | pele/máscara neutra (pouco uso) |
| `MarcadorLocal` | #EBE0C7 | anel do jogador local |

## Convenções de modelo

- Blender 5.2, por script (`--background --factory-startup`), um script por autor:
  - `Tools/Blender/build_<coisa>.py`
  - exporta FBX para `Assets/_Game/Art/Models/<Pasta>/<Nome>.fbx`
  - salva o `.blend` em `Art/Blender/<Pasta>/`
- Use as mesmas opções de exportação de `Tools/Blender/build_props.py`: `bake_space_transform=True`, `axis_forward="-Z"`, `axis_up="Y"`, `FBX_SCALE_UNITS`, rotação e escala aplicadas.
- Pivô no chão, Z para cima no Blender (vira Y na Unity). A frente é **-Y** no Blender.
- Peças que se mexem (engrenagens, placas, pás, hélices) ficam em **objetos separados com nome claro**, sob um Empty raiz, como em `build_enemies.py`.
- Orçamento: até ~3k faces por prédio, ~5k pelo personagem. Low-poly com chanfro dá o melhor resultado em pixel.
