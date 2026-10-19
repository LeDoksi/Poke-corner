param([string]$Filter, [string]$Log)
$env:POKE_SHELL_CAPTURE_DIR = Join-Path $env:TEMP 'pokecorner-task4-captures'
if ($Filter) { dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter $Filter --logger 'console;verbosity=normal' *> $Log }
else { dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --logger 'console;verbosity=normal' *> $Log }
$testExit = $LASTEXITCODE
Write-Output "NATIVE_EXIT=$testExit"
Get-Content $Log -Tail 14
exit $testExit
