using CompanionApp.Models;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CompanionApp.Events
{
    public class MenuSelectionChangedEvent : PubSubEvent<Section> { }
    public class LoadModuleEvent : PubSubEvent<Module> { }

    public class ShowSlidingViewEvent : PubSubEvent<bool> { }
    public class NewVersionAvaliableEvent : PubSubEvent<string> { }

    // Raised by the Kids Coding (under-7 tangible programming) view when the child closes it.
    public class KidsCodingCloseEvent : PubSubEvent { }

    // Raised by the embedded VPL (Coding under-6) view when the child closes it.
    public class VplCloseEvent : PubSubEvent { }

}
