### Spec Compliance

- Issues found: 1 Minor in the requested remaining UI scope (shop/settings/history/onboarding plus their bounded App/menu/collection adapters). No Critical or Important task-local production defect demonstrated. Scope reviewed: BASE 58c48cc08a47e62ec7c660bf6e80c6db558037fc -> HEAD edc909b11c9fcd8c8547cda65b92d6b29a97b77d, retained task-6-review-package.md, 2,275 lines / 138,246 bytes.
- Cannot verify from this diff: unchanged Chill/root-only acquisition, standalone species eligibility, unsupported assets, busy/active exclusions, persisted frozen encounter outcomes, global save idempotency, historical save-loss/upgrade/MSI acceptance and startup single-instance defenses. These require their owning task evidence; this presentation review does not recertify them.
- Cannot verify from this diff/evidence: exact accepted 37-state visual fidelity, real manual focus/reachability at system DPI/work-area extremes, full real-profile restore/restart, or a current all-green full App gate. The coordinated visual/fixture pass and historical recovery acceptance remain Task8/POKE-34.

### Strengths

- Shop renders the real service goods and state, not demo inventory: src/PokeCorner.App/Popup/ShopView.xaml.cs:50,67,242; tests/PokeCorner.App.Tests/GlobalUpdate/shopTests.cs:396 checks both accepted sizes, all 12 images/goods and quantity/buy action widths. Quantity validation/long total/shortage checks precede confirmation, and the service remains the mutation boundary at ShopView.xaml.cs:160.
- Failed purchases reuse the item/quantity operation ID; success removes that key before refresh, so another successful purchase can allocate a new ID (ShopView.xaml.cs:172,185). Tests at shopTests.cs:308 and :451 check refused commits, stable retry identity, disk/main/live/stock preservation and the deliberate repaired-baseline fixture. The fixture reload is explicit and does not repair or bypass production save latches.
- Per-pet shop use no longer applies to the active pet. App.xaml.cs:1004 invokes the narrow CollectionView.OpenItemSelection adapter (CollectionView.xaml.cs:395), closing stale detail/species and selecting owned individuals. shopTests.cs:420 meaningfully starts from an old detail and asserts owned catalog/detail closure/query preservation. Lucky-charm production routing goes to the existing Catch flow (App.xaml.cs:1005).
- Settings reset has two async confirmations, pending exclusion and unload generation checks (SettingsView.xaml.cs:152). The new tests at onboardingTests.cs:457,474 cover both refusal points and delayed acceptance after unload. Autostart uses injected read/write delegates; onboardingTests.cs:529 exercises denial and checkbox restoration without a registry write.
- History binds public date/count/coin fields rather than entry.ToString/path (SaveHistoryWindow.xaml:20; SaveHistoryWindow.xaml.cs:67). Selection captures the exact entry before awaiting two prompts and rejects completion after close/unload (SaveHistoryWindow.xaml.cs:61). Tests at onboardingTests.cs:492,513,665 cover both cancellation boundaries and closing either pending prompt.
- In-menu history is selected before the recovery freeze (App.xaml.cs:1372,1385,1394); the existing restore engine is retained, with pending commit wait, failed-writer memory capture, expedition flush and finally resume. The no-menu RestoreFromHistory route remains standalone (App.xaml.cs:1363). The menu host uses expected-content identity, cancels outstanding confirmation and restores prior content (AppMenuWindow.xaml.cs:83,96); onboardingTests.cs:543,709 cover usable host cancellation and old completion versus new content.
- Tour progress commits precede displayed movement; pending actions are excluded and errors remain on the current step (OnboardingWindow.xaml.cs:152; onboardingTests.cs:434,588). Final gift text reads committed queue count (OnboardingWindow.xaml.cs:71; App.xaml.cs:941). No new grant engine is introduced; existing FirstRunFlow acceptance is retained in the reported synthetic test set.
- Starter loading requests canonical frames before legacy fetch, accepts one frame, and stops after close before fallback/animation (StarterSelectionWindow.xaml.cs:78). Added tests cover empty/one/two canonical frames, null/error fallback and closing during fetch (onboardingTests.cs:610,629,649). The unchanged throwing-network regression passes in retained evidence.

### Issues

#### Critical (Must Fix)

- None demonstrated in this scoped review.

#### Important (Should Fix)

- None demonstrated in this scoped review.

#### Minor (Nice to Have)

