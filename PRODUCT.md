# Product

<!-- impeccable:product-schema 1 -->

## Users

The owner and a small circle of two or three friends use the app on their Windows desktops.

## Product Purpose

PokéCorner is a desktop Pokémon companion. It stays near the user's work, grows through computer activity and care, and gives the user a collection to discover and manage. Success means the pet feels present without interrupting the workday, while collecting remains rewarding.

## Positioning

An always-available desktop pet whose growth is tied to the user's normal computer activity, paired with a collectible Pokédex and care loop.

## Operating Context

The app runs as a Windows desktop companion with a tray menu, a small on-desktop sprite, and compact popup screens for collection, catching, shop, and settings. Users may also hide the pet while fullscreen apps are active.

## Capabilities and Constraints

- The current app includes activity-based catching, care points, feeding, evolution, eggs, a shop, collection, favorites, nicknames, and sprite animation.
- The core is .NET 8 and the UI is WPF/XAML.
- The app is designed to work offline after assets and catalog data are available locally.
- Activity monitoring records event counts, not typed or clicked content.
- Existing saved collections and gameplay rules must remain intact during visual redesign.
- The owner wants the product useful for personal use and sharing with a few friends; public-market needs are not established.

## Brand Commitments

The product is named PokéCorner and uses Pokémon names, sprites, and game data. No visual palette or style is a binding commitment.

## Evidence on Hand

- UI theme and tokens: `src/PokeCorner.App/Theme/PokedexTheme.xaml`.
- Main popup and collection surfaces: `src/PokeCorner.App/Popup/AppMenuWindow.xaml`, `src/PokeCorner.App/Popup/CollectionView.xaml`.
- Other popup surfaces: `src/PokeCorner.App/Popup/CatchView.xaml`, `ShopView.xaml`, and `SettingsView.xaml`.
- Existing sprites and app icon are present under `src/PokeCorner.App/Data/` and `src/PokeCorner.App/PokeCorner.ico`.

## Product Principles

- The pet and collection should each have a clear, meaningful place in the experience.
- The companion should feel present without disrupting the user's main task.
- Collection and progress should be understandable at a glance.
- Offline behavior and local-first user data should be preserved.
