using System;
using System.IO;
using System.Windows;

using CoreLib;

namespace SatisfactorySavegameTool.Dialogs
{
	/// <summary>
	/// Interaction logic for AboutDialog.xaml
	/// </summary>
	public partial class AboutDialog : Window
	{
		public AboutDialog(Window parent, string title)
			: base()
		{
			Owner = parent;
			Title = title;

			InitializeComponent();

			string filename = Path.Combine(App.RESOURCEPATH, App.LANGUAGE, "About.res");
			string content = File.ReadAllText(filename);

			// Replace logo
			string logofile = Path.Combine(App.RESOURCEPATH, "Logo-128x128.png");
			string base64 = "data:image/png;base64," + ImageHandler.Image2Base64(logofile);

			content = content.Replace("[[LOGO]]", base64);

			Display.NavigateToString(content);
		}

		
		private void Close_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

	}
}
