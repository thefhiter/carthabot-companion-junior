using System;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
namespace LearningProject
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class PdfViewer1 : Window
    {
        public PdfViewer1()
        {
            InitializeComponent();
            String path = AppDomain.CurrentDomain.BaseDirectory;
            path = path + "Assets/FormFillingDocument.pdf";
            pdfViewer.Load(path);
        }
        private void ExportImage_Click(object sender, RoutedEventArgs e)
        {
            //Export the particular PDF page as image at the page index of 0
            BitmapSource image = pdfViewer.ExportAsImage(0);
            //Set up the output path
            string output = @"..\..\Image";
            if (image != null)
            {
                //Initialize the new Jpeg bitmap encoder
                BitmapEncoder encoder = new JpegBitmapEncoder();
                //Create the bitmap frame using the bitmap source and add it to the encoder
                encoder.Frames.Add(BitmapFrame.Create(image));
                //Create the file stream for the output in the desired image format
                FileStream stream = new FileStream(output + ".Jpeg", FileMode.Create);
                //Save the stream, so that the image will be generated in the output location
                encoder.Save(stream);
            }
        }																																									
    }
}
