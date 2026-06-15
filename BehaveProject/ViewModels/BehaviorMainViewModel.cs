using BehaveProject.Events;
using BehaveProject.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Windows.Media;

namespace BehaveProject.ViewModels
{
    public class BehaviorMainViewModel : BindableBase
    {
        IEventAggregator _eventAggregator;
        public DelegateCommand CloseViewCommand { get; set; }
        public DelegateCommand GoRightCommand { get; set; }
        public DelegateCommand GoLeftCommand { get; set; }


        

        /// <summary>/// Prism Property/// </summary>
		private ObservableCollection<Mode> _modes;

        public ObservableCollection<Mode> Modes
        {
            get { return _modes; }
            set { SetProperty(ref _modes, value); }
        }
        /// <summary>/// Prism Property/// </summary>
		private Mode _selecetdMode;

        public Mode SelecetdMode
        {
            get { return _selecetdMode; }
            set { SetProperty(ref _selecetdMode, value); }
        }

        public bool IsEnglish = false;
        public BehaviorMainViewModel(IEventAggregator eventAggregator)
        {
            CloseViewCommand = new DelegateCommand(CloseViewMethod);
            string folderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");

            _eventAggregator = eventAggregator;
            Modes = new ObservableCollection<Mode>();

            Modes.Add(new Mode(_eventAggregator,$"{folderPath}/ObstacleOvoid.jpg", "Obstacle Avoider",
                new SolidColorBrush(Color.FromRgb(247, 148, 28)),
                "In this mode, the robot uses infrared (IR) sensors to detect obstacles in its path and autonomously adjusts its direction to prevent collisions. The IR sensors continuously monitor the surroundings, and when an object is detected within a predefined distance, the robot changes its trajectory to navigate safely.",
                true)); // #F7941C

            Modes.Add(new Mode(_eventAggregator, $"{folderPath}/LineFollower.jpg", "Line Follower",
                new SolidColorBrush(Color.FromRgb(0, 255, 0)),
                "In this mode, the robot follows a predefined path marked by a line (black line on a white surface) using infrared line sensors. It continuously reads the line position and adjusts its steering to stay on track.")); // #00FF00

            Modes.Add(new Mode(_eventAggregator, $"{folderPath}/Friendly.jpg", "Friendly",
                new SolidColorBrush(Color.FromRgb(88, 127, 237)),
                "In this mode, the robot’s movement is manually controlled using physical buttons located on the robot itself. Each button corresponds to a specific action, such as moving forward, backward, turning left, or turning right.")); // #587fed

            Modes.Add(new Mode(_eventAggregator, $"{folderPath}/Obdidient.jpg", "Obedient",
                new SolidColorBrush(Color.FromRgb(241, 100, 162)),
                "In this mode, the robot uses infrared (IR) sensors to detect and follow a specific object, such as a hand. The robot continuously monitors the target’s position and adjusts its movement to minimize the gap and stay close to the object.")); // #F164A2
            _eventAggregator.GetEvent<SelectedModeEvent>().Subscribe(SelectedModeMethod);
            SelecetdMode = Modes[0];
            GoLeftCommand = new DelegateCommand(GoLeftMethod);
            GoRightCommand = new DelegateCommand(GoRightMethod);

            _eventAggregator.GetEvent<LanguageUpdatedEvent>().Subscribe(LanguageChangedMethod);



        }

        public void LanguageChangedMethod(string lng)
        {
            if (lng == "fr"){
                Modes[0].Name = "Éviteur d'obstacles";
                Modes[1].Name = "Suiveur";
                Modes[2].Name = "Amical";
                Modes[3].Name = "Obéissant";

            }
            else 
            {
                Modes[0].Name = "Obstacle Avoider";
                Modes[1].Name = "Follower";
                Modes[2].Name = "Friendly";
                Modes[3].Name = "Obedient";
            }
        }

        private void GoRightMethod()
        {
            int index = Modes.IndexOf(SelecetdMode);
            if (index == 3)
            {
                SelecetdMode = Modes[0];

            }
            else
            {
                SelecetdMode = Modes[index+1];
            }
            foreach (var mode in Modes)
            {
                if (mode.Name == SelecetdMode.Name)
                {
                    mode.IsSelected = true;
                    SelecetdMode = mode;

                }
                else
                {
                    mode.IsSelected = false;
                }
            }
        }

        private void GoLeftMethod()
        {
            int index = Modes.IndexOf(SelecetdMode);
            if (index == 0)
            {
                SelecetdMode = Modes[3];
            }
            else
            {
                SelecetdMode = Modes[index - 1];
            }

            foreach (var mode in Modes)
            {
                if (mode.Name == SelecetdMode.Name)
                {
                    mode.IsSelected = true;
                    SelecetdMode = mode;
                }
                else
                {
                    mode.IsSelected = false;
                }
            }
        }

        private void CloseViewMethod()
        {
            _eventAggregator.GetEvent<BehaveCloseEvent>().Publish();
        }

        private void SelectedModeMethod(Mode obj)
        {

            foreach(var mode in Modes)
            {
                if(mode.Name == obj.Name)
                {
                    mode.IsSelected = true;
                    SelecetdMode = mode;

                }
                else
                {
                    mode.IsSelected = false;
                }
            }
        }
    }
}
