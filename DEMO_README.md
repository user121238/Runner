# Runner Demo - DM.Maui.Framework 示例应用

## 概述

这是一个完整的 DM.Maui.Framework 示例应用，展示了框架的所有核心功能（除硬件部分）。

## 已实现的功能

### 1. 数据模型
- ✅ `User` - 用户模型
- ✅ `Product` - 产品模型

### 2. ViewModels
- ✅ `MainViewModel` - 主页，展示框架核心功能
  - 点击计数器（带缓存）
  - 导航演示
  - 对话框演示
  - 操作记录
  - 主题切换响应

- ✅ `UserListViewModel` - 用户列表，展示数据库和缓存
  - 用户列表加载（带缓存）
  - 添加用户
  - 删除用户
  - 切换用户状态
  - 刷新列表
  - 清空所有用户

- ✅ `ProductListViewModel` - 产品列表，展示数据库和缓存
  - 产品列表加载（带缓存）
  - 添加示例产品
  - 删除产品
  - 搜索产品
  - 刷新列表
  - 清空所有产品

- ✅ `SettingsViewModel` - 设置页面，展示主题和配置
  - 主题切换（System/Light/Dark）
  - 缓存信息显示
  - 清除缓存
  - 重置应用
  - 关于信息
  - 测试通知

### 3. 配置
- ✅ `MauiProgram.cs` - 完整的服务注册
  - 核心服务（导航、对话框、缓存、主题）
  - 数据库服务（LiteDB）
  - 蓝牙服务（Shiny）
  - 自动注册 ViewModels 和 Views

- ✅ `AppShell.xaml` - Tab 导航配置
  - 主页
  - 用户列表
  - 产品列表
  - 设置

## 框架功能演示

### MVVM 架构
- 所有 ViewModel 继承自 `BaseViewModel`
- 使用 `CommunityToolkit.Mvvm` 的 `[ObservableProperty]` 和 `[RelayCommand]`
- 自动属性通知和命令生成

### 数据库操作 (LiteDB)
- Repository 模式
- UnitOfWork 模式
- CRUD 操作
- 异步数据访问

### 缓存管理
- 内存缓存
- GetOrCreate 模式
- 自动过期
- 缓存清除

### 主题切换
- BaseViewModel 集成主题服务
- 自动订阅主题变化
- OnThemeChanged 虚方法
- 内置主题切换命令

### 导航服务
- Shell 导航
- 类型安全导航
- 参数传递

### 对话框服务
- 警告对话框
- 确认对话框
- 操作表

### 日志记录
- Microsoft.Extensions.Logging
- 结构化日志
- Debug 输出

## 下一步：创建 XAML 页面

需要创建以下 XAML 页面（Views 文件夹）：

### 1. MainPage.xaml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:Runner.Client.ViewModels"
             x:Class="Runner.Client.Views.MainPage"
             x:DataType="vm:MainViewModel"
             Title="{Binding Title}">

    <ScrollView>
        <VerticalStackLayout Padding="20" Spacing="15">

            <!-- 欢迎消息 -->
            <Label Text="{Binding WelcomeMessage}"
                   FontSize="24"
                   FontAttributes="Bold"
                   HorizontalOptions="Center" />

            <!-- 点击计数器 -->
            <Frame>
                <VerticalStackLayout Spacing="10">
                    <Label Text="{Binding ClickCount, StringFormat='点击次数: {0}'}"
                           FontSize="18" />
                    <Button Text="点击我"
                            Command="{Binding IncrementCounterCommand}" />
                </VerticalStackLayout>
            </Frame>

            <!-- 导航按钮 -->
            <Label Text="导航演示" FontSize="18" FontAttributes="Bold" />
            <Button Text="用户列表" Command="{Binding NavigateToUsersCommand}" />
            <Button Text="产品列表" Command="{Binding NavigateToProductsCommand}" />
            <Button Text="设置" Command="{Binding NavigateToSettingsCommand}" />

            <!-- 对话框演示 -->
            <Label Text="对话框演示" FontSize="18" FontAttributes="Bold" />
            <Button Text="显示对话框" Command="{Binding ShowDialogCommand}" />
            <Button Text="显示操作表" Command="{Binding ShowActionSheetCommand}" />

            <!-- 主题切换 -->
            <Label Text="主题切换" FontSize="18" FontAttributes="Bold" />
            <HorizontalStackLayout Spacing="10">
                <Button Text="浅色" Command="{Binding SetLightThemeCommand}" />
                <Button Text="深色" Command="{Binding SetDarkThemeCommand}" />
                <Button Text="系统" Command="{Binding SetSystemThemeCommand}" />
                <Button Text="切换" Command="{Binding ToggleThemeCommand}" />
            </HorizontalStackLayout>

            <!-- 缓存操作 -->
            <Label Text="缓存操作" FontSize="18" FontAttributes="Bold" />
            <Button Text="清除缓存" Command="{Binding ClearCacheCommand}" />

            <!-- 最近操作 -->
            <Label Text="最近操作" FontSize="18" FontAttributes="Bold" />
            <Button Text="清除记录" Command="{Binding ClearActionsCommand}" />
            <CollectionView ItemsSource="{Binding RecentActions}"
                            MaximumHeightRequest="200">
                <CollectionView.ItemTemplate>
                    <DataTemplate>
                        <Label Text="{Binding .}"
                               FontSize="12"
                               Padding="5" />
                    </DataTemplate>
                </CollectionView.ItemTemplate>
            </CollectionView>

        </VerticalStackLayout>
    </ScrollView>

