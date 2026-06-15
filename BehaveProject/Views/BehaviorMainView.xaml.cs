using BehaveProject.ViewModels;
using Prism.Events;
using System.Windows.Controls;

namespace BehaveProject.Views
{
    /// <summary>
    /// Interaction logic for BehaviorMainView
    /// </summary>
    public partial class BehaviorMainView : UserControl
    {
        IEventAggregator _eventAggregator;
        public BehaviorMainView(IEventAggregator eventAggregator,string lng)
        {
            InitializeComponent();
            (this.DataContext as BehaviorMainViewModel).LanguageChangedMethod(lng);
            _eventAggregator = eventAggregator;
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            this.Focus();
        }

    }
}
