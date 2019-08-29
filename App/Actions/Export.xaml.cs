using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

using F = CoreLib.Filters;

using P = Savegame.Properties;

using SatisfactorySavegameTool.Supplements;


namespace SatisfactorySavegameTool.Actions
{

	public static class Export //: IAction <- Non-static only!
	{
		//public string       Name        { get; }
		//public string       Description { get; }
		//public BitmapSource Icon        { get; }

		// This represents a "match all" filter
		public static readonly List<ExportAction.FilterDefinition> EMPTY_FILTER = new List<ExportAction.FilterDefinition>();

		public static void Run(Savegame.Savegame savegame)
		{
			// Show dialog, gather data, then run method below
			ExportAction.Dialog dlg = new ExportAction.Dialog();
			if (dlg.ShowDialog() == true)
				Run(savegame, dlg.Filename, dlg.Filters, dlg.DeepTraversal, dlg.RecursExport);
		}

		public static void Run(Savegame.Savegame savegame, string filename, List<ExportAction.FilterDefinition> filters, 
			bool deep_traversal, bool recursive_export)
		{
			ExportAction.Impl.Run(savegame, filename, filters, deep_traversal, recursive_export);
		}

	}


	namespace ExportAction
	{

		public partial class Dialog : Window
		{
			public string                 Filename      { get; private set; }
			public List<FilterDefinition> Filters       { get; private set; }
			public bool                   DeepTraversal { get; private set; }
			public bool                   RecursExport  { get; private set; }

			public Dialog(Window parent = null, string title = null)
			{
				InitializeComponent();

				Icon = Application.Current.MainWindow.Icon;

				if (parent == null)
					parent = Application.Current.MainWindow;
				Owner = parent;

				if (!string.IsNullOrEmpty(title))
					Title = title;

				if (Config.Root.HasSection("dialogs") && Config.Root.dialogs.HasSection("export"))
				{
					dynamic section = Config.Root.dialogs.export;
					Left   = section.pos_x;
					Top    = section.pos_y;
					Width  = section.size_x;
					Height = section.size_y;
				}
				else
				{
					//Width  = 500;
					//Height = 400;
					//=> using SizeToContent="WidthAndHeight"
					WindowStartupLocation = WindowStartupLocation.CenterOwner;
				}

#if DEBUG
				filename.Text = @"E:\GitHub\satisfactory-savegame-tool-ng\App\exports\test.export";
				//(filters.ItemsSource as Filters).Add(
				//	new FilterDefinition(F.Operations.And, Sources.ClassName, F.Conditions.Equal, 
				//		"/Game/FactoryGame/Character/Player/Char_Player.Char_Player_C"));
				(filters.ItemsSource as Filters).Add(
					new FilterDefinition(F.Operations.And, Sources.ClassName, F.Conditions.StartsWith, 
						"/Game/FactoryGame/Character/"));
				(filters.ItemsSource as Filters).Add(
					new FilterDefinition(F.Operations.And, Sources.ClassName, F.Conditions.Contains, 
						"SpaceRabbit"));
				deep_traversal.IsChecked = true;
#endif

				filters_SelectionChanged(null, null);
			}

			private void Browse_Destination_Click(object sender, RoutedEventArgs e)
			{
				SaveFileDialog dlg = new SaveFileDialog();
				dlg.Title = Translate._("Action.Export.Dialog.Destination.Title");
				if (string.IsNullOrEmpty(Filename) || !Directory.Exists(Path.GetDirectoryName(Filename)))
				dlg.InitialDirectory = Settings.EXPORTPATH;
				dlg.DefaultExt = Translate._("Action.Export.Dialog.Destination.DefaultExt");
				dlg.Filter = Translate._("Action.Export.Dialog.Destination.Filter");
				if (dlg.ShowDialog() == true)
					filename.Text = dlg.FileName;
			}

			private void fieldButton_Click(object sender, RoutedEventArgs e)
			{
				//TODO: Browse thru known fields
				//Field textbox might be changed to combo box instead
			}

			private void filters_SelectionChanged(object sender, SelectionChangedEventArgs e)
			{
				bool has_sel = filters.SelectedItem != null;
				addButton.IsEnabled  = true;
				delButton.IsEnabled  = has_sel;
				upButton.IsEnabled   = has_sel && filters.SelectedIndex > 0;
				downButton.IsEnabled = has_sel && filters.SelectedIndex < (filters.Items.Count - 1);
			}

			private void addButton_Click(object sender, RoutedEventArgs e)
			{
				Filters f = filters.ItemsSource as Filters;
				FilterDefinition filter = new FilterDefinition();
				f.Add(filter);
				filters_SelectionChanged(null, null);
			}

