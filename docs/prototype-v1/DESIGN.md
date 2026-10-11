---
name: PokéCorner browser prototype
description: Cream-paper controls around a cozy pixel companion world.
colors:
  paper: "#faf5e7"
  cream: "#f1ead8"
  ink: "#29372c"
  muted: "#626655"
  green: "#345b43"
  rust: "#ad563c"
  line: "#d7cdb7"
typography:
  headline:
    fontFamily: "Corner, 'Segoe UI', sans-serif"
    fontSize: "26px"
    fontWeight: 800
    lineHeight: 1.2
    letterSpacing: "-0.025em"
  title:
    fontFamily: "Corner, 'Segoe UI', sans-serif"
    fontSize: "18px"
    fontWeight: 800
  body:
    fontFamily: "Corner, 'Segoe UI', sans-serif"
    fontSize: "14px"
    fontWeight: 400
    lineHeight: 1.45
  label:
    fontFamily: "Corner, 'Segoe UI', sans-serif"
    fontSize: "12px"
rounded:
  control: "7px"
  scene: "8px"
  window: "12px"
spacing:
  tight: "8px"
  group: "12px"
  inset: "13px"
  window: "20px"
components:
  button-default:
    backgroundColor: "{colors.paper}"
    textColor: "{colors.ink}"
    rounded: "{rounded.control}"
    padding: "9px 13px"
  button-primary:
    backgroundColor: "{colors.green}"
    textColor: "#fffdf2"
    rounded: "{rounded.control}"
    padding: "9px 13px"
  button-soft:
    backgroundColor: "#f4ddc8"
    textColor: "#733d26"
    rounded: "{rounded.control}"
    padding: "9px 13px"
  button-care:
    backgroundColor: "{colors.rust}"
    textColor: "#fff8ed"
    rounded: "{rounded.control}"
    padding: "9px 13px"
  search:
    backgroundColor: "#fffaf0"
    textColor: "{colors.ink}"
    rounded: "{rounded.control}"
    padding: "11px 13px"
  navigation:
    backgroundColor: "{colors.paper}"
    textColor: "{colors.ink}"
    rounded: "{rounded.control}"
  filter:
    rounded: "{rounded.control}"
    padding: "8px"
  collection-card:
    backgroundColor: "#f5efdf"
    rounded: "{rounded.control}"
    padding: "9px 5px"
---

# Design System: PokéCorner browser prototype

## Overview

**Creative North Star: "A cozy corner beside your work"**

This record describes the implemented browser prototype in this directory, not the shipped WPF application or a replacement for the root design system. Its authority is `styles.css`, `index.html`, and `app.js`, interpreted against the approved `assets/first-concept.png` and the direction recorded in `BRIEF.md`. Warm controls frame a detailed pixel room and forest; local animated Pokémon supply the living presence. Readable rounded type carries the interface rather than imitating pixel lettering.

The desktop preview leaves the companion alone by default. Decoration belongs inside the application, and optional progress indicators are a user setting. Density suits short visits, with stable navigation, compact actions and quiet result rows. The five-line direction comment was recorded after the initial build. The original hero-reproduction checkpoint and independent FORM-roll evidence were not retained; current screenshots cannot replace those historical proofs. This document records current implementation and does not assert complete shipping approval.

**Key Characteristics:**

- Cream paper and forest-green controls.
- Detailed pixel scenes with separate animated companions.
- Rounded Nunito typography and compact, repeated controls.
- Explicit state text, gentle feedback and optional desktop indicators.

## Colors

The palette combines warm paper neutrals with forest green and restrained terracotta.

### Primary

- **Forest green:** primary actions, current navigation, selected filters and collection selection.

### Secondary

- **Terracotta:** affectionate care action and feedback character; pale peach soft buttons carry supporting actions.

### Neutral

- **Paper:** application, dialog and default button surfaces.
- **Cream:** scrollbar track; retain its limited role rather than expanding it into a new surface hierarchy.
- **Ink:** main copy and outlines in the small brand mark.
- **Muted:** descriptions and secondary state text.
- **Line:** dividers and control boundaries.

**The State Has Words Rule.** Selection and availability use labels and native control state alongside color; green alone does not explain what a pet is doing.

## Typography

**Display and Body Font:** bundled Nunito (`assets/nunito.ttf`), registered as `Corner`, with Segoe UI and sans-serif fallbacks. There is no distinct display face or mono role. The variable font supports weights 200–1000; final cascade uses 800 for headings and brand, 700 for buttons, strong text and fourth-level headings.

**Character:** rounded, friendly text keeps Cyrillic actions readable against detailed artwork. The ramp is role-based rather than a strict mathematical scale: page headline, compact section title, body and supporting label.

### Hierarchy

- **Headline:** page heading; narrows to 24px at the compact breakpoint. Dialog headings also use 24px.
- **Title:** brand and recurring group headings; pet and narrative subheadings vary by context and are not additional reusable tokens.
- **Body:** descriptions and base control text.
- **Label:** navigation, filters, descriptions and supporting action text; button weight remains 700.

**The Art Carries Nostalgia Rule.** Pixel rendering belongs to scenes, items and companions; control text uses the bundled readable typeface.

## Layout

