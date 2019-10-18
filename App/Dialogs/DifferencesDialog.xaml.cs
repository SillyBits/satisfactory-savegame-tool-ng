using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

using P = Savegame.Properties;

using SatisfactorySavegameTool.Actions.Compare;

using ICSharpCode.TreeView;


namespace SatisfactorySavegameTool.Dialogs
{
	/// <summary>
	/// Interaction logic for DifferencesDialog.xaml
	/// </summary>
	public partial class DifferencesDialog : Window
	{
		public static void Show(Difference.DifferenceModel diff)
		{
			var dlg = new DifferencesDialog(diff);
			dlg.ShowDialog();
		}


		public DifferencesDialog(Difference.DifferenceModel diff)
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
			_RecursExecuter(view.Root as Difference.DifferenceNode, (node) => { if (node.Left == null) node.IsHidden = flag; } );
		}

		private void Different_Click(object sender, RoutedEventArgs e)
		{
			bool flag = !DifferentBtn.IsChecked.GetValueOrDefault(false);
			_RecursExecuter(view.Root as Difference.DifferenceNode, (node) => { if (node.Left != null && node.Right != null) node.IsHidden = flag; } );
		}

		private void RightMissing_Click(object sender, RoutedEventArgs e)
		{
			bool flag = !RightMissingBtn.IsChecked.GetValueOrDefault(false);
			_RecursExecuter(view.Root as Difference.DifferenceNode, (node) => { if (node.Right == null) node.IsHidden = flag; } );
		}

		//TODO: Add those center buttons for migrating nodes into opposite save

		private void Close_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		
		protected void _RecursExecuter(Difference.DifferenceNode node, Action<Difference.DifferenceNode> action)
		{
			foreach(Difference.DifferenceNode child in node.Children)
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
	public class DifferenceModel
	{
		public DifferenceNode    Root          { get; set; }
		public Savegame.Savegame LeftSavegame  { get; private set; }
		public Savegame.Savegame RightSavegame { get; private set; }

		public DifferenceModel(Savegame.Savegame left, Savegame.Savegame right)
		{
			Root          = new DifferenceNode(@"\", "", "");
			LeftSavegame  = left;
			RightSavegame = right;

			Root.IsExpanded = true;
		}

		public DifferenceNode Add(DifferenceNode node)
		{
			Root.Add(node);
			return node;
		}
	}

	public class DifferenceNode : SharpTreeNode
	{
		public override object Text
		{
			get
			{
				string t = Title;
				if (t != null && Children.Count > 0)
					t += string.Format(" [{0}]", Children.Count);
				return t;
			}
		}

		public override object ToolTip { get { return Title; } }

		public override bool CanCopy(SharpTreeNode[] nodes) { return false; }
		public override bool CanPaste(IDataObject data) { return false; }
		public override bool CanDelete(SharpTreeNode[] nodes) { return false; }
		public override bool IsCheckable { get { return false; } }

		public string        Title { get; set; }
		public object        Left { get; private set; }
		public object        Right { get; private set; }
		public int           ChildCount { get { return (Children != null) ? Children.Count : 0; } }


		public DifferenceNode(string title, object left, object right)
			: base()
		{
			Title     = title;
			Left      = left;
			Right     = right;

			LazyLoading = false;
			IsHidden = false;
		}

		public DifferenceNode Add(string title, object left, object right)
		{
			return Add(new DifferenceNode(title, left, right));
		}

		public DifferenceNode Add(DifferenceNode node)
		{
			Children.Add(node);
			return node;
		}

		public override void ShowContextMenu(ContextMenuEventArgs e)
		{
			if (_context_menu == null)
			{
				Func<string,RoutedEventHandler,MenuItem> add = (lang_id,handler) =>
				{
					lang_id = "DifferencesDialog.Context." + lang_id;

					MenuItem item = new MenuItem() {
						Header = Translate._(lang_id),
					};

					lang_id += ".Tooltip";
					if (Translate.Has(lang_id))
						item.ToolTip = Translate._(lang_id);

					item.Click += handler;

					_context_menu.Items.Add(item);

					return item;
				};

				_context_menu = new ContextMenu();
				add("ExpandAll", Contextmenu_ExpandAll_Click);
				add("CollapseAll", Contextmenu_CollapseAll_Click);
			}

			if (Children.Count > 0)
			{
				_context_menu.PlacementRectangle = new Rect(e.CursorLeft, e.CursorTop, 0, 0);
				_context_menu.IsOpen = true;
			}
		}

		private void Contextmenu_ExpandAll_Click(object sender, RoutedEventArgs e)
		{
			IsExpanded = false;
			foreach (var child in Descendants())
			{
				if (child.Children.Count > 0)
					child.IsExpanded = true;
			}
			IsExpanded = true;
		}

		private void Contextmenu_CollapseAll_Click(object sender, RoutedEventArgs e)
		{
			foreach (var child in DescendantsAndSelf())
			{
				if (child.Children.Count > 0)
					child.IsExpanded = false;
			}
		}

		private ContextMenu _context_menu;
	}


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
