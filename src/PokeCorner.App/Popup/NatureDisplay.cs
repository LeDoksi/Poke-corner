using PokeCorner.Core.Domain;

namespace PokeCorner.App.Popup;

public static class NatureDisplay
{
    public static string Name(Nature nature) => nature switch
    {
        Nature.Hardy => "выносливый", Nature.Lonely => "одинокий", Nature.Brave => "храбрый",
        Nature.Adamant => "непреклонный", Nature.Naughty => "озорной", Nature.Bold => "смелый",
        Nature.Docile => "послушный", Nature.Relaxed => "расслабленный", Nature.Impish => "шаловливый",
        Nature.Lax => "беспечный", Nature.Timid => "робкий", Nature.Hasty => "торопливый",
        Nature.Serious => "серьёзный", Nature.Jolly => "весёлый", Nature.Naive => "наивный",
        Nature.Modest => "скромный", Nature.Mild => "мягкий", Nature.Quiet => "тихий",
        Nature.Bashful => "застенчивый", Nature.Rash => "опрометчивый", Nature.Calm => "спокойный",
        Nature.Gentle => "нежный", Nature.Sassy => "дерзкий", Nature.Careful => "осторожный",
        Nature.Quirky => "причудливый", _ => "неизвестный"
    };
}
