using DM.Maui.Framework.Core.Mvvm;
using Runner.Client.ViewModels;

namespace Runner.Client.Views;

public partial class WatchWorkoutPage : BaseContentPage
{
    public WatchWorkoutPage(WatchWorkoutViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }
}
