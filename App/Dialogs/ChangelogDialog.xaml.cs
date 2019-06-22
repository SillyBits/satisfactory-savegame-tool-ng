using System;
using System.IO;
using System.Windows;

using CoreLib;

namespace SatisfactorySavegameTool.Dialogs
{
	/// <summary>
	/// Interaction logic for ChangelogDialog.xaml
	/// </summary>
	public partial class ChangelogDialog : Window
	{
		public ChangelogDialog(Window parent, string title)
			: base()
		{
			InitializeComponent();

			Icon = Application.Current.MainWindow.Icon;

			if (parent == null)
				parent = Application.Current.MainWindow;
			Owner = parent;

			if (!string.IsNullOrEmpty(title))
				Title = title;

			string filename = Path.Combine(App.RESOURCEPATH, App.LANGUAGE, "Changelog.res");
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
