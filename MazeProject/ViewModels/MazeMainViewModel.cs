using ControlzEx.Standard;
using MazeProject.Events;
using MazeProject.Models;
using MazeProject.Tools;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;

namespace MazeProject.ViewModels
{
    public class MazeMainViewModel : BindableBase
    {
        private IDialogService _dialogService;
        IEventAggregator _eventAggregator;
        /// <summary>/// Prism Property/// </summary>
		private Robot _playingRobot;

        public Robot PlayingRobot
        {
            get { return _playingRobot; }
            set { SetProperty(ref _playingRobot, value); }
        }

        /// <summary>/// Prism Property/// </summary>
		private Map _selectedMap;

        public Map SelectedMap
        {
            get { return _selectedMap; }
            set { SetProperty(ref _selectedMap, value); }
        }
        /// <summary>/// Prism Property/// </summary>
		private ObservableCollection<Map> _mapList;

        public ObservableCollection<Map> MapList
        {
            get { return _mapList; }
            set { SetProperty(ref _mapList, value); }
        }

        public DelegateCommand GoForwardCommand { get; set; }
        public DelegateCommand GoBackwardCommand { get; set; }
        public DelegateCommand GoLeftCommand { get; set; }
        public DelegateCommand GoRightCommand { get; set; }
        public DelegateCommand SelecetMapCommand { get; set; }

        public DelegateCommand CloseViewCommand { get; set; }
        public DelegateCommand ClearCommand { get; set; }
        public DelegateCommand SendCommand { get; set; }

        /// <summary>/// Prism Property/// </summary>
		private int _selectedIndex;

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set { SetProperty(ref _selectedIndex, value); firstMapMethod(SelectedIndex); }
        }

        public MazeMainViewModel(IDialogService dialogService,IEventAggregator eventAggregator)
        {
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            PlayingRobot = new Robot();
            MapList = new ObservableCollection<Map>();
            GoForwardCommand = new DelegateCommand(GoForwardMethod);
            GoBackwardCommand = new DelegateCommand(GoBackwardMethod);
            GoLeftCommand = new DelegateCommand(GoLeftMethod);
            GoRightCommand = new DelegateCommand(GoRightMethod);
            ClearCommand = new DelegateCommand(ClearMethod);
            SelecetMapCommand = new DelegateCommand(SelecetMapMethod);
            SendCommand = new DelegateCommand(SendMethod);

            CloseViewCommand = new DelegateCommand(CloseViewMethod);
            _eventAggregator.GetEvent<SelecetdMapEvent>().Subscribe(selecetdMapMethod);
            initMap();
        }

        private void SendMethod()
        {
            SendDataService.SendData(PlayingRobot.Mouvments.Where(step => step.Mvt != mouvment.None).ToList());
        }

        private void selecetdMapMethod(int obj)
        {

            ClearMethod();
            foreach (var map in MapList)
            {
                map.IsSelecetd = false;
            }
            MapList[obj].IsSelecetd = true;
            SelectedMap = MapList[obj];
        }

        private void firstMapMethod(int obj)
        {

            foreach (var map in MapList)
            {
                map.IsFirst = false;
            }
            MapList[obj].IsFirst = true;
        }

        private void ClearMethod()
        {
            PlayingRobot.Clear();
            SelectedMap.Clear();
        }

        void initMap()
        {
            MapList.Add(new Map(_eventAggregator, $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}/Images/Maze.png",0, 4, 6, 5, 3,"test"));
            //MapList.Add(new Map(_eventAggregator, $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}/Images/Maze.png", 1, 4, 6, 5, 3, "test"));
            //MapList.Add(new Map(_eventAggregator, $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}/Images/Maze.png", 2, 4, 6, 5, 3, "test"));
            //MapList.Add(new Map(_eventAggregator, $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}/Images/Maze.png", 3, 4, 6, 5, 3, "test"));
            //MapList.Add(new Map(_eventAggregator, $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}/Images/Maze.png", 4, 4, 6, 5, 3, "test"));
            //MapList.Add(new Map(_eventAggregator, $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}/Images/Maze.png", 5, 4, 6, 5, 3, "test"));
            //MapList.Add(new Map(_eventAggregator, $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}/Images/Maze.png", 6, 4, 6, 5, 3, "test"));
            //MapList.Add(new Map(_eventAggregator, $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}/Images/Maze.png", 7, 4, 6, 5, 3, "test"));
            SelectedMap = MapList.Last();
            selecetdMapMethod(MapList.Count - 1);
        }
        private void CloseViewMethod()
        {
            _eventAggregator.GetEvent<MazeCloseEvent>().Publish();
        }
        private void SelecetMapMethod()
        {

            _dialogService.ShowDialog("SelectMapImageView", new DialogParameters
            {

            }, result =>
            {
                if (result.Parameters.Count > 0)
                {
                    SelectedMap.Update(result.Parameters.GetValue<string>("image"), result.Parameters.GetValue<int>("RowNumber"), result.Parameters.GetValue<int>("ColumnsNumber"), result.Parameters.GetValue<int>("Id"));
                    PlayingRobot = new Robot();

                }


            }, "SelectMapImageShell");


        }
        private void GoForwardMethod()
        {

            if ((PlayingRobot.CurrentStep + 1) > PlayingRobot.MaxSteps) return;
            if(SelectedMap.GoForwardMethod())
            {
                PlayingRobot.GoForwardMethod();
            }
            

        }
        private void GoBackwardMethod()
        {
            if ((PlayingRobot.CurrentStep + 1) > PlayingRobot.MaxSteps) return;
            if (SelectedMap.GoBackwardMethod())
            {
                PlayingRobot.GoBackwardMethod();
            }

        }
        private void GoLeftMethod()
        {
            if ((PlayingRobot.CurrentStep + 1) > PlayingRobot.MaxSteps) return;
            if (SelectedMap.GoLeftMethod())
            {
                PlayingRobot.GoLeftMethod();
            }

        }
        private void GoRightMethod()
        {
            if ((PlayingRobot.CurrentStep + 1) > PlayingRobot.MaxSteps) return;
            if (SelectedMap.GoRightMethod())
            {
                PlayingRobot.GoRightMethod();
            }
        }
    }
}
