using DMSkin.Core.MVVM;
using MazeProject.Events;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MazeProject.Models
{
    public class Map : BindableBase
    {
        IEventAggregator _eventAggregator;

        public DelegateCommand SelectedCommand { get; set; }

        private ObservableCollection<CustomCell> _flatMapGrid;

        public ObservableCollection<CustomCell> FlatMapGrid
        {
            get { return _flatMapGrid; }
            set { SetProperty(ref _flatMapGrid, value); }
        }

        /// <summary>/// Prism Property/// </summary>
		private string _imagePath;

        public string ImagePath
        {
            get { return _imagePath; }
            set { SetProperty(ref _imagePath, value); }
        }

        /// <summary>/// Prism Property/// </summary>
		private string _robotImageString;

        public string RobotImageString
        {
            get { return _robotImageString; }
            set { SetProperty(ref _robotImageString, value); }
        }


        /// <summary>/// Prism Property/// </summary>
        private int _rowNumber;

        public int RowNumber
        {
            get { return _rowNumber; }
            set
            {
                SetProperty(ref _rowNumber, value);
            }
        }
        /// <summary>/// Prism Property/// </summary>
		private int _columnsNumber;

        public int ColumnsNumber
        {
            get { return _columnsNumber; }
            set
            {
                SetProperty(ref _columnsNumber, value);
            }
        }
        /// <summary>/// Prism Property/// </summary>
		private bool _isSelected =false;

        public bool IsSelecetd
        {
            get { return _isSelected; }
            set { SetProperty(ref _isSelected, value); }
        }
        /// <summary>/// Prism Property/// </summary>
		private int _index;

        public int Index
        {
            get { return _index; }
            set { SetProperty(ref _index, value); }
        }

        /// <summary>/// Prism Property/// </summary>
		private string _name;

        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value); }
        }
        /// <summary>/// Prism Property/// </summary>
		private bool _isFirst = false;

        public bool IsFirst
        {
            get { return _isFirst; }
            set { SetProperty(ref _isFirst , value); }
        }

        int _startrow, _startcolumn;
        public Map(IEventAggregator eventAggregator,string imagePath,int index,int columns,int rows,int startRow,int startColumn,string name)
        {
            Index = index;
            SelectedCommand = new DelegateCommand(SelectedMethod);
            _eventAggregator = eventAggregator;
            this.ImagePath = imagePath; 
            BitmapImage image = new BitmapImage(new Uri(ImagePath));
            RowNumber = rows;
            ColumnsNumber = columns;
            FlatMapGrid = new ObservableCollection<CustomCell>();
            CustomCell.Orientation = 0;
            _eventAggregator.GetEvent<ImageSizeChangedEvent>().Subscribe(UpdateMap);
            for(int i=0;i<columns*rows;i++)
            {
                FlatMapGrid.Add(new CustomCell(i));

            }

            FlatMapGrid[startRow* ColumnsNumber + startColumn].IsThere = true;
            _startrow = startRow;
            _startcolumn = startColumn;
            Name = name;
        }

        private void SelectedMethod(object obj)
        {
            _eventAggregator.GetEvent<SelecetdMapEvent>().Publish(Index);

        }

        private void UpdateMap(double[] dimen)
        {
            double DisplayedWidth = dimen[0];
            double DisplayedHeight = dimen[1];

            if (DisplayedWidth > 0 && DisplayedHeight > 0) // Important check!
            {
                double cellWidth = DisplayedWidth / (double)ColumnsNumber;
                double cellHeight = DisplayedHeight / (double)RowNumber;

                foreach(var cell in FlatMapGrid)
                {
                    cell.Width = cellWidth;
                    cell.Height = cellHeight;

                }
            }
        }
        public void Clear()
        {
            CustomCell.Orientation = 0;
            foreach(var cell in FlatMapGrid)
            {
                cell.IsThere = false;
                cell.Update(Facing.Up);
            }
            FlatMapGrid[_startrow * ColumnsNumber + _startcolumn].IsThere = true;

        }
        public void Update(string image, int rows, int colum,int startingId)
        {

            RowNumber = rows;
            ColumnsNumber = colum;
            ImagePath = image;
            FlatMapGrid = new ObservableCollection<CustomCell>();
            for (int i = 0; i < ColumnsNumber; i++)
            {
                for (int j = 0; j < RowNumber; j++)
                {
                    FlatMapGrid.Add(new CustomCell()
                    {
                        Width = 500 / (double)ColumnsNumber,
                        Height = 500 / (double)RowNumber,
                        Id = FlatMapGrid.Count()
                    }); 
                }

            }
            FlatMapGrid.Where(cl => cl.Id==startingId).First().IsThere = true;
            FlatMapGrid.Where(cl => cl.Id == startingId).First().Update(CustomCell.Orientation);
        }
        public bool GoForwardMethod()
        {
            int index = FlatMapGrid.IndexOf(FlatMapGrid.Where(w => w.IsThere).FirstOrDefault());
            int x = 0;

            switch (CustomCell.Orientation)
            {
                case Facing.Up:

                    if ((index - ColumnsNumber) >= 0)
                    {
                        foreach (var item in FlatMapGrid)
                        {
                            item.IsThere = false;
                        }
                        FlatMapGrid[index - ColumnsNumber].IsThere = true;
                        return true;

                    }
                    return false;
                case Facing.Down:
                    if ((index + ColumnsNumber) < ColumnsNumber * RowNumber)
                    {
                        foreach (var item in FlatMapGrid)
                        {
                            item.IsThere = false;
                        }
                        FlatMapGrid[index + ColumnsNumber].IsThere = true;
                        return true;
                    }
                    return false;
                case Facing.Left:
                     x = (index % ColumnsNumber);
                    if (x > 0)
                    {
                        foreach (var item in FlatMapGrid)
                        {
                            item.IsThere = false;
                        }
                        FlatMapGrid[index - 1].IsThere = true;
                        return true;
                    }
                    return false;
                case Facing.Right:
                     x = (index % ColumnsNumber);

                    if (x < ColumnsNumber - 1)
                    {
                        foreach (var item in FlatMapGrid)
                        {
                            item.IsThere = false;
                        }
                        FlatMapGrid[index + 1].IsThere = true;
                        return true;

                    }
                    return false;
                default:
                    break;
            }


            return false;


        }
        public bool GoBackwardMethod()
        {
            int index = FlatMapGrid.IndexOf(FlatMapGrid.Where(w => w.IsThere).FirstOrDefault());
            int x = 0;

            switch (CustomCell.Orientation)
            {
                case Facing.Up:

                    if ((index + ColumnsNumber) < ColumnsNumber * RowNumber)
                    {
                        foreach (var item in FlatMapGrid)
                        {
                            item.IsThere = false;
                        }
                        FlatMapGrid[index + ColumnsNumber].IsThere = true;
                        return true;
                    }
                    return false;
                case Facing.Down:
                    if ((index - ColumnsNumber) >= 0)
                    {
                        foreach (var item in FlatMapGrid)
                        {
                            item.IsThere = false;
                        }
                        FlatMapGrid[index - ColumnsNumber].IsThere = true;
                        return true;

                    }
                    return false;
                case Facing.Right:
                    x = (index % ColumnsNumber);
                    if (x > 0)
                    {
                        foreach (var item in FlatMapGrid)
                        {
                            item.IsThere = false;
                        }
                        FlatMapGrid[index - 1].IsThere = true;
                        return true;
                    }
                    return false;
                case Facing.Left:
                    x = (index % ColumnsNumber);

                    if (x < ColumnsNumber - 1)
                    {
                        foreach (var item in FlatMapGrid)
                        {
                            item.IsThere = false;
                        }
                        FlatMapGrid[index + 1].IsThere = true;
                        return true;

                    }
                    return false;
                default:
                    break;
            }


            return false;

        }

        public bool GoLeftMethod()
        {

            switch (CustomCell.Orientation)
            {
                case Facing.Up:
                    CustomCell.Orientation = Facing.Left;
                    break;
                case Facing.Down:
                    CustomCell.Orientation = Facing.Right;
                    break;
                case Facing.Left:
                    CustomCell.Orientation = Facing.Down;
                    break;
                case Facing.Right:
                    CustomCell.Orientation = Facing.Up;
                    break;
                default:
                    break;
            }

            foreach (var item in FlatMapGrid)
            {
                item.Update(CustomCell.Orientation);
            }
            return true;

            //int index = FlatMapGrid.IndexOf(FlatMapGrid.Where(w => w.IsThere).FirstOrDefault());
            //var x = (index % ColumnsNumber);


            //if (x > 0)
            //{
            //    foreach (var item in FlatMapGrid)
            //    {
            //        item.IsThere = false;
            //    }
            //    FlatMapGrid[index - 1].IsThere = true;
            //    return true;

            //}
            return false;

        }
        public bool GoRightMethod()
        {
            switch (CustomCell.Orientation)
            {
                case Facing.Up:
                    CustomCell.Orientation = Facing.Right;
                    break;
                case Facing.Down:
                    CustomCell.Orientation = Facing.Left;
                    break;
                case Facing.Left:
                    CustomCell.Orientation = Facing.Up;
                    break;
                case Facing.Right:
                    CustomCell.Orientation = Facing.Down;
                    break;
                default:
                    break;
            }
            foreach (var item in FlatMapGrid)
            {
                item.Update(CustomCell.Orientation);
            }
            return true;
            //int index = FlatMapGrid.IndexOf(FlatMapGrid.Where(w => w.IsThere).FirstOrDefault());
            //var x = (index % ColumnsNumber);

            //if (x < ColumnsNumber - 1)
            //{
            //    foreach (var item in FlatMapGrid)
            //    {
            //        item.IsThere = false;
            //    }
            //    FlatMapGrid[index + 1].IsThere = true;
            //    return true;

            //}
            //return false;

        }
    }
    public class MapItem : BindableBase 
	{
        /// <summary>/// Prism Property/// </summary>
        private int _id;

        public int Id
        {
            get { return _id; }
            set { SetProperty(ref _id, value); }
        }

        /// <summary>/// Prism Property/// </summary>
        private int _cordX;

		public int CordX
		{
			get { return _cordX; }
			set { SetProperty(ref _cordX, value); }
		}

		/// <summary>/// Prism Property/// </summary>
		private int _cordY;

		public int CordY
		{
			get { return _cordY; }
			set { SetProperty(ref _cordY, value); }
		}
        public MapItem(int cordx,int cordy,int id)
        {
            this.CordX = cordx;
			this.CordY = cordy;
			this.Id = id;
			IsThere = false;
        }

		/// <summary>/// Prism Property/// </summary>
		private bool _isThere;

		public bool IsThere
		{
			get { return _isThere; }
			set { SetProperty(ref _isThere, value); }
		}

	}
}
