using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace MazeProject.Models
{
    public class CustomCell : BindableBase
    {
        public event Action<int> SelectedEvent;

        public static Facing Orientation { get; set; }
        public DelegateCommand SelectedCommand { get; set; }
        /// <summary>/// Prism Property/// </summary>
        private int _id;

        public int Id
        {
            get { return _id; }
            set { SetProperty(ref _id, value); }

        }
        /// <summary>/// Prism Property/// </summary>
		private double _width;

        public double Width
        {
            get { return _width; }
            set { SetProperty(ref _width, value); }
        }

        /// <summary>/// Prism Property/// </summary>
        private double _height;

        public double Height
        {
            get { return _height; }
            set { SetProperty(ref _height, value); }
        }

        /// <summary>/// Prism Property/// </summary>
		private bool _selected;

        public bool Selected
        {
            get { return _selected; }
            set { SetProperty(ref _selected, value); }
        }

        /// <summary>/// Prism Property/// </summary>
		private bool _isThere;

        public bool IsThere
        {
            get { return _isThere; }
            set { SetProperty(ref _isThere, value); }
        }

        /// <summary>/// Prism Property/// </summary>
        private string _robotImagePath;


        /// <summary>/// Prism Property/// </summary>
		private CellType _cellT;

        public CellType CellT
        {
            get { return _cellT; }
            set { SetProperty(ref _cellT, value); }
        }

        public string RobotImagePath
        {
            get { return _robotImagePath; }
            set { SetProperty(ref _robotImagePath, value); }
        }

        public CustomCell()
        {
            SelectedCommand = new DelegateCommand(SelectedMethod);
            IsThere = false;
            Selected = false;
            RobotImagePath = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}/Resources/robotU.png";
            Update(Orientation);

        }

        public CustomCell(int id) : this()
        {
            Id = id;

        }
        private void SelectedMethod()
        {
            SelectedEvent?.Invoke(Id);
        }

        public void Update(Facing face)
        {
            switch (face)
            {
                case Facing.Up:
                    RobotImagePath = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}/Resources/robotU.png";

                    break;
                case Facing.Down:
                    RobotImagePath = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}/Resources/robotD.png";

                    break;
                case Facing.Left:
                    RobotImagePath = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}/Resources/robotL.png";

                    break;
                case Facing.Right:
                    RobotImagePath = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}/Resources/robotR.png";

                    break;
                default:
                    break;
            }
        }
    }

}
