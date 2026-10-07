# Relatório — Fase 9: Coop de volta (D-083 a D-087)

## 1. O que foi construído

O coop (host e cliente por IP, reconexão, sala de espera e alavanca) já existia desde a Fase 3 e estava guardado (D-015, D-018). Esta fase fecha as três regras de jogo que faltavam para jogá-lo de verdade no mapa novo:

- **Levantar um aliado (D-083, resolve P-003).** O aliado chega perto de quem caiu e segura **E** por ~2 s. Quem caiu levanta no lugar da queda, com metade da vida. O aliado fica parado (sem andar nem atacar) enquanto segura e volta a andar ao soltar. A aura de quem caiu mostra o progresso sem número: as runas acendem em anel, uma a uma, em sentido horário, e a luz e o latão voltam; ao completar, um pulso. Um caído só avança com um aliado de cada vez (o mais perto). O toque curto de E continua pegando carta e puxando a alavanca.
- **Todos caídos: a partida recomeça (D-084, D-086, D-087, resolve P-004).** Quando todos os jogadores estão caídos ao mesmo tempo (no solo, o único jogador), a tela escurece devagar (~2 s) com um som grave, em todos os clientes. No escuro a arena zera: inimigos, projéteis, cartas no chão e minas somem, a onda volta à 1 e **todas as cartas somem** (inventário, equipadas, cinto, qualidade e caminho). Todos acordam no spawn com vida cheia. No **coop** a partida volta à sala de espera (portões apagados) e o host puxa a alavanca de novo; no **solo** a largada é automática. A queda total só vale com a partida iniciada.
- **O 20 no coop dá bênção de dano ao grupo (D-085, resolve P-009).** Com 2 ou mais jogadores, o 20 do D20 dá a todos ×1,5 de dano por 30 s, com um anel dourado e faíscas douradas na aura (forma própria, além da cor; paleta alternativa respeitada, D-065). A bênção entra junto com a carta, depois do dado parar (para não revelar o 20 antes da hora, D-047). Vale para o golpe básico e para todas as skills de dano, e multiplica por cima do reforço da Mola de Recuo. No solo o 20 continua só a carta.

## 2. Arquivos

**Novos**
- Core: `Core/Combat/ReviveProgress.cs`, `Core/Combat/BlessingState.cs`, `Core/Session/WipeRule.cs`, `Core/Session/WipeSequence.cs`.
- Jogo: `Player/PlayerRevive.cs`, `Combat/ReviveSettings.cs`, `Combat/PlayerBlessing.cs`, `Net/MatchSettings.cs`, `Net/MatchReset.cs`, `UI/WipeFadeUi.cs`, `UI/WipeSounds.cs`.
- Editor: `Editor/ReviveSetup.cs`, `Editor/BlessingSetup.cs`, `Editor/MatchResetSetup.cs`.
- Dados (gerados pelo construtor): `Data/Combat/ReviveSettings.asset`, `Data/Net/MatchSettings.asset`.
- Testes: `ReviveProgressTests`, `BlessingStateTests`, `WipeRuleTests` (EditMode); `ReviveTests`, `BlessingTests`, `WipeTests` (PlayMode).

**Alterados:** `PlayerLife` (levantar, reset), `PlayerInputReader` (E segurado), `NetworkHealth`, `HealthModel`, `PlayerCombat` e os 8 efeitos de dano das cartas (multiplicador da bênção), `PlayerCards` (`ServerClearAll`, `DamageMultiplier`), `Loadout.Clear`, `PathTracker.Clear`, `MatchState.ServerResetStarted`, `WaveSpawner.ServerResetForRestart`, `CardDropService` (bênção e reset), `DiceSettings`, `PlayerShield.Deactivate`, a aura (`AuraTypes`, `AuraMapper`, `AuraVisual`, `PlayerAura`, `AuraSettings`) e `ArenaBuilder` (três chamadas novas).

## 3. Como testar

**Solo (no editor):** `Game → Setup → Construir Arena` → Play. Deixe os inimigos derrubarem você: a tela escurece, tudo zera e você volta ao spawn sem cartas e com a onda 1. As teclas de debug (F3, Shift+F3) dão cartas de volta. F2 força o 20 (no solo não dá bênção).

