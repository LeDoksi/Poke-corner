# Night Diary synthetic WPF preview

Run from the isolated night-diary-pilot worktree. This test fixture constructs WPF views with in-memory data and packaged assets. It does not invoke App.OnStartup, read a production profile, install MSI, register autostart, or start desktop overlay/tray services. Changes in the preview last only for this process.

Capture actual controls at 1000x760 and 840x650:

```powershell
$env:POKE_NIGHT_DIARY_CAPTURE_DIR = Join-Path (Get-Location) 'artifacts/r3-preview'
rtk dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter 'FullyQualifiedName~NightDiaryPilotTests.CaptureIsolatedNightDiaryMatrix'
Remove-Item Env:POKE_NIGHT_DIARY_CAPTURE_DIR
```

Interactive synthetic window (close the window to finish the test):

```powershell
$env:POKE_NIGHT_DIARY_INTERACTIVE = '1'
rtk dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter 'FullyQualifiedName~NightDiaryPilotTests.InteractiveSyntheticPreview'
Remove-Item Env:POKE_NIGHT_DIARY_INTERACTIVE
```

Do not run other WPF tests concurrently with interactive mode; the test holds the shared STA dispatcher while the preview is open. It begins with three level10 Pokemon and a separate active Eevee; actual ExpeditionService computes the preview. Start/cancel/claim mutate synthetic memory only; normal time still passes, and no accelerated fake clock or production mutation is introduced.

The PNG matrix covers empty/selected preparation, picker/empty search, running/cancel confirmation, ready/result, and injected write refusal. It checks visible primary controls stay within both capture sizes. Rendering PNG at 96 DPI does not establish physical per-monitor DPI, focus, hardware input, sound or animation smoothness. Existing behavioral tests remain authoritative for producer persistence/retry.
