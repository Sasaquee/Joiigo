<#
.SYNOPSIS
  Entrega uma tarefa ao DeepSeek harness (modo headless) e guarda o resultado para revisão.

.DESCRIPTION
  Usado no modo híbrido (o Claude orquestra e revisa) e também à mão pelo dono.
  - Escolhe o modelo pelo papel (Tools/Agentes/papeis.json) ou por -Modelo.
  - Monta o prompt: texto do papel (.claude/agents/<papel>.md) + tarefa + regras de entrega.
  - Usa a configuração do app desktop (~/.dsh/profiles/desktop/cordis.patch.yml), trocando só o modelo padrão.
  - Roda em acesso total (o modo seguro marca a pasta e quebra build/testes; ver agentes-e-modelos.md §4.4).
  - Recusa: arte (D-044), outra tarefa do harness já rodando, pasta marcada com integridade baixa.
  - Saída em .dsh-saida/<data>-<papel>/: tarefa.md, patch.yml, eventos.jsonl, stderr.txt, final.md, resumo.txt.
  O harness nunca faz commit: quem revisa e commita é o Claude ou o dono.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Tools/Agentes/dsh-tarefa.ps1 -Papel testador -Tarefa tarefa.md
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Tools/Agentes/dsh-tarefa.ps1 -Papel batedor -Tarefa "Liste os ScriptableObjects de Assets/_Game/Data e seus campos."
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Tools/Agentes/dsh-tarefa.ps1 -Papel programador -Modelo "openrouter/qwen/qwen3.8-27b:free" -Tarefa tarefa.md
#>
param(
    [Parameter(Mandatory = $true)][string]$Tarefa,
    [string]$Papel = 'programador',
    [string]$Modelo = '',
    [string]$Esforco = '',
    [string]$Pasta = '',
    [int]$TimeoutMin = 60
)

$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding $false

function Falhar([string]$msg) { Write-Host "ERRO: $msg" -ForegroundColor Red; exit 2 }

$raiz = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ($Pasta -eq '') { $Pasta = $raiz }
$Pasta = (Resolve-Path $Pasta).Path

