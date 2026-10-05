# Configurar o DeepSeek harness numa máquina nova

São duas partes: **você** só pega as chaves de API e cola no app (Parte 1). **O Claude** faz todo o resto (Parte 2). No fim, a máquina fica igual à principal: os mesmos modelos, limites, regras e o modo híbrido funcionando.

---

## Parte 1 — você (uns 10 minutos)

### 1. Chave da NVIDIA (grátis, a principal)

1. Entre em **https://build.nvidia.com** e clique em **Login** (dá para usar conta Google).
2. Abra qualquer modelo da lista, por exemplo **z-ai/glm-5-3**.
3. No bloco de código da página, clique em **Generate API Key** (ou "Get API Key").
4. Copie a chave. Ela começa com `nvapi-`.

Limite: cerca de 40 pedidos por minuto, sem teto diário. É grátis.

### 2. Chave do Google AI Studio (Gemini, grátis)

1. Entre em **https://aistudio.google.com/apikey** com a sua conta Google.
2. Clique em **Create API key**. Se ele pedir um projeto, deixe criar um novo.
3. Copie a chave.

Limite: no plano grátis o Gemini 3.8 Flash dá **cerca de 20 pedidos por dia**. Os limites atuais aparecem em https://aistudio.google.com/rate-limit. O plano Google AI Plus **não** aumenta esse limite.

### 3. Chave da OpenRouter (modelos grátis)

1. Entre em **https://openrouter.ai** e faça login.
2. Abra **https://openrouter.ai/keys** e clique em **Create Key**. Dê o nome `joiigo`.
3. Copie a chave. Ela começa com `sk-or-` e **aparece uma vez só**.

