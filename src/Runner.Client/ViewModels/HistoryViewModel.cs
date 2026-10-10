using System.Collections.ObjectModel;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Prism.Navigation.Regions;
using Prism.Services;
using Runner.Business.Services.Activities;

namespace Runner.Client.ViewModels;

/// <summary>记录列表：点开看详情，删除前确认。</summary>
public class HistoryViewModel : BindableBase, IRegionAware
{
    private readonly ActivityStore _store;
    private readonly INavigationService _navigation;
    private readonly IPageDialogService _dialogs;
    private string _message = "";

    public HistoryViewModel(ActivityStore store, INavigationService navigation, IPageDialogService dialogs)
    {
        _store = store;
        _navigation = navigation;
        _dialogs = dialogs;
        OpenCommand = new AsyncDelegateCommand<ActivityListItem>(item => _navigation.NavigateAsync("ActivityDetailPage", ("id", item.Id)));
        DeleteCommand = new AsyncDelegateCommand<ActivityListItem>(DeleteAsync);
        _store.Changed += OnStoreChanged;
    }

    public ObservableCollection<ActivityListItem> Items { get; } = new();
    public AsyncDelegateCommand<ActivityListItem> OpenCommand { get; }
    public AsyncDelegateCommand<ActivityListItem> DeleteCommand { get; }

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
            var records = await _store.GetAllAsync();
            Items.Clear();
            foreach (var record in records) Items.Add(new ActivityListItem(record));
            Message = "";
        }
        catch (Exception ex)
        {
            Message = $"加载失败: {ex.Message}";
        }
    }

    private async Task DeleteAsync(ActivityListItem item)
    {
        var confirmed = await _dialogs.DisplayAlertAsync("删除记录", $"删除 {item.Title} 的记录？", "删除", "取消");
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
