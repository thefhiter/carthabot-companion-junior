using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MazeProject.Events
{
    public class MazeCloseEvent : PubSubEvent { }
    public class ImageSizeChangedEvent : PubSubEvent<double[]> { }
    public class SelecetdMapEvent : PubSubEvent<int> { }

}
