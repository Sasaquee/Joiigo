# Relatório — Passe do mapa (D-073 a D-082)

Origem: a sessão de playtest de 2026-10-05 (`Docs/Playtest/sessao-2026-10-05-1.md`). O dono pediu a arena "mais para dentro da cidade, não só um círculo", com prédios como limite. Plano técnico em `Docs/Tecnico/plano-passe-mapa.md`.

## 1. O que foi construído

- **Mapa novo (D-073 a D-075).** A arena deixa de ser uma praça redonda fechada por muro baixo. Agora é a praça central (a mesma, com a plataforma de spawn, a alavanca, a alcova, as caldeiras, as fornalhas e o núcleo) mais **3 avenidas** (-45°, 0° e +45°) que saem dos portões até **3 praças menores**, e uma **boca de rua** no fim de cada avenida. O limite do mapa são **prédios que bloqueiam** o jogador e os inimigos, mais uma vedação invisível que fecha as frestas. Raio jogável de ~56 m.
- **Cidade jogável.** O `CityBuilder` foi reescrito sobre um planejador (`CityPlanner`) que posiciona cada prédio do kit existente: fachadas das avenidas e bocas, arcos das praças, quarteirões e fundo até r ~85. Um `BoxCollider` por prédio (camada `Cenario`). A torre do relógio fica no centro da praça menor norte e os pilões arcanos nas outras duas.
- **Prédios translúcidos (D-076, D-079).** Quando um prédio tapa o jogador ou um inimigo, o shader `Game/LitVazado` abre um círculo no prédio, com a silhueta do prédio fraca (~25%) por cima e o contorno na borda do buraco. O `SeeThroughDriver` (na câmera) calcula quem está tapado; cada cliente usa a própria câmera, sem nada novo na rede.
- **Inimigos pelas bocas das ruas (D-077, D-080).** Os inimigos nascem nas bocas, em rodízio (pulando a boca com jogador vivo a menos de 12 m), e atravessam a cidade pela NavMesh (só para calcular o caminho; o `CharacterController` continua movendo). A mais de 18 m de todos os jogadores eles correm o dobro. O drone só atira com linha de visão, e a checagem usa a espessura do orbe.
- **Portões nas bocas (D-081).** Os 3 portões-máquina fecham o fim de cada rua; a largada os acende, junto com o trilho de cristal que corre da praça até eles.
- **Carta onde caiu o último inimigo (D-082).** A carta do fim da onda surge na posição de morte do último inimigo (ponto andável mais próximo se necessário).
- **Cristal visível (P-016, D-069).** Trilho de cristal no chão de cada avenida (apagado até a largada), lampiões de cristal nas avenidas, poeira mágica baixa nas avenidas e praças menores (violeta a 3%, D-070).
- **Cena mais leve.** `Arena.unity` caiu de 10,5 MB para ~3,4 MB: a fumaça e as partículas do ambiente passaram a nascer só em jogo.
- **Ajustes pequenos do mesmo dia:** violeta da poeira a 3% (D-070), aviso do 1 aos 70% da rolagem (D-071), sem tranco do dado (D-072).

## 2. Arquivos

**Novos**
- Core: `Core/Map/` (`MapLayout`, `MapLayoutParams`, `MapShapes`, `MapBoundary`), `Core/AI/PathCursor.cs`, `Core/AI/SpawnMouths.cs`, `Core/View/SeeThroughMath.cs`, `Core/View/CameraFootprint.cs`.
- Jogo: `Arena/MapLayers.cs`, `Arena/MapLayoutSettings.cs`, `Arena/VazadoLayer.cs`, `Arena/Life/AmbienceParticles.cs`, `Enemies/EnemyPathFollower.cs`, `Enemies/EnemyNavigationSettings.cs`, `Camera/SeeThroughDriver.cs`, `Camera/SeeThroughSettings.cs`.
- Shader: `Art/Shaders/LitVazado.shader` e `Vazado.hlsl`.
- Editor: `Editor/WalkableBuilder.cs`, `ArenaNavMeshBuilder.cs`, `CityPlanner.cs`.
- Dados e geradas pelo construtor: `Data/Map/MapLayoutSettings.asset`, `Data/Camera/SeeThroughSettings.asset`, `Data/Enemies/EnemyNavigationSettings.asset`, `Arena/ArenaNavMesh.asset`.
- Testes: `MapLayoutTests`, `MapBoundaryTests`, `PathCursorTests`, `SpawnMouthTests`, `SeeThroughMathTests`, `CameraFootprintTests`, `SceneSizeTests` (EditMode); `CityLayoutTests`, `EnemyNavigationTests`, `WaveSpawnerTests`, `LitVazadoTests`, `SeeThroughTests`, `CameraCoverageTests`, `SmokeEmitterTests`, `PasseMapaCapturas` (explícito).

