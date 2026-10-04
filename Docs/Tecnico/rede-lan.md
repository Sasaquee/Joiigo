# Como testar o coop (LAN)

Fase 3: host-cliente por IP direto, até 4 jogadores. Decisões relacionadas: D-009 a D-014 em `Docs/Design/decisoes.md`.

## a) Multiplayer Play Mode (vários jogadores no mesmo PC)

Pacote `com.unity.multiplayer.playmode` 2.0.2, Unity 6.3.

1. Abra o menu **Window > Multiplayer > Multiplayer Playmode** (caminho conferido no `CHANGELOG.md` do pacote em `Library/PackageCache/com.unity.multiplayer.playmode@*/`; o código-fonte do editor não vem no pacote, então não há `MenuItem` para buscar).
2. Ative os jogadores virtuais Player 2 a Player 4 que quiser.
3. Aperte Play.
4. No editor principal, clique em **Hospedar**.
5. Em cada janela de jogador virtual, mantenha o IP `127.0.0.1` e clique em **Entrar**.

Os jogadores virtuais demoram para iniciar na primeira vez. Espere as janelas ficarem prontas antes de clicar.

## b) LAN real com amigos

1. Gere a build pelo menu **Game > Build > Development Build (Windows)**. Saída: `Builds/Dev/Joiigo.exe` (a pasta está no `.gitignore`). Copie a pasta inteira para os amigos.
2. O host abre o jogo e clica em **Hospedar**. O IP aparece no canto inferior esquerdo (se houver mais de um, são todos listados, os de LAN doméstica primeiro).
3. Cada amigo digita esse IP e clica em **Entrar**.

Requisitos:
- O Firewall do Windows precisa permitir o `Joiigo` em redes privadas (aceite o aviso na primeira execução do host).
- Porta UDP **7777**. Pode ser mudada em `Assets/_Game/Data/Net/NetSettings.asset` (campo `port`). Host e clientes devem usar a mesma.
- Todos na mesma rede local.

## c) Sala de espera e largada (D-011, D-013, D-014)

- Quem entra aparece na plataforma de latão do spawn e já pode andar.
- Os portões-máquina ficam parados e apagados.
- Só o host dá a largada: ele anda até a máquina da alavanca, no fundo da plataforma de spawn. Quando ele chega perto, os cristais da alavanca acendem na tela dele. Aperte **E** para puxar.
- Com a largada, os portões acordam.
- Depois da largada, quem tentar entrar de novo como jogador novo recebe "A partida já começou."
- Quem caiu da conexão pode voltar pelo mesmo IP, com o mesmo jogo aberto, e reaparece no spawn. O token de identificação vale só enquanto o processo do jogo estiver aberto.

## d) Teste automatizado (smoke test)

A build de desenvolvimento (e o editor) aceitam estes argumentos, tratados em `Assets/_Game/Debug/DevAutoConnect.cs`:

| Argumento | Efeito |
|---|---|
| `-autohost` | Hospeda. |
| `-autojoin <ip>` | Entra no IP (padrão `127.0.0.1`). |
| `-automove` | Cliente anda para frente por 1 s depois de conectar. |
| `-autostart` | Host dá a largada sozinho depois de 2 s. |
| `-autoquit N` | Registra as posições no log a cada 2 s e fecha depois de N segundos. |

Exemplo em PowerShell:

```powershell
$exe = "Builds\Dev\Joiigo.exe"
$host_ = Start-Process $exe -PassThru -ArgumentList "-batchmode","-nographics","-logFile","host.log","-autohost","-autostart","-autoquit","15"
Start-Sleep -Seconds 3
$client = Start-Process $exe -PassThru -ArgumentList "-batchmode","-nographics","-logFile","client.log","-autojoin","127.0.0.1","-automove","-autoquit","10"
Wait-Process -Id $host_.Id, $client.Id
Select-String "\[DevAutoConnect\]" host.log, client.log
```

No log, confira "meu jogador", "andei; previsto agora" no cliente e, no fim, "largada=True conectado=True".

## e) Limitações conhecidas

- O token de reconexão se perde se o jogo for fechado. Ao reabrir, vale como jogador novo.
- Não há migração de host. Se o host sair, a sessão acaba para todos, que voltam à tela de conexão com "O host encerrou a sessão."
- A previsão de movimento corrige erros pequenos em silêncio (zona morta de 0,15 m) e teleporta o jogador quando o erro passa de 2 m. Os dois valores ficam em `NetSettings.asset`.
