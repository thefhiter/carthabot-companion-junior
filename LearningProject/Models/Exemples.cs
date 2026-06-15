using LearningProject.Models.Events;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LearningProject.Models
{
    public class Exemples : BindableBase
    {
		private IEventAggregator _eventAggregator;
		public DelegateCommand SelectCommand { get; set; }
		/// <summary>/// Prism Property/// </summary>
		private string _name;

		public string Name
		{
			get { return _name; }
			set { SetProperty(ref _name, value); }
		}

		/// <summary>/// Prism Property/// </summary>
		private string _path;

		public string Path
		{
			get { return _path; }
			set { SetProperty(ref _path, value); }
		}

		/// <summary>/// Prism Property/// </summary>
		private bool _isSelected;

		public bool IsSelected
        {
			get { return _isSelected; }
			set { SetProperty(ref _isSelected, value); }
		}

		public Exemples()
        {
            
        }

        public Exemples(string name,string path, IEventAggregator eventAggregator)
        {
			SelectCommand = new DelegateCommand(SelectMethod);
            _eventAggregator = eventAggregator;

			this.Path = path;
			this.Name = name;
        }

        private void SelectMethod()
        {
			_eventAggregator.GetEvent<PDFSelectedEvent>().Publish(this.Name);
        }

        private string GetFileName(string path)
		{
            string sourceFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PDFs");
            return System.IO.Path.Combine(sourceFolder, "Commande Boutons.pdf");
        }
    }
}
