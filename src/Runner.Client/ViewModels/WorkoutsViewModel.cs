using System.Collections.ObjectModel;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Prism.Navigation.Regions;
using Prism.Services;
using Runner.Business.Services.Workouts;

namespace Runner.Client.ViewModels;

/// <summary>课程列表：新建 / 编辑进独立页面，删除前确认。</summary>
public class WorkoutsViewModel : BindableBase, IRegionAware
{
    private readonly WorkoutStore _store;
    private readonly INavigationService _navigation;
    private readonly IPageDialogService _dialogs;
    private string _message = "";

    public WorkoutsViewModel(WorkoutStore store, INavigationService navigation, IPageDialogService dialogs)
    {
        _store = store;
        _navigation = navigation;
        _dialogs = dialogs;
        CreateCommand = new AsyncDelegateCommand(() => _navigation.NavigateAsync("WorkoutEditorPage"));
        EditCommand = new AsyncDelegateCommand<WorkoutListItem>(item => _navigation.NavigateAsync("WorkoutEditorPage", ("id", item.Id)));
        DeleteCommand = new AsyncDelegateCommand<WorkoutListItem>(DeleteAsync);
        _store.Changed += OnStoreChanged;
    }

    public ObservableCollection<WorkoutListItem> Items { get; } = new();
    public AsyncDelegateCommand CreateCommand { get; }
    public AsyncDelegateCommand<WorkoutListItem> EditCommand { get; }
    public AsyncDelegateCommand<WorkoutListItem> DeleteCommand { get; }

    public string Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value)) RaisePropertyChanged(nameof(HasMessage));
        }
    }

    public bool HasMessage => _message.Length > 0;

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext) { }

    public void OnNavigatedTo(NavigationContext navigationContext) => _ = LoadAsync();

    private void OnStoreChanged() => MainThread.BeginInvokeOnMainThread(() => _ = LoadAsync());

    private async Task LoadAsync()
    {
        try
        {
            var plans = await _store.GetAllAsync();
            Items.Clear();
            foreach (var plan in plans) Items.Add(new WorkoutListItem(plan));
            Message = "";
        }
        catch (Exception ex)
        {
            Message = $"加载失败: {ex.Message}";
        }
    }

    private async Task DeleteAsync(WorkoutListItem item)
    {
        var confirmed = await _dialogs.DisplayAlertAsync("删除课程", $"删除「{item.Name}」？", "删除", "取消");
        if (!confirmed) return;
        try
        {
            await _store.DeleteAsync(item.Id);
        }
        catch (Exception ex)
        {
            Message = $"删除失败: {ex.Message}";
        }
    }
}
