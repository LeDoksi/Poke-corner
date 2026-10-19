using PokeCorner.App.Imaging;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;
using Button = System.Windows.Controls.Button;
using Image = System.Windows.Controls.Image;
using Orientation = System.Windows.Controls.Orientation;
using UserControl = System.Windows.Controls.UserControl;

namespace PokeCorner.App.Popup;

public partial class NurseryView : UserControl
{
    private readonly Func<SaveData> _snapshot;
    private readonly Func<IReadOnlyList<Guid>, int?, NurseryPreview> _preview;
    private readonly Func<NurseryRequest, Task<NurseryAction>> _start;
    private readonly Func<Guid, Guid, Task<NurseryAction>> _hatch;
    private readonly Func<DateTimeOffset, Task<bool>> _refreshReady;
    private readonly Action<string, int> _shopShortage;
    private readonly Action<Guid> _openPokedex;
    private readonly Func<Guid, Task> _activate;
    private readonly CatalogPackage _catalog;
    private readonly NurseryCatalog _recipes;
    private readonly LevelProgression _levels;
    private readonly Dictionary<int,CatalogSpecies> _species;
    private readonly Dictionary<(int,bool),ImageSource?> _spriteCache = [];
    private readonly Dictionary<string,ImageSource?> _itemArtCache = [];
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private Guid _first, _second, _draftFirst, _draftSecond;
    private int? _choice, _group;
    private int _page, _pages = 1, _editing, _selectionGeneration, _lifetimeGeneration;
    private NurseryRequest? _pendingStart;
    private Guid _pendingHatchNest, _pendingHatchOperation, _dismissedResult;
    private string? _shortageBerry;
    private int _shortageRequired;
    private bool _busy, _refreshingReady, _pickerOpen, _suspended, _awaitingHatchContinue;

