using Prism.Commands;
using Prism.Mvvm;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MazeProject.Models
{
    public class Robot : BindableBase
    {
		/// <summary>/// Prism Property/// </summary>
		private ObservableCollection<Step> _mouvments;

		public ObservableCollection<Step> Mouvments
        {
			get { return _mouvments; }
			set { SetProperty(ref _mouvments, value); }
		}

        /// <summary>/// Prism Property/// </summary>
		private int _maxSteps;

        public int MaxSteps
        {
            get { return _maxSteps; }
            set { SetProperty(ref _maxSteps, value); }
        }
        /// <summary>/// Prism Property/// </summary>
		private int _currentStep;

        public int CurrentStep
        {
            get { return _currentStep; }
            set { SetProperty(ref _currentStep, value); }
        }

        public Robot()
        {
            MaxSteps = 20;
            Mouvments = new ObservableCollection<Step>(new Step[MaxSteps]);

            for (int  i = 0; i < MaxSteps; i++)
            {
                Mouvments[i] = new Step(i+1);
            }

            CurrentStep = 0;
        }
        public void Clear()
        {
            foreach (var step in Mouvments) step.Mvt = mouvment.None;
            CurrentStep = 0;

        }
        public void GoForwardMethod()
        {
            if ((CurrentStep + 1) < MaxSteps)
            {
                Mouvments[CurrentStep].Mvt = mouvment.Forward;
                CurrentStep++;
            }
        }
        public void GoBackwardMethod()
        {
            if ((CurrentStep + 1) <= MaxSteps)
            {
                Mouvments[CurrentStep].Mvt = mouvment.Backward;
                CurrentStep++;
            }
        }
        public void GoLeftMethod()
        {
            if ((CurrentStep + 1) <= MaxSteps)
            {
                Mouvments[CurrentStep].Mvt = mouvment.Left;
                CurrentStep++;
            }
        }
        public void GoRightMethod()
        {
            if ((CurrentStep + 1) <= MaxSteps)
            {
                Mouvments[CurrentStep].Mvt = mouvment.Right;
                CurrentStep++;
            }
        }
    }

}
