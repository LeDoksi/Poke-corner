### Spec Compliance

- ✅ Spec compliant under the root authority clarification below. No missing or extra Task 5 implementation found in the seven-file BASE e4de4d72244af18e3088905f22a4d3cb94329e02 → HEAD 6ca78beb51a9779c176ef08411f1990ed4711db0 package.
- Binding clarification: the task brief's “must not reappear on a second capture” wording overextended the original user complaint about a repeated mystery heading within a throw and on a known expedition. Root explicitly ruled during this review that each independent unknown closed ball may show “Кто внутри?” once before Throw. Its throw phases and every known expedition must not return to that heading. Root owns the brief/ledger correction. Under that clarified contract, CatchView.xaml.cs:163 is valid; :231 changes to “Новая встреча” / the known species, and no subsequent phase restores the mystery caption. catchTests.cs:177–186 validly verifies the next independent closed scene.
- ⚠️ Cannot verify from this task diff: full preservation of Chill-only acquisition, every standalone/forked family, all busy/active exclusions, and every frozen/unsupported producer partition. Production Core was not changed; App.xaml.cs:677 retains EncounterService.Open and :1038 retains ShopService.TryActivateLuckyCharm. Existing focused service tests passed, but this review did not re-audit the full unchanged engine.
- ⚠️ Cannot verify from this diff/evidence: actual assembled 6250 ms rhythm/sound, GIF/frame appearance, 37-state visual match, long-name and complete responsive/DPI behavior, complete Russian facts, or MSI/reboot acceptance. Those remain the later coordinated gates.
- ⚠️ Ordinary menu close/reopen is structurally verified below; the new tests simulate Unloaded / new CatchView and a real save boundary, rather than actually closing/reopening AppMenuWindow. A synthetic assembled close/reopen check remains appropriate at Task 8. No actual-profile or OS operation was used.

### Strengths

- Exact encounter navigation precedes grant: ExpeditionView.xaml.cs:505–507 uses the optional entry callback; App.xaml.cs:1048–1051 pins then navigates. CatchView.xaml.cs:106–115 accepts only a resolved expedition with public species and saved individual. App.xaml.cs:662–680 validates ID/source/readiness again in the candidate, calls the canonical producer, then publishes the committed receipt. The pending receipt cannot be replaced by another encounter.
- Unknown identities remain closed before the result: CatchView.xaml.cs:145–174 only resolves/fetches an expedition's public identity; :237–240 clears unknown art before animation. The new source tests at catchTests.cs:38–61 and phase assertions at :76–98 exercise the distinctions.
- Exact phase delays and pauses are explicit: CatchView.xaml.cs:248–277 implements 1100 + 650 + 800 + 3×1000 + 700 ms, with zero-angle hold after 55% of each wobble. :242–244 reads the injected/default Windows animation policy and uses the calm 250 ms path. catchTests.cs:76–110 verifies order, total, concealment and reduced motion without altering OS settings.
- Skip/restore use the existing committed object, rather than issuing another producer call: CatchView.xaml.cs:296–307. Continuation clears presentation/pinning and acknowledges the pending App receipt (:195–200; App.xaml.cs:1036). Real temporary-save tests at catchTests.cs:272–334 and :337–389 exercise commit-before-reopen, refusal, preserved live state and same-ID retry.
- Lifecycle invalidation is explicit: CatchView.xaml.cs:68–95 subscribes once, detaches Tick/preference events, cancels delays, stops clocks, and increments art/scenario generations. :211–215 handles commit completion after navigation; :347–355 prevents stale art completion from overwriting a new/hidden scene. catchTests.cs:151–174, :243–268 and the real receipt test cover these boundaries.
- The charm is activated only through a durable App callback (App.xaml.cs:1037–1040); CatchView.xaml.cs:122–132 reports stock/reservation and truthful frozen-source limitations, while :180–188 prevents repeat activation during an operation. No newly added catch copy exposes percentages/formulas, and no ordinary confirmation window was added.
- Canonical normal/shiny assets precede legacy fallback; unavailable shiny cannot request normal/network fallback (App.xaml.cs:687–693; CatchView.xaml.cs:347–355). catchTests.cs:411–435 checks the App adapter against actual packaged bytes.

### Issues

#### Critical (Must Fix)

- None.

#### Important (Should Fix)

