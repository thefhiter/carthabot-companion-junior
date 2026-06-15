using LearningProject.Models.Events;
using Prism.Events;
using Syncfusion.Windows.PdfViewer;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace LearningProject.Views
{
    /// <summary>
    /// Interaction logic for LearningMainView
    /// </summary>
    public partial class LearningMainView : UserControl
    {
        IEventAggregator _eventAggregator;

        public LearningMainView(IEventAggregator eventAggregator)
        {
            InitializeComponent();
            _eventAggregator = eventAggregator;
        }


        private void WebView2_NavigationStarting(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs e)
        {
            LoadingBorder.Visibility = Visibility.Visible;
        }

        private async void WebView2_NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            await Task.Delay(3000);
            //LoadingBorder.Visibility = Visibility.Collapsed;
        }
    }
}