			private void delButton_Click(object sender, RoutedEventArgs e)
			{
				int index = filters.SelectedIndex;
				Filters f = filters.ItemsSource as Filters;
				f.RemoveAt(index);
				filters_SelectionChanged(null, null);
			}

			private void upButton_Click(object sender, RoutedEventArgs e)
			{
				int index = filters.SelectedIndex;
				Filters f = filters.ItemsSource as Filters;
				FilterDefinition selected = f[index];
				f.RemoveAt(index);
				f.Insert(index - 1, selected);
				filters.SelectedIndex = index - 1;
				filters.InvalidateVisual();
			}

			private void downButton_Click(object sender, RoutedEventArgs e)
			{
				int index = filters.SelectedIndex;
				Filters f = filters.ItemsSource as Filters;
				FilterDefinition selected = f[index];
				f.RemoveAt(index);
				f.Insert(index + 1, selected);
				filters.SelectedIndex = index + 1;
			}

			private void Start_Click(object sender, RoutedEventArgs e)
			{
				Filename      = filename.Text;
				Filters       = (filters.ItemsSource as Filters).ToList();
				DeepTraversal = (deep_traversal.IsChecked == true);
				RecursExport  = true;//TODO: (recurs_export.IsChecked == true);

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
				if (!Config.Root.dialogs.HasSection("export"))
				{
					section = Config.Root.dialogs.AddSection("export");
					section.AddItem("pos_x" , (int)Left);
					section.AddItem("pos_y" , (int)Top);
					section.AddItem("size_x", (int)Width);
					section.AddItem("size_y", (int)Height);
				}
				else
				{
					section = Config.Root.dialogs.export;
					section.pos_x  = (int)Left;
					section.pos_y  = (int)Top;
					section.size_x = (int)Width;
					section.size_y = (int)Height;
				}

				base.OnClosing(e);
			}
		}


