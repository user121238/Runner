using DM.Maui.Framework.Core.Mvvm;
using Runner.Client.Views;

namespace Runner.Client
{
    public partial class AppShell : BaseShell
    {
        public AppShell()
        {
            InitializeComponent();

        }

        protected override Type SetMainPageType()
        {
            return typeof(MainPage);
        }
    }
}