**Alterados:** `ArenaBuilder`, `CityBuilder`, `AmbienceBuilder`, `PixelPalette`, `PixelRenderSetup`, `EnemyPrefabBuilder`, `SceneSnapshot`, `EnemyController`, `WaveSpawner`, `WaveSettings`, `CardDropService`, `DiceSettings`, `DevCombatTools`, `SmokeEmitter`, `CrystalAmbienceSettings`; `ProjectSettings/TagManager.asset` (camada 8 `Cenario`, rendering layer 8 `Vazavel`), `NavMeshAreas.asset` (agente Humanoid com raio 0,9); `Game.Editor.asmdef` e `Game.Tests.PlayMode.asmdef` (`Unity.AI.Navigation`). `Arena.unity` e prefabs foram regenerados.

## 3. Como testar

1. `Game → Setup → Construir Arena` → Play.
2. Ande até uma avenida: prédios dos dois lados, lampiões, bueiros e o trilho de cobre no chão. Puxe a alavanca: o trilho e os portões acendem em ciano.
3. Fique atrás de um prédio alto em relação à câmera (por exemplo na avenida nordeste, junto de uma caixa d'água): o prédio abre um círculo em volta do personagem.
4. Deixe uma onda vir: os inimigos entram pelas bocas, atravessam a cidade e chegam até você (longe correm). Mate o último e veja a carta surgir onde ele caiu.
5. Capturas: `Docs/Capturas/passe-mapa/` (vistas da arena e três fotos em Play: avenida, translucidez e trilhos acesos).
6. Testes: EditMode **288/288**, PlayMode **152 passados, 0 falhas, 3 pulados** (as 3 capturas explícitas).

## 4. Pilares

- **Pilar 4 (tecnologia e magia):** o cristal vivo agora aparece no chão das avenidas (trilho), nos lampiões e na poeira, e a torre do relógio com cristal fica no centro de uma praça que se pode visitar.
- **Pilar 1 e 2:** a carta cai onde você lutou, sem andar até o centro.
- **Pilar 3:** a corrida dos inimigos distantes mantém o ritmo das ondas.

## 5. Números novos (todos em ScriptableObject)

- **`MapLayoutSettings`:** praça central raio 29 (anda até 28,3); avenidas -45°, 0°, +45°, 9 m entre fachadas, de s 26 a s 41; praças menores em s 47 com **raio 9** (ver §7); bocas 6 m, de s 53 a s 61; spawn dos inimigos em s 57,5 (raio 1,5); portões em s 60,6; limite do jogador **56**; vedação 6 m; margem do envelope 1,5 m.
- **`EnemyNavigationSettings`:** recálculo 0,4 s / 1,5 m; alcance de quina 0,5 m; reto até 6 m; amostra 2 m; preso 1,5 s / 0,3 m; `farSprintDistance` 18 m; `farSprintMultiplier` 2; altura da linha de visão 1 m; sonda do recuo do drone 1 m.
- **`SeeThroughSettings`:** 12 alvos; raio do jogador 1,8 m; raio do inimigo 1,4 m; margem de profundidade 0,8 m; recorte perto 3 m; fade 0,12 / 0,3 s; opacidade do fantasma 0,25; alturas de checagem 0,4 / 1,2 / 1,9 m; varredura 0,25 s.
- **`WaveSettings`:** `mouthPlayerClearance` 12; `spawnStagger` 0,5; `mouthSpawnSpread` 0,6; `mouthNavSampleRadius` 2.
- **`DiceSettings`:** `ambushNavSampleRadius` 2,5; `ambushTries` 6; `ambushRetryAngle` 30; `ambushRetryRadiusFactor` 0,7; `dropNavSampleRadius` 4; `criticalTintStart` 0,7 (D-071).
- **`CrystalAmbienceSettings`:** trilho (início s 1,6, trecho 2,5 m, largura 0,14 m); lampiões (8 m, folga 0,6 m, 1 luz a cada 2, máximo 6, alcance 7, intensidade 3); poeira (por avenida 40 / 4 por s; por praça menor 60 / 6 por s; brilho 1,2; violeta 3%; dourado 10%).

## 6. Perguntas e respostas

D-070 (violeta 3%), D-071 (aviso do 1 aos 70%), D-072 (sem tranco), D-073 (a arena se abre para a cidade), D-074 (praça e avenidas), D-075 (cerca do dobro), D-076 e D-079 (prédios translúcidos, recorte com fantasma), D-077 (inimigos pelas bocas), D-080 (correm quando longe), D-081 (portões fecham o fim de cada rua), D-082 (carta onde caiu o último inimigo). D-078: quem escreve código usa Sonnet, só modelagem e arte usam Opus. Todas em `Docs/Design/decisoes.md`.

## 7. Decisões técnicas do orquestrador (sem pergunta ao dono)

- **Praças menores com raio 9 m (antes 7,5 m).** A torre do relógio tem base de 7,2 m e, numa praça de 7,5 m, sobravam ~2,4 m nas diagonais: o agente (raio 0,9) não passava e os inimigos da boca norte não chegavam ao jogador. Com 9 m sobram ~3,9 m. O D-075 fixou só o tamanho da área total, então tratei como ajuste técnico; se preferir praças menores, o caminho é reduzir a torre.
- **NavMesh só do chão andável.** O grupo de colliders dos prédios, dos marcos, da vedação e dos lampiões é marcado como "Not Walkable" no bake: sem isso o chão dentro da pegada de um prédio virava uma ilha andável (onde uma carta ou um inimigo poderia aparecer).
- **Cobertura da câmera até r 83.** O chão visual vai até 85 e os prédios do fundo até ~82; o teste usa 83.

## 8. Pendentes

- **Playtest humano** do mapa novo (ritmo das ondas com a corrida dos inimigos, carta no lugar da luta, leitura do combate nas avenidas, translucidez em movimento).
- **Medir desempenho:** cerca de 220 renderers com `MaterialPropertyBlock` por quadro (veios, trilhos, torres) saem do SRP Batcher; sem medida no Profiler. A cena tem muito mais prédios e luzes (até ~34 pontuais).
- **Coop:** o passe só valeu no solo; a translucidez e o spawn pelas bocas rodam por cliente/host, mas o coop está guardado (D-018). Perguntas que esperam o coop voltar: P-003, P-004 e P-009.
- Cristais das torres e pontes de canos da cidade ficam fora da câmera de jogo (só se veem de longe); o que o jogador vê do cristal é o trilho, os lampiões, os veios e a poeira.

## 9. Problemas conhecidos

- Na vista a partir do spawn dos jogadores, só 2 postes entram no quadro (3 em 21:9): o teste de cobertura de cristal passa por pouco nesse foco.
- O recorte tem a borda serrilhada com o MSAA (risco já previsto no plano).
- `LitVazado` é uma cópia do `Lit` do URP 17.3 e precisa ser refeito quando o URP for atualizado.
- O lampião de cristal e a vedação ficam na camada `Cenario`, então o `Linecast` do `SeeThroughDriver` também os acerta: um poste fino entre a câmera e o alvo pode abrir um recorte à toa num prédio atrás.
- A crista de luz do trilho corre da praça aos portões continuamente, mas não "nasce" no instante da largada.
- Em batchmode o log mostra o aviso de licença "Access token is unavailable" (não afeta o jogo).
