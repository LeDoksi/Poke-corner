namespace PokeCorner.App.Popup;

/// Human-readable names for the offline starter catalog in
/// src/PokeCorner.Core/PokeApi/OfflineStarterFallback.json (9 chains, 27 species across all
/// evolution stages) — UI-only display data, not game logic, which is why it lives here
/// rather than in Core. When the full Gen 1-5 catalog lands (see
/// docs/superpowers/plans/2026-08-19-pokecorner-full-experience-and-installer.md), this
/// needs to grow to match — species ids not found here fall back to "Покемон #{id}".
public static class SpeciesNames
{
    private static readonly Dictionary<int, string> Names = new()
    {
        [1] = "Бульбазавр", [2] = "Ивизавр", [3] = "Венузавр",
        [4] = "Чармандер", [5] = "Чармелеон", [6] = "Чаризард",
        [7] = "Сквиртл", [8] = "Вартортл", [9] = "Бластойз",
        [10] = "Катерпи", [11] = "Метапод", [12] = "Батерфри",
        [16] = "Пиджи", [17] = "Пиджотто", [18] = "Пиджот",
        [152] = "Чикорита", [153] = "Бэйлиф", [154] = "Мегениум",
        [155] = "Циндаквил", [156] = "Квилава", [157] = "Тайфлозион",
        [158] = "Тотодайл", [159] = "Кроконав", [160] = "Фералигатр",
        [25] = "Пикачу", [26] = "Райчу",
    };

    public static string GetName(int speciesId) =>
        Names.TryGetValue(speciesId, out var name) ? name : $"Покемон #{speciesId}";
}
