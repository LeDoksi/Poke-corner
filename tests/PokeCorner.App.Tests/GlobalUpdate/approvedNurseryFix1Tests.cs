using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Tests.GlobalUpdate;

public sealed partial class NurseryUiTests
{
    [Theory]
    [InlineData("start", false)] [InlineData("start", true)]
    [InlineData("hatch", false)] [InlineData("hatch", true)]
    [InlineData("ready", false)] [InlineData("ready", true)]
    [InlineData("activate", false)] [InlineData("activate", true)]
    public async Task Fix1ReloadBeforePendingCompletionReconcilesCurrentControlsAndKeepsRetryIdentity(string action, bool success)
    {
        var (data, first, second) = State();
        var mutationGate = new TaskCompletionSource<NurseryAction>();
        var readyGate = new TaskCompletionSource<bool>();
        var activationGate = new TaskCompletionSource<bool>();
        var starts = new List<NurseryRequest>();
        var hatches = new List<(Guid Nest, Guid Operation)>();
        var activations = new List<Guid>();
        var readyCalls = 0;
        NurseryView? view = null;
        const string currentMessage = "Текущая заметка после возврата";

        WpfTestHost.Run(() =>
        {
            if (action != "start")
            {
                var service = new NurseryService(Catalog.Value, data);
                var request = new NurseryRequest([first, second], null, Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-12));
                service.Start(request);
                if (action == "hatch") data.Nest!.Status = "ready";
                if (action == "activate") service.Hatch(request.OperationId, Guid.NewGuid());
            }
            view = new NurseryView(() => data, (ids, selected) => new NurseryService(Catalog.Value, data).Preview(ids, selected),
                request =>
                {
                    starts.Add(request);
                    return starts.Count == 1 ? mutationGate.Task : Task.FromResult(new NurseryService(Catalog.Value, data).Start(request));
                },
                (nest, operation) =>
                {
                    hatches.Add((nest, operation));
                    return hatches.Count == 1 ? mutationGate.Task : Task.FromResult(new NurseryService(Catalog.Value, data).Hatch(nest, operation));
                },
                now => ++readyCalls == 1 ? readyGate.Task : Task.FromResult(new NurseryService(Catalog.Value, data).RefreshReady(now)),
                Catalog.Value, (_, _) => { }, _ => { },
                id =>
                {
                    activations.Add(id);
                    if (activations.Count == 1) return activationGate.Task;
                    data.ActiveIndividualId = id;
                    return Task.CompletedTask;
                });
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            if (action == "start") Choose(view, first, second);
            if (action == "ready")
                _ = (Task)typeof(NurseryView).GetMethod("TickAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(view, null)!;
            else
                ((Button)view.FindName(action == "start" ? "StartButton" : action == "hatch" ? "HatchButton" : "ActivateButton")!)
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            ((TextBlock)view.FindName("MessageText")!).Text = currentMessage;
            if (action == "start")
            {
                Assert.False(((Button)view.FindName("StartButton")!).IsEnabled);
                Assert.False(((Button)view.FindName("ChooseFirstButton")!).IsEnabled);
            }
            if (!success)
            {
                if (action == "ready") readyGate.SetException(new IOException("stale ready refusal"));
                else if (action == "activate") activationGate.SetException(new IOException("stale activation refusal"));
                else mutationGate.SetException(new IOException("stale mutation refusal"));
            }
            else if (action == "start") mutationGate.SetResult(new NurseryService(Catalog.Value, data).Start(starts[0]));
            else if (action == "hatch") mutationGate.SetResult(new NurseryService(Catalog.Value, data).Hatch(hatches[0].Nest, hatches[0].Operation));
            else if (action == "ready") readyGate.SetResult(new NurseryService(Catalog.Value, data).RefreshReady(DateTimeOffset.UtcNow));
            else
            {
                data.ActiveIndividualId = activations[0];
                activationGate.SetResult(true);
            }
        });

        await view!.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle).Task;
        WpfTestHost.Run(() =>
        {
            try
            {
                Assert.False((bool)NurseryPrivate(view, "_busy")!);
                Assert.False((bool)NurseryPrivate(view, "_refreshingReady")!);
                Assert.Equal(currentMessage, ((TextBlock)view.FindName("MessageText")!).Text);
                Assert.False(((Image)view.FindName("BabyImage")!).HasAnimatedProperties);
                if (!success && action == "start")
                {
                    Assert.Null(data.Nest);
                    Assert.Null(((Image)view.FindName("BabyImage")!).Source);
                    Assert.Empty(data.HatchedNurseryResults);
                    Assert.True(((Button)view.FindName("ChooseFirstButton")!).IsEnabled);
                    Assert.True(((Button)view.FindName("ChooseSecondButton")!).IsEnabled);
                    var start = (Button)view.FindName("StartButton")!;
                    Assert.True(start.IsEnabled);
                    start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(2, starts.Count);
                    Assert.Equal(starts[0], starts[1]);
                    Assert.Equal(starts[0].OperationId, data.Nest!.Id);
                }
                else if (!success && action == "hatch")
                {
                    Assert.Equal("ready", data.Nest!.Status);
                    Assert.Null(((Image)view.FindName("BabyImage")!).Source);
                    Assert.Empty(data.HatchedNurseryResults);
                    var hatch = (Button)view.FindName("HatchButton")!;
                    Assert.True(hatch.IsEnabled);
                    hatch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(2, hatches.Count);
                    Assert.Equal(hatches[0], hatches[1]);
                    Assert.Single(data.HatchedNurseryResults);
                    Assert.Single(data.Individuals.Where(p => p.Id == data.Nest.Child.Id));
                }
                else if (!success && action == "activate")
                {
                    Assert.Null(data.ActiveIndividualId);
                    var activate = (Button)view.FindName("ActivateButton")!;
                    Assert.True(activate.IsEnabled);
                    activate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(2, activations.Count);
                    Assert.Equal(activations[0], activations[1]);
                    Assert.Equal(activations[0], data.ActiveIndividualId);
                }
                else if (action == "ready")
                {
                    if (!success)
                        _ = (Task)typeof(NurseryView).GetMethod("TickAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(view, null)!;
                    Assert.Equal(Visibility.Visible, ((Button)view.FindName("HatchButton")!).Visibility);
                    Assert.Null(((Image)view.FindName("BabyImage")!).Source);
                }
                else if (action == "start")
                {
                    Assert.Equal(Visibility.Visible, ((StackPanel)view.FindName("NestPanel")!).Visibility);
                    Assert.Null(((Image)view.FindName("BabyImage")!).Source);
                }
                else
                {
                    Assert.Equal(Visibility.Visible, ((Border)view.FindName("ResultPanel")!).Visibility);
                    Assert.NotNull(((Image)view.FindName("BabyImage")!).Source);
                    Assert.True(((Button)view.FindName("OpenPokedexButton")!).IsEnabled);
                    Assert.True(((Button)view.FindName("ActivateButton")!).IsEnabled);
                    Assert.Single(data.HatchedNurseryResults);
                }
            }
            finally { view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
        });
    }
}
