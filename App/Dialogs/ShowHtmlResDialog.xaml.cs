using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

using CoreLib;

namespace SatisfactorySavegameTool.Dialogs
{
	/// <summary>
	/// Interaction logic for ShowHtmlResDialog.xaml
	/// </summary>
	public partial class ShowHtmlResDialog : Window
	{
		public static void Show(string title, string content)
		{
			Show(null, title, content);
		}

		public static void Show(Window parent, string title, string content)
		{
			new ShowHtmlResDialog(parent, title, content).ShowDialog();
		}


		private ShowHtmlResDialog(Window parent, string title, string content)
			: base()
		{
			InitializeComponent();

			Icon = Application.Current.MainWindow.Icon;

			if (parent == null)
				parent = Application.Current.MainWindow;
			Owner = parent;

			if (!string.IsNullOrEmpty(title))
				Title = title;

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
					string imgpath = Path.Combine(App.RESOURCEPATH, imgfile);
					string ext = Path.GetExtension(imgfile).Substring(1).ToLower();
					base64 = "data:image/" + ext + ";base64," + ImageHandler.Image2Base64(imgpath);
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

		private static Regex _regex = new Regex(@"\[\[IMG\:(?<file>.*?)\]\]", 
			/*RegexOptions.Compiled|*/RegexOptions.CultureInvariant|RegexOptions.ExplicitCapture);
		
	}
}
