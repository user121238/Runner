using DM.Maui.Framework.Core.Mvvm;
using Runner.Client.ViewModels;

namespace Runner.Client.Views;

public partial class MainPage : BaseContentPage
{
    public MainPage(MainViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }
}
