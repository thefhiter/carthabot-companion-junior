using CompanionApp.ViewModels;
using Prism.Events;
using System.Collections.Generic;
using System.Windows.Controls;

namespace CompanionApp.Views
{
    /// <summary>
    /// Interaction logic for KidsCodingView – the under-7 tangible block-programming method.
    /// The view-model needs the list of COM ports that existed before the MicroPython firmware
    /// was flashed, so it can pick out the CarthaBot's freshly-created port.
    /// </summary>
    public partial class KidsCodingView : UserControl
    {
        public KidsCodingView(IEventAggregator eventAggregator, List<string> oldComs)
        {
            InitializeComponent();
            DataContext = new KidsCodingViewModel(eventAggregator, oldComs);
        }
    }
}
