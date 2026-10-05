<#
.SYNOPSIS
  Aplica a configuração do Joiigo no DeepSeek harness desta máquina (ver Docs/Tecnico/configurar-deepseek-harness.md).

.DESCRIPTION
  - Faz backup de ~/.dsh/profiles/desktop/cordis.patch.yml em ~/.dsh/backups/.
  - Troca só os blocos do Joiigo (llm-pi-ai, agent-default-model, subagent, workflow-ptc) pelos de
    Tools/Agentes/dsh-config/cordis.patch.yml. Os blocos que o próprio app escreveu (ui-*, conta) ficam como estão.
  - Copia as regras globais (AGENTS.global.md) para ~/.dsh/AGENTS.md (com backup se já existir e for diferente).
  - Não lê nem mexe nas chaves (~/.dsh/.credentials.yaml).
  Rode com o app do DeepSeek harness FECHADO (ele pode regravar o arquivo ao sair).
#>
$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding $false
$home2 = if ($env:DSH_HOME) { $env:DSH_HOME } else { Join-Path $env:USERPROFILE '.dsh' }
$desk = Join-Path $home2 'profiles\desktop\cordis.patch.yml'
$ref = Join-Path $PSScriptRoot 'cordis.patch.yml'
$global = Join-Path $PSScriptRoot 'AGENTS.global.md'

if (-not (Test-Path $desk)) { Write-Host "ERRO: $desk não existe. Abra o app do DeepSeek harness uma vez (e feche) antes." -ForegroundColor Red; exit 2 }

# Entradas de topo: cada uma começa em "- " na coluna 0 (comentários soltos vão junto com a entrada seguinte).
function Split-Entries([string]$text) {
    $entries = New-Object System.Collections.Generic.List[string]
    $current = New-Object System.Text.StringBuilder
    foreach ($line in ($text -replace "`r`n", "`n").Split("`n")) {
        if ($line.StartsWith('- ') -and $current.ToString().Trim().Length -gt 0 -and $current.ToString() -match '(?m)^- ') {
            $entries.Add($current.ToString()); [void]$current.Clear()
        }
        [void]$current.Append($line + "`n")
    }
    if ($current.ToString().Trim().Length -gt 0) { $entries.Add($current.ToString()) }
    return $entries
}
function Entry-Id([string]$entry) { if ($entry -match '(?m)^- id:\s*(\S+)') { return $Matches[1] } return $null }

$ours = Split-Entries ([IO.File]::ReadAllText($ref, $utf8))
$oursIds = @($ours | ForEach-Object { Entry-Id $_ } | Where-Object { $_ })
$theirs = Split-Entries ([IO.File]::ReadAllText($desk, $utf8))

$kept = @($theirs | Where-Object { $id = Entry-Id $_; -not ($id -and $oursIds -contains $id) })
$result = (($kept + @($ours | Where-Object { Entry-Id $_ })) -join '').TrimEnd() + "`n"

$backups = Join-Path $home2 'backups'
New-Item -ItemType Directory -Force $backups | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
Copy-Item $desk (Join-Path $backups "cordis.patch.yml.$stamp")
[IO.File]::WriteAllText($desk, $result, $utf8)
Write-Host "Configuração aplicada: $desk (backup em $backups)"
Write-Host ("Blocos do Joiigo: " + ($oursIds -join ', '))

$globalDest = Join-Path $home2 'AGENTS.md'
$newGlobal = [IO.File]::ReadAllText($global, $utf8)
if ((Test-Path $globalDest) -and ([IO.File]::ReadAllText($globalDest, $utf8) -ne $newGlobal)) {
    Copy-Item $globalDest (Join-Path $backups "AGENTS.md.$stamp")
}
[IO.File]::WriteAllText($globalDest, $newGlobal, $utf8)
Write-Host "Regras globais: $globalDest"

# Conferência dos nomes das chaves (só os nomes, nunca os valores).
$cred = Join-Path $home2 '.credentials.yaml'
$needed = 'NVIDIA_API_KEY', 'GEMINI_API_KEY', 'OPENROUTER_API_KEY'
$present = @()
if (Test-Path $cred) {
    $inRefs = $false
    foreach ($line in [IO.File]::ReadAllLines($cred, $utf8)) {
        if ($line -match '^refs:') { $inRefs = $true; continue }
        if ($inRefs -and $line -match '^\s+([A-Z0-9_]+):') { $present += $Matches[1] }
        elseif ($inRefs -and $line -match '^\S') { $inRefs = $false }
    }
}
foreach ($n in $needed) {
    $userVar = [Environment]::GetEnvironmentVariable($n, 'User')
    if ($present -contains $n -or $userVar) { Write-Host "  chave ${n}: ok" } else { Write-Host "  chave ${n}: FALTA (cole no app: Configurações > Modelos)" -ForegroundColor Yellow }
}
