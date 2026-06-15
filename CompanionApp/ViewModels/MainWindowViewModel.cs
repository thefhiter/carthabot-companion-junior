using CompanionApp.Events;
using CompanionApp.Models.Classes;
using CompanionApp.Service;
using DMSkin.Core.MVVM;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using BehaveProject.Events;

namespace CompanionApp.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        IEventAggregator _eventAggregator;
        public DelegateCommand UpdateCommand { get; set; }
        /// <summary>/// Prism Property/// </summary>
		private bool _isUpToDate;

        public bool IsUpToDate
        {
            get { return _isUpToDate; }
            set { SetProperty(ref _isUpToDate, value); }
        }

        /// <summary>/// Prism Property/// </summary>
        private string _version;

        public string Version
        {
            get { return _version; }
            set { SetProperty(ref _version, value); }
        }

        /// <summary>/// Prism Property/// </summary>
		private string _title;

        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }
        /// <summary>/// Prism Property/// </summary>
		private string _updateContent;

        public string UpdateContent
        {
            get { return _updateContent; }
            set { SetProperty(ref _updateContent, value); }
        }

        /// <summary>/// Prism Property/// </summary>
		private ObservableCollection<Language> _languages;

        public ObservableCollection<Language> Languages
        {
            get { return _languages; }
            set { SetProperty(ref _languages, value); }
        }
        /// <summary>/// Prism Property/// </summary>
		private int _selectedIndex;

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set { SetProperty(ref _selectedIndex, value); SetLanguageDictionary(SelectedIndex);  }
        }


        public MainWindowViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<NewVersionAvaliableEvent>().Subscribe(NewVersionAvaliableMethod);
            Version = IniSupport.GetVersion();

            Title = $"Companion Application ({Version})";
            IsUpToDate = true;

            UpdateCommand = new DelegateCommand(UpdateMethod);

            Languages =new ObservableCollection<Language>();

            Languages.Add(new Language("FR", $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}/Resources/fr.png"));
            Languages.Add(new Language("EN", $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}/Resources/en.png"));

            SelectedIndex = IniSupport.GetLanguage() == "FR" ? 0 : 1;
        }



        public void SetLanguageDictionary(int index)
        {
            IniSupport.UpdateLanguage(Languages[index].Name);
            ResourceDictionary dict = new ResourceDictionary();
            switch (index)
            {
                case 0:
                    dict.Source = new Uri("..\\Resources\\StringResources-FR.xaml", UriKind.Relative);

                    Settings.Default.Language = "fr";

                    break;

                case 1:
                    dict.Source = new Uri("..\\Resources\\StringResources.xaml", UriKind.Relative);
                    Settings.Default.Language = "en";

                    break;

                default:
                    dict.Source = new Uri("..\\Resources\\StringResources.xaml", UriKind.Relative);
                    Settings.Default.Language = "en";

                    break;

            }
            _eventAggregator.GetEvent<LanguageUpdatedEvent>().Publish(Settings.Default.Language);
            App.Current.Resources.MergedDictionaries.Add(dict);
        }
        private void UpdateMethod(object obj)
        {
            
            CheckVersion.OpenNewVersion(newVersion);
        }
        private string newVersion;
        private void NewVersionAvaliableMethod(string version)
        {
            if (!string.IsNullOrEmpty(version))
            {
                newVersion = version;
                UpdateContent = $"Update to {version}";
                IsUpToDate = false;
            }
        }


    }
}
