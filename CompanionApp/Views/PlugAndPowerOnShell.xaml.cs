using CompanionApp.ViewModels;
using DMSkin.WPF;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CompanionApp.Views
{
    /// <summary>
    /// Interaction logic for PlugAndPowerOnShell.xaml
    /// </summary>
    public partial class PlugAndPowerOnShell : DMSkinSimpleWindow, IDialogWindow
    {
        public IDialogResult Result { get; set; }

        public PlugAndPowerOnShell()
        {
            InitializeComponent();
        }

        private void GifPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            GifPlayer.Position = TimeSpan.Zero; // Reset the position to loop
            GifPlayer.Play(); // Start playing again
        }


    }
}
