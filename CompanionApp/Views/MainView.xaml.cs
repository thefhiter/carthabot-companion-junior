using CompanionApp.Events;
using Prism.Events;
using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CompanionApp.Views
{
    /// <summary>
    /// Interaction logic for MainView
    /// </summary>
    public partial class MainView : UserControl
    {
        IEventAggregator _eventAggregator;
        public MainView(IEventAggregator eventAggregator)
        {
            InitializeComponent();
            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<ShowSlidingViewEvent>().Subscribe(ShowSlidingViewMethod);

        }

        private void ShowSlidingViewMethod(bool obj)
        {
            if (obj)
            {
                // Expand the border width to 600
                Storyboard expandWidth = (Storyboard)FindResource("ExpandHeightStoryboard");
                expandWidth.Begin();

            }
            else
            {
                // Collapse the border width to 0
                Storyboard collapseWidth = (Storyboard)FindResource("CollapseHeightStoryboard");
                collapseWidth.Begin();


            }
        }


    }
}
