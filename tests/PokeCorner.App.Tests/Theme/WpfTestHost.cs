using System.Windows;
using System.Windows.Threading;

namespace PokeCorner.App.Tests.Theme;

/// A single dedicated STA thread with a WPF Application whose resources are the real
/// PokedexTheme dictionary — the same setup App.xaml produces at runtime.
///
/// This exists so the visual layer can be verified without a human looking at a screen:
/// WPF resolves every StaticResource at XAML load time and throws when a key is missing, so
/// merely constructing and laying out a view proves its resource references are intact and
/// its control templates are structurally valid.
internal static class WpfTestHost
{
    private static readonly object Gate = new();
    private static Dispatcher? _dispatcher;

    /// Runs <paramref name="action"/> on the shared UI thread, rethrowing anything it threw
    /// so the failure surfaces as a normal test failure rather than a silent swallow.
    public static void Run(Action action)
    {
        EnsureStarted();

        Exception? failure = null;
        _dispatcher!.Invoke(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        if (failure is not null)
        {
            throw new InvalidOperationException($"UI-thread action failed: {failure.Message}", failure);
        }
    }

    /// Forces the element through a full measure/arrange pass. Without this, ItemsControl
    /// never realizes its item containers, so the card DataTemplates — where most of the
    /// redesign lives — would never actually be expanded or resource-resolved.
    public static void Layout(FrameworkElement element, double width = 560, double height = 4000)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    private static void EnsureStarted()
    {
        lock (Gate)
        {
            if (_dispatcher is not null) return;

            var ready = new ManualResetEventSlim();
            var thread = new Thread(() =>
            {
                var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                // Assembly name read off the app assembly rather than written out literally:
                // it ships as PokeCorner.exe but the project/namespace is still PokeCorner.App,
                // and a hardcoded pack URI silently breaks the whole WPF test host the next
                // time those two drift apart.
                var appAssembly = typeof(global::PokeCorner.App.App).Assembly.GetName().Name;
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri($"pack://application:,,,/{appAssembly};component/Theme/PokedexTheme.xaml", UriKind.Absolute)
                });

                _dispatcher = Dispatcher.CurrentDispatcher;
                ready.Set();
                Dispatcher.Run();
            })
            {
                IsBackground = true
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            ready.Wait();
        }
    }
}