		public static class Impl
		{
			public static void Run(Savegame.Savegame savegame, string filename, List<FilterDefinition> filters, 
				bool deep_traversal, bool recursive_export)
			{
				Writer.IWriter writer = Writer.CreateWriter(Writer.Destinations.None, filename, recursive_export);
				if (writer == null)
				{
					string msg = string.Format(Translate._("Unable to create writer for file\n\n{0}"), filename);
					MessageBox.Show(msg, Translate._("MainWindow.Title"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
					return;
				}
				try
				{
					writer.Open();
				}
				catch (Exception exc)
				{
					string msg = string.Format(Translate._("Unable to create writer for file\n\n{0}"), filename) + "\n\n" + exc.ToLongString();
					MessageBox.Show(msg, Translate._("MainWindow.Title"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
					return;
				}

				var defs = new F.Definitions(filters.Cast<F.Definition>());
				F.FilterChain chain = F.CreateChain(defs);
				if (chain == null)
				{
					string msg = "Internal error!\n\nUnable to create filter chain.";
					MessageBox.Show(msg, Translate._("MainWindow.Title"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
					return;
				}

				FilterImpl.Filters source_filters = new FilterImpl.Filters(filters.Select(def => FilterImpl.CreateFilter(def)));

				HierarchyRunner.Runner action;
				if (chain.Count == 0)
					action = (prop) => {
						// Empty "Export all" filter was passed
						writer.Write(prop);
					};
				else
					action = (prop) => {
						// Test property against filter(s) given
						FilterImpl.Result result = source_filters.Test(prop, chain);
						// If successful, pass to writer
						if (result != null)
						{
							if (result.Single != null)
								writer.Write(result.Single);
							else if (result.Multi != null)
								result.Multi.ForEach(res => writer.Write(res));
						}
					};

				try
				{
					HierarchyRunner runner = new HierarchyRunner(savegame, deep_traversal);
					runner.Run(Translate._("Action.Export.Progress.Title"), action);

					string msg = string.Format(Translate._("Action.Export.Done"), filename);
					MessageBox.Show(msg, Translate._("MainWindow.Title"));//, MessageBoxButton.OK, MessageBoxImage.Information);
				}
				catch
				{
					string msg = "Internal error!\n\nFailure within hierarchy runner.";
					MessageBox.Show(msg, Translate._("MainWindow.Title"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
				}
				finally
				{
					writer.Close();
				}
			}

		}

		#region Writer implementation
		public static class Writer
		{
			public enum Destinations
			{
				None = 0, // -> Select by file extension
				RawText,
				CSV,
			//TODO:
			//	Json,
			//	XML,
			}


			public interface IWriter
			{
				void Open();
				void Write(object value);
				void Close();
			}

			public static IWriter CreateWriter(Destinations destination, string filename, bool recursive_export)
			{
				if (destination == Destinations.None)
				{
					string ext = Path.GetExtension(filename).ToLower();
					switch (ext)
					{
						case ".export": destination = Destinations.RawText; break;
						case ".csv"   : destination = Destinations.CSV; break;
					//TODO:
					//	case ".json"  : destination = Destinations.Json; break;
					//	case ".xml"   : destination = Destinations.XML; break;
					}
				}

				switch (destination)
				{
					case Destinations.RawText: return new TextDumper(filename, recursive_export);
					case Destinations.CSV    : return new CSVExporter(filename, recursive_export);
				//TODO:
				//	case Destinations.Json   : return new TextDumper(filename, recursive_export);
				//	case Destinations.XML    : return new TextDumper(filename, recursive_export);
				}

				return null;
			}

			private abstract class BaseWriter : IWriter
			{
				public BaseWriter(string filename, bool recursive_export)
				{
					_filename  = filename;
					_recursive = recursive_export;
				}

				~BaseWriter()
				{
					Close();
				}

				public virtual void Open()
				{
					_stream = File.CreateText(_filename);
				}

				public virtual void Write(object value)
				{
					if (_stream == null)
						throw new InvalidOperationException();
					_stream.Write(value);
				}

				public virtual void Close()
				{
					if (_stream != null)
						_stream.Dispose();
					_stream = null;
				}

				protected string       _filename;
				protected bool         _recursive;
				protected StreamWriter _stream;
			}

			private class TextDumper : BaseWriter
			{
				public TextDumper(string filename, bool recursive_export)
					: base(filename, recursive_export)
				{ }

				public override void Write(object value)
				{
					// For now, Properties.Dumper is recursive all the times,
					// might be changed in future
					if (value is P.Property)
						P.Dumper.Dump(value as P.Property, base.Write);
				}
			}

			private class CSVExporter : BaseWriter
			{
				public CSVExporter(string filename, bool recursive_export)
					: base(filename, recursive_export)
				{
					_first_line = true;
					_stringbuilder = new StringBuilder();
				}

				public override void Write(object value)
				{
					// For now, CSV export won't use recursive flag,
					// but recursion might be added in future
					if (value is P.Property)
					{
						var childs = (value as P.Property).GetChilds();

						if (_first_line)
						{
							_first_line = false;
							foreach (var pair in childs)
							{
								_stringbuilder.Append(pair.Key);
								_stringbuilder.Append(',');
							}
							if (_stringbuilder.Length > 0)
								_stringbuilder.Remove(_stringbuilder.Length - 1, 1);
							_stringbuilder.Append('\n');
							base.Write(_stringbuilder.ToString());
							_stringbuilder.Clear();
						}

						foreach (var pair in childs)
						{
							if (pair.Value != null)
								_stringbuilder.Append(pair.Value);
							_stringbuilder.Append(',');
						}
						if (_stringbuilder.Length > 0)
							_stringbuilder.Remove(_stringbuilder.Length - 1, 1);
						_stringbuilder.Append('\n');
						base.Write(_stringbuilder.ToString());
						_stringbuilder.Clear();
					}
					else
					{
						base.Write(value + "\n");
					}
				}

				private bool _first_line;
				private StringBuilder _stringbuilder;
			}

			//private JsonExporter...

			//private XmlExporter...

		}
		#endregion


		#region Filter implementation
		public static class FilterImpl
		{

			public class Result
			{
				public object       Single;
				public List<object> Multi;

				public Result()
				{ }

				public Result(object single)
				{
					Single = single;
				}

				public Result(IEnumerable<object> multi)
					: this(multi.ToList())
				{ }

				public Result(List<object> multi)
				{
					Multi = multi;
				}
			}

			public interface IFilter
			{
				Result Test(P.Property prop, F.FilterChain chain);
			}

			public class Filters : List<IFilter>, IFilter
			{
				public Filters(IEnumerable<IFilter> filters)
					: base(filters)
				{ }

				public Result Test(P.Property prop, F.FilterChain chain)
				{
					// Get all matches and compress results into a flat, distinct list

					// Do test first, eliminating empty results
					List<object> empty = new List<object>();
					var results = this
						.Select(filter => filter.Test(prop, chain))
						.Where(r => r != null)
						;
					// Any Property-related result?
					var filtered = results
						.SelectMany(r =>
						{
							if (r.Single != null && r.Single is P.Property)
								return new List<object> { r.Single };
							if (r.Multi != null)
								return r.Multi.Where(o => o is P.Property);
							return empty;
						})
						.Distinct()
						;
					//??? Add ourself to list ???
					int count = filtered.Count();
					if (count > 1)
						return new Result(filtered);
					if (count == 1)
						return new Result(filtered.First());
					// Any non-Property result? If so, we're the goal
					if (results.Count() > 0)
						return new Result(prop);
					return null;
					//bool outcome = this
					//	.Select(filter => filter.Test(prop, chain))
					//	.Any(r => r != null)
					//	;
					//if (outcome)
					//	return new Result(prop);
					//return null;
				}
			}

			public static IFilter CreateFilter(FilterDefinition def)
			{
				switch (def.Source)
				{
					case Sources.None      : return new None();
					case Sources.ClassName : return new ChildFilter("ClassName");
					case Sources.PathName  : return new ChildFilter("PathName");
					case Sources.LevelName : return new ChildFilter("LevelName");
					case Sources.FieldName : return new FieldName();
					case Sources.FieldValue: return new FieldValue();
				}
				return null;
			}

			private class None : IFilter
			{
				public Result Test(P.Property prop, F.FilterChain chain)
				{
					return new Result(prop);
				}
			}

			private class ChildFilter : IFilter
			{
				public ChildFilter(string name)
				{
					_name = name;
				}

				public Result Test(P.Property prop, F.FilterChain chain)
				{
					var childs = prop.GetChilds();
					if (childs.ContainsKey(_name))
						if (chain.Test(childs[_name].ToString()))
							return new Result(childs[_name]);
					return null;
				}

				private string _name;
			}

			private class FieldName : IFilter 
			{
				public Result Test(P.Property prop, F.FilterChain chain)
				{
					var result = prop
						.GetChilds()
						.Where(pair => chain.Test(pair.Key))
						;
					int count = result.Count();
					if (count > 1)
						return new Result(result);
					if (count == 1)
						return new Result(result.First());
					return null;
				}
			}

			private class FieldValue : IFilter 
			{
				public Result Test(P.Property prop, F.FilterChain chain)
				{
					//return prop.GetChilds().Any(pair => pair.Value != null ? chain.Test(pair.Value) : false);
					var result = prop
						.GetChilds()
						.Where(pair => {
							if (pair.Value != null)
							{
								if (pair.Value is string)
									return chain.Test(pair.Value);
								else
									return chain.Test(pair.Value.ToString());
							}
							return false;
						})
						;
					int count = result.Count();
					if (count > 1)
						return new Result(result);
					if (count == 1)
						return new Result(result.First());
					return null;
				}
			}

		}
		#endregion


		#region Filter operations
		public class FilterOperations : ObservableCollection<F.Operations>
		{
			public FilterOperations()
			{
				foreach (F.Operations op in Enum.GetValues(typeof(F.Operations)))
					if (op != 0)
						Add(op);
			}
		}

		[ValueConversion(typeof(F.Operations), typeof(string))]
		public class FilterOperationConverter : IValueConverter
		{
			public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			{
				F.Operations op = (F.Operations)value;
				return Translate._("Action.Export.Dialog.Operation." + op.ToString());
			}

			public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			{
				throw new NotImplementedException();
			}
		}
		#endregion


		#region Filter sources
		public enum Sources
		{
			None = 0,
			ClassName,
			PathName,
			LevelName,
			FieldName,
			FieldValue,
		}

		public class FilterSources : ObservableCollection<Sources>
		{
			public FilterSources()
			{
				foreach (Sources source in Enum.GetValues(typeof(Sources)))
					if (source != 0)
						Add(source);
			}
		}

		[ValueConversion(typeof(Sources), typeof(string))]
		public class FilterSourceConverter : IValueConverter
		{
			public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			{
				Sources source = (Sources)value;
				return Translate._("Action.Export.Dialog.Source." + source.ToString());
			}

			public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			{
				throw new NotImplementedException();
			}
		}
		#endregion


		#region Filter conditions
		public class FilterConditions : ObservableCollection<F.Conditions>
		{
			public FilterConditions()
			{
				foreach (F.Conditions cond in Enum.GetValues(typeof(F.Conditions)))
					if (cond != 0)
						Add(cond);
			}
		}

		[ValueConversion(typeof(F.Conditions), typeof(string))]
		public class FilterConditionConverter : IValueConverter
		{
			public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			{
				F.Conditions condition = (F.Conditions)value;
				return Translate._("Action.Export.Dialog.Condition." + condition.ToString());
			}

			public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			{
				throw new NotImplementedException();
			}
		}
		#endregion


		#region Filter definitions
		public class FilterDefinition : F.Definition
		{
			public Sources Source { get; set; }

			internal FilterDefinition()
				: base()
			{
				Source = Sources.None;
			}

			internal FilterDefinition(F.Operations operation, Sources source, F.Conditions condition, string value)
				: base(operation, condition, value)
			{
				Source = source;
			}
		}

		public class Filters : ObservableCollection<FilterDefinition>
		{
			public Filters()
				: base()
			{ }

			public Filters(List<FilterDefinition> filters)
				: base(filters)
			{ }
		}
		#endregion

	}
}

