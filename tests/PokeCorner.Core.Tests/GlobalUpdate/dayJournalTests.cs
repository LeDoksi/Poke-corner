using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using Xunit;

namespace PokeCorner.Core.Tests.GlobalUpdate;

public class DayJournalTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 18, 0, 0, TimeSpan.Zero);

    private static PokemonIndividual Pet(int id, DateTimeOffset at, string source, bool shiny = false) => new()
    {
        SpeciesId = id, CurrentSpeciesId = id, EvolutionChainId = id, ChainLength = 1, Nature = Nature.Hardy,
        IsShiny = shiny, CaughtAtUtc = at, AcquisitionSource = source
    };

    [Fact]
    public void TodayListsOnlyWhatHappenedSinceLocalMidnightNewestFirst()
    {
        var data = new SaveData
        {
            Settings = new AppSettings(),
            Individuals = [Pet(25, Now.AddHours(-2), "activity"), Pet(133, Now.AddHours(-1), "nursery", shiny: true), Pet(1, Now.AddDays(-1), "activity")]
        };
        data.AwardLedger["a"] = new AwardEntry { AwardedAtUtc = Now.AddMinutes(-5) };
        var events = DayJournal.Today(data, Now, id => $"№{id}", TimeSpan.Zero);
        Assert.Equal(["Новая награда в альбоме", "В питомнике вылупился №133, шайни", "Встретили: №25"], events.Select(e => e.Text));
        // Three hours east the day began earlier, but yesterday's catch is still yesterday's.
        Assert.Equal(3, DayJournal.Today(data, Now, id => $"№{id}", TimeSpan.FromHours(3)).Count);
    }

    [Fact]
    public void RunningShowsTripAndNestUntilTheyAreReady()
    {
        var data = new SaveData { Settings = new AppSettings(), Individuals = [] };
        data.Expedition = new ExpeditionState { Status = "running", ReadyAtUtc = Now.AddMinutes(95) };
        var running = Assert.Single(DayJournal.Running(data, Now));
        Assert.Equal("1 ч 35 мин", DayJournal.Remaining(running.AtUtc, Now));
        Assert.Empty(DayJournal.Running(data, Now.AddHours(2)));
        Assert.Equal("готово", DayJournal.Remaining(running.AtUtc, Now.AddHours(2)));
    }
}
