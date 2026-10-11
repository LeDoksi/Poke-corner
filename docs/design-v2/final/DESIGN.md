---
name: PokéCorner — Fixed Room
description: Cream paper controls around a detailed timber room and independent earned awards.
colors:
  forest-action: "#31694d"
  forest-selected: "#2f654d"
  forest-link: "#24543d"
  forest-button-text: "#274932"
  ink: "#26352d"
  muted-ink: "#50614f"
  white: "#fff"
  paper-ground: "#e9e2d3"
  paper-surface: "#f8f4e9"
  paper-button: "#f7f4e8"
  paper-quantity: "#ebe7d9"
  paper-shelf: "#eee7d7"
  border-window: "#b9b2a1"
  border-navigation: "#c6c0b1"
  border-button: "#929a8e"
  border-row: "#c8c2b3"
  meter-track: "#d8d7ce"
  encounter-surface: "#f4dfd0"
  encounter-ink: "#873b2c"
  encounter-border: "#b76650"
typography:
  headline:
    fontFamily: 'Nunito, "Segoe UI", sans-serif'
    fontSize: "38px"
    letterSpacing: "-0.025em"
  section-title:
    fontFamily: 'Nunito, "Segoe UI", sans-serif'
    fontSize: "24px"
  item-title:
    fontFamily: 'Nunito, "Segoe UI", sans-serif'
    fontSize: "20px"
  body:
    fontFamily: 'Nunito, "Segoe UI", sans-serif'
    fontSize: "17px"
    lineHeight: 1.5
  label:
    fontFamily: 'Nunito, "Segoe UI", sans-serif'
    fontSize: "17px"
    fontWeight: 700
  button:
    fontFamily: 'Nunito, "Segoe UI", sans-serif'
    fontSize: "16px"
    fontWeight: 700
  brand:
    fontFamily: 'Nunito, "Segoe UI", sans-serif'
    fontSize: "26px"
    fontWeight: 800
  wallet:
    fontFamily: 'Nunito, "Segoe UI", sans-serif'
    fontSize: "18px"
rounded:
  meter: "7px"
  image: "8px"
  control: "9px"
  window: "12px"
spacing:
  compact: "8px"
  text: "10px"
  small: "12px"
  control: "14px"
  action-gap: "16px"
  panel: "18px"
  row: "20px"
  grid: "24px"
components:
  button-secondary:
    backgroundColor: "{colors.paper-button}"
    textColor: "{colors.forest-button-text}"
    typography: "{typography.button}"
    rounded: "{rounded.control}"
    padding: "10px 18px"
  button-purchase:
    backgroundColor: "{colors.forest-action}"
    textColor: "{colors.white}"
    typography: "{typography.button}"
    rounded: "{rounded.control}"
    padding: "10px 18px"
  button-care:
    backgroundColor: "{colors.forest-action}"
    textColor: "{colors.white}"
    typography: "{typography.button}"
    rounded: "{rounded.control}"
    padding: "14px"
  navigation-selected:
    backgroundColor: "{colors.forest-selected}"
    textColor: "{colors.white}"
    typography: "{typography.label}"
    padding: "14px 8px"
  quantity:
    backgroundColor: "{colors.paper-quantity}"
    textColor: "{colors.ink}"
    rounded: "{rounded.image}"
    padding: "8px 14px"
  window:
    backgroundColor: "{colors.paper-surface}"
    textColor: "{colors.ink}"
    rounded: "{rounded.window}"
    padding: "18px"
  care-track:
    backgroundColor: "{colors.meter-track}"
    rounded: "{rounded.meter}"
    height: "11px"
---

# Design System: PokéCorner — Fixed Room

## Overview

**Creative North Star: "The Fixed Companion Room"**

Cream paper, forest controls and rounded Nunito lettering frame a detailed honey-timber illustration. The room carries the material richness: sunlit wood, checked fabric, foliage, a patterned rug, framed pictures and an empty side bed. Earned objects have illustrated silhouettes and consistent timber bases. Original Pokémon sprites remain independent of this artwork.