Limite: **50 pedidos por dia** nos modelos `:free`. Com **US$ 10 de crédito** (compra única, feita por você em https://openrouter.ai/credits), sobe para 1.000 por dia, e os modelos pagos baratos (DeepSeek V4 Pro, Kimi K3) passam a funcionar.

### 4. (Opcional, pago) DeepSeek oficial

Se um dia quiser: https://platform.deepseek.com → **API keys** → criar e copiar. No PC principal, o campo `DEEPSEEK_API_KEY` estava com a chave da NVIDIA por engano; não precisa dele para nada do projeto.

### 5. Colar as chaves no app

1. Abra o **DeepSeek Harness**.
2. Vá em **Configurações → Modelos** (ou "Providers").
3. Cole cada chave no provedor certo: **NVIDIA**, **Google** e **OpenRouter**. Salve.
   - Se o Google ou a OpenRouter ainda não aparecerem na lista, cole primeiro a da NVIDIA, peça a Parte 2 ao Claude e cole as outras depois. O script da Parte 2 cria os provedores.
4. Feche o app. Fechar mesmo, não só minimizar para a bandeja.

> **Segurança:** nunca cole uma chave no chat, num arquivo do projeto ou num commit. Elas ficam só no app, em `~/.dsh/.credentials.yaml`.

Depois, abra o Claude Code na pasta do projeto e peça:

> **"Leia Docs/Tecnico/configurar-deepseek-harness.md e faça a Parte 2."**

---

## Parte 2 — o Claude

Siga na ordem. Confirme com o dono antes de **instalar** qualquer coisa. **Nunca** leia nem mostre o conteúdo de `~/.dsh/.credentials.yaml`; só os nomes das chaves podem ser conferidos.

### 2.1 Pré-requisitos

Confira o que já está instalado e liste para o dono o que falta:

- `git` com acesso ao repositório `Sasaquee/Joiigo` (clone se a pasta não existir).
- **DeepSeek Harness desktop**, instalado pelo instalador oficial da DeepSeek (repositório oficial: github.com/deepseek-ai/deepseek-harness; ele se atualiza por download.deepseek.com).
- Unity Hub + **Unity 6000.3.25f1**, Blender 5.2, Python 3 com Pillow e, opcional, .NET SDK 8.

### 2.2 Achar o executável do harness

O executável de linha de comando fica em `<pasta de instalação>\resources\runtime\cli\bin\dsh.cmd`. Na máquina principal é `E:\Deepseek\...`. Se aqui for outro lugar, defina a variável de usuário (o `Tools/Agentes/dsh-tarefa.ps1` usa ela):

```
setx DSH_CMD "C:\caminho\para\resources\runtime\cli\bin\dsh.cmd"
```

### 2.3 Aplicar a configuração do projeto

1. Se `~/.dsh/profiles/desktop/cordis.patch.yml` não existir, peça ao dono para abrir o app uma vez e fechar.
2. Com o app **fechado**, rode:
   ```
   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Agentes/dsh-config/aplicar.ps1
   ```
   O script faz backup, troca só os blocos do Joiigo (provedores e modelos, timeout de 2 min, 2 tentativas, modelo padrão GLM-5.3 `medium`, no máximo 2 subagentes), instala as regras globais em `~/.dsh/AGENTS.md` e diz quais chaves estão **ok** e quais **faltam**.
3. Se faltar chave, peça ao dono para colar no app (Parte 1, item 5) e rode o script de novo.

### 2.4 Caminhos desta máquina

`AGENTS.md`, `Tools/Agentes/dsh-tarefa.ps1` e as regras dos agentes citam `E:/Unity/Editors/6000.3.25f1/Editor/Unity.exe` e `E:/Program Files/Blender/blender.exe`. Se nesta máquina os caminhos forem outros, **avise o dono** antes de mudar arquivos versionados (os caminhos valem para a máquina principal).

### 2.5 Validar, um modelo por vez (limite da NVIDIA)

Cada teste é uma tarefa só de leitura pelo script do modo híbrido:

```
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Agentes/dsh-tarefa.ps1 -Papel batedor -Modelo nvidia/z-ai/glm-5.3 -TimeoutMin 10 -Tarefa "Liste os arquivos .md da pasta Docs/Design e diga quantos são. Não altere nada."
```

Repita trocando `-Modelo` por `nvidia/nvidia/nemotron-3-super-120b-a12b`, `google/gemini-3.8-flash` e `openrouter/qwen/qwen3.8-27b:free`. Um modelo responde bem quando a resposta lista os arquivos certos. Se aparecer "!!!!", resposta vazia ou erro 429, anote e siga para o próximo.

### 2.6 Conferências finais

- No app: permissão em **acesso total**. **Nunca** use o modo seguro: ele marca a pasta com integridade baixa e quebra build e testes depois (`agentes-e-modelos.md` §4.4).
- `icacls <pasta do projeto>` **não** pode mostrar "Nível Obrigatório Baixo".
- Mostre ao dono uma tabela: provedor, modelo, funcionou ou não, tempo.

---

## O que a configuração do projeto faz

| Item | Valor |
|---|---|
| Provedores | NVIDIA (11 modelos), Google (Gemini 3.8 / 3.7 Flash, 3.5 Flash Lite), OpenRouter (5 grátis + 4 pagos, que só funcionam com crédito) |
| Modelo padrão | `z-ai/glm-5.3`, raciocínio `medium` |
| Desistir de modelo mudo | 2 minutos (`streamIdleTimeoutMs`) |
| Tentativas | 2, com espera de 15 a 60 s |
| Agentes ao mesmo tempo | 2 subagentes + o principal |
| Regras globais | `~/.dsh/AGENTS.md`: limite da NVIDIA e "nunca ler as chaves" |
| Fonte | `Tools/Agentes/dsh-config/` (referência versionada, sem chaves) |

## Problemas comuns

- **"!!!!" ou resposta vazia (Kimi K3 da NVIDIA):** ele falha depois de usar ferramenta. Use o GLM-5.3 ou outro modelo (`agentes-e-modelos.md` §4.4).
- **Erro 429:** limite por minuto (NVIDIA) ou por dia (Gemini, OpenRouter). Espere ou troque de provedor.
- **A configuração "voltou" sozinha:** o app regrava o arquivo ao fechar. Rode o `aplicar.ps1` com o app fechado.
- **Build ou testes com "Acesso negado":** a pasta foi marcada pelo modo seguro. Copie o projeto para uma pasta nova (§4.4).
