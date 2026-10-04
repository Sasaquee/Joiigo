# Passe visual — o que falta (cidade steampunk)

> Escrito em 2026-10-04 04:05, quando o limite de uso acabou com o agente da cidade ainda trabalhando.

## Estado

| Item | Situação |
|---|---|
| 3D pixelado (PixelCamera, PixelPost.shader, PixelRenderSetup, PixelPalette) | ✅ no repo, compila |
| Andarilho encapuzado + animação procedural | ✅ no repo |
| 14 cartas em pixel art + brilho | ✅ no repo |
| Efeitos (combate, skills, revelação de carta) | ✅ no repo |
| Componentes de vida (`Assets/_Game/Arena/Life`) | ✅ no repo |
| **Kit da cidade (18 modelos FBX)** | ✅ no repo: `Assets/_Game/Art/Models/City/`, gerador `Tools/Blender/build_city.py` |
| **Montador da cidade (`CityBuilder`)** | ⏳ escrito (~930 linhas), **não compilado nem testado**. Guardado como texto em `Docs/Tecnico/wip/CityBuilder.cs.txt` |
| Ajuste de luz com a cidade, rodar testes, relatório | ❌ falta |

Peças do kit: CasaEstreitaA, CasaEstreitaB, CasaAlta, CasaLarga, Oficina, Fabrica, TorreRelogio, Dirigivel, PonteCanos, PilaoArcano, TanqueAgua, BancaMercado, LampiaoRua, PlacaPendurada, Caixotes, CanosParede, BueiroVapor, Arco.

## O plano do montador (ideia do agente)

- Cria `Cidade` sob a raiz da arena, com:
  - calçamento até 85 m;
  - três anéis de prédios fora do muro (raios 30,6 / 44 / 57 m), sendo que o terceiro anel só existe no lado oposto à câmera (-80° a 140°);
  - 7 ruas radiais, com uma avenida norte de 7 m que leva à **torre do relógio** como marco;
  - pontes de canos entre telhados;
  - pilões arcanos;
  - mercado e caixotes na rua em volta do muro;
  - 2 a 3 dirigíveis em rota no céu.
- **Regra da câmera:** cada prédio só entra se a altura dele couber abaixo da linha entre a câmera de jogo e a praça (`AllowedHeight`). Assim o lado sul/oeste (o da câmera) fica baixo ou vazio, e o norte/leste recebe os prédios altos. Nada fora do muro tem collider.
- **Vida:**
  - `SmokeEmitter` nas chaminés, válvulas, fábrica e pilões, com uma fila de prioridade para limitar quantos sistemas são criados;
  - `EmissivePulse` nos cristais;
  - `WindowFlicker` nas casas;
  - `Sway` nas placas;
  - `Spinner` nas engrenagens e hélices;
  - `PathMover` nos dirigíveis;
  - poucas luzes pontuais (≤ 24), quentes nos lampiões e ciano nos cristais.

## Como terminar (próxima pessoa ou IA)

1. Mova `Docs/Tecnico/wip/CityBuilder.cs.txt` para `Assets/_Game/Editor/CityBuilder.cs`.
2. No `ArenaBuilder.Build()`, logo depois de `BuildBoilers(arena);`, acrescente `CityBuilder.Build(arena);`.
3. Abra a Unity, corrija erros de compilação (se houver) e rode **Game → Setup → Construir Arena**.
4. Olhe pela câmera de jogo (Play) e ajuste a luz, que ainda está escura e azulada:
   - constantes no topo de `Editor/AmbienceBuilder.cs`;
   - `Art/Shaders/PixelPost.mat` (faixas, pontilhado, contorno);
   - `Art/Ambience/ArenaVolume.asset`.
5. Rode os testes (Window → General → Test Runner, EditMode e PlayMode). Na Fase 5 eram 149 passando.
6. Feche o passe visual com um relatório no formato do §7 de `Docs/Design/prompt-prototipo.md`, registre o que mudou e siga para a **Fase 6 (carta no chão e D20)**.

Para regerar o kit: `"E:/Program Files/Blender/blender.exe" --background --factory-startup --python Tools/Blender/build_city.py`
