using System.Windows;
using System.Windows.Controls;
using PokeCorner.App.Tests.Theme;
namespace PokeCorner.App.Tests.GlobalUpdate;
public sealed partial class NurseryUiTests
{
    [Fact]
    public void ApprovedNurseryStartsWithEmptySlotsAndPackagedScene()
    {
        WpfTestHost.Run(() => {
            var (data, _, _) = State(); var view = View(data);
            WpfTestHost.Layout(view,840,540);
            Assert.NotNull(view.FindName("GreenhouseImage"));
            Assert.Equal("Выбрать",((Button)view.FindName("ChooseFirstButton")!).Content);
            Assert.Equal("Выбрать",((Button)view.FindName("ChooseSecondButton")!).Content);
            Assert.False(((Button)view.FindName("StartButton")!).IsEnabled);
        });
    }
    [Fact]
    public void ApprovedNurseryDraftCancelKeepsPairEmpty()
    {
        WpfTestHost.Run(() => {
            var (data, _, _) = State(); var view = View(data);
            ((Button)view.FindName("ChooseFirstButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(Visibility.Visible,((Grid)view.FindName("PickerPanel")!).Visibility);
            ((WrapPanel)view.FindName("PickerIndividualsPanel")!).Children.OfType<Button>().First(b=>b.IsEnabled).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ((Button)view.FindName("PickerCancelButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(((Button)view.FindName("StartButton")!).IsEnabled);
            Assert.Equal("Выбрать",((Button)view.FindName("ChooseFirstButton")!).Content);
        });
    }
    [Theory]
    [InlineData(1000,650)] [InlineData(840,540)]
    public void ApprovedNurseryListsEightHundredIndividualsInBoundedPages(int width,int height)
    {
        WpfTestHost.Run(() => {
            var (data, _, _) = State(); var species=data.Individuals[0].CurrentSpeciesId;
            data.Individuals.Clear();
            for(int i=0;i<800;i++) {var pet=Individual(species);pet.Nickname="Очень длинная кириллическая кличка "+i;data.Individuals.Add(pet);}
            var view=View(data);WpfTestHost.Layout(view,width,height);
            ((Button)view.FindName("ChooseFirstButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            // D-17: three rows of tiles per page, so the bound follows the width (still far below 800).
            Assert.InRange(((WrapPanel)view.FindName("PickerIndividualsPanel")!).Children.OfType<Button>().Count(),1,30);
            Assert.Contains("800",((TextBlock)view.FindName("PickerCountText")!).Text);
            ((TextBox)view.FindName("ParentSearchBox")!).Text="000000";
            Assert.Empty(((WrapPanel)view.FindName("PickerIndividualsPanel")!).Children.OfType<Button>());
        });
    }
    [Fact]
    public void ApprovedNurseryRarityFilterUsesCanonicalTagsAndRussianLabels()
    {
        WpfTestHost.Run(()=> {
            var (data,_,_)=State();var view=View(data);
            ((Button)view.FindName("ChooseFirstButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var box=(ComboBox)view.FindName("RarityBox")!;
            var labels=new Dictionary<string,string> { ["Common"]="Обычный",["Uncommon"]="Необычный",["Rare"]="Редкий",["Legendary"]="Легендарный" };
            foreach(var item in box.Items.OfType<ComboBoxItem>().Skip(1))
            {
                Assert.Equal(labels[(string)item.Tag],item.Content);
                box.SelectedItem=item;
                var ids=data.Individuals.Where(p=>Catalog.Value.Species.Single(s=>s.Id==p.CurrentSpeciesId).Rarity==(string)item.Tag).Select(p=>p.CurrentSpeciesId).Distinct().Order().ToArray();
                Assert.Equal(ids,((WrapPanel)view.FindName("PickerIndividualsPanel")!).Children.OfType<Button>().Select(b=>data.Individuals.Single(p=>p.Id==(Guid)b.Tag).CurrentSpeciesId).Distinct().Order().ToArray());
            }
        });
    }
}