This is a local record of the shipped static sheets in `docs/design-v2/final`, not a replacement for the application theme or older design directions. Its evidence is the generated `index.html`, its `build.py`, the canonical asset manifest/proof, inspected master/first-tier-room/open-album/locked-album PNGs, and the product constraints in root `PRODUCT.md` and `DECOR.md`, `SHOP.md`, `ACHIEVEMENTS.md`. The direction contract lands as a fixed detailed companion room with independent awards; the three sheets show visiting the pet, 12 shop entries and the 26-card album with three shelf selections. The five-section navigation precedes the wide room; three care bars and care actions follow it. The form is a static Operate sheet, seed `pokecorner-fixed-room`.

CSS dimensions below are source-derived, not browser-computed measurements. Artwork appearance was inspected in PNGs; manifest coordinates are intrinsic asset measurements. `QA.md` says `ship` for art exports and code/spec consistency only. It does not establish live CSS rendering, responsive acceptance, font loading, GIF playback, WPF behavior or user visual approval. Buttons are disabled, navigation sections are spans, balances/progress are demonstration values and there is no runtime JavaScript.

**Key Characteristics:**

- Detailed fixed interior with independently positioned earned objects.
- Cream surfaces, forest actions and descriptive Nunito text.
- Plain divided catalog rows instead of boxed cards within boxed cards.
- Exact-alpha locked silhouettes that preserve first-tier secrecy.

## Colors

Warm paper carries the controls; the illustration supplies the timber, green foliage and collectible color variety.

### Primary

- **Forest action** (`forest-action`): purchase and pet-care fills, with white labels.
- **Forest selection** (`forest-selected`): the selected section of the five-part navigation.
- **Forest link** (`forest-link`): sheet links and their visible focus outline.
- **Forest button text** (`forest-button-text`): outlined secondary controls.

### Secondary

- **Encounter warmth** (`encounter-surface`, `encounter-ink`, `encounter-border`): the repeated encounter counter beside the wallet. This is a compact status treatment, not a large page accent.

### Neutral

- **Paper ground / surface / button** (`paper-ground`, `paper-surface`, `paper-button`): outer sheet, screen surface and secondary controls.
- **Paper quantity / shelf** (`paper-quantity`, `paper-shelf`): batch quantity and the three-place shelf chooser.
- **Ink / muted ink / white** (`ink`, `muted-ink`, `white`): normal copy, supporting inventory/state copy and filled-control labels.
- **Window / navigation / button / row borders**: separate the screen shell, segmented navigation, secondary controls and repeated catalog rows.
- **Meter track** (`meter-track`): the neutral remainder of the three status bars.

**The Material Carries Color Rule.** Keep the room and awards in their source raster colors; use the extracted paper and forest palette for the surrounding controls.

The three care fills are local semantic colors in the home sheet, not a general decorative accent scale. Raster artwork has many shades; sampled room pixels are not UI tokens. The build defines no CSS custom properties or tonal ramps.

## Typography

**Display Font:** Nunito, with Segoe UI and sans-serif fallbacks.
**Body Font:** The same Nunito stack.

The rounded family connects readable interface labels to the friendly illustrated room. `@font-face` loads the existing `docs/prototype-v1/assets/nunito.ttf` with `font-display: swap`; the source file exists, but live loading was not verified.

### Hierarchy

- **Headline** (38px, tracking −0.025em): each sheet title; reduced to 29px by the compact breakpoint.
- **Section title** (24px): consumable, berry and album group headings.
- **Item title** (20px): product and achievement names.
- **Body** (17px; paragraph leading 1.5): descriptions and requirements. The explanatory sheet caption alone uses 1.55 leading and a 75ch maximum.
- **Label** (700, 17px): navigation at the regular width, reduced to 13px in compact CSS.
- **Button** (700, 16px): secondary, purchase and care controls.
- **Brand** (800, 26px): the repeated application name; **wallet** (18px) uses tabular numerals.

Heading weights and heading leading are not explicitly defined in the source and therefore are not invented as tokens. No separate display or mono face ships in the UI; monospaced labels in art contact sheets belong to review tooling.

**The One Family Rule.** Use Nunito for titles, descriptions, navigation and controls; preserve the source's size-based hierarchy without introducing a separate display face.

## Layout

