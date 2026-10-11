# Reusable room objects

Consume only `assets/*.png`. `manifest.json` contains95direct rows, grouped by category, with dimensions/newLogicalSize, normalized footAnchor, useSlots, optional per-slot sizes, inventory id/name, sourceSHA and exact embedded prompt. `proof.json` contains executable-check evidence and placement records; `previews/` is review-only.

Plants:12small variants fit22×26 on a table,22×28 at the window and20×18 on a cabinet. Eight floor variants fit43×78 and remain left of chair. Preserve source aspect; choose recorded slotSizes then nearest resize. Canonical floor widths may be41/42pixels to keep an asymmetric pot foot inside the room.

Paintings:12identical40×34 transparent canvases, original physical frame/motif scaled uniformly inside. Center slots [221,41],[266,41],[311,41].

Trophies:63source appearances normalized at0.30of prior logical sizes, source footAnchor preserved. Shelf anchors [226,106],[262,106],[300,106]; draw cabinet frontlip after trophies. Allfourcabinet models use shared slot rectangles.

Scene uses480×193 native units. Scale complete scene with nearest sampling. Render window plant behind foreground chair; table contact varies per furniture model. Keep selectors, labels, family eligibility and inventory names semantic. No UI chrome was rasterized.

Review starting points: `previews/all-object-roles-room.png`, `previews/plants-contact.png`, `previews/paintings-contact.png`, `previews/trophies-contact.png`, and `previews/c1-all-trophies-shelf.png` through `c4-all-trophies-shelf.png`. SeeQA.mdfor limits.