**Coop LAN (2 a 4 jogadores):** guia completo em `Docs/Tecnico/rede-lan.md`. Resumo:
1. Build de desenvolvimento: `Builds/Dev/Joiigo.exe` (pasta no `.gitignore`; gerada por **Game → Build → Development Build (Windows)**). Copie a pasta inteira para o outro PC.
2. Um jogador abre, aperta **F9** e clica em **Hospedar**; o IP aparece no canto. Os outros apertam **F9** e digitam o IP e clicam em **Entrar**. O host puxa a alavanca no fundo da plataforma de spawn.
3. Para ver as regras novas: deixe um jogador cair e o outro **segurar E** perto dele; deixem **os dois** caírem; e com o host faça **F2** (próximo dado vira o 20 em um dos ciclos) e **Shift+F2** (solta uma carta) para ver a bênção dourada nos dois.

**Testes:** EditMode **358/358**, PlayMode **177 passados, 0 falhas, 4 pulados** (as capturas e a medição de desempenho, explícitas). O teste automático de coop com a build (`-autohost`/`-autojoin`) conectou host e cliente no mapa novo, e a previsão de movimento do cliente fechou com o host.

## 4. Pilares

- **Pilar 3 (escolhas com peso):** a queda total custa todas as cartas; levantar o aliado custa ficar parado e exposto.
- **Pilar 2 (a sorte como emoção):** o 20 vira um momento do grupo, com brilho nas auras de todos.
- **Pilar 1 e "mostrar pelo mundo":** nada disso usa número na tela (progresso e bênção só na aura; a perda das cartas só na barra vazia depois do escuro).

## 5. Números novos (todos em ScriptableObject, provisórios)

- **`ReviveSettings`:** `holdSeconds` 2 s; `reviveRadius` 2,5 m; `graceAfterRelease` 0,3 s; `reviveHealthFraction` 0,5; `rescuerFrozen` ligado.
- **`MatchSettings`:** `wipeFadeSeconds` 2; `wipeHoldBlackSeconds` 0,6; `wipeFadeInSeconds` 1; `wipeSoundVolume` 0,8.
- **`DiceSettings`:** `blessingDuration` 30 s; `blessingDamageMultiplier` 1,5; `blessingMinPlayers` 2.
- **`AuraSettings` (visual):** `reviveRadius` 0,9; `reviveIntensity` 0,8; `blessingRingRadius` 0,5; `blessingRingBreath` 0,07; `blessingRingRate` 0,9; `blessingRingBand` 0,22; `blessingRingGlow` 1,3; `blessingSparksPerSecond` 14; `blessingSparkRiseSpeed` 2,6.

## 6. Perguntas e respostas

D-083 (segurar E por perto), D-084 (todos caídos recomeça e perde todas as cartas; vale igual no solo), D-085 (bênção de dano do 20), D-086 (escurece e volta), D-087 (coop espera a alavanca; solo é automático). Resolvem P-003, P-004 e P-009. Em `Docs/Design/decisoes.md`.

## 7. Pendentes (perguntas para o dono)

- **Tempo da queda durante o levantar:** o relógio da queda (5 s, D-022) continua correndo enquanto o aliado segura E. Se acabar no meio, quem caiu volta no spawn e o aliado é liberado. Congelar o relógio durante o levantar seria uma decisão de design.
- **Reabrir a sala depois do recomeço:** hoje quem é novo continua barrado ("A partida já começou") mesmo com todos na sala de espera. Reabrir para entrar gente nova seria uma decisão de design.
- **Teste humano em LAN** com 2 a 4 pessoas (o roteiro é `Docs/Playtest/roteiro.md`, seção Coop). É a parte que só pessoas podem fazer.
- Perguntas antigas que seguem abertas: P-006 (nomes finais dos 3 inimigos).

## 8. Problemas conhecidos

- **Restos no cliente depois do reset:** as peças e a fumaça de inimigos mortos são objetos locais com tempo próprio (~4 s) e podem aparecer no começo do clarear.
- **D20 na tela durante a queda total:** se alguém pegou a carta nos ~2,8 s antes da queda, a animação do dado continua nos clientes (a carta e a bênção pendentes foram canceladas no host).
- **Respawn por tempo no meio do escurecer:** se o relógio da queda de alguém acabar durante o escurecer, ele levanta antes do reset, de pé e exposto por até 2 s.
- **Visual da bênção e do levantar não foi visto em captura:** só testes automáticos. Na paleta alternativa o dourado é um amarelo pálido e a forma (anel e faíscas) carrega o sinal.
- **Aliados nos testes:** os testes de levantar e de queda total usam cópias do prefab do jogador no host (só há um cliente real nos testes automáticos); o comportamento com mais de um cliente real só foi conferido no teste de conexão da build e pelo playtest humano.
