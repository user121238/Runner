using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
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
    private string _errorDetails = "";
    private string _copyErrorText = "复制错误详情";
    private bool _isLoading;
    private bool _hasLoaded;

    public WorkoutsViewModel(WorkoutStore store, INavigationService navigation, IPageDialogService dialogs)
    {
        _store = store;
        _navigation = navigation;
        _dialogs = dialogs;
        CreateCommand = new AsyncDelegateCommand(() => _navigation.NavigateAsync("WorkoutEditorPage"));
        EditCommand = new AsyncDelegateCommand<WorkoutListItem>(item => _navigation.NavigateAsync("WorkoutEditorPage", ("id", item.Id)));
        DeleteCommand = new AsyncDelegateCommand<WorkoutListItem>(DeleteAsync);
        RetryCommand = new AsyncDelegateCommand(LoadAsync, () => !IsLoading);
        CopyErrorDetailsCommand = new AsyncDelegateCommand(CopyErrorDetailsAsync, () => HasErrorDetails);
        Items.CollectionChanged += (_, _) => RaisePropertyChanged(nameof(ShowEmpty));
        _store.Changed += OnStoreChanged;
    }

    public ObservableCollection<WorkoutListItem> Items { get; } = new();
    public AsyncDelegateCommand CreateCommand { get; }
    public AsyncDelegateCommand<WorkoutListItem> EditCommand { get; }
    public AsyncDelegateCommand<WorkoutListItem> DeleteCommand { get; }
    public AsyncDelegateCommand RetryCommand { get; }
    public AsyncDelegateCommand CopyErrorDetailsCommand { get; }

    public string Message
    {
        get => _message;
        private set
        {
            if (!SetProperty(ref _message, value)) return;
            RaisePropertyChanged(nameof(HasMessage));
            RaisePropertyChanged(nameof(ShowEmpty));
        }
    }

    public bool HasMessage => _message.Length > 0;

    public string ErrorDetails
    {
        get => _errorDetails;
        private set
        {
            if (!SetProperty(ref _errorDetails, value)) return;
            RaisePropertyChanged(nameof(HasErrorDetails));
            CopyErrorText = "复制错误详情";
            CopyErrorDetailsCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasErrorDetails => _errorDetails.Length > 0;
    public string CopyErrorText { get => _copyErrorText; private set => SetProperty(ref _copyErrorText, value); }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (!SetProperty(ref _isLoading, value)) return;
            RaisePropertyChanged(nameof(ShowEmpty));
            RetryCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>只有成功读取了空列表，才显示“还没有课程”；加载失败不是空数据。</summary>
    public bool ShowEmpty => _hasLoaded && !IsLoading && !HasMessage && Items.Count == 0;

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext) { }

    public void OnNavigatedTo(NavigationContext navigationContext) => _ = LoadAsync();

    private void OnStoreChanged() => MainThread.BeginInvokeOnMainThread(() => _ = LoadAsync());

    private async Task LoadAsync()
    {
        if (IsLoading) return;
        _hasLoaded = false;
        IsLoading = true;
        Message = "";
        ErrorDetails = "";
        try
        {
            var plans = await _store.GetAllAsync();
            // 全部转换成功后再替换，失败时保留上次成功加载的列表。
            var items = plans.Select(plan => new WorkoutListItem(plan)).ToList();
            Items.Clear();
            foreach (var item in items) Items.Add(item);
            _hasLoaded = true;
        }
        catch (Exception ex)
        {
            Message = $"课程加载失败：{ex.Message}";
            ErrorDetails = ex.ToString();
        }
        finally
        {
            IsLoading = false;
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
            ErrorDetails = ex.ToString();
        }
    }

    private async Task CopyErrorDetailsAsync()
    {
        try
        {
            await Clipboard.Default.SetTextAsync(ErrorDetails);
            CopyErrorText = "已复制错误详情";
        }
        catch
        {
            // 不用剪贴板故障覆盖原始的数据错误，用户仍能重新复制或重试加载。
            CopyErrorText = "复制失败，点击重试";
        }
    }
}
