using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

/*
 * TODO:
 * 
 * - Add export button
 * 
 */

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

		private void Save_Click(object sender, RoutedEventArgs e)
		{
			SaveFileDialog dlg = new SaveFileDialog();
			dlg.Title = Translate._("ImageDialog.Save.Title");
			dlg.InitialDirectory = App.EXPORTPATH;
			dlg.DefaultExt = Translate._("ImageDialog.Save.DefaultExt");
			dlg.Filter = Translate._("ImageDialog.Save.Filter");
			if (dlg.ShowDialog().GetValueOrDefault(false) == true)
			{
				// Get selected extension to find encoder needed
				string[] filters = dlg.Filter.Split('|');
				int index = ((dlg.FilterIndex-1) * 2) + 1;
				string ext;
				if (index >= 0 || index < filters.Length)
					ext = filters[index].Substring(2);
				else
					ext = Path.GetExtension(dlg.FileName).Substring(1);

				BitmapEncoder enc = null;
				switch (ext.ToLower())
				{
					case "png": enc = new PngBitmapEncoder(); break;
				}

				if (enc == null)
				{
					MessageBox.Show(Translate._("ImageDialog.Save.UnknownFormat"), null, MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}

				Stream strm = File.Create(dlg.FileName);
				enc.Frames.Add(BitmapFrame.Create((BitmapSource)Image.Source));
				try
				{
					enc.Save(strm);
				}
				catch (Exception exc)
				{
					MessageBox.Show(string.Format(Translate._("ImageDialog.Save.Failed"), exc.ToString()), null, MessageBoxButton.OK, MessageBoxImage.Error);
				}
				finally
				{
					strm.Close();
				}
			}
		}

		private void Close_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}