</ContentPage>
```

### 2. UserListPage.xaml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:Runner.Client.ViewModels"
             x:Class="Runner.Client.Views.UserListPage"
             x:DataType="vm:UserListViewModel"
             Title="{Binding Title}">

    <Grid RowDefinitions="Auto,*">

        <!-- 工具栏 -->
        <HorizontalStackLayout Grid.Row="0" Padding="10" Spacing="10">
            <Button Text="添加用户" Command="{Binding AddUserCommand}" />
            <Button Text="刷新" Command="{Binding RefreshCommand}" />
            <Button Text="清空" Command="{Binding ClearAllUsersCommand}" />
        </HorizontalStackLayout>

        <!-- 用户列表 -->
        <RefreshView Grid.Row="1"
                     IsRefreshing="{Binding IsRefreshing}"
                     Command="{Binding RefreshCommand}">
            <CollectionView ItemsSource="{Binding Users}">
                <CollectionView.ItemTemplate>
                    <DataTemplate>
                        <SwipeView>
                            <SwipeView.RightItems>
                                <SwipeItems>
                                    <SwipeItem Text="删除"
                                               BackgroundColor="Red"
                                               Command="{Binding Source={RelativeSource AncestorType={x:Type vm:UserListViewModel}}, Path=DeleteUserCommand}"
                                               CommandParameter="{Binding .}" />
                                </SwipeItems>
                            </SwipeView.RightItems>

                            <Frame Margin="10,5" Padding="10">
                                <Grid ColumnDefinitions="Auto,*,Auto">
                                    <Label Grid.Column="0" Text="{Binding Avatar}" FontSize="24" />
                                    <VerticalStackLayout Grid.Column="1" Padding="10,0">
                                        <Label Text="{Binding Name}" FontAttributes="Bold" />
                                        <Label Text="{Binding Email}" FontSize="12" />
                                        <Label Text="{Binding CreatedAt, StringFormat='{0:yyyy-MM-dd HH:mm}'}" FontSize="10" />
                                    </VerticalStackLayout>
                                    <Switch Grid.Column="2"
                                            IsToggled="{Binding IsActive}"
                                            OnColor="Green" />
                                </Grid>
                            </Frame>
                        </SwipeView>
                    </DataTemplate>
                </CollectionView.ItemTemplate>
            </CollectionView>
        </RefreshView>

    </Grid>

</ContentPage>
```

### 3. ProductListPage.xaml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:Runner.Client.ViewModels"
             x:Class="Runner.Client.Views.ProductListPage"
             x:DataType="vm:ProductListViewModel"
             Title="{Binding Title}">

    <Grid RowDefinitions="Auto,Auto,*">

        <!-- 搜索栏 -->
        <SearchBar Grid.Row="0"
                   Placeholder="搜索产品..."
                   Text="{Binding SearchText}"
                   SearchCommand="{Binding SearchCommand}" />

        <!-- 工具栏 -->
        <HorizontalStackLayout Grid.Row="1" Padding="10" Spacing="10">
            <Button Text="添加示例" Command="{Binding AddSampleProductsCommand}" />
            <Button Text="刷新" Command="{Binding RefreshCommand}" />
            <Button Text="清空" Command="{Binding ClearAllProductsCommand}" />
        </HorizontalStackLayout>

        <!-- 产品列表 -->
        <RefreshView Grid.Row="2"
                     IsRefreshing="{Binding IsRefreshing}"
                     Command="{Binding RefreshCommand}">
            <CollectionView ItemsSource="{Binding Products}">
                <CollectionView.ItemTemplate>
                    <DataTemplate>
                        <SwipeView>
                            <SwipeView.RightItems>
                                <SwipeItems>
                                    <SwipeItem Text="删除"
                                               BackgroundColor="Red"
                                               Command="{Binding Source={RelativeSource AncestorType={x:Type vm:ProductListViewModel}}, Path=DeleteProductCommand}"
                                               CommandParameter="{Binding .}" />
                                </SwipeItems>
                            </SwipeView.RightItems>

                            <Frame Margin="10,5" Padding="10">
                                <Grid ColumnDefinitions="Auto,*,Auto">
                                    <Label Grid.Column="0" Text="{Binding ImageUrl}" FontSize="32" />
                                    <VerticalStackLayout Grid.Column="1" Padding="10,0">
                                        <Label Text="{Binding Name}" FontAttributes="Bold" />
                                        <Label Text="{Binding Description}" FontSize="12" />
                                        <Label Text="{Binding Category}" FontSize="10" />
                                    </VerticalStackLayout>
                                    <VerticalStackLayout Grid.Column="2">
                                        <Label Text="{Binding Price, StringFormat='¥{0:F2}'}" FontAttributes="Bold" />
                                        <Label Text="{Binding Stock, StringFormat='库存: {0}'}" FontSize="10" />
                                    </VerticalStackLayout>
                                </Grid>
                            </Frame>
                        </SwipeView>
                    </DataTemplate>
                </CollectionView.ItemTemplate>
            </CollectionView>
        </RefreshView>

    </Grid>