The outer sheet is centered at a maximum width of 1460px with 24px padding. Each repeated screen has an 18px inset and a 52px gap after it. The header separates brand and wallet; five equal navigation segments sit below it. Local title rows use a 12px gap. Shop and album rows occupy two equal columns with a 24px column gutter and horizontal separators, not independent raised cards. Repeated content spacing uses 8–24px steps recorded above; it is an observed vocabulary, not an enforced modular scale.

At the sole source breakpoint (`max-width: 850px`), outer/screen padding becomes 12px; header, wallet, title and shelf chooser wrap; shop/album become one column; care becomes a two-column grid; action buttons flex equally without the regular 220px minimum. The five navigation segments remain a flex row. These declarations have not been tested in a live viewport; no minimum supported viewport or no-overflow guarantee is established.

The art is intrinsically 1448×582 and displayed at `width: 100%; height: auto`. The generated HTML uses a relatively positioned stage and percentage positions derived from that same coordinate frame. This describes one scene's registration, not the dimensions of every future interface surface.

**The Shared Coordinate Frame Rule.** Register room, awards, shelf lip and independent pet against the same 1448×582 scene, then scale the composition together.

## Elevation & Depth

The surrounding CSS UI uses borders and tonal separation; it defines no box shadows or control motion. The artwork itself has light, cast shadows, illustrated object bases and perspective. Do not flatten or remove those native materials. Three independent trophies sit behind the separately exported shelf-front lip, so its pixels physically cover their feet. The original pet has a separate elliptical ground shadow; this is scene grounding, not a general interface elevation token.

**The Shelf Occlusion Rule.** Draw the clean room first, independent awards second, the intrinsic shelf lip third, and the independent pet above the scene layers.

## Shapes

Screen shells use the soft window radius (12px); navigation, buttons, wallet status and shelf chooser share the control radius (9px); room clipping and batch quantities use the image radius (8px); meter tracks and fills use the meter radius (7px). Borders are one pixel in the source. Product and achievement rows remain unboxed, separated by a bottom rule. Award bases and locked silhouettes retain their exported contours instead of receiving generic card clipping.

## Components

### Buttons

Readable solid actions and quiet outlined navigation controls. Secondary buttons use paper, forest text, the button border and `10px 18px` padding. Purchases share that padding with a forest-action fill and white text; pet-care actions use 14px padding and a regular-width minimum of 220px. All retain the control radius and button typography. The static build disables every button while keeping `opacity: 1`; this displays intended visual treatments without implementing gameplay. No custom hover, button focus or active state is evidenced. The sheet's actual anchor links do have a forest focus outline (3px, offset 4px).

### Quantity and wallet status

The six berry rows use a paper-quantity capsule with `8px 14px` padding and visible minus/count/plus text. It is a span, not a working stepper. The repeated encounter badge uses the warm status trio, the same padding and the control radius; wallet numerals are tabular. Demo amounts are examples, not design tokens or economic defaults.

### Navigation

Five equal-width sections use bold centered labels and `14px 8px` padding inside a bordered rounded strip. Forest selection with white text denotes the current section. The home and achievement sheets select the corner; the shop sheet selects the shop. These are static spans. The achievement album lives within the corner rather than creating a sixth main section.

### Catalog rows

Products place an independent transparent 80×80 item icon, then title, effect and inventory, with total price and purchase control at the right. Twelve 160×160 PNG sources in `assets/shop/` share illustration finish and lighting; contain-fitting preserves each distinct silhouette. The six consumables and six berries appear in two groups on one shop sheet. At widths up to 500px the icon is 64×64 and the purchase group moves below the copy; this source layout is not live-validated. Achievement rows pair a contain-fitted 132×94 image area with a name, condition, progress where applicable and state text. Their regular gap is 24px, compact gap 16px; row padding is 20px vertically. The shared row border is their separator. There are no input fields in this build.

### Care meters

The home sheet has identity plus three meters in a `1.1fr repeat(3, 1fr)` grid with a 20px gap. Tracks and fills are 11px high and share the meter radius. Labels identify care, catching and treats, so color is accompanied by text. The shown progress percentages are demo data; no animation or automatic updates ship in the sheet.

### Fixed room and three shelf places

