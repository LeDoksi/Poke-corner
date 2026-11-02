using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Tests.GlobalUpdate;
public sealed partial class NurseryUiTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ApprovedNurseryShowsCanonicalRootsForEvolvedAndStandalonePureParents(bool standalone)
    {
        WpfTestHost.Run(()=> {
            var recipe=new NurseryCatalog(Catalog.Value).SameSpeciesRecipes.First(r=>r.IsAvailable &&
                (standalone?Catalog.Value.Species.Count(s=>s.FamilyRootId==r.FirstParentId)==1:
                    Catalog.Value.Species.Single(s=>s.Id==r.FirstParentId).FamilyRootId!=r.FirstParentId));
            var (data,first,second)=State(recipe);var view=View(data);Choose(view,first,second);
            var preview=new NurseryService(Catalog.Value,data).Preview([first,second]);
            Assert.All(preview.Outcomes,o=>Assert.Equal(o.SpeciesId,Catalog.Value.Species.Single(s=>s.Id==o.SpeciesId).FamilyRootId));
            Assert.All(preview.Outcomes,o=>Assert.Contains(OutcomeLabels(view),t=>t.Text.Contains($"№{o.SpeciesId} ·")));
            Assert.True(((Button)view.FindName("StartButton")!).IsEnabled);
        });
    }
    [Theory]
    [InlineData(1000,650)] [InlineData(840,540)]
    public void ApprovedNurseryAppliedPairSurvivesDraftCancelAndLongNamesKeepActionsReachable(int width,int height)
    {
        WpfTestHost.Run(()=> {
            var (data,first,second)=State();var extra=Individual(data.Individuals[0].CurrentSpeciesId);data.Individuals.Add(extra);
            foreach(var p in data.Individuals)p.Nickname="Оченьдлиннаякириллица";
            var view=View(data);WpfTestHost.Layout(view,width,height);Choose(view,first,second);WpfTestHost.Layout(view,width,height);
            var cost=((TextBlock)view.FindName("CostText")!).Text;
            ((Button)view.FindName("ChooseFirstButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ShowIndividual(view,extra.Id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            WpfTestHost.Layout(view,width,height);
            var save=(Button)view.FindName("PickerSaveButton")!;var corner=save.TransformToAncestor(view).Transform(new Point(save.ActualWidth,save.ActualHeight));
            Assert.InRange(corner.X,0,width+1);Assert.InRange(corner.Y,0,height+1);
            ((Button)view.FindName("PickerCancelButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(first,NurseryPrivate(view,"_first"));Assert.Equal(second,NurseryPrivate(view,"_second"));Assert.Equal(cost,((TextBlock)view.FindName("CostText")!).Text);
            var scroll=(ScrollViewer)view.FindName("ScenePanel")!;scroll.ScrollToEnd();WpfTestHost.Layout(view,width,height);
            Assert.True(scroll.ExtentWidth<=scroll.ViewportWidth+1);
            var start=(Button)view.FindName("StartButton")!;var at=start.TransformToAncestor(view).Transform(new Point());
            Assert.InRange(at.X,0,width);Assert.InRange(at.Y,0,height);Assert.True(at.Y+start.ActualHeight<=height+1);
        });
    }
    [Fact]
    public void ApprovedNurseryUnloadRemovesAnActiveBabyRevealClockAndReloadRestoresCommittedBaby()
    {
        WpfTestHost.Run(()=> {
            var (data,first,second)=State();var service=new NurseryService(Catalog.Value,data);
            var request=new NurseryRequest([first,second],null,Guid.NewGuid(),DateTimeOffset.UtcNow.AddHours(-12));service.Start(request);service.Hatch(request.OperationId,Guid.NewGuid());
            var view=View(data);var image=(Image)view.FindName("BabyImage")!;
            image.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromSeconds(2)));Assert.True(image.HasAnimatedProperties);
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));Assert.False(image.HasAnimatedProperties);
            var child=data.HatchedNurseryResults.Single().Value;view.Refresh();Assert.False(image.HasAnimatedProperties);
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));Assert.NotNull(image.Source);Assert.False(image.HasAnimatedProperties);
            Assert.Single(data.Individuals.Where(p=>p.Id==child.Id));view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }
    [Theory]
    [InlineData(1000,650)] [InlineData(840,540)]
    public void ApprovedNurseryReadyHasOneLocalHatchAndNoFrozenBabyOrParentBusyLabel(int width,int height)
    {
        WpfTestHost.Run(()=> {
            var (data,first,second)=State();new NurseryService(Catalog.Value,data).Start(new NurseryRequest([first,second],null,Guid.NewGuid(),DateTimeOffset.UtcNow.AddHours(-12)));data.Nest!.Status="ready";
            var view=View(data);WpfTestHost.Layout(view,width,height);var hatch=(Button)view.FindName("HatchButton")!;
            Assert.Equal("Вылупить",hatch.Content);Assert.Equal(Visibility.Visible,hatch.Visibility);Assert.Equal(Visibility.Collapsed,((StackPanel)view.FindName("PreparationPanel")!).Visibility);
            Assert.Same(view.FindResource("UiNurseryReadyImage"),((Image)view.FindName("GreenhouseImage")!).Source);
            Assert.Null(((Image)view.FindName("BabyImage")!).Source);Assert.DoesNotContain("занят",((TextBlock)view.FindName("NestParents")!).Text);
            Assert.DoesNotContain("В питомнике",((TextBlock)view.FindName("NestParents")!).Text);
            var at=hatch.TransformToAncestor(view).Transform(new Point());Assert.InRange(at.Y,0,height);Assert.True(at.Y+hatch.ActualHeight<=height+1);
            Assert.True(((FrameworkElement)view.FindName("FooterHost")!).IsAncestorOf(hatch));Assert.True(at.X+hatch.ActualWidth>=width-40);
        });
    }
}