- None under the root clarification of the caption requirement.

#### Minor (Nice to Have)

- M1 — Retained pre-existing test warnings remain visible in the final build output: approvedCollectionTests.cs:283 CS8602 and :255 xUnit2013, recorded at task-5-logs/final-app-gate.log:5–6. These are outside this task's changed paths and do not establish a Task 5 regression, but the controller should retain/assign them so the final gate can be pristine.

### Evidence and exact focused checks

- Review input: task-5-review-package.md, 1425 lines, seven source/test files. The initial tool transport truncated its middle; only the omitted package spans were fetched to complete the single review pass. Line references above were calculated from that package's new-side hunk counters. No git diff/status/log rederivation was run.
- Named risk: an ordinary menu Close might hide/reuse the old view or open a fresh scene while a receipt is pending. Ran `rtk rg -n -C 8 "Closing|Closed|Hide\\(|OnClosed|OnClosing|CloseButton_Click|RunWhenReady|Unloaded|IsVisibleChanged" src/PokeCorner.App/Popup/AppMenuWindow.xaml.cs`. AppMenuWindow.xaml.cs:143 calls Close(), :41–47 stops presentation on unload/close. The package shows App.xaml.cs:1083 discarding the closed window and :1043 restoring the pending receipt into its replacement. Because the App hunk cuts OpenMenu mid-function, read only the missing source boundary ranges 951–1005 and 1086–1130 with `rtk proxy python -X utf8 -c`; App.xaml.cs:966–967 defers reopen with RunWhenReady during a commit. No changed-source reread otherwise.
- Named risk: scene lookup compares encounter ID with expedition ID, which could choose the wrong route if the producer used different IDs. One focused source check: `rtk rg -n -C 6 "Reserve\\(|EncounterId|new Encounter" src/PokeCorner.Core/Domain/ExpeditionService.cs src/PokeCorner.Core/Domain/EncounterService.cs`. ExpeditionService.cs:229 passes the trip ID; EncounterService.cs:69 stores that exact operation ID as encounter ID. The comparison in App.xaml.cs:699 is therefore valid for the retained current expedition. Initial guessed paths without /Domain failed; `rtk rg --files src/PokeCorner.Core -g '*Encounter*' -g '*ExpeditionService*'` located only these target files before the check. No wider source crawl.
- No tests were rerun. Read retained raw result summaries with scoped `rtk proxy python -X utf8 -c` and parsed task-5-logs/results/task5-app.trx via Python ElementTree. TRX confirms 638 total / 635 passed / 3 failed and 21 CatchTests cases, all Passed. task-5-logs/red-behavior.log:300–302 confirms behavioral RED 11 failed; focused-4.log:120–122 confirms focused 101 passed.
- The final App gate remains failed, not waived: final-app-gate.log:729–732 records 638/635/3. The retained failures are OfflineStarterUsesCanonicalFramesBeforeAnyLegacyPngFetch (:317–320), CaptureAssembledApplicationMatrix (:171–185), and FinishReviewEvidenceRendersPopulatedPokedexAndResetArea (:498–512). The first points to the unchanged starter; the latter two fail before their capture harness at acceptanceVisualTests.cs:22 and shellTests.cs:211. Missing POKE_SHELL_CAPTURE_DIR is the implementer's/root's reported setup diagnosis; this review verified the assertion/stack evidence without broadening into those fixtures. These are Task 6 / Task 8 follow-ups, not evidence that the capture harness or assembled UI passed.
- Source/index/HEAD stayed read-only. No app, profile, installer, OS, art, subagent or visual-capture action was performed; the sole write was this requested review report.

### Assessment

**Task quality:** Approved.

**Reasoning:** The catch feature consistently delegates durable mutation to the existing producers, pins known encounters before navigation, restores committed results across presentation lifetimes, and provides focused behavioral plus real-save tests. No Critical/Important Task 5 defect was found after the explicit root authority clarification; this local approval does not convert the retained failed App gate or later visual gates into a pass.

**Counts:** Critical 0; Important 0; Minor 1 (pre-existing warning cleanup). Spec: compliant with explicit clarification; quality: Approved.

Not checked: assembled visual/DPI/rhythm/MSI or full unchanged Core invariants; later gates still own them.
Risk: the single final App gate is Exit 1 with three retained failures and two retained warnings.
