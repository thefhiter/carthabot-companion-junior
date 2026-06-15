using Prism.Services.Dialogs;
using System.Windows;

namespace MazeProject.Views
{
    /// <summary>
    /// Interaction logic for SelectMapImageShell.xaml
    /// </summary>
    public partial class SelectMapImageShell : IDialogWindow
    {
        public IDialogResult Result { get; set; }

        public SelectMapImageShell()
        {
            InitializeComponent();
        }
    }
}
