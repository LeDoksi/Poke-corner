param([switch]$Worker)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$logDirectory = Join-Path $root 'artifacts/r3-preview'
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$log = Join-Path $logDirectory 'interactive-preview.log'
if (-not $Worker) {
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"' + $PSCommandPath + '"'), '-Worker')
    $process = Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -WorkingDirectory $root -WindowStyle Hidden -PassThru
    Write-Output ("Synthetic preview starting. Close its window to finish. Process: " + $process.Id + ". Log: " + $log)
    exit
}
Set-Location -LiteralPath $root
$env:POKE_NIGHT_DIARY_INTERACTIVE = '1'
try {
    & rtk dotnet test 'tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj' --no-build --filter 'FullyQualifiedName~NightDiaryPilotTests.InteractiveSyntheticPreview' --logger 'console;verbosity=minimal' *> $log
    if ($LASTEXITCODE -ne 0) { throw ("Synthetic preview test failed. See " + $log) }
} finally {
    Remove-Item Env:POKE_NIGHT_DIARY_INTERACTIVE -ErrorAction SilentlyContinue
}
