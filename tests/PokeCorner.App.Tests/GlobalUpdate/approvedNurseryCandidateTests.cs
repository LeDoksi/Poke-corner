using System.Reflection;
using System.Runtime.ExceptionServices;
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
namespace PokeCorner.App.Tests.GlobalUpdate;
public sealed partial class NurseryUiTests
{
    private static NurseryAction StartCandidate(SaveData next,NurseryRequest request)
    {
        // Exercise the exact production candidate guard and canonical mutation inside the real commit boundary.
        var method=typeof(PokeCorner.App.App).GetMethod("ValidateNurseryStartCandidate",BindingFlags.Static|BindingFlags.NonPublic);
        Assert.NotNull(method);
        try {method!.Invoke(null,[Catalog.Value,next,request]);}
        catch(TargetInvocationException ex) {ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();}
        return new NurseryService(Catalog.Value,next).Start(request);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ApprovedNurseryQueuedCandidateRejectsChangedAvailabilityAndReplaysCommittedOperation(bool unsupported)
    {
        var (data,first,second)=State();var live=new AppState();live.Hydrate(data);
        var request=new NurseryRequest([first,second],null,Guid.NewGuid(),DateTimeOffset.UtcNow);
        var release=new TaskCompletionSource<bool>();var originalSpecies=data.Individuals.Single(p=>p.Id==first).CurrentSpeciesId;
        var root=Path.Combine(Path.GetTempPath(),"nursery-candidate-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try {
            var writer=new BackgroundSaveWriter(new JsonSaveStore(Path.Combine(root,"state.json")));
            var commits=0;
            async Task StartQueued(){await release.Task;await live.CommitAsync(writer,next=>{commits++;StartCandidate(next,request);},()=>0);}
            var pending=StartQueued();
            // The first action was valid when the view selected it; this changes before its candidate executes.
            if(unsupported)live.Collection.Individuals.Single(p=>p.Id==first).CurrentSpeciesId=Catalog.Value.Species.First(s=>!Catalog.Value.CanObtain(s.Id,false)).Id;
            else live.Collection.SetActive(first);
            var before=live.ToSaveData(0);release.SetResult(true);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>pending);
            Assert.Equal(1,commits);var refused=live.ToSaveData(0);Assert.Null(refused.Nest);Assert.Empty(refused.HatchedNurseryResults);
            Assert.Equal(before.Berries,refused.Berries);Assert.Equal(before.Items,refused.Items);Assert.DoesNotContain($"nursery:start:{request.OperationId}",refused.CompletedOperations.Keys);
            live.Collection.ClearActive();live.Collection.Individuals.Single(p=>p.Id==first).CurrentSpeciesId=originalSpecies;
            await live.CommitAsync(writer,next=>StartCandidate(next,request),()=>0);
            var committed=live.ToSaveData(0);var frozen=committed.Nest!.Child;
            live.Collection.SetActive(first); // Replay is not a new acquisition, and canonical fingerprint remains authoritative.
            await live.CommitAsync(writer,next=>Assert.True(StartCandidate(next,request).Repeated),()=>0);
            var replayed=live.ToSaveData(0);Assert.Equal(committed.Berries,replayed.Berries);Assert.Equal(committed.Items,replayed.Items);Assert.Equal(frozen.Id,replayed.Nest!.Child.Id);Assert.Equal(frozen.SpeciesId,replayed.Nest.Child.SpeciesId);
            var changed=request with {SelectedSpeciesId=Catalog.Value.Species.First(s=>s.Id!=frozen.SpeciesId).Id};
            await Assert.ThrowsAsync<InvalidOperationException>(()=>live.CommitAsync(writer,next=>StartCandidate(next,changed),()=>0));
        } finally {Directory.Delete(root,true);}
    }
}
