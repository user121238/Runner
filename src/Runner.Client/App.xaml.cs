namespace Runner.Client
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            // 全部界面按深色设计，不跟随系统主题，免得系统弹窗 / 原生控件在浅色下突兀
            UserAppTheme = AppTheme.Dark;
        }
    }
}
