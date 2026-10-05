# Roteiro de playtest do protótipo

O protótipo prova três coisas (visão, item 76): **carta + D20**, **aura** e **combate**, numa arena. Este roteiro serve para uma sessão de 20 a 30 minutos, solo ou em coop LAN. Quem conduz observa e anota; quem joga não recebe explicação além da que está em "Antes de começar".

## Preparação (quem conduz)

1. Unity 6000.3.25f1 → abrir o projeto → `Game → Setup → Construir Arena`.
2. **Solo:** Play. O jogo já entra com a partida começada (D-018).
3. **Coop LAN (2 a 4):** gerar uma build de desenvolvimento, ou usar o Multiplayer Play Mode.
   - Um jogador aperta **F9**, depois "Hospedar"; os outros apertam **F9**, depois "Entrar" com o IP do host.
   - O host dá a largada na alavanca.
4. Para conferir números durante o teste, **F1** abre o overlay de debug. **Não deixe aberto enquanto a pessoa joga**: o jogo é feito para não ter números na tela.
5. Tenha uma folha (ou este arquivo copiado) para as anotações abaixo.

## Antes de começar (o que dizer a quem joga)

> "Você é um andarilho numa arena no meio de uma cidade movida a vapor e cristal. Mova com WASD, mire com o mouse, ataque com o botão esquerdo. Quando uma onda acabar, aparece uma carta; pegue com F. Tab abre suas cartas. 1 a 4 usam skills; Q, E e R usam o cinto."

Não explique a aura, a tabela do dado nem a qualidade das cartas. Ver se a pessoa entende sozinha faz parte do teste.

## Durante (o que observar)

Anote o minuto e o que a pessoa disse ou fez.

### Combate
- Ela percebe quando um inimigo vai atacar (o cristal sobe e pisca)?
- Desvia dos golpes, ou só troca dano?
- Entende a diferença entre os três inimigos? Algum parece injusto?

### Carta no chão e D20
- Nota a carta flutuando no fim da onda sem você apontar?
- Que reação ela tem quando o D20 rola: espera, comenta, torce? (Pilar 2)
- No **1** (emboscada) e no **20**: o que ela diz?
- Entende que a carta repetida melhorou (a moldura)? Ou acha que "não ganhou nada"?

### Equipar e usar
- Acha o Tab sozinha? Arrasta as cartas para os espaços certos?
- Usa as skills, ou esquece que estão lá?
- Com a carta amaldiçoada: percebe que ela cobra vida?

### Aura
- **Sem você perguntar**, ela comenta o círculo aos pés?
- Quando está com pouca vida, percebe isso pela aura (encolhe, falha, batimento)? Fica mais cuidadosa?
- Percebe a energia (faíscas) e espera ela encher para usar uma skill?
- No coop: sabe dizer quem está mal só olhando a aura dos outros?

### Coop (se houver)
- O jogo trava, teleporta alguém ou dessincroniza?
- Todos veem o mesmo dado?
- Alguém fica perdido sem saber qual é o próprio personagem (anel branco)?

## Depois (perguntas, nesta ordem)

1. Em uma frase: o que você fez nesse jogo?
2. Como você sabia quanta vida tinha? E quanta energia?
3. O que acontecia quando você pegava uma carta? Qual foi o melhor momento com o dado? E o pior?
4. Alguma carta mudou seu jeito de jogar? Qual?
5. Teve algo confuso ou que você não entendeu?
6. Teve algo injusto?
7. Quer jogar de novo? Por quê?
8. (Só no fim) Mostre a aura e pergunte: "o que você acha que isso mostrava?"

## Checagem dos pilares (quem conduz, depois)

| Pilar | Pergunta | Sinal de que funcionou |
|---|---|---|
| 1 · Identidade descoberta | As cartas que chegaram combinavam com o jeito de jogar dela? | Ela cita um "estilo" sem você usar essa palavra |
| 2 · A sorte como emoção | O dado gerou expectativa? | Reação audível na rolagem; lembra de um número específico |
| 3 · Escolhas com peso cinza | Ela hesitou antes de usar a carta amaldiçoada ou de trocar uma carta boa? | Pausa, comentário, mudança de ideia |
| 4 · Tecnologia e magia | Ela descreve o mundo misturando as duas coisas? | "máquina de cristal", "vapor mágico" e afins |
| Mostrar pelo mundo | Ela entendeu HP e energia sem números? | Responde à pergunta 2 citando a aura |

## Onde registrar

- Crie `Docs/Playtest/sessao-AAAA-MM-DD-<n>.md` com: quem jogou (só um apelido), solo ou coop, duração, as anotações e as respostas.
- Problemas técnicos (travou, erro, dessincronizou) vão para "Problemas" no fim do arquivo da sessão, com o passo a passo para repetir.
- Ideias que surgirem (da pessoa ou suas) vão para `Docs/Design/estacionamento.md`, não para o código.
- Pontos que pedem decisão do dono viram perguntas em `Docs/Design/decisoes.md` (seção de pendentes).

## Teclas de debug (só quem conduz; editor e development build)

F1 overlay · F2 próximo dado (normal → 1 → 20 → 10) · Shift+F2 carta no chão · F3 dar carta (Shift+F3 todas) · F4 gerar inimigo · F5 dano em si · F6 cura · F7 paleta da aura · F9 conexão (coop).
