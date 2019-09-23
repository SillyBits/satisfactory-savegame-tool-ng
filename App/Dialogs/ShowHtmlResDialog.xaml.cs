using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

using CoreLib;


namespace SatisfactorySavegameTool.Dialogs
{
	/// <summary>
	/// Interaction logic for ShowHtmlResDialog.xaml
	/// </summary>
	public partial class ShowHtmlResDialog : Window
	{
		public static bool Show(string title, string content, string[] extra = null)
		{
			return Show(null, title, content, extra);
		}

		public static bool Show(Window parent, string title, string content, string[] extra = null)
		{
			var dlg = new ShowHtmlResDialog(parent, title, content, extra);
			return dlg.ShowDialog().GetValueOrDefault();
		}


		private ShowHtmlResDialog(Window parent, string title, string content, string[] extra = null)
			: base()
		{
			InitializeComponent();

			Icon = Application.Current.MainWindow.Icon;

			if (parent == null && Application.Current.MainWindow.IsLoaded)
				parent = Application.Current.MainWindow;
			if (parent != null)
				Owner = parent;

			if (Config.Root.HasSection("dialogs") && Config.Root.dialogs.HasSection("html_res"))
			{
				dynamic section = Config.Root.dialogs.html_res;
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

			if (extra != null && extra.Length >= 2)
			{
				ExtraBtn.Content = extra[0];
				ExtraBtn.Visibility = Visibility.Visible;
				CloseBtn.Content = extra[1];
				if (extra.Length == 3)
				{
					Hint.Content = extra[2];
					Hint.Visibility = Visibility.Visible;
				}
			}

			// Replace image tags with embedded base64
			Dictionary<string,string> images = new Dictionary<string, string>();
			int offset = 0;
			Match match = _regex.Match(content, offset);
			while (match.Success)
			{
				string imgfile = match.Groups["file"].Value;
				string base64;
				if (!images.ContainsKey(imgfile))
				{
					BitmapSource img = new BitmapImage(Helpers.GetResourceUri(imgfile));
					base64 = "data:image/png;base64," + ImageHandler.Image2Base64(img);
					images.Add(imgfile, base64);
				}
				else
				{
					base64 = images[imgfile];
				}

				string to_replace = match.Value;
				content = content.Replace(to_replace, base64);

				int match_pos = match.Index;
				offset = match_pos + base64.Length;

				match = _regex.Match(content, offset);
			}

			WebCtrl.NavigateToString(content);
		}

		private void Extra_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
			Close();
		}

		private void Close_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			if (!Config.Root.HasSection("dialogs"))
				Config.Root.AddSection("dialogs");

			dynamic section;
			if (!Config.Root.dialogs.HasSection("html_res"))
			{
				section = Config.Root.dialogs.AddSection("html_res");
				section.AddItem("pos_x" , (int)Left);
				section.AddItem("pos_y" , (int)Top);
				section.AddItem("size_x", (int)Width);
				section.AddItem("size_y", (int)Height);
			}
			else
			{
				section = Config.Root.dialogs.html_res;
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
			if (ExtraBtn.Visibility == Visibility.Collapsed && e.Key == Key.Escape)
				Close();
		}

		private static Regex _regex = new Regex(@"\[\[IMG\:(?<file>.*?)\]\]", 
			RegexOptions.Compiled|RegexOptions.CultureInvariant|RegexOptions.ExplicitCapture);
		
	}
}
