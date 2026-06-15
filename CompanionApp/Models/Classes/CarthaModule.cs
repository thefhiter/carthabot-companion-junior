using CompanionApp.Events;
using DMSkin.Core.MVVM;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace CompanionApp.Models.Classes
{
    public class CarthaModule : BindableBase
    {

		IEventAggregator _eventAggregator;

		public DelegateCommand SelectedCommand { get; set; }
        public DelegateCommand BorderLeaveCommand { get; set; }
        public DelegateCommand BorderHoverCommand { get; set; }

        /// <summary>/// Prism Property/// </summary>
        private string _name;

		public string Name
		{
			get { return _name; }
			set { SetProperty(ref _name, value); }
		}
		/// <summary>/// Prism Property/// </summary>
		private string _image;

		public string Image
		{
			get { return _image; }
			set { SetProperty(ref _image, value); }
		}

		/// <summary>/// Prism Property/// </summary>
		private string _discription;

		public string Discription
		{
			get { return _discription; }
			set { SetProperty(ref _discription, value); }
		}
		/// <summary>/// Prism Property/// </summary>
		private bool _onHovered;

		public bool OnHovered
		{
			get { return _onHovered; }
			set { SetProperty(ref _onHovered, value); }
		}
		/// <summary>/// Prism Property/// </summary>
		private string _color;

		public string Color
		{
			get { return _color; }
			set { SetProperty(ref _color, value); }
		}
		/// <summary>/// Prism Property/// </summary>
		private Module _module;

		public Module Module
		{
			get { return _module; }
			set { SetProperty(ref _module, value); }
		}



		public CarthaModule(string name,string image,string color, string discription, Module module, IEventAggregator eventAggregator)
		{
			this.Name = name;
			this.Image = image;
			this.Discription = discription;
			this.Color = color;
			this.Module = module;

            this._eventAggregator = eventAggregator;

            SelectedCommand = new DelegateCommand(SelectedMethod);
            BorderHoverCommand = new DelegateCommand(BorderHoverMethod);
            BorderLeaveCommand = new DelegateCommand(BorderLeaveMethod);
			OnHovered = false;

        }

        private void BorderLeaveMethod(object obj)
        {
			OnHovered = false;
        }

        private void BorderHoverMethod(object obj)
        {
			OnHovered = true;
        }

        private void SelectedMethod(object obj)
        {
            _eventAggregator.GetEvent<LoadModuleEvent>().Publish(Module);




        }
    }
}
