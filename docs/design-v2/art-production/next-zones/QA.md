# Independent combined-batch review — 2026-10-11

One bounded review of the shipped art files; no image edits, rebuilds, application code changes, live browser or WPF checks.

## Ordered material finding

1. **Small pilea matte cleanup before shipping reusable plant art — root correction pending confirmation.** `left/plant-pilea.png` retains a warm opaque tabletop/backdrop fragment at its lower right, approximately local x21–28, y27–29. It is visible when the actual alpha image is displayed against a contrasting background; the fragment travels with the pot when transferred to the other table. Root is removing only this confirmed lower-right strip using technical alpha cleanup. The uncertain right pot contour should remain unchanged. Preserve the leaf/stem gaps: the four single-pixel transparent holes have brown source RGB and are not evidence of missing green foliage. This is a local reusable-layer issue, not a reason to reopen the accepted room direction.

2. **One-choice animation loop closure — root correction pending confirmation.** The reviewed eight-state sequence changes one choice at each ordinary transition, but its last-to-first loop transition changes four choices. Root is extending the closing sequence and adding a cyclic one-choice assertion. The independent APNG measurements below describe the original reviewed artifact, before that planned rebuild.

No other material fix identified in the reviewed states. The source-derived cabinet front lip sits above the three independent trophy bases; the representative trophies are small but recognizable. Cabinet and left furniture have a visible gap. Table/pot surface contact is plausible in the twelve displayed left-corner combinations. No major rectangular patch seam, clipped pot, missing green plant part or cabinet/left overlap was visible. The previously accepted BW sprite/background style difference was not reassessed.

## Evidence

- Read `compose.py`, combined `proof.json`, `left/assemble.py` and `left/proof.json` without executing their mutating rebuild paths. The case product is 2 chairs × 2 tables × 3 plant states × 2 cabinets × 2 shelf states = 48. Independently checked that all seven adjacent transitions in the eight-state sequence change exactly one independent choice.
- Inspected actual `state-1.png`, `state-6.png`, `left/all-12-combinations.png`, `left/plant-succulent.png` and `left/plant-pilea.png`. Compared pilea alpha with its registered contextual source crop using in-memory nearest-neighbor magnification; no artifact file was written.
- Independently decoded the actual `room-independent-swaps.png`: 150 encoded frames, 16000 ms, 707987 bytes. Compared the original GIF's opaque pet pixels at all 160 timed animation states against decoded APNG pixels. All comparisons passed; the original 67-frame sequence cycles with the intended frame index.
- Actual matte measurements: succulent retains 377 alpha pixels, discards 7 high-delta component pixels and has no isolated one-pixel holes. Pilea retains 510 alpha pixels, discards 12 high-delta component pixels and has four single-pixel alpha holes. Neither discarded set contains green-dominant pixels. Largest-component extraction does not itself prove a perfect semantic silhouette; the lower-right pilea backdrop fragment is the concrete limit observed.

## Limits

The 48-case preservation assertions were reviewed in source and existing proof; this review independently reran targeted sequence and pet/APNG checks, not the mutating 48-case build. The twelve left-corner states and two combined stills received visual inspection; all 48 combined states did not. Existing 378 trophy-size checks prove bounds, not the visual legibility of all 63 trophies. No new cabinet-top plant combination, WPF rendering, user interaction, responsive browser display or live animation playback was checked.

Status: `needs_parent_review` for final common-scene acceptance and confirmation of root's two bounded corrections. No rebuild performed by this reviewer.

## Final bounded confirmation after root corrections

Both ordered corrections are confirmed and the pending correction statuses above are superseded. Reinspected current `state-6.png` and actual `left/plant-pilea.png` only; the lower-right tabletop strip is cleared and the plant remains seated on the drawer table. Independently checked zero alpha for every pixel in local x21–28, y27–29 and the final alpha count of 492, down from 510.

Independently decoded the final APNG: **206 encoded frames, 22000 ms, 948044 bytes**. Original opaque BW pet pixels matched at **all 220 source timestamps**. The current 11-state sequence passes the one-choice distance check for all **11 cyclic transitions, including last→first**. Current root proof reports 48 checked combinations; this confirmation did not rerun the mutating combination builder.

No open material fix remains from this bounded review. Final common-scene acceptance remains with the parent/user. No broader scan, rebuild, artistic edit, browser playback or WPF check was performed; the existing review limits remain.
