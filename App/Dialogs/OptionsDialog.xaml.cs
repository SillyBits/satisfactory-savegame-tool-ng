using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

using F = System.Windows.Forms;


namespace SatisfactorySavegameTool.Dialogs
{
	/// <summary>
	/// Interaction logic for OptionsDialog.xaml
	/// </summary>
	public partial class OptionsDialog : Window
	{
		public OptionsDialog(Window parent = null, string title = null)
			: base()
		{
			InitializeComponent();

			Icon = Application.Current.MainWindow.Icon;

			if (parent == null)
				parent = Application.Current.MainWindow;
			Owner = parent;

			if (!string.IsNullOrEmpty(title))
				Title = title;

			Collections.Languages langs = FindResource("langData") as Collections.Languages;
			languages.SelectedItem = langs.FirstOrDefault(lang => lang.ID == App.LANGUAGE);
			defaultpath.Text = Path.GetFullPath(Config.Root.core.defaultpath);
			exportpath.Text = Path.GetFullPath(Config.Root.core.exportpath.Length > 0 ? Config.Root.core.exportpath : App.EXPORTPATH);
			deep_analysis.IsChecked = Config.Root.deep_analysis.enabled;
			crash_reports.IsChecked = Config.Root.crash_reports.enabled;
			incident_reports.IsChecked = Config.Root.incident_reports.enabled;
			online_map.IsEnabled = Config.Root.online_mapping.enabled;
		}

		private void Browse_DefaultPath_Click(object sender, RoutedEventArgs e)
		{
			using (var dlg = new F.FolderBrowserDialog())
			{
				dlg.SelectedPath = defaultpath.Text;
				dlg.Description = Translate._("OptionsDialog.DefaultPath.Browse");
				dlg.ShowNewFolderButton = false;
				if (dlg.ShowDialog() == F.DialogResult.OK)
					defaultpath.Text = dlg.SelectedPath;
			}
		}

		private void Browse_ExportPath_Click(object sender, RoutedEventArgs e)
		{
			using (var dlg = new F.FolderBrowserDialog())
			{
				dlg.SelectedPath = exportpath.Text;
				dlg.Description = Translate._("OptionsDialog.ExportPath.Browse");
				dlg.ShowNewFolderButton = false;
				if (dlg.ShowDialog() == F.DialogResult.OK)
					exportpath.Text = dlg.SelectedPath;
			}
		}

		private void Save_Click(object sender, RoutedEventArgs e)
		{
			if (languages.SelectedItem != null)
				Config.Root.core.language = (languages.SelectedItem as Collections.Language).ID;
			if (defaultpath.Text.Length > 0)
				Config.Root.core.defaultpath = Path.GetFullPath(defaultpath.Text);
			if (exportpath.Text.Length > 0)
				Config.Root.core.exportpath = Path.GetFullPath(exportpath.Text);
			Config.Root.deep_analysis.enabled = deep_analysis.IsEnabled;
			Config.Root.crash_reports.enabled = crash_reports.IsEnabled;
			Config.Root.incident_reports.enabled = incident_reports.IsEnabled;
			Config.Root.online_mapping.enabled = online_map.IsEnabled;

			// Trigger updating statics in App
			(Application.Current as App).SaveConfig();

			MessageBox.Show(Translate._("OptionsDialog.Saved"));
		}

		private void Abort_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

	}

}

namespace SatisfactorySavegameTool.Dialogs.Collections
{

	public class Language
	{
		public Language(string title, string id)
		{
			Title = title;
			ID = id;
		}

		public string Title;
		public string ID;
	}

	public class Languages : ObservableCollection<Language>
	{
		public Languages()
		{
			Add(new Language(Translate._("OptionsDialog.Language.English"), "en-US" ));
			Add(new Language(Translate._("OptionsDialog.Language.German"), "de-DE" ));
		}
	}

}

namespace SatisfactorySavegameTool.Dialogs.Converters
{

	[ValueConversion(typeof(Collections.Language), typeof(string))]
	public class LanguageConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			Collections.Language lang = value as Collections.Language;
			return lang.Title;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

}
