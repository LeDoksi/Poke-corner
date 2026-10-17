### Spec Compliance

- Approved: Task 1 shared foundation is spec compliant for the behavior visible in the diff. AppMenuWindow.xaml:4 establishes 1000×760 / 840×650; AppMenuWindow.xaml.cs:33-38 clamps initial size and minimum to the work area. AppMenuWindow.xaml:70-92 provides the five approved sections in order and retains the hidden legacy content routes.
- Approved: App.xaml.cs:990 supplies the current SaveData snapshot; AppMenuWindow.xaml.cs:108-115 refreshes the wallet, with load/navigation/selection/timer integration and lifecycle stop at lines 39-48, 73-75, 168-171. AppMenuWindow.xaml.cs:245-250 keeps encounters driven by the existing catch-ready signal.
- Approved: AppMenuWindow.xaml.cs:81-105 implements asynchronous completion, duplicate rejection, retained disabled background and focus restoration; lines 141-146 cancel on Escape; lines 42-48 cancel on close/unload. ConfirmationOverlay.xaml:4-5 cycles focus, and ConfirmationOverlay.xaml.cs:12-20 focuses Cancel. No nested dispatcher bridge or persistence enters the overlay.
- Approved: PokeCorner.App.csproj:41-46 links exactly the six approved source PNGs as resources; UiSystem.xaml:3-14 exposes the exact semantic colors and shared BitmapImages. PokedexTheme.xaml:6-8 merges the dictionary while retaining legacy resource keys. App.xaml needs no duplicate merge.
- Approved: Public enum values remain unchanged at AppMenuWindow.xaml.cs:9; constructor extension is final and optional at lines 26-28. Compatibility routing remains intact at lines 221-231. Only the production construction line changes in App.xaml.cs; no service/schema/economy changes appear.
- Cannot verify from diff: complete 37-state visual equivalence, cross-monitor/high-DPI work-area behavior and later scenario bodies. These are outside this shared-foundation task; source clamping alone is not hardware acceptance.

### Strengths

- AppMenuWindow.xaml.cs:86-104 uses RunContinuationsAsynchronously, clears the active prompt before completing it, and restores background input/focus before resuming callers. This avoids modal reentrancy and accidental default confirmation.
- ConfirmationOverlay.xaml:6-17 wraps prompt content for long text, with an in-window surface instead of a second window.
- approvedShellTests.cs:22-40 verifies real timer-driven wallet refresh and no reads after close; lines 52-78 exercise shown-window Cancel focus, focus cycling, routed Escape and focus restoration; lines 121-143 exercise actual button routes, duplicate rejection, navigation blocking, confirm and close cancellation.
- UiSystem.xaml:72-94 supplies five equal tabs and explicit selected/focus/disabled states, matching the approved review.html:1 header structure and review.css:1 52-DIP header/54-DIP tab dimensions. Existing view content and delegate contracts remain in place.

### Issues

#### Critical (Must Fix)

- None identified.

#### Important (Should Fix)

- None identified within Task 1.

#### Minor (Nice to Have)

- tests/PokeCorner.App.Tests/GlobalUpdate/approvedShellTests.cs:99-102 assumes an available work area of at least 1000×760, whereas AppMenuWindow.xaml.cs:33-38 deliberately shrinks on smaller work areas. The test can fail on a supported small/high-DPI desktop despite correct behavior. Assert the clamped expected values derived from SystemParameters.WorkArea, while keeping the nominal XAML sizing contract separately covered if desired.

### Full-gate failures and bounded checks

- Checked retained task-1-focused-final.log: 114 passed / 0 failed / 0 skipped. Checked retained task-1-full-app.log: 540 passed / 2 failed / 0 skipped. No tests were rerun.
- Named risk: shared theme/resource changes might explain OfflineStarterUsesCanonicalFramesBeforeAnyLegacyPngFetch. Checked acceptanceTests.cs:12-39 and unchanged Onboarding/StarterSelectionWindow.xaml.cs:76-104. The failure is deterministically explained by line 88 invoking the deliberately throwing legacy-fetch delegate before the animated-frame delegate at line 93; this path contains no Task 1 shell/overlay call. It does not expose a Task 1 regression.
- Named risk: new shell timer/theme could freeze the competing hero fixture. Checked acceptanceTests.cs:105-122 and the unchanged ThemeRenderingTests.cs:1236-1261 assertion. The wrapper constructs a CollectionView directly and requests an 850ms competing UI action against a 700ms observation deadline; it does not create AppMenuWindow or its timer/overlay. No direct Task 1 regression is exposed by this evidence. Dispatcher reentrancy/timing causation remains unproven; do not describe this as a green full suite or a diagnosed harmless flake.
- Check boundary: reviewed the package once; tool output truncated its middle, so only that missing package interval was fetched again. Read AppMenuWindow.xaml.cs:195-242 separately because diff context omitted the compatibility navigation/ShowCatchReveal methods needed to assess modal navigation guards. Other outside-diff reads were the named failure checks and approved shell HTML/CSS. No source, index, HEAD or branch mutation; this report is the only write.

### Assessment

**Task quality:** Approved.

**Reasoning:** The task supplies a compact shared shell, exact packaged resources and a safe async confirmation host with meaningful control-level tests. The two retained full-suite failures prevent an all-App-green claim but do not demonstrate a defect introduced by this task.

**Not checked:** No suite rerun, real profile, installer, full visual polish or hardware validation. The competing hero-fixture failure remains unresolved outside this task gate.