The browser presentation has a maximum 1140px workspace, 22px horizontal padding, and a 35px gap between the application column (`minmax(560px,820px)`) and 230px desktop preview. These are prototype presentation dimensions, not WPF window requirements. The application has a 62px header, five equal navigation segments, and 20px horizontal content insets. The view reserves 630px minimum height. Rows and dividers organize supporting content; collection uses three columns with 9px gaps, shop uses two columns, and pickers use three columns.

At 1000px and below, the desktop preview hides and the main column becomes at most 760px. At 600px and below, workspace insets reduce to 8px, content insets to 13px, heading actions stack, and icons and artwork shrink. Collection and picker column counts remain three; this is a compact Windows-window study, not evidence of a separately designed mobile product. The room aspect ratio is 1.8 normally, 1.95 from 1200px, and 1.6 at the compact breakpoint. The route scene is 2.1 normally and 1.8 compact.

## Elevation & Depth

The interface combines thin warm borders, tonal surfaces and diffuse window elevation. Most rows remain flat. The application, dialog and toast use soft shadows, listed exactly in the sidecar. Scene artwork supplies its own painted light and material depth; its lighting does not become a generic CSS effect.

**The Window Before Card Rule.** Reserve floating depth for the application shell, modal and transient feedback; repeated collection controls use borders and an inset selection outline.

## Shapes

Controls and shared navigation have gently rounded corners; scenes and toast have a slightly larger radius; application and dialog share the largest radius. Borders are generally 1px. Selected collection controls have a 2px inset green outline. Empty team slots use dashed borders. Circular coins and optional progress rings are domain imagery, not a pill-shaped control system.

## Components

### Buttons

Compact, labelled and tactile. Default, primary, peach supporting, terracotta care and transparent quiet variants share native button behavior. The baseline hover surface is `#eae3cf`; primary hover is `#254932`. Soft and care backgrounds remain fixed through hover in the current cascade. Pressing translates controls down 1px; disabled controls have opacity .5 and a not-allowed cursor. The shared keyboard outline is 3px terracotta (`#a85537`) with 3px offset. Do not infer an elaborate variant framework from these classes.

### Inputs / Fields

The full-width collection search uses a pale field, thin warm border and shared focus outline; its caret is terracotta. Collection filters are equal-width buttons, with green selected state. Settings uses a native checkbox with green accent. No custom validation-field system is established.

### Navigation

Five equal segments share one rounded, bordered enclosure, with thin internal separators. Inline SVG line icons accompany Russian labels. The current segment has green fill and pale text; `aria-current` exposes state. Settings and encounters sit separately in the application header. The compact window keeps all five labelled segments.

### Cards / Containers

Collection controls contain sprite, name, availability text and optional SVG favorite icon. An inset outline marks the selected pet. Team and parent controls use the same button vocabulary; occupied or incompatible pets are disabled with explanatory text. Results, achievements and shop entries generally use divided rows rather than floating cards.

### Living scenes and pixel items

`assets/room.png` and `assets/forest.png` form reusable scene backgrounds. Animated GIFs load from `../../src/PokeCorner.App/Data/animated/`, remain separate from the background and use pixelated rendering with containment. The eight-cell `assets/items.png` atlas (four columns, two rows) supplies berries, stone, fern, egg and achievement imagery; it repeats across rewards, shop, nursery, shelf and album. Check `assets/asset-provenance.json` before changing image lineage. The approved concept is a reference, not a runtime background; `second-concept.png` remains a proposal.

### Feedback and dialogs

Native dialogs provide focused selection and confirmations, a dimmed backdrop, close control and outside-click dismissal. Green status toasts use a polite live region and clear after 3600ms. Controls and toast use 150ms transitions. The pet's affection reaction rises and fades over 1s; reduced-motion CSS removes CSS animations and transitions, and the open-app scroll respects the preference. Animated GIF motion is not stopped by that media query, so the implementation does not establish a complete reduced-motion sprite mode.

### Prototype behavior boundary

State persists only under the browser localStorage key `pokecorner-prototype-v1`; reset restores demonstration data. Waiting completes through the explicit demo control. Prices, rewards, care thresholds, slots and luck marks are illustrative, not approved balance tokens. No anime audio is connected, and this prototype proves neither full expanded-catalog asset coverage nor production time progression or save migration. The desktop companion is an in-page preview; click and double-click demonstrate gestures without implementing a Windows overlay.

## Do's and Don'ts

### Do:

- **Do** use the bundled Nunito face for readable controls and pixel assets for nostalgic character.
- **Do** keep availability text with pet controls and native disabled states.
- **Do** reuse scene, item and sprite assets with their recorded provenance.
- **Do** preserve the optional nature of desktop indicators.

### Don't:

- **Don't** turn demonstration numbers into game-balance or production-window requirements.
- **Don't** claim missing historical checkpoints were satisfied by final screenshots.
- **Don't** inherit the prototype's glyph-based heart reaction, coin marker or close/back symbols as an icon system; those are remaining craft defects, while reusable control icons are SVG.
- **Don't** promote tiny footer/ring text or the shelf's hard offset shadow into reusable system tokens; the former needs context-specific legibility review and the latter is a carried craft defect outside a neobrutalist world.
