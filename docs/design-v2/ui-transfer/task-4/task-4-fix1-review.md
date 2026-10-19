### Verdict

**Important reload-before-completion: ADDRESSED. Task4 fix round1: Approved.**

- Reviewed only04246b40bc5e7be04cc0131c3753fb68324666ab..45ca1520e75e2360798478e0e6edc3fb90df6bcc in the supplied fix package, appended correction report, original Important finding and retained RED/GREEN log tails. No source reread, git rederivation, outside-code check or test rerun.

### Evidence

- NurseryView.xaml.cs:281-285,340: ReleaseBusy clears the shared busy flag and refreshes the current resumed view independently of the completed operation lifetime. This directly repairs the original disabled empty-nest retry path. The suspended check retains hidden-view immutability.
- NurseryView.xaml.cs:327,336,358,375,397,404: result/exception messages, receipt processing and hatch reveal still require the original captured lifetime; current snapshot reconciliation does not apply the stale callback result.
- NurseryView.xaml.cs:311,379,408: ready completion and hatch/activation release reconcile the same resumed controls, while suspended views receive no Refresh and no restarted timer.
- approvedNurseryFix1Tests.cs:69-70,98-115: tests now reload BEFORE releasing the pending mutation; failed Start verifies usable controls, no archive/baby/stale message/reveal, and retry with the same request. Lines119-130 preserve Hatch operation/nest identity and unique child on retry; activation/ready and successful-current-snapshot branches cover the other three release paths.
- task-4-fix1-red.log: retained summary is1 passed/7 failed of8 before correction. task-4-fix1-covering.log: retained summary is161/161 passed in12.7498seconds after correction, matching the report. Existing completion-while-still-unloaded coverage remains in that covering filter. No full App/Core rerun was claimed or required.

### New Breakage

- Critical:0. Important:0. Minor:0 introduced by this fix diff.
- The change reads the current snapshot for resumed presentation, keeps _busy held until finally and leaves failed request/hatch identities intact. No new producer, publication, RNG, schema or acquisition behavior is introduced.

### Out of Scope

- Root-deferred bulk formatting and existing Task2B CS8602/xUnit2013 warnings remain the prior Minor items; the four changed async methods are now ordinarily formatted.
- The prior606/608 full App checkpoint remains historical; Task6 starter, Task8 old capture harness, coordinated visual/DPI/manual acceptance and unchanged Core invariants are not re-reviewed by this correction.
- Review performed no source/index/HEAD/branch mutations, real-profile/application/MSI/OS changes, subagents or suite regeneration. Only this requested report was written.
