using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

using P = Savegame.Properties;

using SatisfactorySavegameTool.Actions.Compare;


namespace SatisfactorySavegameTool.Dialogs
{
	/// <summary>
	/// Interaction logic for DifferencesDialog.xaml
	/// </summary>
	public partial class DifferencesDialog : Window
	{
		public static void Show(DifferenceModel diff)
		{
			var dlg = new DifferencesDialog(diff);
			dlg.ShowDialog();
		}


		public DifferencesDialog(DifferenceModel diff)
		{
			InitializeComponent();

			Icon = Application.Current.MainWindow.Icon;
			Owner = Application.Current.MainWindow;

			if (Config.Root.HasSection("dialogs") && Config.Root.dialogs.HasSection("diff"))
			{
				dynamic section = Config.Root.dialogs.diff;
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

			view.DataContext = diff;
			view.Root = diff.Root;

			var cols = ((view as ListView).View as GridView).Columns;
			cols[1].Header = Path.GetFileNameWithoutExtension(diff.LeftSavegame.Filename);
			cols[2].Header = Path.GetFileNameWithoutExtension(diff.RightSavegame.Filename);
		}


		private void LeftMissing_Click(object sender, RoutedEventArgs e)
		{
			bool flag = !LeftMissingBtn.IsChecked.GetValueOrDefault(false);
			_RecursExecuter(view.Root as DifferenceNode, (node) => { if (node.Left == null) node.IsHidden = flag; } );
		}

		private void Different_Click(object sender, RoutedEventArgs e)
		{
			bool flag = !DifferentBtn.IsChecked.GetValueOrDefault(false);
			_RecursExecuter(view.Root as DifferenceNode, (node) => { if (node.Left != null && node.Right != null) node.IsHidden = flag; } );
		}

		private void RightMissing_Click(object sender, RoutedEventArgs e)
		{
			bool flag = !RightMissingBtn.IsChecked.GetValueOrDefault(false);
			_RecursExecuter(view.Root as DifferenceNode, (node) => { if (node.Right == null) node.IsHidden = flag; } );
		}

		//TODO: Add those center buttons for migrating nodes into opposite save

		private void Close_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		
		protected void _RecursExecuter(DifferenceNode node, Action<DifferenceNode> action)
		{
			foreach(DifferenceNode child in node.Children)
				_RecursExecuter(child, action);
			action(node);
		}


		protected override void OnClosing(CancelEventArgs e)
		{
			if (!Config.Root.HasSection("dialogs"))
				Config.Root.AddSection("dialogs");

			dynamic section;
			if (!Config.Root.dialogs.HasSection("diff"))
			{
				section = Config.Root.dialogs.AddSection("diff");
				section.AddItem("pos_x" , (int)Left);
				section.AddItem("pos_y" , (int)Top);
				section.AddItem("size_x", (int)Width);
				section.AddItem("size_y", (int)Height);
			}
			else
			{
				section = Config.Root.dialogs.diff;
				section.pos_x  = (int)Left;
				section.pos_y  = (int)Top;
				section.size_x = (int)Width;
				section.size_y = (int)Height;
			}

			base.OnClosing(e);
		}

	}
}

namespace SatisfactorySavegameTool.Dialogs.Difference
{

	// Converter for node values
	public class DiffNodeValueConverter : IValueConverter
	{
		private static string _empty = null;

		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (_empty == null)
				_empty = Translate._("DifferencesDialog.Empty");

			if (value == null)
				return _empty;
			if (value is P.Property)
				return string.Format("[{0}]", (value as P.Property).TypeName);
			return value.ToString();
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new Exception("Not allowed");
		}
	}

	// Converter for node colors
	public class DiffNodeColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return (value == null) ? Brushes.DarkRed : SystemColors.WindowTextBrush;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new Exception("Not allowed");
		}
	}

}
