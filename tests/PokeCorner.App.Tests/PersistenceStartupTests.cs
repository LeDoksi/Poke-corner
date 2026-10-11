using System.Reflection;
using System.Runtime.CompilerServices;
using PokeCorner.Core;
using PokeCorner.Core.Persistence;

namespace PokeCorner.App.Tests;

public class PersistenceStartupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PersistState_BeforeSuccessfulHydration_DoesNotOverwriteOriginal(bool flush)
    {
        var path = Path.Combine(Path.GetTempPath(), "PokeCornerStartupTest_" + Guid.NewGuid() + ".json");
        const string original = "{broken save";
        try
        {
            File.WriteAllText(path, original);
            // Skip Application construction: no windows, app-data reads, hooks or process startup.
            var app = (global::PokeCorner.App.App)RuntimeHelpers.GetUninitializedObject(typeof(global::PokeCorner.App.App));
            SetField(app, "_appState", new AppState());
            SetField(app, "_saveWriter", new BackgroundSaveWriter(new JsonSaveStore(path), write => write()));
            typeof(global::PokeCorner.App.App).GetMethod("PersistState", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(app, new object[] { flush });
            Assert.Equal(original, File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    private static void SetField(object app, string name, object value) =>
        app.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(app, value);
}
