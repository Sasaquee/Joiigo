# Prompt do protótipo (tarefa original)

> Texto da tarefa dada ao agente no início do projeto (2026-10-03). Junto com `visao-e-pilares.md`, é a referência do que construir e como.

## Tarefa: construir a arena de protótipo do RPG Steampunk Mágico na Unity

Você vai construir, em fases, a primeira base jogável de um RPG de ação 2.5D cooperativo. O documento de design é `visao-e-pilares.md`. Leia o documento inteiro antes de qualquer coisa. Ele é a autoridade sobre o que o jogo é; este prompt diz o que construir agora e como.

### 1. Regras que valem acima de tudo

1. Os pilares mandam. Em ordem de prioridade: (1) Identidade descoberta, (2) A sorte como emoção, (3) Escolhas com peso cinza, (4) Fusão de tecnologia e magia. Nenhuma sugestão técnica pode violar um "nunca fazemos" de um pilar.
2. Design é decisão do Thiago. Sensação, experiência, regra de jogo, controles, ritmo, o que aparece na tela, nomes, sons, escopo: tudo isso é dele. Se o documento não define algo de design, pare e pergunte. Nunca escolha por ele, nem "provisoriamente para não travar".
3. Como perguntar: use a ferramenta de perguntas de múltipla escolha. Cada pergunta tem 1 a 2 frases de contexto, uma decisão só, 2 a 4 opções com a consequência de cada uma, e uma recomendação marcada com o motivo e o pilar que ela serve. No máximo 4 perguntas por rodada. Ele sempre pode responder outra coisa. Registre cada resposta em `Docs/Design/decisoes.md`, com a resposta literal.
4. Decisões técnicas são do agente, desde que não mudem a experiência. Se uma escolha técnica afetar o que o jogador sente ou vê, ela vira pergunta de design.
5. Números são provisórios. Dano, vida, cooldown, chances e tempos ficam em ScriptableObjects, nunca no código, e são listados no relatório de cada fase para aprovação.
6. Escopo fechado. Construa só o que a fase pede. Ideias novas vão para `Docs/Design/estacionamento.md`, não para o código.
7. Git: um commit por fase, mensagem descritiva. Push só quando o Thiago pedir.
8. Pare ao fim de cada fase e espere o OK antes de seguir.

### 2. O que o protótipo prova (e o que fica de fora)

Dentro:
- Uma arena de testes, não o mundo do jogo.
- Coop local/LAN jogável, de 2 a 4 jogadores, desde o protótipo.
- Combate de meio-termo: tempo real, com a build pesando tanto quanto o reflexo.
- Cartas de tarô: pegar carta no chão, D20 visível para todo o grupo, críticos no 1 e no 20, equipar e usar.
- Aura comunicando HP, energia e estados, com som e paleta alternativa.

Fora (não implementar): mundo, história, ato curto, dilema da carta proibida, traidores, quests, NPCs narrativos, rotas, XP e nível, retrospectiva, save/load, menus de configuração além do necessário para testar.

Proibições dos pilares que já valem no protótipo:
- Nenhum número de HP, energia ou afinidade na tela durante o jogo normal (só no overlay de debug).
- Nenhum nome de classe e nenhuma tela de classe.
- O D20 nunca é escondido e nunca é modificado. Ele decide só raridade, qualidade e quantidade, nunca o tema da carta.
- Nenhum popup ou texto explicativo longo. Mostrar pelo mundo.
- Cenário e cartas mostram tecnologia e magia juntas, mesmo com placeholders.

### 3. Base técnica

Pacotes: URP, Input System (asset de ações versionado: WASD mover, mouse mirar, botão esquerdo ataque básico, 1 a 4 skills, E pegar carta, teclas de debug só em desenvolvimento; demais atalhos são perguntas de design), Netcode for GameObjects + Unity Transport, Multiplayer Play Mode, Unity Test Framework. Versões exatas em `Docs/Tecnico/versoes.md`.

Rede: host-cliente com autoridade do host (única fonte da verdade para dano, vida, cartas e dado). Clientes enviam intenções; o host valida e aplica; nunca aceitar resultado enviado pelo cliente. O D20 é rolado no host e todos veem a mesma rolagem. LAN por IP direto (tela Host / Entrar com IP).

