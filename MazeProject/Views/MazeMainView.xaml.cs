using MazeProject.Events;
using MazeProject.Tools;
using Prism.Events;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using static System.Net.Mime.MediaTypeNames;

namespace MazeProject.Views
{
    /// <summary>
    /// Interaction logic for MazeMainView
    /// </summary>
    public partial class MazeMainView : UserControl
    {
        IEventAggregator _eventAggregator;

        public MazeMainView(IEventAggregator eventAggregator,List<string> olsComs)
        {

            InitializeComponent();
            _eventAggregator = eventAggregator;
            List<string> newcoms = new List<string>(SerialPort.GetPortNames());
            SendDataService.COMPort = newcoms.Except(olsComs).First();
        }

        private void MyImage_Loaded(object sender, RoutedEventArgs e)
        {
            SetImageSize();
        }

        private void MyImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SetImageSize();
        }

        private void SetImageSize()
        {
            if (MyImage != null)
            {
                _eventAggregator.GetEvent<ImageSizeChangedEvent>().Publish(new double[] { MyImage.ActualWidth, MyImage.ActualHeight });
            }
        }
    }
}
