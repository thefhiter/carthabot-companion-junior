using LearningProject.Models.Events;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CompanionApp.ViewModels
{
    public class AssamblyViewModel : BindableBase
    {
        private IEventAggregator _eventAggregator;

        public DelegateCommand CloseViewCommand { get; set; }

        private Uri _src;
        public Uri source
        {
            get { return _src; }
            set { SetProperty(ref _src, value); }
        }

        public AssamblyViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            CloseViewCommand = new DelegateCommand(CloseViewMethod);

            string sourceFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources/Assambly");
            string htmlFile = Path.Combine(sourceFolder, "Main View.svg");

            source = new Uri(@$"{htmlFile}");
        }

        private void CloseViewMethod()
        {
            _eventAggregator.GetEvent<LearnCloseEvent>().Publish();
        }
    }
}