Arquitetura: `Assets/_Game/` com subpastas por sistema; assemblies `Game.Core` (C# puro, `noEngineReferences`: tabela do D20, sorteio do tema, dano, loadout, mapeamento estado → aura), `Game.Runtime`, `Game.Editor`, `Game.Debug` (só editor/development build), `Game.Tests.EditMode`, `Game.Tests.PlayMode`. Lógica separada da apresentação. Conteúdo por dados (ScriptableObjects). Aleatoriedade injetada no Core (seed fixa em teste; em jogo, uniforme de 1 a 20 sem modificadores). Sem singletons, exceto o `NetworkManager`.

### 4. Sistemas do protótipo

- **4.1 Arena:** spawn dos jogadores, centro de combate, spawns de inimigos, área de testes de cartas. Placeholders que já mostram a fusão (cobre com cristal embutido, engrenagens, máquinas, postes com luz arcana). Instalação industrial e mágica parcialmente funcionando.
- **4.2 Personagem e câmera:** personagem genérico igual para todos; câmera 2.5D diagonal leve seguindo o jogador local, configurável no Inspector; movimento em plano.
- **4.3 Combate:** ataque básico e até 4 skills vindas das cartas equipadas. Três inimigos placeholder com IA no host: pequeno autômato corpo a corpo; drone que ataca à distância; constructo arcano resistente a dano mágico. Nomes e comportamentos finais são perguntas de design.
- **4.4 Cartas de tarô:** `CardData` (SO) com id, nome, arcano (maior/menor), naipe para menores (skill, item consumível, passiva, equipamento), número ou figura, tags de tema, raridade, custo de energia, cooldown, efeito, amaldiçoada. Efeitos como SOs combináveis. Loadout: 4 skills, 2 passivas, 2 equipamentos (+ cinto de consumíveis, D-001). Conteúdo mínimo: 5 skills, 2 itens, 2 passivas, 2 equipamentos, 1 amaldiçoada, 2 arcanos maiores de teste. Cada carta mistura máquina e magia no efeito. Tema = pool da zona + "caminho" interno do jogador (tags mais usadas), nunca exibido.
- **4.5 Carta no chão e D20:** ao pegar, o host rola o D20 e todos veem. Tabela em SO liga faixas 1–20 a raridade, qualidade e quantidade. 1: carta amaldiçoada + evento de perigo. 20: chance de arcano maior/carta única e, no coop, efeito temporário para o grupo. Revelação visual, sem texto longo.
- **4.6 Aura:** comunica HP, energia e estados; o mapeamento estado → aura fica no Core; sons sutis para perigo e HP baixo; paleta alternativa para daltonismo (tecla de debug); aura dos outros jogadores legível.
- **4.7 Debug** (editor e development build): overlay com números (HP, energia, dado, caminho), forçar próximo resultado do dado, dar carta, gerar inimigos, dano, cura, trocar paleta.

### 5. Testes

EditMode (Core): D20 só gera 1–20, distribuição uniforme com seed fixa; nenhum caminho de código modifica a rolagem (falha se aparecer parâmetro de modificador na API do dado); tabela do D20 cobre 1–20 sem buracos nem sobreposição; o resultado do dado nunca altera o tema; mapeamento da aura (HP cheio, médio, baixo, zero, com e sem estados, nas duas paletas); regras do loadout; dano e resistência do constructo.
PlayMode: teste de fumaça que sobe a arena, inicia o host, gera um inimigo e confirma dano e morte.
Todo bug corrigido ganha um teste que falharia antes da correção.

### 6. Fases

Ao fim de cada fase: compila sem erros, testes passam, commit e relatório (§7). Depois para e espera o OK.

- **Fase 0** · Análise e perguntas. ✅
- **Fase 1** · Setup. ✅
- **Fase 2** · Personagem e câmera (local), arena com placeholders. ✅
- **Fase 3** · Coop LAN: Host / Entrar com IP, 2 a 4 jogadores, Multiplayer Play Mode. ✅ (falta playtest humano)
- **Fase 4** · Combate e inimigos: ataque básico, três inimigos com IA no host, dano e morte sincronizados.
- **Fase 5** · Cartas e loadout: dados das cartas, efeitos, loadout e uso das skills.
- **Fase 6** · Carta no chão e D20: rolagem no host, animação visível para todos, tabela, críticos 1 e 20.
- **Fase 7** · Aura: mapeamento, visual, sons, paleta alternativa; remover qualquer número da tela no jogo normal.
- **Fase 8** · Ciclo completo e playtest: debug finalizado, fluxo arena → combate → carta → dado → equipar → usar, e `Docs/Playtest/roteiro.md`.

### 7. Relatório de cada fase

1. O que foi construído, em linguagem simples. 2. Arquivos criados e alterados. 3. Como testar, passo a passo (incluindo Multiplayer Play Mode quando houver rede). 4. Checagem dos pilares (pergunta-teste de cada pilar tocado). 5. Números provisórios usados e onde mudar. 6. Perguntas de design feitas, respostas e onde foram registradas. 7. Pendentes e ideias enviadas ao estacionamento. 8. Problemas conhecidos.
