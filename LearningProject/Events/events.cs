using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LearningProject.Models.Events
{
    public class PDFSelectedEvent : PubSubEvent<string> { }
    public class LoadPDFEvent : PubSubEvent<string> { }
    public class LearnCloseEvent : PubSubEvent { }

}
