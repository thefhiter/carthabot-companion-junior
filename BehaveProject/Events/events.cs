using BehaveProject.Models;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BehaveProject.Events
{
    public class SelectedModeEvent : PubSubEvent<Mode> { }
    public class BehaveCloseEvent : PubSubEvent { }
    public class LanguageUpdatedEvent : PubSubEvent<string> { }

}
