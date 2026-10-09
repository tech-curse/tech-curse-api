param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Comando
)

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent $PSScriptRoot
$arquivo = Join-Path $raiz '.env'

if (-not (Test-Path $arquivo)) {
    Write-Error "Arquivo .env não encontrado em $raiz. Copie o .env.example para .env e preencha os valores."
    exit 1
}

foreach ($linha in Get-Content -Path $arquivo -Encoding UTF8) {
    $linha = $linha.TrimEnd("`r")
    if ($linha -eq '' -or $linha.StartsWith('#')) { continue }
    $posicao = $linha.IndexOf('=')
    if ($posicao -lt 1) { continue }
    [Environment]::SetEnvironmentVariable($linha.Substring(0, $posicao), $linha.Substring($posicao + 1), 'Process')
}

if (-not $Comando -or $Comando.Count -eq 0) {
    $Comando = @('dotnet', 'run', '--project', (Join-Path $raiz 'src/Api'))
}

$executavel, $argumentos = $Comando
& $executavel @argumentos
exit $LASTEXITCODE
