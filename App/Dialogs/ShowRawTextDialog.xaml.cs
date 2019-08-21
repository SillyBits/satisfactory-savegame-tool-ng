using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;


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

			if (Config.Root.HasSection("dialogs") && Config.Root.dialogs.HasSection("raw_text"))
			{
				dynamic section = Config.Root.dialogs.raw_text;
				Left   = section.pos_x;
				Top    = section.pos_y;
				Width  = section.size_x;
				Height = section.size_y;
			}
			else
			{
				Width  = 750;
				Height = 500;
				WindowStartupLocation = WindowStartupLocation.CenterOwner;
			}

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
			dlg.InitialDirectory = Settings.EXPORTPATH;
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

		protected override void OnClosing(CancelEventArgs e)
		{
			if (!Config.Root.HasSection("dialogs"))
				Config.Root.AddSection("dialogs");

			dynamic section;
			if (!Config.Root.dialogs.HasSection("raw_text"))
			{
				section = Config.Root.dialogs.AddSection("raw_text");
				section.AddItem("pos_x" , (int)Left);
				section.AddItem("pos_y" , (int)Top);
				section.AddItem("size_x", (int)Width);
				section.AddItem("size_y", (int)Height);
			}
			else
			{
				section = Config.Root.dialogs.raw_text;
				section.pos_x  = (int)Left;
				section.pos_y  = (int)Top;
				section.size_x = (int)Width;
				section.size_y = (int)Height;
			}

			base.OnClosing(e);
		}

		protected override void OnKeyUp(KeyEventArgs e)
		{
			base.OnKeyUp(e);
			if (e.Key == Key.Escape)
				Close();
		}

	}
}