The master raster is clean of pet and earned awards; furnishings are part of this fixed interior. Slot foot anchors are (664,280), (810,280), (956,280). The front lip occupies (590,281)–(1031,288), intrinsic size 441×7. Place each award by its own `logicalSize` and `footAnchor`, not by image-center alignment. The manifest limits the award envelope to 132×94 and records slot boundaries; proof covers all 56 awards in all three places (168 placements).

The canonical room-asset inventory is 114 PNGs: room, lip, 56 award images and 56 white exact-alpha masks. The shop adds 12 independent PNG icons; its source atlas and contact preview are not runtime assets. Eight additional room preview PNGs are review material, not game assets. The active registry is 10 four-tier chains plus 16 specials (56 appearances), represented by 26 album cards. Seven retired customization appearances and the63-image source bank remain archived. After the user approved the semantic audit,20new miniatures replaced ambiguous botanical/camp imagery and4original population stages were restored as a growing companion group. Canonical IDs and thresholds are unchanged; each current image's source and pixel hashes are recorded in the manifest. The miniature camera, warm material finish, readable large motifs and shared placement frame remain the governing art rules, not any one depicted species.

The corner has one “Достижения” action. The album opens with the “Моя полка” block containing all three selection places; there is no separate corner “Полка” button.

### Award secrecy and independent pet

**The Earned Appearance Rule.** Show the last earned chain appearance; before the first award, use only the first-tier exact-alpha silhouette, never a later-tier preview.

The canonical mask is white RGB with the award's exact alpha. A UI-owned dark brush supplies the locked fill; the static HTML implements its silhouette with `brightness(0)` and 0.8 opacity. The dark green silhouette seen in the raster contact sheet is review compositing, not an additional CSS token. Locked initial requirements and opened next-step requirements are separate in `build.py`; specials stay independent goals. Selection slots show the companion group, mountain and heart in the demonstration; real persistence and award issuance remain implementation work.

The independent original Eevee GIF is 46×47 at scene scale 3, placed at (731,383), with pixelated image rendering in CSS. This corresponds to a 138×141 scene box. Its original 67 temporal frames are preserved according to the implementation evidence. The HTML uses that original GIF separately from the raster layers; live playback is unverified. Other species require their own bounds and registration rather than inheriting Eevee's example dimensions.

## Do's and Don'ts

### Do:

- **Do** retain the source room's timber, fabric, foliage and illustrated depth while keeping controls on cream paper.
- **Do** scale the room and independent layers in one coordinate frame and position awards by their recorded feet.
- **Do** show one album entry per chain and preserve the first-tier locked silhouette until its first award.
- **Do** use Nunito, descriptive status text and the repeated forest/paper control treatments.
- **Do** keep game assets, archived sources and review previews distinct when handing off the scene.

### Don't:

- **Don't** bake the pet or earned awards into the master room or draw awards above the shelf-front lip.
- **Don't** expose future chain-tier art through a locked card or place locked awards on the shelf.
- **Don't** promote archived customization IDs or the eight preview PNGs into the active asset inventory.
- **Don't** treat static demo balances, disabled controls or CSS declarations as proof of gameplay, live rendering or user acceptance.

**Not canonized:** Full-opacity disabled controls, span-based navigation/quantity controls, and missing custom button interaction states belong to this static sheet, not a production accessibility or interaction standard; the 13px compact navigation declaration remains unvalidated for live legibility. Review-contact monospaced IDs, demo values, local progress colors and absent motion are not house-style mandates. No kickers, eyebrows, glyph icons, system display face or hard-offset UI shadows were found in the scoped artifact. No live browser/WPF validation was performed; their rendering and behavior remain implementation risks.

The extension-only sidecar is `DESIGN.json` beside this file by the delivery boundary's instruction. It uses schema version 2; frontmatter above owns primitives. Snippets document only shipped static states and do not invent interaction or tonal scales.

## Extension shipped 2026-10-11

The same paper surfaces, forest controls and Nunito hierarchy now cover settings, six onboarding steps and eighteen service/confirmation states in index.html. The added states show closed-species search and explicit individual selection. Settings use divided rows, explicit labels and native static sliders/checks. The onboarding sheet presents six numbered steps in two columns, collapsing to one below 850px; the runtime tour presents one step at a time. Confirmation states keep a visible cancel action and use the existing warm red border/ink for destructive actions. No new raster room, JavaScript, production interaction states or live rendering evidence was introduced.
