### Spec Compliance

- ❌ Issues found: retry after navigation can remain disabled if the view reloads before a failed start finishes (NurseryView.xaml.cs:295). Other inspected Task4 requirements are implemented within the authorized scope.
- ⚠️ Cannot verify from diff: global 37-state visual fidelity/DPI/manual acceptance and actual OS motion-policy delivery; these remain the coordinated later gate. Canonical Chill-only and complete acquisition/economy invariants remain in unchanged Core; this review does not re-audit them.
- Reviewed aa7e332d2e7f57d77cc86b82732dfa87a4e64b7f..04246b40bc5e7be04cc0131c3753fb68324666ab through the supplied full package in bounded sequential passes. No changed source file reread or git rederivation; later extraction from package only located exact line references.

### Strengths

- NurseryView.xaml.cs:94-101,125-132,156-195: exact GUID draft selection has canonical pair validation, distinct/available compatible second parents, bounded group/individual pages and bounded sprite caching; cancellation preserves the applied pair. Normal/shiny specimens remain separate.
- NurseryView.xaml.cs:219-237,265,270-278,297-308: preview/wait/ready avoid reading the frozen true child; normal-form discovery masks names independently, stable hatch IDs survive refusal, and publication requires the matching archived child/receipt before the baby appears.
- App.xaml.cs:1127-1154: the authorized active/unsupported check executes inside the existing candidate mutation immediately before canonical Start; completed-operation replay still reaches canonical fingerprint validation. No Core/writer/schema/economy rewrite.
- NurseryView.xaml:23-38; NurseryView.xaml.cs:200-215: packaged whole scenes replace the separate egg and ready exposes one local Hatch action.
- approvedNurseryMoreTests.cs:60-96; reactionsTests.cs:256-279: proposed-write failures retain secrecy, charm stock, frozen identity and metadata until retry; the authorized interrupted-hatch update adds hidden-view immutability plus saved-child resume without removing uniqueness/receipt assertions.
- Raw evidence checked: task-4-label-covering.log reports153/153; task-4-app-gate.log:671-674 reports606/608 before the last label fix. The two failures at253 and268 match the reported old visual harness and starter cases. No suite was rerun.

### Issues

#### Critical (Must Fix)

- None.

#### Important (Should Fix)

- NurseryView.xaml.cs:295 — reload-before-completion can strand a refused start. Start disables the controls, Unloaded increments the lifetime, and Loaded refreshes while _busy is still true (60-67). If the pending start then fails, finally clears _busy but refuses Refresh because its captured lifetime is old. With no nest, subsequent timer ticks also return without Refresh (282-283). The visible first-use nursery therefore retains disabled Choose/Start controls and cannot retry until another navigation cycle. Keep stale messages/reveals suppressed, but reconcile enabled state from the current snapshot when the operation releases busy and the view is currently loaded. Add a focused regression with pending start -> Unloaded -> Loaded -> failure, asserting controls become usable, no stale message/child appears, and retry retains the same request. Existing lifecycle coverage (approvedNurseryMoreTests.cs:34-55) always reloads after completion and does not cover this order.

#### Minor (Nice to Have)

- NurseryView.xaml.cs:125-195,200-245,287-318 — the rewritten selection/render/lifetime logic compresses multiple mutations, guards and callbacks onto single lines. Expand the touched methods into ordinary formatted statements so transaction and lifetime changes can be reviewed without overlooking ordering. No abstraction/refactor is needed.
- task-4-app-gate.log:5-6 — the App checkpoint is not warning-clean: CS8602 at approvedCollectionTests.cs:283 and xUnit2013 at255. These paths are outside Task4 and were not inspected further; root should retain/route the known warnings rather than describing this gate as pristine.

### Focused Outside Checks / Cannot Verify

- Compatibility-index risk: checked unchanged NurseryCatalog.cs:90-111 only. Partners is built solely from available recipes, and Find(PokemonIndividual, PokemonIndividual) rejects identical GUIDs; the new second-parent prefilter is consistent.
- Reload/retry shared-state risk: checked unchanged AppMenuWindow.xaml.cs:28-80,114-123. The shell retains the same NurseryView as tab content; its periodic snapshot refresh updates wallet/trip badges, not nursery controls. It does not repair the Important finding.
- No focused test was run: the missing ordering is directly determined by the reviewed guards; recommended regression is specified above. No source/index/HEAD/branch, real profile, App, MSI or OS changes.

### Assessment

**Task quality:** Needs fixes

**Reasoning:** The feature preserves the principal selection, secrecy and durable-publication protections and has substantial focused evidence. Fix the reload-before-failed-start control reconciliation before accepting Task4; global visual/cross-task gates remain separate.
