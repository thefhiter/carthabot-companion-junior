using MazeProject.Models;
using Microsoft.Win32;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MazeProject.ViewModels
{
    public class SelectMapImageViewModel : BindableBase, IDialogAware
    {
        public DelegateCommand LoadImageCommand { get; set; }
        public DelegateCommand OkCommand { get; set; }
        public DelegateCommand CancelCommand { get; set; }

        /// <summary>/// Prism Property/// </summary>
        private string _imagePath;

        public string ImagePath
        {
            get { return _imagePath; }
            set { SetProperty(ref _imagePath, value); }
        }

        /// <summary>/// Prism Property/// </summary>
		private int _columnsNumber;

        public int ColumnsNumber
        {
            get { return _columnsNumber; }
            set { SetProperty(ref _columnsNumber, value);
                LoadImageCommand = new DelegateCommand(LoadImageMethod);

                CustGrid = new ObservableCollection<CustomCell>();
                for (int i = 0; i < ColumnsNumber; i++)
                {
                    for (int j = 0; j < RowNumber; j++)
                    {
                        CustomCell cell = new CustomCell()
                        {
                            Width = 700 / (double)ColumnsNumber,
                            Height = 700 / (double)RowNumber,
                            Id = CustGrid.Count()

                        };
                        cell.SelectedEvent += selected;

                        CustGrid.Add(cell);
                    }

                }
            }
        }
        /// <summary>/// Prism Property/// </summary>
		private int _rowNumber;

        public int RowNumber
        {
            get { return _rowNumber; }
            set { SetProperty(ref _rowNumber, value);
                LoadImageCommand = new DelegateCommand(LoadImageMethod);

                CustGrid = new ObservableCollection<CustomCell>();
                for (int i = 0; i < ColumnsNumber; i++)
                {
                    for (int j = 0; j < RowNumber; j++)
                    {
                        CustomCell cell = new CustomCell()
                        {
                            Width = 700 / (double)ColumnsNumber,
                            Height = 700 / (double)RowNumber,
                            Id= CustGrid.Count()
                        };
                        cell.SelectedEvent += selected;
                        CustGrid.Add(cell);
                    }

                }
            }
        }

        /// <summary>/// Prism Property/// </summary>
		private ObservableCollection<CustomCell> _custGrid;

        public ObservableCollection<CustomCell> CustGrid
        {
            get { return _custGrid; }
            set { SetProperty(ref _custGrid, value); }
        }

        public SelectMapImageViewModel()
        {
            LoadImageCommand = new DelegateCommand(LoadImageMethod);
            OkCommand = new DelegateCommand(OkMethod);
            CancelCommand = new DelegateCommand(CancelMethode);

            CustGrid = new ObservableCollection<CustomCell>();
            ImagePath = "";
            RowNumber = 4;
            ColumnsNumber = 4;



        }

        private void selected(int id)
        {
            if (CustGrid.Where(cl => cl.Id == id && cl.Selected).Any())
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
                CustGrid.Where(cl => cl.Id == id).First().Update(CustomCell.Orientation);

            }
            else
            {
                foreach (CustomCell cell in CustGrid)
                {
                    cell.Selected = false;
                }
                CustGrid.Where(cl => cl.Id == id).First().Selected = true;
                CustomCell.Orientation = Facing.Up;
                CustGrid.Where(cl => cl.Id == id).First().Update(CustomCell.Orientation);

            }


        }
        private void OkMethod()
        {

            DialogResult dialogResult = new DialogResult();
            dialogResult.Parameters.Add("image", ImagePath);
            dialogResult.Parameters.Add("RowNumber", RowNumber);
            dialogResult.Parameters.Add("ColumnsNumber", ColumnsNumber);
            dialogResult.Parameters.Add("Id", CustGrid.Where(cl => cl.Selected).First().Id);

            RequestClose(dialogResult);

        }
        private void CancelMethode()
        {
            RequestClose(new DialogResult());

        }

        private void LoadImageMethod()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Select an Image",
                Filter = "Image Files (*.png;*.jpeg;*.jpg)|*.png;*.jpeg;*.jpg",
                Multiselect = false // Ensure only one file can be selected
            };

            // Show the dialog and process the result
            if (openFileDialog.ShowDialog() == true)
            {
                // Set the selected file path to ImagePath
                ImagePath = openFileDialog.FileName;
            }
        }

        public string Title => "";

        public event Action<IDialogResult> RequestClose;

        public bool CanCloseDialog()
        {
            return true;
        }

        public void OnDialogClosed()
        {

        }

        public void OnDialogOpened(IDialogParameters parameters)
        {

        }
    }
}
