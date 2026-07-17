using CompanionApp.Events;
using CompanionApp.Models;
using CompanionApp.Models.Classes;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace CompanionApp.ViewModels
{
    public class AtelierViewModel : BindableBase
    {
        IEventAggregator _eventAggregator;
        /// <summary>/// Prism Property/// </summary>
		private Visibility _isVisible;

        public Visibility IsVisible
        {
            get { return _isVisible; }
            set { SetProperty(ref _isVisible, value); }
        }

        /// <summary>/// Prism Property/// </summary>
        private ObservableCollection<CarthaModule> _atelier;

        public ObservableCollection<CarthaModule> Atelier
        {
            get { return _atelier; }
            set { SetProperty(ref _atelier, value); }
        }


        public AtelierViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<MenuSelectionChangedEvent>().Subscribe(
                (Section selectedSection) =>
                {
                    IsVisible = selectedSection == Section.Ateliers ? Visibility.Visible : Visibility.Collapsed;
                }
            );
            Atelier = new ObservableCollection<CarthaModule>();
            string folderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "ModulesImages");

            Atelier.Add(new CarthaModule("Learn", $"{folderPath}/mod.png", "#2bc0e8", "Learn",Module.Learn, _eventAggregator));
            Atelier.Add(new CarthaModule("Behaviours", $"{folderPath}/mod.png", "#da44e2", "Behaviours", Module.Behaviour, _eventAggregator));
            Atelier.Add(new CarthaModule("Explore", $"{folderPath}/mod.png", "#7359fa", "Advanced Programming (python)", Module.Python, _eventAggregator));
            // NOTE: keep KidsCoding at index [3] so the VPL card's Atelier[4] binding in AtelierView.xaml stays valid,
            // even though the "Kids Coding" card itself was removed from the grid (replaced by the Companion VPL).
            Atelier.Add(new CarthaModule("KidsCoding", $"{folderPath}/mod.png", "#2bc0e8", "Kids Coding (under 7)", Module.KidsCoding, _eventAggregator));
            Atelier.Add(new CarthaModule("VplJunior", $"{folderPath}/mod.png", "#EE7B2F", "Coding (under 6)", Module.VplJunior, _eventAggregator));
            // Draw with a pen — turtle plotter. Kept at index [5] (the DrawCard binds Atelier[5] in AtelierView.xaml).
            Atelier.Add(new CarthaModule("Draw", $"{folderPath}/mod.png", "#E84393", "Draw with a pen", Module.Draw, _eventAggregator));
            //Atelier.Add(new CarthaModule("Explore", $"{folderPath}/mod.png", "#7359fa", "Explore", Module.Explore, _eventAggregator));
            //Atelier.Add(new CarthaModule("Tracer", $"{folderPath}/mod.png", "#EB5A3C", "Tracer", Module.Tracer, _eventAggregator));

        }

    }
}
