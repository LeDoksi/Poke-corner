// src/PokeCorner.App/Popup/CatchReveal.cs
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Popup;

public sealed record CatchReveal(CatchResult Result, RarityTier Rarity);