1. Sixth tour step becomes unillustrated after gifts have been opened. OnboardingWindow.xaml.cs:133 computes a zero image count for a real empty gift queue; :134 adds no image. Replay after both gifts are opened is a normal reachable state, so the requested six illustrated steps are not preserved there. onboardingTests.cs:575 always supplies giftQueueCount: () => 1 and cannot catch this case. Add the queue-zero case and use an existing approved opened-ball/scene illustration while retaining truthful zero/all-opened text. Root has explicitly assigned this minor to the shared Task8 visual/fixture batch; no new art or fake gifts are needed.

2. Retained final-source test output is not pristine. task-6-post-gate-focused.log:5-6 reports CS8602 at tests/PokeCorner.App.Tests/GlobalUpdate/approvedCollectionTests.cs:283 and xUnit2013 at :255. These are outside this task's changed files, but remain real warnings rather than clean evidence. The owning collection/test batch should resolve nullable handling and use Assert.Empty. This is not attributed to a newly introduced Task6 production defect.

### Focused checks and retained evidence

- Read the brief/report and the full retained diff in five consecutive bounded ranges, without git re-derivation or source/index/HEAD mutation. A later mechanical unified-hunk pass emitted only source location annotations; no changed source file was reread and no cut-hunk reread was needed.
- Named risk: shared local confirmation might lack cancel-first keyboard behavior. One focused outside-diff read of src/PokeCorner.App/Popup/ConfirmationOverlay.xaml and .xaml.cs checked its cycle/focus-scope markup and Present -> CancelButton.Focus(). Ordinary menu calls inject AppMenuWindow.ConfirmAsync. This does not certify manual focus-return for every standalone fallback.
- Named risk: an unfiltered good.Effect could reintroduce gameplay probability/formula text. Focused ShopService discovery and its LoadGoods definition (src/PokeCorner.Core/Domain/ShopService.cs:21-40) establish the 12 canonical goods/6 berries and descriptions; ShopView.xaml.cs:93 substitutes the inherited care/charm mechanics copy. Remaining descriptions are item/XP quantities or short effects. shopTests.cs:493 additionally checks visible non-berry help for chance/multiplier/percent wording. No new gameplay probability/formula copy found in reviewed XAML/tour text.
- Named risk: SaveLoadException might bypass the final shop recovery guidance. Focused class-declaration query found src/PokeCorner.Core/Persistence/SaveLoadException.cs:4 derives IOException, so ShopView.xaml.cs:191's IO guidance includes that failure type.
- No tests, app launches, visual captures, real profile operations, reset/restore, MSI, registry/OS actions or suite reruns were performed by this review seat.
- Independently read the retained UTF-16 raw logs. RED was 0/3 (task-6-red.log:90-92); recipient RED was 0/1 (task-6-recipient-red.log:58-60); focused checkpoint was 161/161 (task-6-focused3.log:181-183); covering checkpoint retained 191/192 (task-6-covering-green2.log:233-236), followed by the repaired synthetic fixture 1/1 (task-6-disk-retry-green.log:21-23).
- The full App checkpoint is 674/677, 35.7494s (task-6-app-gate.log:776-779), not full acceptance. It retains the visual capture null lookup (log:211-239), obsolete Settings root cast (log:488-511) and Catch caption mismatch (log:695-726). The Settings Grid root introduced by this diff explains that specific fixture's structural incompatibility; root explicitly assigns its replacement to Task8.
- Final-source focused evidence is 14/14, 7.3821s (task-6-post-gate-focused.log:34-36), including the named prior Catch failure (log:31). This passing subset neither erases the earlier failure nor establishes its cause. No Catch source change appears in the reviewed diff; shared scheduling/noninterference remains Cannot verify pending the concrete Task8 diagnostic.
- The source shows clearing a committed shop operation key, but no explicit third purchase/success-to-success test was identified in the added coverage. The tour illustration test proves six images only for a nonempty gift queue. Hosted-menu tests exercise UI hosting; they do not drive the private App recovery adapter through actual replacement/restart.

### Assessment

**Task quality: Approved with Minor findings (0 Critical / 0 Important / 2 Minor).**
**Spec compliance: Issues found, 1 Minor illustration edge; other observable task-local requirements match the reviewed implementation.**

**Reasoning:** Production changes preserve the service/commit boundaries and provide meaningful retry/cancellation tests; the observed gift-zero illustration gap is bounded and already assigned to Task8. This is a task-scoped source review, not approval of the failing full App checkpoint or of unexercised cross-task/historical recovery guarantees.

Skipped: suite reruns, app/profile/OS operations and the coordinated visual comparison.
Risk: 3 full-gate failures remain retained; the later 14/14 subset does not establish overall acceptance or the Catch failure's cause.