# --- Papel e modelo ---
$cfg = Get-Content (Join-Path $PSScriptRoot 'papeis.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$papelCfg = $cfg.papeis.$Papel
if ($null -ne $papelCfg -and $papelCfg.bloqueado) { Falhar "papel '$Papel' bloqueado no harness. $($papelCfg.motivo)" }
if ($Modelo -eq '') {
    if ($null -ne $papelCfg) { $Modelo = $papelCfg.modelo; if ($Esforco -eq '' -and $papelCfg.esforco) { $Esforco = $papelCfg.esforco } }
    else { $Modelo = $cfg.padrao.modelo; if ($Esforco -eq '' -and $cfg.padrao.esforco) { $Esforco = $cfg.padrao.esforco } }
}
$i = $Modelo.IndexOf('/')
if ($i -lt 1) { Falhar "modelo '$Modelo' deve ser provedor/id (ex.: nvidia/z-ai/glm-5.3)" }
$provedor = $Modelo.Substring(0, $i)
$modeloId = $Modelo.Substring($i + 1)

# --- Travas de segurança ---
$rodando = Get-CimInstance Win32_Process -Filter "Name='DeepSeek Harness.exe'" | Where-Object { $_.CommandLine -like '*--profile headless*' }
if ($rodando) { Falhar "já há uma tarefa do harness rodando (PID $($rodando.ProcessId -join ', ')). Uma por vez (limite da NVIDIA)." }
$acl = (icacls $Pasta | Out-String)
if ($acl -match 'Obrigat.rio Baixo|Mandatory Label\\Low') { Falhar "a pasta $Pasta está marcada com integridade baixa (sobra do modo seguro). Build e testes vão falhar. Ver agentes-e-modelos.md §4.4." }

# --- Pasta de saída ---
$saida = Join-Path $raiz ('.dsh-saida\' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + $Papel)
New-Item -ItemType Directory -Force -Path $saida | Out-Null

# --- Configuração: a do app, com o modelo padrão trocado ---
$desk = Join-Path $env:USERPROFILE '.dsh\profiles\desktop\cordis.patch.yml'
if (-not (Test-Path $desk)) { Falhar "configuração do app não encontrada em $desk" }
$yml = [IO.File]::ReadAllText($desk, $utf8)
$ini = $yml.IndexOf('- id: llm-pi-ai')
if ($ini -lt 0) { Falhar "bloco llm-pi-ai não encontrado em $desk" }
$yml = $yml.Substring($ini).Replace("`r`n", "`n")
$bloco = "    provider: $provedor`n    model: $modeloId`n"
if ($Esforco -ne '' -and $Esforco -ne 'off') { $bloco += "    reasoningEffort: $Esforco`n" }
$novo = [regex]::Replace($yml, '(- id: agent-default-model\n  name: [^\n]*\n  config:\n)(?:    [^\n]*\n)+', { param($m) $m.Groups[1].Value + $bloco })
if ($novo -eq $yml) { Falhar "não consegui trocar o modelo padrão na configuração" }
$patch = Join-Path $saida 'patch.yml'
[IO.File]::WriteAllText($patch, $novo, $utf8)

# --- Prompt ---
$textoTarefa = if (Test-Path $Tarefa -PathType Leaf) { [IO.File]::ReadAllText((Resolve-Path $Tarefa).Path, $utf8) } else { $Tarefa }
$papelTxt = ''
$papelArq = Join-Path $raiz ".claude\agents\$Papel.md"
if (Test-Path $papelArq) {
    $p = [IO.File]::ReadAllText($papelArq, $utf8).Replace("`r`n", "`n")
    $m = [regex]::Match($p, '^---\n.*?\n---\n(.*)$', 'Singleline')
    $papelTxt = if ($m.Success) { $m.Groups[1].Value.Trim() } else { $p.Trim() }
}
$prompt = @"
$papelTxt

=== TAREFA ===
$textoTarefa

=== REGRAS DESTA EXECUÇÃO ===
- Leia AGENTS.md antes de tudo e siga as regras do projeto.
- NÃO faça commit nem push. Quem revisa e commita é o Claude ou o dono.
- Não mexa em arquivos fora do que a tarefa pede. Não crie pastas de rascunho no projeto (use %TEMP%).
- Dúvida de design: não decida; descreva no seu resumo final.
- Modo headless: não encerre o turno antes de terminar; se usar subagentes ou jobs, espere com job_output.
- Unity (se precisar): "E:/Unity/Editors/6000.3.25f1/Editor/Unity.exe", só com o editor fechado, Start-Process -PassThru + WaitForExit; -runTests SEM -quit; uma Unity por vez.
- NUNCA mate processos (Unity, Unity Hub, harness). Em PowerShell não use $Args como nome de variável (é automática).
- Ao terminar, responda com: arquivos criados/alterados, comandos rodados e resultado (testes passando/falhando), dúvidas e riscos.
"@
$promptArq = Join-Path $saida 'tarefa.md'
[IO.File]::WriteAllText($promptArq, $prompt, $utf8)

# --- Execução ---
$dsh = if ($env:DSH_CMD) { $env:DSH_CMD } else { 'E:\Deepseek\resources\runtime\cli\bin\dsh.cmd' }
if (-not (Test-Path $dsh)) { Falhar "dsh.cmd não encontrado em $dsh (defina DSH_CMD)" }
$env:DSH_PERMISSION_MODE = 'danger-full-access'
$eventos = Join-Path $saida 'eventos.jsonl'
$erros = Join-Path $saida 'stderr.txt'
Write-Host "Harness: papel=$Papel modelo=$Modelo esforco=$Esforco pasta=$Pasta"
Write-Host "Saída: $saida"
$t0 = Get-Date
$proc = Start-Process -FilePath $dsh -ArgumentList @('--profile', 'headless', '--patch', "`"$patch`"", '--json', '-') `
    -WorkingDirectory $Pasta -RedirectStandardInput $promptArq -RedirectStandardOutput $eventos -RedirectStandardError $erros `
    -NoNewWindow -PassThru
if (-not $proc.WaitForExit($TimeoutMin * 60 * 1000)) {
    Write-Host "Tempo esgotado ($TimeoutMin min). Encerrando o harness." -ForegroundColor Yellow
    Get-CimInstance Win32_Process -Filter "Name='DeepSeek Harness.exe'" | Where-Object { $_.CommandLine -like "*$patch*" } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
    try { $proc.Kill() } catch {}
}
$dur = [int]((Get-Date) - $t0).TotalMinutes

# --- Resultado ---
$final = ''
$erroMsg = ''
foreach ($linha in [IO.File]::ReadAllLines($eventos, $utf8)) {
    try { $o = $linha | ConvertFrom-Json } catch { continue }
    if ($o.type -eq 'final') { $final = $o.text }
    if ($o.type -eq 'status' -and $o.phase -eq 'turn_end' -and $o.reason.kind -eq 'error') { $erroMsg = $o.reason.error.message }
}
[IO.File]::WriteAllText((Join-Path $saida 'final.md'), $final, $utf8)
Push-Location $Pasta
$ErrorActionPreference = 'Continue'  # o git escreve avisos (fim de linha) no stderr
$status = (git -c core.safecrlf=false status --short 2>$null | Out-String)
$diff = (git -c core.safecrlf=false diff --stat 2>$null | Out-String)
$ErrorActionPreference = 'Stop'
Pop-Location
$resumo = "papel=$Papel modelo=$Modelo duracao=${dur}min erro=$erroMsg`n`n== git status ==`n$status`n== git diff --stat ==`n$diff"
[IO.File]::WriteAllText((Join-Path $saida 'resumo.txt'), $resumo, $utf8)

Write-Host "`n==== RESPOSTA DO HARNESS ($dur min) ===="
if ($final) { Write-Host $final } else { Write-Host "(sem resposta final) $erroMsg" -ForegroundColor Yellow }
Write-Host "`n==== MUDANÇAS NO PROJETO ===="
Write-Host $status
Write-Host "Detalhes: $saida"
if (-not $final) { exit 1 }
