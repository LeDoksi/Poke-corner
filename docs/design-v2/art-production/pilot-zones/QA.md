# Pilot zones: visual review

User disposition, 2026-10-11: accepted the displayed pilot («ощущается, норм, давай, работаем»). Findings below record the independent pre-acceptance inspection, including the visible residual style difference. Scope remains this small pair; the user authorized continued zone production.

Status: `needs_parent_review`. Correction batch reviewed: the previous B boundary defect is resolved; room/BW contour and palette compatibility remains partial and needs user judgment. This is not approval for a full room system or mass production.

## Review contract

Compare the actual separately assembled pilot with `../../room-layout-v2.png` (approved direction) and `../previews/assembled-room.png` (rejected collage). Inspect camera, pixel step and contours, furniture proportions, common light, floor contacts, zone boundaries, and the real animated BW Eevee. Numeric checks support this review; they do not replace it. Production beyond this pilot remains outside scope.

## Preflight evidence

- `profile.json`: 480×193 native room; display scale 3; zone bounds `[0,72,225,193]`; pet floor anchor `[278,173]`.
- Source `../../catalog-package/assets/normal/133.gif`: 46×47, 67 frames. Its first frame has a 100 ms duration and alpha bounds `[3,0,40,47]`; it exactly matches `eevee-reference.png` pixel for pixel.
- Approved room: warm wood and cream walls, shallow front three-quarter furniture view, daylight from upper left, shared warm reflections and floor-contact shadows.
- Real BW Eevee has stronger dark outlines and less saturated browns than the illustrated Eevee in the approved mock. Integer scaling preserves its source pixels but does not by itself establish visual compatibility.

## Ordered material findings

1. **Resolved — previous B sunlight/floor seam.** The correction replaces the hard rectangular replacement with a manually fitted chair/contact polygon, 3 native pixels of feathering, and a soft 2-pixel table exclusion. Reopened `assembled-b.png` at 1440×579 and enlarged x=225 / y=72 plus the chair/table mask region. The previous straight sunlight/floor cut at display x=675 is absent; the room's floor and rays now continue through that region. No similarly distracting new seam or halo is evident around the fitted join at intended display size. Changed pixels now end at native x=161, leaving the former x=225 boundary unchanged. This judgment applies to this tailored pair only.
2. **Partial — pet and room still use different shading/contour grammars.** The common 480 grid reduces the dramatic pixel-size mismatch seen in `../previews/assembled-room.png`. It does not fully erase the difference: Eevee has a sparse, hard dark outline and compact palette, while the chair/table use dense painterly color steps and softer edges. The mismatch is now smaller, but the pet remains a visibly separate sprite. The correction preserves the source pet and does not change that artistic issue. Evaluate this residual difference with the user; do not label it solved merely because all pixels were scaled by integers.
3. **Unresolved scope limit — chair mask and protected table/plant are special cases.** `[128,92,187,167]` stays exact A content; the surrounding exclusion is feathered. The table's contact shadows remain plausible, and the enlarged current join does not show a major rectangular boundary. This test still does not prove independent tables/plants or arbitrary combinations can share local shading, or that one mask can serve other furniture silhouettes. It demonstrates two chairs with one fixed table/plant, using a manually tailored mask.
4. **Scope limit — simplified composition and contact testing.** The pilot preserves the warm timber world, left window, shallow room camera and left-to-right daylight. It is a much emptier composition than the approved room: curtains, patterned rug, cabinet and paintings are absent by pilot scope. Eevee stands clear of the chair/table, so this pair tests ground contact but not furniture occlusion, moving pets behind furniture, other zone joins, or complete-room density. It must not replace the approved overall composition by implication.

## Visual observations that hold for this pair

- The wooden and upholstered chairs read as furniture in the same room rather than isolated PNG stickers. Visible top planes and legs use the room camera; chair/table feet meet the floor, and the surrounding warm shading/contact shadows are materially better than the rejected independent collage.
- The table and succulent remain stable between variants. B changes the chair's silhouette and upholstery plus nearby contact/background pixels within the fitted mask, without changing the pet or the room outside the zone.
- The pet is the genuine BW Eevee. The added small contact ellipse puts its feet on the floor; it does not recolor the sprite or establish matching reflected light. No generated Eevee is visibly baked into either background.
- At intended 1440×579 display, room and pet are nearest-neighbor scaled from their shared 480×193 composition. Coarsening is visible and intentional; the room is not crisp source-authored BW pixel art merely because it uses that output grid.

## Inspection and independent technical evidence

Opened actual initial `assembled-a.png`, `assembled-b.png`, `zone-a.png`, and `zone-b.png`, plus the approved `../../room-layout-v2.png`, rejected `../previews/assembled-room.png`, empty reference, and Eevee reference. Reopened corrected B and inspected the former outer boundary, top cut, chair edge and protected-table region at 2× in memory without writing/repainting image assets.

Independently reread the source GIF and produced APNG using Pillow:

- Source pet: 46×47, 67 frames.
- Corrected APNG: 480×193, 126 encoded frames, 134 source-time pictures, total 13,400 ms, 529,664 bytes. Identical pictures are merged without discarding source timing.
- Independently reconstructed all 134 expected source-time pictures from master + delivered zone alpha + contact ellipse + original GIF, then compared them with the actual APNG decoded at each original timestamp: zero full RGB mismatches.
- Opaque source-pet pixels: zero mismatch timestamps across all 134 source-time pictures.
- Outside-zone comparison: zero mismatching A/B frame pairs across all 67 pairs.
- Protected table/plant rectangle: exact A/B pixel equality.
- Display PNGs: both 1440×579.
- Actual APNG at the first A timestamp and B swap timestamp exactly matches the corresponding display PNG reduced by its integer scale.
- Corrected changed-pixel bounds: `[17,72,161,193]`.

These checks substantiate source-pet preservation and constrained replacement only. They are not visual approval and do not substitute for watching the animation in the intended viewer.

## Execution order

1. Show the corrected small pilot for user judgment of the residual room/BW style difference.
2. Expand to other independently selectable objects and zone contacts only after the pilot direction is accepted; count local combinations before production.

## Blockers

No missing input or tooling blocker. The former B join is resolved; pending user judgment of the remaining style difference prevents an unconditional acceptance claim.

## Assumptions

- Two chairs are alternate states, not two simultaneous seats.
- Table/plant and the rest of the room are fixed for this narrow pilot.
- Source-pet recoloring and artistic repainting were outside authorization.
- Full app implementation and mass asset production are outside this review.

Skipped: live animation playback, WPF rendering, other zone/object combinations, generation, and repainting; this review owns only this document.
Risk: residual pet/room style mismatch needs user judgment; the corrected mask is tailored to this pair, and the full customization system remains unproven.
