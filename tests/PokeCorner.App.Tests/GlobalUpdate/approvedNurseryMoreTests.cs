using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PokeCorner.App.Popup;
using PokeCorner.App.Tests.Theme;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;

namespace PokeCorner.App.Tests.GlobalUpdate;
public sealed partial class NurseryUiTests
{
    private static object? NurseryPrivate(NurseryView view,string field)=>typeof(NurseryView).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(view);
    [Theory]
    [InlineData("start",false)] [InlineData("start",true)]
    [InlineData("hatch",false)] [InlineData("hatch",true)]
    [InlineData("ready",false)] [InlineData("ready",true)]
    public async Task ApprovedNurseryPendingCompletionCannotReviveUnloadedPresentation(string action,bool success)
    {
        var (data,first,second)=State();
        var gate=new TaskCompletionSource<NurseryAction>();var ready=new TaskCompletionSource<bool>();
        NurseryRequest? request=null;Guid hatchNest=Guid.Empty,hatchOp=Guid.Empty;
        NurseryView? view=null;string message="";object? originalScene=null;
        WpfTestHost.Run(()=> {
            if(action!="start") {
                new NurseryService(Catalog.Value,data).Start(new NurseryRequest([first,second],null,Guid.NewGuid(),DateTimeOffset.UtcNow.AddHours(-12)));
                if(action=="hatch") data.Nest!.Status="ready";
            }
            view=new NurseryView(()=>data,(ids,selected)=>new NurseryService(Catalog.Value,data).Preview(ids,selected),
                value=>{request=value;return gate.Task;},(id,op)=>{hatchNest=id;hatchOp=op;return gate.Task;},
                _=>ready.Task,Catalog.Value,(_,_)=>{},_=>{},_=>Task.CompletedTask);
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            if(action=="start") {Choose(view,first,second);((Button)view.FindName("StartButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
            else if(action=="hatch") ((Button)view.FindName("HatchButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            else _=(Task)typeof(NurseryView).GetMethod("TickAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(view,null)!;
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            message=((TextBlock)view.FindName("MessageText")!).Text;originalScene=((Image)view.FindName("GreenhouseImage")!).Source;
            Assert.False(((DispatcherTimer)NurseryPrivate(view,"_timer")!).IsEnabled);
            if(!success) {gate.SetException(new IOException("refused after unload"));ready.SetException(new IOException("refused after unload"));}
            else if(action=="start")gate.SetResult(new NurseryService(Catalog.Value,data).Start(request!));
            else if(action=="hatch")gate.SetResult(new NurseryService(Catalog.Value,data).Hatch(hatchNest,hatchOp));
            else ready.SetResult(new NurseryService(Catalog.Value,data).RefreshReady(DateTimeOffset.UtcNow));
        });
        await view!.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle).Task;
        WpfTestHost.Run(()=> {
            view.Refresh();
            Assert.Equal(message,((TextBlock)view.FindName("MessageText")!).Text);
            Assert.Same(originalScene,((Image)view.FindName("GreenhouseImage")!).Source);
            Assert.Null(((Image)view.FindName("BabyImage")!).Source);
            Assert.False(((Image)view.FindName("BabyImage")!).HasAnimatedProperties);
            Assert.False(((DispatcherTimer)NurseryPrivate(view,"_timer")!).IsEnabled);
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            if(success&&action=="hatch") {Assert.NotNull(((Image)view.FindName("BabyImage")!).Source);Assert.Single(data.HatchedNurseryResults);}
            if(success&&action=="start")Assert.Equal(Visibility.Visible,((StackPanel)view.FindName("NestPanel")!).Visibility);
            if(success&&action=="ready")Assert.Equal(Visibility.Visible,((Button)view.FindName("HatchButton")!).Visibility);
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }
    [Fact]
    public void ApprovedNurseryRefusedProposedWritesKeepFrozenChildCharmAndMetadataUntilRetry()
    {
        WpfTestHost.Run(()=> {
            var (published,first,second)=State();var requests=new List<NurseryRequest>();var hatchOps=new List<Guid>();
            var startFail=true;var hatchFail=true;
            var view=new NurseryView(()=>published,(ids,selected)=>new NurseryService(Catalog.Value,published).Preview(ids,selected),
                request=>{
                    requests.Add(request);var proposed=JsonSaveStore.Clone(published);var result=new NurseryService(Catalog.Value,proposed).Start(request);
                    if(startFail){startFail=false;return Task.FromException<NurseryAction>(new IOException("candidate start refused"));}
                    published=proposed;return Task.FromResult(result);
                },(id,op)=>{
                    hatchOps.Add(op);var proposed=JsonSaveStore.Clone(published);var result=new NurseryService(Catalog.Value,proposed).Hatch(id,op);
                    if(hatchFail){hatchFail=false;return Task.FromException<NurseryAction>(new IOException("candidate hatch refused"));}
                    published=proposed;return Task.FromResult(result);
                },_=>Task.FromResult(false),Catalog.Value,(_,_)=>{},_=>{},_=>Task.CompletedTask);
            Choose(view,first,second);
            ((StackPanel)view.FindName("OutcomePanel")!).Children.OfType<Button>().Skip(1).First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var before=JsonSaveStore.Clone(published);var start=(Button)view.FindName("StartButton")!;
            start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Null(published.Nest);Assert.Equal(before.Items,published.Items);Assert.Equal(before.Berries,published.Berries);Assert.Empty(published.NormalSpeciesIds);
            start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(requests[0],requests[1]);Assert.Equal(before.Items["offspring-charm"]-1,published.Items["offspring-charm"]);
            var frozen=published.Nest!.Child;published.Nest.Status="ready";view.Refresh();
            var text=((TextBlock)view.FindName("NestTerms")!).Text;
            Assert.DoesNotContain(SpeciesNameForTest(frozen.SpeciesId),text);Assert.Null(((Image)view.FindName("BabyImage")!).Source);
            var hatch=(Button)view.FindName("HatchButton")!;hatch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal("ready",published.Nest.Status);Assert.Empty(published.HatchedNurseryResults);Assert.DoesNotContain(published.Individuals,p=>p.Id==frozen.Id);
            Assert.Empty(published.NormalSpeciesIds);Assert.Empty(published.ShinySpeciesIds);Assert.Null(((Image)view.FindName("BabyImage")!).Source);
            hatch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(hatchOps[0],hatchOps[1]);Assert.Single(published.HatchedNurseryResults);Assert.Single(published.Individuals.Where(p=>p.Id==frozen.Id));
            Assert.Equal(frozen.Id,published.HatchedNurseryResults.Single().Value.Id);Assert.NotNull(((Image)view.FindName("BabyImage")!).Source);
            Assert.Contains(frozen.SpeciesId,frozen.IsShiny?published.ShinySpeciesIds:published.NormalSpeciesIds);
            Assert.Equal(before.Items["offspring-charm"]-1,published.Items["offspring-charm"]);
        });
    }
    private static string SpeciesNameForTest(int id)=>Catalog.Value.Species.Single(s=>s.Id==id).DisplayName;

    [Fact]
    public void ApprovedNurseryExactSpecimensCompatibilityAndFormDiscoveryStayIndependent()
    {
        WpfTestHost.Run(()=> {
            var recipe=new NurseryCatalog(Catalog.Value).SameSpeciesRecipes.First(r=>r.IsAvailable&&Catalog.Value.CanObtain(r.FirstParentId,true));
            var (data,first,second)=State(recipe);data.Individuals[1]=Individual(recipe.SecondParentId,true);second=data.Individuals[1].Id;
            var active=Individual(recipe.FirstParentId);var busy=Individual(recipe.FirstParentId);data.Individuals.Add(active);data.Individuals.Add(busy);
            data.ActiveIndividualId=active.Id;data.Expedition=new ExpeditionState {Status="running",ParticipantIds=[busy.Id]};
            var unsupported=Catalog.Value.Species.First(s=>!Catalog.Value.CanObtain(s.Id,true));var unavailable=Individual(unsupported.Id,true);data.Individuals.Add(unavailable);
            var view=View(data);((Button)view.FindName("ChooseFirstButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(ShowIndividual(view,active.Id).IsEnabled);Assert.False(ShowIndividual(view,busy.Id).IsEnabled);Assert.Throws<InvalidOperationException>(()=>ShowIndividual(view,unavailable.Id)); // no partner possible: not offered at all
            ShowIndividual(view,first).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var buttons=((WrapPanel)view.FindName("PickerIndividualsPanel")!).Children.OfType<Button>().Where(b=>Equals(b.Tag,second)).ToArray();
            Assert.Single(buttons);Assert.Equal(second,buttons[0].Tag);Assert.True(buttons[0].IsEnabled);
            buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));((Button)view.FindName("PickerSaveButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            data.ShinySpeciesIds.Add(recipe.AvailableOutcomes[0].SpeciesId);data.DiscoveredSpeciesIds.Add(recipe.AvailableOutcomes[0].SpeciesId);view.Refresh();
            var rows=OutcomeLabels(view).ToArray();
            Assert.Contains(rows,p=>p.Text.Contains("неизвестный вид"));Assert.DoesNotContain(rows,p=>p.Text.Contains(SpeciesNameForTest(recipe.AvailableOutcomes[0].SpeciesId)));
            data.NormalSpeciesIds.Add(recipe.AvailableOutcomes[0].SpeciesId);view.Refresh();
            Assert.Contains(OutcomeLabels(view),p=>p.Text.Contains(SpeciesNameForTest(recipe.AvailableOutcomes[0].SpeciesId)));
            foreach(var field in new[]{"PreviewText","CostText","ShortageText","ResultText","NestTerms","LuckText"})Assert.DoesNotContain("%",((TextBlock)view.FindName(field)!).Text);
        });
    }
    [Theory]
    [InlineData(1000,650)] [InlineData(840,540)]
    public void ApprovedNurseryTwoHundredDuplicatesKeepSearchPagingDraftAndActionsReachable(int width,int height)
    {
        WpfTestHost.Run(()=> {
            var (data,_,_)=State();data.Individuals.Clear();
            var species=Catalog.Value.Species.Where(s=>Catalog.Value.CanObtain(s.Id,false)).Take(600).ToArray();Assert.Equal(600,species.Length);
            foreach(var s in species)data.Individuals.Add(Individual(s.Id));
            foreach(var s in species.Take(200))data.Individuals.Add(Individual(s.Id));
            var view=View(data);WpfTestHost.Layout(view,width,height);((Button)view.FindName("ChooseFirstButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));WpfTestHost.Layout(view,width,height);
            Assert.InRange(((WrapPanel)view.FindName("PickerIndividualsPanel")!).Children.Count,12,30);Assert.Contains("найдено",((TextBlock)view.FindName("PickerCountText")!).Text);
            var next=(Button)view.FindName("PickerNextButton")!;next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var page=((TextBlock)view.FindName("PickerPageText")!).Text;Assert.StartsWith("2 /",page);
            ((Button)view.FindName("PickerCancelButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));((Button)view.FindName("ChooseFirstButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert.Equal(page,((TextBlock)view.FindName("PickerPageText")!).Text);
            ((TextBox)view.FindName("ParentSearchBox")!).Text=$"#{species[0].Id:D4}";
            Assert.Equal(2,((WrapPanel)view.FindName("PickerIndividualsPanel")!).Children.OfType<Button>().Count());
            foreach(var name in new[]{"PickerCancelButton","PickerSaveButton","PickerNextButton","ParentSearchBox"}){
                var control=(FrameworkElement)view.FindName(name)!;var point=control.TransformToAncestor(view).Transform(new Point());
                Assert.InRange(point.X,0,width);Assert.InRange(point.Y,0,height);Assert.True(point.X+control.ActualWidth<=width+1);Assert.True(point.Y+control.ActualHeight<=height+1);
            }
            Assert.InRange(((System.Collections.IDictionary)NurseryPrivate(view,"_spriteCache")!).Count,0,128);
        });
    }
}
