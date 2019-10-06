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

	// Export action is an exception with not being derived from IAction
	public static class Export
	{
		// This represents a "match all" filter
		public static readonly List<ExportAction.FilterDefinition> EMPTY_FILTER = new List<ExportAction.FilterDefinition>();

		public static void Run(Savegame.Savegame savegame)
		{
			// Show dialog, gather data, then run method below
			ExportAction.Dialog dlg = new ExportAction.Dialog();
			if (dlg.ShowDialog() == true)
				Run(savegame, dlg.Filename, dlg.DestinationType, dlg.Filters, dlg.DeepTraversal, dlg.RecursExport);
		}

		public static void Run(Savegame.Savegame savegame, string filename, ExportAction.Writer.Destinations destination_type,
			List<ExportAction.FilterDefinition> filters, bool deep_traversal, bool recursive_export)
		{
			ExportAction.Impl.Run(savegame, filename, destination_type, filters, deep_traversal, recursive_export);
		}

	}


	namespace ExportAction
	{

		public partial class Dialog : Window
		{
			public string                 Filename        { get; private set; }
			public Writer.Destinations    DestinationType { get; set; }
			public List<FilterDefinition> Filters         { get; private set; }
			public bool                   DeepTraversal   { get; private set; }
			public bool                   RecursExport    { get; private set; }

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

				// Setup defaults, if any
				destinationtype.SelectedIndex = 0;

				filename_TextChanged(null, null);
				filters_SelectionChanged(null, null);
			}

			private void filename_TextChanged(object sender, TextChangedEventArgs e)
			{
				if (destinationtype.SelectedIndex == 0)
				{
					startBtn.IsEnabled = (Writer.GetDestByExt(filename.Text) != Writer.Destinations.Auto);
				}
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

			private void destinationtype_SelectionChanged(object sender, SelectionChangedEventArgs e)
			{
				if (destinationtype.SelectedIndex == 0)
					filename_TextChanged(null, null);
				else
					startBtn.IsEnabled = true;
			}

			private void filters_SelectionChanged(object sender, SelectionChangedEventArgs e)
			{
				bool has_sel = filters.SelectedItem != null;
				addButton.IsEnabled  = true;
				delButton.IsEnabled  = has_sel;
				upButton.IsEnabled   = has_sel && filters.SelectedIndex > 0;
				downButton.IsEnabled = has_sel && filters.SelectedIndex < (filters.Items.Count - 1);
				// Deep option only avail with filters
				deep_traversal.IsEnabled = (filters.Items.Count > 0);
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
				Filename        = filename.Text;
				DestinationType = (Writer.Destinations)destinationtype.SelectedItem;
				Filters         = (filters.ItemsSource as Filters).ToList();
				DeepTraversal   = (deep_traversal.IsChecked == true);
				RecursExport    = true;//TODO: (recurs_export.IsChecked == true);

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
			public static void Run(Savegame.Savegame savegame, string filename, ExportAction.Writer.Destinations destination_type,
				List<FilterDefinition> filters, bool deep_traversal, bool recursive_export)
			{
				Writer.IWriter writer = Writer.CreateWriter(destination_type, filename, recursive_export);
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
				F.CreatorCallback creator = (def) => FilterImpl.CreateFilter(def as FilterDefinition);
				F.FilterChain chain = F.CreateChain(defs, creator);
				if (chain == null)
				{
					string msg = "Internal error!\n\nUnable to create filter chain.";
					MessageBox.Show(msg, Translate._("MainWindow.Title"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
					return;
				}

				HierarchyRunner.Runner action;
				if (chain.Count == 0)
					action = (prop) => {
						// Empty "Export all" filter was passed
						writer.Write(prop);
					};
					// Also remove deep flag
					deep_traversal = false;
				else
					action = (prop) => {
						// Test property against filter(s) given
						F.IResult result = chain.Test(prop);
						// If successful, pass to writer
						if (result.Success)
							writer.Write(prop);
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
				Auto = 0, // -> Select by file extension
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

			public static Destinations GetDestByExt(string filename)
			{
				try
				{
					string ext = Path.GetExtension(filename).ToLower();
					switch (ext)
					{
						case ".export": return Destinations.RawText;
						case ".csv"   : return Destinations.CSV;
					//TODO:
					//	case ".json"  : return Destinations.Json;
					//	case ".xml"   : return Destinations.XML;
					}
				}
				catch
				{
				}
				return Destinations.Auto;
			}

			public static IWriter CreateWriter(Destinations destination, string filename, bool recursive_export)
			{
				if (destination == Destinations.Auto)
					destination = GetDestByExt(filename);

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
					if (value is string)
						_stream.Write((value as string).Replace("\n", "\r\n"));
					else
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
				public CSVExporter(string filename, bool recursive_export, char separator = ';')
					: base(filename, recursive_export)
				{
					_separator = separator;
					_first_line = true;
					_stringbuilder = new StringBuilder();
				}

				public override void Write(object value)
				{
					if (_first_line)
					{
						_first_line = false;
						_WriteHeader(value);
						if (_stringbuilder.Length > 0)
							_stringbuilder.Replace(_separator, '\n', _stringbuilder.Length - 1, 1);
						base.Write(_stringbuilder.ToString());
						_stringbuilder.Clear();
					}

					_Write(value);
					if (_stringbuilder.Length > 0)
						_stringbuilder.Replace(_separator, '\n', _stringbuilder.Length - 1, 1);
					base.Write(_stringbuilder.ToString());
					_stringbuilder.Clear();
				}
				
				private void _WriteHeader(object value)
				{
					if (value is P.Property)
					{
						var childs = (value as P.Property).GetChilds();

						foreach (var pair in childs)
						{
							_stringbuilder.Append(pair.Key);
							_stringbuilder.Append(_separator);

							if (_recursive && pair.Value is P.Property)
								_WriteHeader(pair.Value);
						}
					}
					else
					{
						_stringbuilder.Append("Value");
						_stringbuilder.Append(_separator);
					}
				}
				
				private void _Write(object value)
				{
					if (value is P.Property)
					{
						var childs = (value as P.Property).GetChilds();

						foreach (var pair in childs)
						{
							if (pair.Value != null)
								_stringbuilder.Append(pair.Value);
							_stringbuilder.Append(_separator);

							if (_recursive && pair.Value is P.Property)
								_Write(pair.Value);
						}
					}
					else
					{
						_stringbuilder.Append(value);
						_stringbuilder.Append(_separator);
					}
				}

				private char          _separator;
				private bool          _first_line;
				private StringBuilder _stringbuilder;
			}

			//private JsonExporter...

			//private XmlExporter...

		}
		#endregion


		#region Filter implementation
		public static class FilterImpl
		{

			public static F.IFilter CreateFilter(FilterDefinition def)
			{
				F.IFilter baseFilter = F.CreateFilterOp(def.Condition, def.Value);

				switch (def.Source)
				{
					case Sources.None      : return new None(baseFilter);
					case Sources.ClassName : return new ChildFilter(baseFilter, "ClassName");
					case Sources.PathName  : return new ChildFilter(baseFilter, "PathName");
					case Sources.LevelName : return new ChildFilter(baseFilter, "LevelName");
					case Sources.FieldName : return new FieldName(baseFilter);
					case Sources.FieldValue: return new FieldValue(baseFilter);
				}
				return null;
			}

			private abstract class FilterBase : F.IFilter
			{
				protected FilterBase(F.IFilter filter)
				{
					_filter = filter;
				}

				public abstract F.IResult Test(object value);

				protected F.IFilter _filter;
			}

			// A match-all filter
			private class None : FilterBase
			{
				public None(F.IFilter filter)
					: base(filter)
				{ }

				public override F.IResult Test(object value)
				{
					return new F.Result(value);
				}
			}

			private class ChildFilter : FilterBase
			{
				public ChildFilter(F.IFilter filter, string name)
					: base(filter)
				{
					_name = name;
				}

				public override F.IResult Test(object value)
				{
					if (value is P.Property)
					{
						P.Property prop = value as P.Property;
						var childs = prop.GetChilds();
						if (childs.ContainsKey(_name))
						{
							F.IResult result = _filter.Test(childs[_name].ToString());
							if (result.Success)
								return new F.Result(childs[_name]);
						}
					}
					return F.EMPTY_RESULT;
				}

				private string _name;
			}

			private class FieldName : FilterBase
			{
				public FieldName(F.IFilter filter)
					: base(filter)
				{ }

				public override F.IResult Test(object value)
				{
					if (value is P.Property)
					{
						P.Property prop = value as P.Property;
						var result = prop
							.GetChilds()
							.Select(pair => _filter.Test(pair.Key))
							.Where(r => r.Success)
							;
						if (result.Count() >= 1)
							return new F.Result(result);
					}
					return F.EMPTY_RESULT;
				}
			}

			private class FieldValue : FilterBase
			{
				public FieldValue(F.IFilter filter)
					: base(filter)
				{ }

				public override F.IResult Test(object value)
				{
					if (value is P.Property)
					{
						P.Property prop = value as P.Property;
						var result = prop
							.GetChilds()
							.Select(pair => {
								if (pair.Value != null)
								{
									if (pair.Value is string)
										return _filter.Test(pair.Value);
									return _filter.Test(pair.Value.ToString());
								}
								return F.EMPTY_RESULT;
							})
							.Where(r => r.Success)
							;
						if (result.Count() >= 1)
							return new F.Result(result);
					}
					return F.EMPTY_RESULT;
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


		#region Destination types
		public class DestinationTypes : ObservableCollection<Writer.Destinations>
		{
			public DestinationTypes()
			{
				foreach (Writer.Destinations dest in Enum.GetValues(typeof(Writer.Destinations)))
					Add(dest);
			}
		}

		[ValueConversion(typeof(Writer.Destinations), typeof(string))]
		public class DestinationTypeConverter : IValueConverter
		{
			public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			{
				if (value is string)
					return Writer.Destinations.Auto;
					//return Translate._("Action.Export.Dialog.Destination.Auto"); 
				Writer.Destinations dest = (Writer.Destinations)value;
				return Translate._("Action.Export.Dialog.Destination." + dest.ToString());
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

