using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

using Savegame.Properties;

namespace SatisfactorySavegameTool.Dialogs
{
	/// <summary>
	/// Interaction logic for ShowRawTextDialog.xaml
	/// </summary>
	public partial class ShowRawTextDialog : Window
	{
		public static void Show(string title, string content)
		{
			Show(null, title, content);
		}

		public static void Show(Window parent, string title, string content)
		{
			new ShowRawTextDialog(parent, title, content).ShowDialog();
		}


		private ShowRawTextDialog(Window parent, string title, string content)
			: base()
		{
			InitializeComponent();

			Icon = Application.Current.MainWindow.Icon;

			if (parent == null)
				parent = Application.Current.MainWindow;
			Owner = parent;

			if (!string.IsNullOrEmpty(title))
				Title = title;

			if (string.IsNullOrEmpty(content))
			{
				SaveBtn.IsEnabled = false;
				content = Translate._("DetailsPanel.Empty");
			}
			TextCtrl.Text = content;
		}

		private void Save_Click(object sender, RoutedEventArgs e)
		{
			SaveFileDialog dlg = new SaveFileDialog();
			dlg.Title = Translate._("ShowRawTextDialog.Save.Title");
			dlg.InitialDirectory = App.EXPORTPATH;
			dlg.DefaultExt = Translate._("ShowRawTextDialog.Save.DefaultExt");
			dlg.Filter = Translate._("ShowRawTextDialog.Save.Filter");
			if (dlg.ShowDialog().GetValueOrDefault(false) == true)
			{
				File.WriteAllText(dlg.FileName, TextCtrl.Text);
			}
		}

		private void Close_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		protected override void OnKeyUp(KeyEventArgs e)
		{
			base.OnKeyUp(e);
			if (e.Key == Key.Escape)
				Close();
		}

	}
}
