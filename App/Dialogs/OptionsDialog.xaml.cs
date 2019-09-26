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

using CoreLib;


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
			languages.SelectedItem = langs.FirstOrDefault(lang => lang.ID == Settings.LANGUAGE);
			defaultpath.Text = Path.GetFullPath(Config.Root.core.defaultpath);
			exportpath.Text = Path.GetFullPath(Config.Root.core.exportpath.Length > 0 ? Config.Root.core.exportpath : Settings.EXPORTPATH);
			update_check.IsChecked = Config.Root.update_check.check_for_updates;
			deep_analysis.IsChecked = Config.Root.deep_analysis.enabled;

			trees.SelectedIndex = 0;
									
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

		private void trees_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			upButton.IsEnabled   = trees.SelectedIndex > 0;
			downButton.IsEnabled = trees.SelectedIndex < (trees.Items.Count - 1);
		}

		private void upButton_Click(object sender, RoutedEventArgs e)
		{
			int index = trees.SelectedIndex;
			Collections.TreeOptions options = trees.ItemsSource as Collections.TreeOptions;
			Collections.TreeOption selected = options[index];
			options.RemoveAt(index);
			options.Insert(index - 1, selected);
			trees.SelectedIndex = index - 1;
		}

		private void downButton_Click(object sender, RoutedEventArgs e)
		{
			int index = trees.SelectedIndex;
			Collections.TreeOptions options = trees.ItemsSource as Collections.TreeOptions;
			Collections.TreeOption selected = options[index];
			options.RemoveAt(index);
			options.Insert(index + 1, selected);
			trees.SelectedIndex = index + 1;
		}

		private void Save_Click(object sender, RoutedEventArgs e)
		{
			if (languages.SelectedItem != null)
				Config.Root.core.language = (languages.SelectedItem as Collections.Language).ID;
			if (defaultpath.Text.Length > 0)
				Config.Root.core.defaultpath = Path.GetFullPath(defaultpath.Text);
			if (exportpath.Text.Length > 0)
				Config.Root.core.exportpath = Path.GetFullPath(exportpath.Text);
			Config.Root.update_check.check_for_updates = update_check.IsChecked;
			Config.Root.deep_analysis.enabled = deep_analysis.IsChecked;

			int order = 0;
			foreach (Collections.TreeOption option in trees.ItemsSource as Collections.TreeOptions)
			{
				Config.Root.trees.order[order] = option.Name;
				Config.Root.trees.Items[option.Name].Value = option.Enabled;
				++order;
			}

			Config.Root.crash_reports.enabled = crash_reports.IsChecked;
			Config.Root.incident_reports.enabled = incident_reports.IsChecked;
			Config.Root.online_mapping.enabled = online_map.IsChecked;

			(Application.Current as App).SaveConfig();

			MessageBox.Show(Translate._("OptionsDialog.Saved"), Translate._("MainWindow.Title"));

			Close();
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


	public class TreeOption
	{
		public string       Name    { get; private set; }
		public string       Title   { get; private set; }
		public bool         Enabled { get; set; }
		public BitmapSource Image   { get; private set; }

		internal TreeOption(string name)
		{
			string capital = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);

			Name    = name;
			Title   = Translate._("TreePanel.Tab." + capital);
			Enabled = (bool) Config.Root.trees.Items[name].Value;
			Image   = new BitmapImage(Helpers.GetResourceUri("Icon.TreePanel." + capital + ".png"));
		}
	}

	public class TreeOptions : ObservableCollection<TreeOption>
	{
		public TreeOptions()
		{
			foreach(string tree in Config.Root.trees.order)
				Add(new TreeOption(tree));
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