    public NurseryView(Func<SaveData> snapshot, Func<IReadOnlyList<Guid>, int?, NurseryPreview> preview,
        Func<NurseryRequest, Task<NurseryAction>> start, Func<Guid, Guid, Task<NurseryAction>> hatch,
        Func<DateTimeOffset, Task<bool>> refreshReady, CatalogPackage catalog, Action<string, int> shopShortage,
        Action<Guid> openPokedex, Func<Guid, Task> activate)
    {
        _snapshot=snapshot; _preview=preview; _start=start; _hatch=hatch; _refreshReady=refreshReady;
        _catalog=catalog; _recipes=new NurseryCatalog(catalog); _levels=new LevelProgression(catalog.DataRoot);
        _species=catalog.Species.ToDictionary(s=>s.Id);
        _shopShortage=shopShortage; _openPokedex=openPokedex; _activate=activate;
        InitializeComponent();
        RarityBox.Items.Add(new ComboBoxItem { Content="Любая редкость", Tag="" });
        foreach(var rarity in catalog.Species.Select(s=>s.Rarity).Distinct())
            RarityBox.Items.Add(new ComboBoxItem { Content=RarityName(rarity),Tag=rarity });
        RarityBox.SelectedIndex=0;
        _timer.Tick += async (_,_)=>await TickAsync();
        Loaded += (_,_)=> { _suspended=false; _timer.Start();
            SystemParameters.StaticPropertyChanged-=MotionPolicyChanged;
            SystemParameters.StaticPropertyChanged+=MotionPolicyChanged; Refresh(); };
        Unloaded += (_,_)=> {
            _suspended=true; _lifetimeGeneration++; _timer.Stop(); _pickerOpen=false;
            SystemParameters.StaticPropertyChanged-=MotionPolicyChanged;
            BabyImage.BeginAnimation(OpacityProperty,null); BabyImage.Opacity=1;
            _awaitingHatchContinue=false;
        };
        PreviewKeyDown += (_,e)=> { if(e.Key==System.Windows.Input.Key.Escape && _pickerOpen) {ClosePicker();e.Handled=true;} };
        SizeChanged += (_,_)=> {if(_pickerOpen && !_suspended) RefreshPicker();};
        Refresh();
    }
    private void MotionPolicyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_suspended || e.PropertyName != nameof(SystemParameters.ClientAreaAnimation)) return;
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => MotionPolicyChanged(sender,e)); return; }
        if (!SystemParameters.ClientAreaAnimation) { BabyImage.BeginAnimation(OpacityProperty,null); BabyImage.Opacity=1; }
    }
    private string SpeciesName(int id)=>_species.GetValueOrDefault(id)?.DisplayName ?? $"№{id:D4}";
    private string ParentName(SaveData data,Guid id) {
        var p=data.Individuals.FirstOrDefault(p=>p.Id==id);
        return p is null ? "Пустое место" : $"{p.Nickname ?? SpeciesName(p.CurrentSpeciesId)} · №{p.CurrentSpeciesId:D4}"+(p.IsShiny?" · Shiny":"");
    }
    private static string RarityName(string rarity)=>rarity.ToLowerInvariant() switch {
        "common"=>"Обычный", "uncommon"=>"Необычный", "rare"=>"Редкий", "epic"=>"Эпический", "legendary"=>"Легендарный", _=>rarity };
    private string? Unavailable(SaveData data,PokemonIndividual p) {
        if(data.ActiveIndividualId==p.Id) return "На рабочем столе";
        if(ExpeditionService.BusyIndividualIds(data).Contains(p.Id)) return "Уже занят";
        if(!_catalog.CanObtain(p.CurrentSpeciesId,p.IsShiny)) return "Изображение этой формы пока недоступно";
        if(_recipes.Partners(p.CurrentSpeciesId).Count==0) return "Нет доступного потомства";
        return null;
    }
    private bool PairValid(SaveData data,Guid first,Guid second) {
        var a=data.Individuals.FirstOrDefault(p=>p.Id==first);var b=data.Individuals.FirstOrDefault(p=>p.Id==second);
        if(a is null || b is null || first==second || Unavailable(data,a)!=null || Unavailable(data,b)!=null) return false;
        return _recipes.Find(a,b)?.IsAvailable==true;
    }
    private ImageSource? Sprite(int id,bool shiny) {
        var key=(id,shiny);if(_spriteCache.TryGetValue(key,out var image)) return image;
        try {var path=_catalog.GetAssetPath(id,shiny); if(path is not null && File.Exists(path)) image=SpriteImages.Decode(File.ReadAllBytes(path));}
        catch(Exception ex) when(ex is IOException or InvalidOperationException or NotSupportedException or ArgumentException) {image=null;}
        if(_spriteCache.Count>=128) _spriteCache.Clear(); _spriteCache[key]=image;return image;
    }
    private void Show(string text) {if(_suspended)return;MessageText.Text=text;MessagePanel.Visibility=Visibility.Visible;}
    private void Slot(StackPanel panel,SaveData data,Guid id) {
        panel.Children.Clear();var p=data.Individuals.FirstOrDefault(p=>p.Id==id);
        var row=new StackPanel {Orientation=Orientation.Horizontal};
        row.Children.Add(new Image {Source=p is null?null:Sprite(p.CurrentSpeciesId,p.IsShiny),Width=48,Height=48,Margin=new Thickness(0,0,8,0)});
        row.Children.Add(new TextBlock {Text=ParentName(data,id),TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center,MaxWidth=Math.Max(65,(ActualWidth>0?ActualWidth:800)*.29-115)});
        panel.Children.Add(row);
    }
    private void OpenPicker(int editing) {
        if(_busy || _suspended || _snapshot().Nest?.Status is "running" or "ready")return;
        _draftFirst=_first;_draftSecond=_second;_editing=editing==1 && _first!=Guid.Empty?1:0;
        _pickerOpen=true;Refresh();ParentSearchBox.Focus();
    }
    private void ChooseFirst_Click(object sender,RoutedEventArgs e)=>OpenPicker(0);
    private void ChooseSecond_Click(object sender,RoutedEventArgs e)=>OpenPicker(1);
    private void DraftFirst_Click(object sender,RoutedEventArgs e){_editing=0;_group=null;_page=0;RefreshPicker();}
    private void DraftSecond_Click(object sender,RoutedEventArgs e){if(_draftFirst==Guid.Empty)return;_editing=1;_group=null;_page=0;RefreshPicker();}
    private void ClosePicker(){_pickerOpen=false;Refresh();ChooseFirstButton.Focus();}
    private void PickerCancel_Click(object sender,RoutedEventArgs e)=>ClosePicker();
    private void PickerSave_Click(object sender,RoutedEventArgs e) {
        if(_busy || _suspended || !PairValid(_snapshot(),_draftFirst,_draftSecond))return;
        // Recheck the canonical producer preview before applying this presentation draft.
        try {_preview([_draftFirst,_draftSecond],null);}catch(Exception ex){Show(ex.Message);RefreshPicker();return;}
        _first=_draftFirst;_second=_draftSecond;_choice=null;_pendingStart=null;_selectionGeneration++;ClosePicker();
    }
    private void ParentFilter_Changed(object sender,RoutedEventArgs e){if(!_pickerOpen)return;_page=0;RefreshPicker();}
    private void BackToGroups_Click(object sender,RoutedEventArgs e){_group=null;_page=0;RefreshPicker();}
    private void PreviousPage_Click(object sender,RoutedEventArgs e){if(_page>0){_page--;RefreshPicker();PickerScroll.ScrollToTop();}}
    private void NextPage_Click(object sender,RoutedEventArgs e){if(_page+1<_pages){_page++;RefreshPicker();PickerScroll.ScrollToTop();}}
    private bool Search(PokemonIndividual p,string query) {
        if(query.Length==0)return true;
        if(SpeciesName(p.CurrentSpeciesId).Contains(query,StringComparison.CurrentCultureIgnoreCase) || (p.Nickname?.Contains(query,StringComparison.CurrentCultureIgnoreCase)??false))return true;
        var number=query.TrimStart('#','№');
        return int.TryParse(number,out var id) && id>0 && p.CurrentSpeciesId==id;
    }
    private void RefreshPicker() {
        if(!_pickerOpen || _suspended)return;var data=_snapshot();
        DraftFirstButton.Content=new TextBlock {Text="Первый: "+ParentName(data,_draftFirst),TextWrapping=TextWrapping.Wrap};
        DraftSecondButton.Content=new TextBlock {Text="Второй: "+ParentName(data,_draftSecond),TextWrapping=TextWrapping.Wrap};
        DraftSecondButton.IsEnabled=_draftFirst!=Guid.Empty;
        DraftFirstButton.BorderThickness=new Thickness(_editing==0?2:1);DraftSecondButton.BorderThickness=new Thickness(_editing==1?2:1);
        var first=data.Individuals.FirstOrDefault(p=>p.Id==_draftFirst);
        var partners=first is null?Array.Empty<int>():_recipes.Partners(first.CurrentSpeciesId).ToArray();
        var rarity=(RarityBox.SelectedItem as ComboBoxItem)?.Tag as string;
        var rows=data.Individuals.Where(p=>Search(p,ParentSearchBox.Text.Trim()) &&
            (string.IsNullOrEmpty(rarity)||_species.GetValueOrDefault(p.CurrentSpeciesId)?.Rarity==rarity) &&
            (ShinyOnlyBox.IsChecked!=true || p.IsShiny) &&
            (_editing==0 || (p.Id!=_draftFirst && partners.Contains(p.CurrentSpeciesId) && Unavailable(data,p)==null)))
            .OrderBy(p=>p.CurrentSpeciesId).ThenBy(p=>p.CaughtAtUtc).ThenBy(p=>p.Id).ToArray();
        var groups=rows.GroupBy(p=>p.CurrentSpeciesId).ToArray();
        SpeciesGroupsPanel.Children.Clear();PickerIndividualsPanel.Children.Clear();
        SpeciesGroupsPanel.Visibility=_group is null?Visibility.Visible:Visibility.Collapsed;
        PickerIndividualsPanel.Visibility=_group is null?Visibility.Collapsed:Visibility.Visible;
        BackToGroupsButton.Visibility=_group is null?Visibility.Collapsed:Visibility.Visible;
        var individuals=rows.Where(p=>p.CurrentSpeciesId==_group).ToArray();
        var count=_group is null?groups.Length:individuals.Length;var size=_group is null?24:12;
        _pages=Math.Max(1,(count+size-1)/size);_page=Math.Clamp(_page,0,_pages-1);
        var width=Math.Max(150,((ActualWidth>0?ActualWidth:800)-50)/(ActualWidth>=930?3:2)-10);
        if(_group is null) foreach(var group in groups.Skip(_page*size).Take(size)) {
            var id=group.Key;
            var button=new Button {Tag=id,Content=new TextBlock {Text=$"№{id:D4} · {SpeciesName(id)}\nЭкземпляров: {group.Count()}",TextWrapping=TextWrapping.Wrap},Width=width,MinHeight=66,Margin=new Thickness(0,0,8,8),Style=(Style)FindResource("SecondaryButtonStyle")};
            button.Click+=(_,_)=>{_group=id;_page=0;RefreshPicker();PickerScroll.ScrollToTop();};SpeciesGroupsPanel.Children.Add(button);
        }
        else foreach(var p in individuals.Skip(_page*size).Take(size)) {
            var reason=Unavailable(data,p);var row=new StackPanel();
            var ordinal=Array.FindIndex(data.Individuals.Where(x=>x.CurrentSpeciesId==p.CurrentSpeciesId).OrderBy(x=>x.CaughtAtUtc).ThenBy(x=>x.Id).ToArray(),x=>x.Id==p.Id)+1;
            var heading=new DockPanel();heading.Children.Add(new Image {Source=Sprite(p.CurrentSpeciesId,p.IsShiny),Width=54,Height=54,Margin=new Thickness(0,0,7,0)});
            heading.Children.Add(new TextBlock {Text=$"{ParentName(data,p.Id)}\nЭкземпляр {ordinal} · ур. {_levels.LevelForXp(p.TotalXp)} · {p.CaughtAtUtc.ToLocalTime():g}",TextWrapping=TextWrapping.Wrap});row.Children.Add(heading);
            row.Children.Add(new TextBlock {Text=CollectionView.TraitLabel(p.FirstTraitId)+" — "+CollectionView.TraitEffect(p.FirstTraitId),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,0)});
            if(reason is not null)row.Children.Add(new TextBlock {Text=reason,TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.Bold});
            var button=new Button {Tag=p.Id,Content=row,Width=width,MinHeight=120,Margin=new Thickness(0,0,8,8),Padding=new Thickness(10),HorizontalContentAlignment=HorizontalAlignment.Stretch,Style=(Style)FindResource("SecondaryButtonStyle"),IsEnabled=reason is null,ToolTip=reason??"Выбрать этот экземпляр"};
            System.Windows.Automation.AutomationProperties.SetName(button,ParentName(data,p.Id));
            button.Click+=(_,_)=>{
                if(_busy || !_pickerOpen || Unavailable(_snapshot(),p)!=null)return;
                if(_editing==0){_draftFirst=p.Id;if(!PairValid(_snapshot(),_draftFirst,_draftSecond))_draftSecond=Guid.Empty;_editing=1;_group=null;_page=0;ParentSearchBox.Text="";}
                else if(PairValid(_snapshot(),_draftFirst,p.Id))_draftSecond=p.Id;
                RefreshPicker();
            };PickerIndividualsPanel.Children.Add(button);
        }
        PickerCountText.Text=$"{(_editing==0?"Первый":"Второй")} родитель · экземпляров: {rows.Length} · видов: {groups.Length}";
        PickerPageText.Text=$"{_page+1} / {_pages}";PickerPreviousButton.IsEnabled=_page>0;PickerNextButton.IsEnabled=_page+1<_pages;
        PickerSaveButton.IsEnabled=PairValid(data,_draftFirst,_draftSecond);
    }
    public void Refresh() {
        if(_suspended)return;var data=_snapshot();var nest=data.Nest;var active=nest?.Status is "running" or "ready";
        var result=nest?.Status=="hatched" && _dismissedResult!=nest.Id;
        if(active||result)_pickerOpen=false;
        var ready=nest?.Status=="ready";
        SceneTitleText.Text=ready?"Малыш готов появиться!":active?"В гнезде кто-то растёт":result?"Добро пожаловать в семью!":"Будущие родители";
        SubtitleText.Text=ready?"Пора познакомиться.":active?"Малыш появится сам, когда придёт время.":result?"Новая история начинается здесь.":"Выберите родителей и узнайте, кто может появиться.";
        Grid.SetColumnSpan(SceneBorder,ready?2:1);SceneGrid.Height=ready?350:210;
        PreviewPanel.Visibility=active||result?Visibility.Collapsed:Visibility.Visible;
        NestParentPanel.Visibility=active&&!ready?Visibility.Visible:Visibility.Collapsed;
        LuckPanel.Visibility=ready?Visibility.Collapsed:Visibility.Visible;
        ScenePanel.Visibility=_pickerOpen?Visibility.Collapsed:Visibility.Visible;PickerPanel.Visibility=_pickerOpen?Visibility.Visible:Visibility.Collapsed;
        PreparationPanel.Visibility=active||result?Visibility.Collapsed:Visibility.Visible;NestPanel.Visibility=active&&!ready?Visibility.Visible:Visibility.Collapsed;
        ResultPanel.Visibility=result&&!_awaitingHatchContinue?Visibility.Visible:Visibility.Collapsed;
        HatchAnimationPanel.Visibility=result&&_awaitingHatchContinue?Visibility.Visible:Visibility.Collapsed;
        GreenhouseImage.Source=(ImageSource)FindResource(active?(nest!.Status=="ready"?"UiNurseryReadyImage":"UiNurseryWaitingImage"):"UiNurseryImage");
        BabyImage.Visibility=Visibility.Collapsed;BabyImage.Source=null;HatchButton.Visibility=Visibility.Collapsed;
        LuckBar.Value=Math.Clamp(data.NurseryLuckBonus/(ShinyRules.NurseryChanceCap-ShinyRules.BaseChance),0,1);
        LuckText.Text=data.NurseryLuckBonus>0?"Набирается · помогает появлению Shiny":"Новая история";
        if(active){RefreshNest(data,nest!);return;}
        var archived=nest is not null && nest.Status=="hatched" && data.HatchedNurseryResults.TryGetValue(nest.Id,out _);
        OpenPokedexButton.IsEnabled=ActivateButton.IsEnabled=!_busy&&archived;
        if(result) {
            if(!data.HatchedNurseryResults.TryGetValue(nest!.Id,out var child))ResultText.Text="Сохранённый результат вылупления недоступен. Откройте историю сохранений.";
            else {
                BabyImage.Source=Sprite(child.SpeciesId,child.IsShiny);BabyImage.Visibility=Visibility.Visible;
                var luck=nest.LuckBeforeHatch is {} before?"Удача питомника "+(nest.ShinySupported&&child.IsShiny?"сброшена":data.NurseryLuckBonus>before?"увеличилась":"не изменилась")+".":"Изменение удачи этого старого вылупления не сохранено.";
                ResultText.Text=$"№{child.SpeciesId} · {SpeciesName(child.SpeciesId)}{(child.IsShiny?" · Shiny":"")} · характер {child.Nature}. Особенность: {CollectionView.TraitLabel(child.FirstTraitId)} — {CollectionView.TraitEffect(child.FirstTraitId)}. Малыш сохранён в коллекции. {luck}";
                HatchStageText.Text=$"{SpeciesName(child.SpeciesId)} появился!";
            }
            return;
        }
        if(!_busy && (_first!=Guid.Empty || _second!=Guid.Empty) && !PairValid(data,_first,_second)) {
            _first=_second=Guid.Empty;_choice=null;_pendingStart=null;_selectionGeneration++;
        }
        Slot(FirstSlot,data,_first);Slot(SecondSlot,data,_second);
        ChooseFirstButton.Content=_first==Guid.Empty?"Выбрать":"Изменить";ChooseSecondButton.Content=_second==Guid.Empty?"Выбрать":"Изменить";
        ChooseFirstButton.IsEnabled=ChooseSecondButton.IsEnabled=!_busy;
        OutcomePanel.Children.Clear();ResourcePricesPanel.Children.Clear();StartButton.IsEnabled=false;ShopButton.Visibility=Visibility.Collapsed;_shortageBerry=null;CostText.Text=ShortageText.Text=TraitsText.Text="";
        if(_pickerOpen)RefreshPicker();
        if(_first==Guid.Empty||_second==Guid.Empty){PreviewText.Text="Выберите двух разных родителей.";return;}
        try {
            var preview=_preview([_first,_second],_choice);
            PreviewText.Text="Один из этих малышей появится после ожидания. Родительские особенности помогают малышу.";
            if(preview.SelectionUseful){OutcomePanel.Children.Add(ChoiceButton("Без талисмана · случайный малыш",null));foreach(var outcome in preview.Outcomes)OutcomePanel.Children.Add(ChoiceButton("Талисман · "+OutcomeName(data,outcome.SpeciesId),outcome.SpeciesId));}
            foreach(var outcome in preview.Outcomes) {
                var id=outcome.SpeciesId;var known=data.NormalSpeciesIds.Contains(id);var row=new StackPanel {Width=Math.Max(75,((ActualWidth>0?ActualWidth:800)*.4-50)/Math.Min(3,preview.Outcomes.Count)-8),Margin=new Thickness(0,5,0,5)};
                if(known)row.Children.Add(new Image {Source=Sprite(id,false),Height=52,Width=52,HorizontalAlignment=HorizontalAlignment.Left});
                row.Children.Add(new TextBlock {Text=OutcomeName(data,id)+"\n"+(outcome.ShinyAvailable?"Возможна Shiny-форма":"Shiny-форма пока недоступна"),TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.Bold});

                OutcomePanel.Children.Add(row);
            }
            TraitsText.Text="Возможные особенности: "+string.Join("; ",preview.TraitIds.Select(t=>CollectionView.TraitLabel(t)+" — "+CollectionView.TraitEffect(t)))+".";
            RenderPrices(preview.Berries);
            CostText.Text=$"Ожидание: {preview.Hours} ч, с учётом родителей до {preview.ReadyAtUtc.ToLocalTime():g}."+(_choice is null?"":" Талисман потомства ×1.");
            var shortage=preview.Berries.Where(x=>data.Berries.GetValueOrDefault(x.Key)<x.Value).ToArray();
            if(shortage.Length>0){ShortageText.Text="Не хватает: "+string.Join(", ",shortage.Select(x=>$"{ExpeditionView.BerryName(x.Key)} ×{x.Value-data.Berries.GetValueOrDefault(x.Key)}"))+". Можно докупить или найти в походе.";_shortageBerry=shortage[0].Key;_shortageRequired=shortage[0].Value;ShopButton.Content="Докупить ягоды";}
            else if(_choice is not null && data.Items.GetValueOrDefault("offspring-charm")<1){ShortageText.Text="Не хватает талисмана потомства.";_shortageBerry="offspring-charm";_shortageRequired=1;ShopButton.Content="Купить талисман";}
            else StartButton.IsEnabled=!_busy;
            ShopButton.Visibility=_shortageBerry is null?Visibility.Collapsed:Visibility.Visible;
        }catch(Exception ex){PreviewText.Text=ex.Message;}
    }
    private void RenderPrices(IReadOnlyDictionary<string,int> prices)
    {
        foreach (var (id,count) in prices)
        {
            if (!_itemArtCache.TryGetValue(id,out var image))
            {
                try { var path=Path.Combine(_catalog.DataRoot,"final","assets","shop",id+".png"); if(File.Exists(path))image=SpriteImages.Decode(File.ReadAllBytes(path)); }
                catch(Exception ex) when(ex is IOException or InvalidOperationException or NotSupportedException or ArgumentException) {image=null;}
                _itemArtCache[id]=image;
            }
            var row=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,10,5)};
            row.Children.Add(new Image {Source=image,Width=30,Height=30,Margin=new Thickness(0,0,5,0)});
            row.Children.Add(new TextBlock {Text=$"{ExpeditionView.BerryName(id)} ×{count}",VerticalAlignment=VerticalAlignment.Center,FontWeight=FontWeights.Bold});
            ResourcePricesPanel.Children.Add(row);
        }
    }
    private string OutcomeName(SaveData data,int id)=>data.NormalSpeciesIds.Contains(id)?$"№{id} · {SpeciesName(id)}":$"№{id} · неизвестный вид";
    private Button ChoiceButton(string title,int? choice) {
        var button=new Button {Content=new TextBlock {Text=title,TextWrapping=TextWrapping.Wrap,MaxWidth=Math.Max(160,(ActualWidth>0?ActualWidth:800)*.4-110)},Tag=choice,IsEnabled=!_busy,Margin=new Thickness(0,0,6,6),Style=(Style)FindResource("SecondaryButtonStyle"),FontWeight=_choice==choice?FontWeights.Bold:FontWeights.Normal};
        button.Click+=(_,_)=>{if(_busy||_suspended)return;_choice=choice;_pendingStart=null;_selectionGeneration++;Refresh();};return button;
    }
    private void RefreshNest(SaveData data,NurseryState nest) {
        var ready=nest.Status=="ready";NestTitle.Text=ready?"Малыш готов появиться":"Тихое ожидание";
        Slot(NestFirstSlot,data,nest.ParticipantIds.ElementAtOrDefault(0));Slot(NestSecondSlot,data,nest.ParticipantIds.ElementAtOrDefault(1));
        NestParents.Text="Родители: "+string.Join(" + ",nest.ParticipantIds.Select(id=>ParentName(data,id)));
        var variants=nest.PossibleOutcomeSpeciesIds.Count==0?"Варианты этого старого гнезда не сохранены.":"Возможные виды: "+string.Join(", ",nest.PossibleOutcomeSpeciesIds.Select(id=>OutcomeName(data,id)))+".";
        NestTerms.Text="Сохранённая цена: "+string.Join(", ",nest.ResourcePrices.Select(x=>$"{ExpeditionView.BerryName(x.Key)} ×{x.Value}"))+". "+(nest.SelectedSpeciesId is {} selected?"Выбранный вид: "+OutcomeName(data,selected)+".":variants+" Случайный исход скрыт до вылупления.");
        NestTime.Text=ready?"Можно познакомиться с малышом.":$"Готовность: {nest.ReadyAtUtc.ToLocalTime():g} · осталось {Math.Max(0,Math.Ceiling((nest.ReadyAtUtc-DateTimeOffset.UtcNow).TotalMinutes))} мин";
        var total=(nest.ReadyAtUtc-nest.StartedAtUtc).TotalSeconds;NestProgress.Value=ready||total<=0?1:Math.Clamp((DateTimeOffset.UtcNow-nest.StartedAtUtc).TotalSeconds/total,0,1);
        HatchButton.Visibility=ready?Visibility.Visible:Visibility.Collapsed;HatchButton.IsEnabled=!_busy;
    }
    private bool Live(int generation)=>!_suspended && generation==_lifetimeGeneration;
    private void ReleaseBusy()
    {
        _busy = false;
        // Result messages/reveals belong to the captured lifetime; enabled controls belong to the current one.
        if (!_suspended) Refresh();
    }

    private async Task TickAsync()
    {
        if (_suspended || _busy || _refreshingReady) return;
        var nest = _snapshot().Nest;
        if (nest?.Status != "running" || DateTimeOffset.UtcNow < nest.ReadyAtUtc)
        {
            if (nest is not null) Refresh();
            return;
        }

        var life = _lifetimeGeneration;
        _refreshingReady = true;
        try
        {
            await _refreshReady(DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            if (Live(life)) Show(ex.Message);
        }
        finally
        {
            _refreshingReady = false;
            if (!_suspended) Refresh();
        }
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _suspended || !PairValid(_snapshot(), _first, _second)) return;
        var generation = _selectionGeneration;
        var life = _lifetimeGeneration;
        _pendingStart ??= new NurseryRequest([_first, _second], _choice, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var request = _pendingStart;
        _busy = true;
        Refresh();
        try
        {
            var result = await _start(request);
            if (!Live(life) || generation != _selectionGeneration) return;
            if (_snapshot().Nest?.Id != request.OperationId || !result.Success ||
                !_snapshot().CompletedOperations.ContainsKey($"nursery:start:{request.OperationId}"))
                throw new InvalidOperationException("Запуск не подтверждён сохранением.");
            _pendingStart = null;
            Show(result.Message);
        }
        catch (Exception ex)
        {
            if (Live(life) && generation == _selectionGeneration) Show(ex.Message);
        }
        finally
        {
            ReleaseBusy();
        }
    }

    private async void HatchButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _suspended || _snapshot().Nest is not { Status: "ready" } nest) return;
        if (_pendingHatchNest != nest.Id)
        {
            _pendingHatchNest = nest.Id;
            _pendingHatchOperation = Guid.NewGuid();
        }
        var life = _lifetimeGeneration;
        _busy = true;
        Refresh();
        try
        {
            var result = await _hatch(nest.Id, _pendingHatchOperation);
            if (!Live(life)) return;
            var current = _snapshot().Nest;
            if (!result.Success || current?.Id != nest.Id || current.Status != "hatched" ||
                current.HatchOperationId != _pendingHatchOperation || result.Child is null ||
                !_snapshot().HatchedNurseryResults.TryGetValue(nest.Id, out var child) || child.Id != result.Child.Id)
                throw new InvalidOperationException("Вылупление не подтверждено сохранением.");
            _pendingHatchNest = _pendingHatchOperation = Guid.Empty;
            _awaitingHatchContinue = true;
            Refresh();
            if (IsLoaded && SystemParameters.ClientAreaAnimation)
                BabyImage.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(450))
                {
                    FillBehavior = FillBehavior.Stop
                });
        }
        catch (Exception ex)
        {
            if (Live(life)) Show(ex.Message);
        }
        finally
        {
            ReleaseBusy();
        }
    }
    private void HatchContinueButton_Click(object sender,RoutedEventArgs e){if(_suspended||!_awaitingHatchContinue)return;_awaitingHatchContinue=false;BabyImage.BeginAnimation(OpacityProperty,null);Refresh();}
    private void NewNest_Click(object sender,RoutedEventArgs e){if(_busy||_suspended)return;_dismissedResult=_snapshot().Nest?.Id??Guid.Empty;_first=_second=Guid.Empty;_choice=null;_pendingStart=null;Refresh();}
    private void ShopButton_Click(object sender,RoutedEventArgs e){if(!_busy&&!_suspended&&_shortageBerry is {} berry)_shopShortage(berry,_shortageRequired);}
    private void OpenPokedexButton_Click(object sender,RoutedEventArgs e){var data=_snapshot();if(!_busy&&!_suspended&&data.Nest is {Status:"hatched"} nest&&data.HatchedNurseryResults.TryGetValue(nest.Id,out var child))_openPokedex(child.Id);}
    private async void ActivateButton_Click(object sender, RoutedEventArgs e)
    {
        var data = _snapshot();
        if (_busy || _suspended || data.Nest is not { Status: "hatched" } nest ||
            !data.HatchedNurseryResults.TryGetValue(nest.Id, out var child)) return;
        var life = _lifetimeGeneration;
        _busy = true;
        Refresh();
        try
        {
            await _activate(child.Id);
            if (!Live(life)) return;
            if (_snapshot().ActiveIndividualId != child.Id)
                throw new InvalidOperationException("Спутник не подтверждён сохранением.");
            Show($"{SpeciesName(child.SpeciesId)} теперь на рабочем столе.");
        }
        catch (Exception ex)
        {
            if (Live(life)) Show(ex.Message);
        }
        finally
        {
            ReleaseBusy();
        }
    }
}
