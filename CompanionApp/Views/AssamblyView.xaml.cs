using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CompanionApp.Views
{
    /// <summary>
    /// Interaction logic for AssamblyView
    /// </summary>
    public partial class AssamblyView : UserControl
    {
        private bool _show3d;

        public AssamblyView()
        {
            InitializeComponent();
        }

        // Toggle between the SVG assembly manual (WebView2) and the interactive 3D exploded view.
        private void OnToggle3D(object sender, RoutedEventArgs e)
        {
            _show3d = !_show3d;

            if (_show3d && string.IsNullOrEmpty(Exploded3D.Source))
            {
                string path = Path.Combine(AppContext.BaseDirectory, "Vpl", "Assets", "carthabot_exploded.obj");
                if (File.Exists(path)) Exploded3D.Source = path;
            }

            Exploded3DHost.Visibility = _show3d ? Visibility.Visible : Visibility.Collapsed;
            ManualHost.Visibility = _show3d ? Visibility.Collapsed : Visibility.Visible;
            Toggle3DText.Text = _show3d ? "2D" : "3D";
        }

        private void WebView2_NavigationStarting(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs e)
        {
            LoadingBorder.Visibility = System.Windows.Visibility.Visible;
        }

        private async void WebView2_NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            await Task.Delay(3000);
            //LoadingBorder.Visibility = Visibility.Collapsed;
        }
    }
}