</ContentPage>
```

### 4. SettingsPage.xaml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:Runner.Client.ViewModels"
             x:Class="Runner.Client.Views.SettingsPage"
             x:DataType="vm:SettingsViewModel"
             Title="{Binding Title}">

    <ScrollView>
        <VerticalStackLayout Padding="20" Spacing="20">

            <!-- 当前主题 -->
            <Frame>
                <VerticalStackLayout Spacing="10">
                    <Label Text="当前主题" FontSize="18" FontAttributes="Bold" />
                    <Label Text="{Binding CurrentThemeDescription}" FontSize="16" />
                </VerticalStackLayout>
            </Frame>

            <!-- 主题切换 -->
            <Frame>
                <VerticalStackLayout Spacing="10">
                    <Label Text="选择主题" FontSize="18" FontAttributes="Bold" />
                    <Button Text="🌓 跟随系统" Command="{Binding SetSystemThemeCommand}" />
                    <Button Text="🌞 浅色主题" Command="{Binding SetLightThemeCommand}" />
                    <Button Text="🌙 深色主题" Command="{Binding SetDarkThemeCommand}" />
                    <Button Text="🔄 切换主题" Command="{Binding ToggleThemeCommand}" />
                </VerticalStackLayout>
            </Frame>

            <!-- 缓存信息 -->
            <Frame>
                <VerticalStackLayout Spacing="10">
                    <Label Text="缓存管理" FontSize="18" FontAttributes="Bold" />
                    <Label Text="{Binding CacheInfo}" />
                    <Button Text="清除缓存" Command="{Binding ClearCacheCommand}" />
                </VerticalStackLayout>
            </Frame>

            <!-- 应用信息 -->
            <Frame>
                <VerticalStackLayout Spacing="10">
                    <Label Text="应用信息" FontSize="18" FontAttributes="Bold" />
                    <Label Text="{Binding AppVersion, StringFormat='版本: {0}'}" />
                    <Button Text="关于" Command="{Binding ShowAboutCommand}" />
                    <Button Text="测试通知" Command="{Binding TestNotificationCommand}" />
                </VerticalStackLayout>
            </Frame>

            <!-- 危险操作 -->
            <Frame BorderColor="Red">
                <VerticalStackLayout Spacing="10">
                    <Label Text="危险操作" FontSize="18" FontAttributes="Bold" TextColor="Red" />
                    <Button Text="重置应用" Command="{Binding ResetAppCommand}" BackgroundColor="Red" />
                </VerticalStackLayout>
            </Frame>

            <!-- 快捷操作 -->
            <Button Text="更多操作..." Command="{Binding ShowActionSheetCommand}" />

        </VerticalStackLayout>
    </ScrollView>

</ContentPage>
```

## 创建 Code-Behind 文件

每个 XAML 页面需要对应的 .cs 文件：

```csharp
// Views/MainPage.xaml.cs
using DM.Maui.Framework.Core.Mvvm;
using Runner.Client.ViewModels;

namespace Runner.Client.Views;

public partial class MainPage : BaseContentPage<MainViewModel>
{
    public MainPage(MainViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }
}

// 其他页面类似...
```

## 编译和运行

```bash
cd D:\Eric\Code\Runner
dotnet build
dotnet run --project src/Runner.Client/Runner.Client.csproj
```

## 功能测试清单

- [ ] 主页点击计数器
- [ ] 导航到各个页面
- [ ] 对话框显示
- [ ] 主题切换
- [ ] 用户列表 CRUD
- [ ] 产品列表 CRUD
- [ ] 缓存功能
- [ ] 数据库持久化
- [ ] 日志记录

## 注意事项

1. **图标文件**：AppShell 中引用的图标需要添加到 Resources/Images 文件夹
2. **数据库路径**：数据库文件保存在 `FileSystem.AppDataDirectory/runner.db`
3. **缓存过期**：用户列表和产品列表缓存 5 分钟
4. **日志级别**：Debug 模式下日志级别为 Debug

## 已展示的框架功能

✅ MVVM 架构（BaseViewModel）
✅ 数据库操作（Repository + UnitOfWork）
✅ 缓存管理（ICacheService）
✅ 主题切换（IThemeService + BaseViewModel 集成）
✅ 导航服务（INavigationService）
✅ 对话框服务（IDialogService）
✅ 依赖注入（自动注册）
✅ 日志记录（ILogger）
✅ 响应式编程（主题变化订阅）

## 未包含的功能

❌ 硬件功能（蓝牙、TCP）- 按要求排除
❌ 国际化（Localization）- 可选功能
❌ 自定义主题资源 - 可选功能
