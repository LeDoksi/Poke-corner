# Right-room furniture production

Four contextual dresser edits and six existing standalone bed variants are complete. Each asset is a true-alpha logical-grid patch containing its independent object and its own derived contact shadow; no wall, floor, bed or UI is baked into a dresser.

## Registration

- Canvas: 480 × 193 logical pixels; nearest display scale 3.
- Dresser patches: 99 × 73, paste at (352, 76), global zone [352, 76, 451, 149]. All front feet end at y141. Model-specific object bounds and plant anchors are in manifest.json.
- Bed patches: 66 × 40, paste at (413, 132), global zone [413, 132, 479, 172]. Empty bed body x417..475 and front ground anchor (446, 166). Optional none omits this layer.
- Draw dresser first, bed later, live BW pet last. Contact shadows are already present; avoid adding duplicate shadows.

## Inspection and evidence

The four raw contextual sources were compared with the accepted common master. Source masks and uniform nearest registration preserve model camera/materials/palette. The 28 actual common-master compositions were inspected in previews/28-combinations-right-contact.png; all full-room logical and display3 previews are also saved. One technical correction restored the D2 front-foot mask. Six existing empty beds fit the warm room and shallow perspective, so no new bed art was generated.

proof.json asserts outside-zone equality for all 28 backgrounds, preserves the independent cabinet reserve [195,65,345,142] and pet reserve [255,126,301,173], and checks alpha-zero backgrounds in all ten files. The embed-prompt scan confirmed 10 rasters and zero missing prompts. Source artwork alpha tops at 253 or 254 for the existing beds; this source transparency was deliberately retained.

## Limits

This is asset production and static common-room verification. App code and root documents were not changed. WPF execution and live multi-frame pet animation were not tested. Parent owns full-room decor integration and final acceptance.
