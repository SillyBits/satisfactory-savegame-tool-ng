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

namespace SatisfactorySavegameTool.Dialogs
{
	/// <summary>
	/// Interaction logic for ImageDialog.xaml
	/// </summary>
	public partial class ImageDialog : Window
	{
		public ImageDialog(Window parent, string title, BitmapSource image)
			: base()
		{
			InitializeComponent();

			Icon = Application.Current.MainWindow.Icon;

			if (parent == null)
				parent = Application.Current.MainWindow;
			Owner = parent;

			if (!string.IsNullOrEmpty(title))
				Title = title;

			// Based on image dimensions, scale it down
			double w = image.Width;
			double h = image.Height;
			if (w > 1024 && h > 1024)
				image = new TransformedBitmap(image, new ScaleTransform(0.5, 0.5));

			Image.Source = image;
		}

		private void Close_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}
