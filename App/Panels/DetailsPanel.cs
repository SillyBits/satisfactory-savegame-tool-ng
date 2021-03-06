﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using M = System.Windows.Media;

using CoreLib;

using FileHandler;

using Savegame.Properties;
using P = Savegame.Properties;

using SatisfactorySavegameTool.Dialogs;
using SatisfactorySavegameTool.Supplements;


/*
 * TODO:
 * 
 * - Changed properties should show a bold label, so we've to store some "IsModified" flag with each IElement.
 * - Rework Checkbox control to allow for different base types (bool,byte,int).
 * 
 */

namespace SatisfactorySavegameTool.Panels
{
	// Our visual panel
	//
	public class DetailsPanel : StackPanel
	{
		internal static readonly string EMPTY = Translate._("DetailsPanel.Empty");

		public DetailsPanel()
			: base()
		{ }


		public delegate void ModifiedHandler(Property prop);
		public event ModifiedHandler Modified;


		internal void ShowProperty(Property prop)
		{
			// Prevent re-displaying as tree might have fired OnSelChange while scrolling
			if (_curr_vis == prop)
				return;

			_ClearAll();

			Log.Debug("Visualizing property '{0}'", prop != null ? prop.ToString() : EMPTY);
			Details.IElement element = Details.ElementFactory.Create(null, null, prop);
			if (element == null)
			{
				Details.Expando expando = new Details.Expando(null, EMPTY, null);
				expando.IsEnabled = false;
				element = expando;
			}
			(element as INotifyPropertyChanged).PropertyChanged += Panel_PropertyChanged;
			Children.Add(element.Visual);

			_curr_vis = prop;
		}

		internal void ShowLiving(LivingTree.Living living)
		{
			// Prevent re-displaying as tree might have fired OnSelChange while scrolling
			if (_curr_vis == living)
				return;

			_ClearAll();

			Log.Debug("Visualizing living entity '{0}'", living != null ? living.Title : EMPTY);
			Details.IElement element = new Details.LivingEntity(living);
			(element as INotifyPropertyChanged).PropertyChanged += Panel_PropertyChanged;
			Children.Add(element.Visual);

			_curr_vis = living;
		}

		internal void ShowBuilding(BuildingsTree.Building building)
		{
			// Prevent re-displaying as tree might have fired OnSelChange while scrolling
			if (_curr_vis == building)
				return;

			_ClearAll();

			Log.Debug("Visualizing building '{0}'", building != null ? building.Title : EMPTY);
			Details.IElement element = new Details.Building(building);
			(element as INotifyPropertyChanged).PropertyChanged += Panel_PropertyChanged;
			Children.Add(element.Visual);

			_curr_vis = building;
		}


		internal void _ClearAll()
		{
			_curr_vis = null;

			foreach (var child in Children)
				(child as INotifyPropertyChanged).PropertyChanged -= Panel_PropertyChanged;
			Children.Clear();
		}


		private void Panel_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			Modified?.Invoke(sender as Property);
		}

		private object _curr_vis;
	}

}


namespace SatisfactorySavegameTool.Panels.Details
{
	
	// Combines all known factories into one type-based Create method
	// to reduce tedious copy&paste with creating our elements
	//
	internal static class MainFactory
	{
		internal static IElement Create(IElement parent, string label, object obj, bool read_only = false)
		{
			string s = (obj != null) ? string.Format("{0}:'{1}'", obj.GetType().Name, obj) : DetailsPanel.EMPTY;
			Log.Debug("- Creating element for label:'{0}', obj={1}", label, s);
			IElement element = null;

			if (label != null)							element = CreateNamed(parent, label, obj, read_only);
			if (element == null && obj is P.Property)	element = ElementFactory.Create(parent, label, obj, read_only);
			if (element == null && obj is IDictionary)	element = new DictControl(parent, label, obj);
			if (element == null && obj is ICollection)	element = new ListControl(parent, label, obj);
			if (element == null)						element = ValueControlFactory.Create(parent, label, obj, read_only);

			Log.Debug("=> created {0}", element);
			return element;
		}


		internal static IElement CreateNamed(IElement parent, string label, object val, bool read_only = false)
		{
			return _named.ContainsKey(label) ? _named[label](parent, label, val) : null;
		}

		internal delegate IElement CreatorFunc(IElement parent, string label, object obj);

		internal static Dictionary<string, CreatorFunc> _named = new Dictionary<string, CreatorFunc>
		{
			{ "ClassName",			(p,l,o) => new ReadonlySimpleValueControl<str>(p, l, (str)o) },
			{ "LevelName",			(p,l,o) => new ReadonlySimpleValueControl<str>(p, l, (str)o) },
			{ "PathName",			(p,l,o) => new ReadonlySimpleValueControl<str>(p, l, (str)o) },
			{ "Length",				(p,l,o) => new ReadonlySimpleValueControl<int>(p, l, (int)o) },
			{ "Missing",			(p,l,o) => new HexdumpControl(p, l, o as byte[]) },
			{ "Unknown",			(p,l,o) => new HexdumpControl(p, l, o as byte[]) },
			{ "WasPlacedInLevel",	(p,l,o) => ValueControlFactory.Create(p, l, (int) o == 1) },// Force boolean display
			{ "NeedTransform",		(p,l,o) => ValueControlFactory.Create(p, l, (int) o == 1) },// Force boolean display
			{ "IsValid",			(p,l,o) => ValueControlFactory.Create(p, l, (byte)o == 1) },// Force boolean display
			//more to come
		};
	}


	// Basic visual element (with or without a label)
	//

	public interface IElement : INotifyPropertyChanged
	{
		// Constructor required: IElement parent, string label, object obj

		FrameworkElement Visual { get; }

		bool HasLabel { get; }
		string Label { get; set; }
		VerticalAlignment LabelVerticalAlign { get; }

		bool HasValue { get; }
		//object Value { get; set; }
		//public event PropertyChangedEventHandler PropertyChanged;
	}

	// More specific, typed element
	//
	public interface IElement<_ValueType> : IElement
	{
		// Constructor required: IElement parent, string label, object obj

		//FrameworkElement Visual { get; }

		//bool HasLabel { get; }
		//string Label { get; set; }
		//VerticalAlignment LabelVerticalAlign { get; }

		//bool HasValue { get; }
		_ValueType Value { get; set; }
		//public event PropertyChangedEventHandler PropertyChanged;
	}


	// A container which can "store" multiple visual elements
	// (could be an Expando or other means like ListView or DataView)
	//
	internal interface IElementContainer : IElement
	{
		// Constructor required: IElement parent, string label, object obj

		//FrameworkElement Visual { get; }

		//bool HasLabel { get; }
		//string Label { get; set; }
		//VerticalAlignment LabelVerticalAlign { get; }

		//bool HasValue { get; }
		//_ValueType Value { get; set; }
		//public event PropertyChangedEventHandler PropertyChanged;

		// Needed for traversing later with saving?
		int Count { get; }
		List<IElement> Childs { get; }

		void Add(IElement element);
	}


	// Our element factory, a dynamic one specialized on 
	// discovering and creating IElement-related instances
	//
	internal class ElementFactory : BaseFactory<IElement>
	{
		internal static IElement Create(IElement parent, string label, object obj, bool read_only = false)
		{
			IElement element = null;
			if (obj != null)
			{
				P.Property prop = obj as P.Property;
				if (prop.GetKeys().Contains("ClassName"))
				{
					string type_name = (prop.GetChilds()["ClassName"] as str).LastName();
					if (read_only)
					{
						if (INSTANCE.IsKnown("Readonly" + type_name))
							element = INSTANCE["Readonly" + type_name, parent, label, obj];
					}
					if (element == null && INSTANCE.IsKnown(type_name))
						element = INSTANCE[type_name, parent, label, obj];
				}
				if (element == null && read_only)
				{
					// Try to get a readonly visualizer
					if (INSTANCE.IsKnown("Readonly" + prop.TypeName))
						element = INSTANCE["Readonly" + prop.TypeName, parent, label, obj];
				}
				if (element == null && INSTANCE.IsKnown(prop.TypeName))
					element = INSTANCE[prop.TypeName, parent, label, obj];
			}
			return element;
		}

		internal static ElementFactory INSTANCE = new ElementFactory();

		protected override void _CreateLookup()
		{
			// Discover actual assembly, might change to an external
			// when visualizers do get their own assembly, if at all
			Assembly assembly = Assembly.GetExecutingAssembly();

			// Setup signature of constructor were looking for
			// (see interface comment)
			Type[] cons = { typeof(IElement), typeof(string), typeof(object) };

			// Creating actual lookup
			_CreateLookup(assembly, cons);
		}
	}


	// The most basic element container avail is an expander
	//
	internal class Expando : Expander, IElementContainer
	{
		public Expando(IElement parent, string label, object obj)
		{
			_parent = parent;
			_label = label;
			Tag = obj;
			_childs = new List<IElement>();
		}

		// IElement
		public FrameworkElement Visual
		{
			get
			{
				if (_grid == null)
					_CreateVisual();
				return this;
			}
		}

		public bool HasLabel { get { return false; } }
		public string Label { get { return null; } set { } }
		public VerticalAlignment LabelVerticalAlign { get { return VerticalAlignment.Center; } }

		public bool HasValue { get { return false; } }
		public object Value { get { return null; } set { } }
		public event PropertyChangedEventHandler PropertyChanged;

		// IElementContainer
		public int Count { get { return _grid.RowDefinitions.Count; } }
		public List<IElement> Childs { get { return _childs; } }

		public virtual void Add(IElement element)
		{
			if (element == null)
				throw new ArgumentNullException();

			(element as INotifyPropertyChanged).PropertyChanged += _PropertyChanged;

			RowDefinition rowdef = new RowDefinition() {
				Height = new GridLength(0, GridUnitType.Auto),
			};
			_grid.RowDefinitions.Add(rowdef);
			int row = _grid.RowDefinitions.Count - 1;

			FrameworkElement value = element.Visual as FrameworkElement;
			if (value == null)
				throw new Exception("Detected element with empty visual!");
			value.Margin = new Thickness(0,2,0,2);//LTRB
			Grid.SetRow(value, row);

			if (element.HasLabel)
			{
				Control lbl = new Label() {
					Content = element.Label + ":",
					HorizontalAlignment = HorizontalAlignment.Left,
					VerticalAlignment = VerticalAlignment.Stretch,
					VerticalContentAlignment = element.LabelVerticalAlign,
				};
				Thickness t = lbl.Padding;
				t.Top = t.Bottom = 0;
				lbl.Padding = t;
				Grid.SetRow(lbl, row);
				Grid.SetColumn(lbl, 0);
				_grid.Children.Add(lbl);

				Grid.SetColumn(value, 1);
			}
			else
			{
				Grid.SetColumnSpan(value, 2);
				Grid.SetColumn(value, 0);
			}
			_grid.Children.Add(value);
		}

		// Override this method if expando needs to be modified
		internal virtual void _CreateVisual()
		{
			if (Tag is Property)
				Header = (Tag as Property).ToString();
			else
				Header = _label;

			HorizontalContentAlignment = HorizontalAlignment.Stretch;

			_grid = new Grid();
			_grid.ColumnDefinitions.Add(new ColumnDefinition() {
				Width = new GridLength( 0, GridUnitType.Auto ),
				SharedSizeGroup = "FirstColGroup",
			});
			_grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength( 1, GridUnitType.Star ) });
			_grid.Margin = new Thickness(10, 4, 5, 4);//LTRB
			_grid.Background = M.Brushes.Transparent;

			if (_parent != null)
				Grid.SetIsSharedSizeScope(_parent.Visual, true);

			Border b = new Border() {
				BorderBrush = M.Brushes.DarkGray,
				BorderThickness = new Thickness(1, 0, 0, 1),//LTRB
				Margin = new Thickness(10, 0, 0, 0),//LTRB
			};
			b.Child = _grid;
			Content = b;

			_CreateChilds();
			if (_childs.Count == 0)
			{
				// No childs -> disabled. Also skip expanding
				IsEnabled = false;
			}
			else
			{
				foreach (IElement element in _childs)
				{
					Add(element);
				}

				if (_parent == null)
				{
					// We're first level object
					IsExpanded = true;
				}
			}
		}

		// Override this method if childs are to be created different
		internal virtual void _CreateChilds()
		{
			if (Tag is Property)
			{
				Property prop = Tag as Property;

				Dictionary<string,object> childs = prop.GetChilds();
				if (childs == null)
					throw new Exception("Childs collection returned a null pointer!");

				_CreateChilds(childs);
			}
		}

		internal virtual void _CreateChilds(Dictionary<string,object> childs)
		{
			if (childs == null)
				throw new Exception("_CreateChilds was passed a null ponter!");

			#region Sort children
			var names = childs.Keys.OrderBy((s) => s);
			List<string> simple = new List<string>();
			List<string> simple2 = new List<string>();
			List<string> props = new List<string>();
			List<string> sets = new List<string>();
			List<string> last = new List<string>();
			foreach (string name in names)
			{
				object sub = childs[name];
				if (sub is ICollection)
				{
					if (name == "Missing")
						last.Add(name);
					else if (name == "Unknown")
						last.Add(name);
					else
						sets.Add(name);
				}
				else if (sub is Property)
				{
					if (sub is Entity)
						sets.Add(name);
					else if (sub is ValueProperty)						
						simple2.Add(name);
					else
						props.Add(name);
				}
				else
					simple.Add(name);
			}
			List<string> order = new List<string>();
			order.AddRange(simple);
			order.AddRange(simple2);
			order.AddRange(props);
			order.AddRange(sets);
			order.AddRange(last);
			#endregion

			foreach (string key in order)
			{
				object obj = childs[key];
				IElement element = MainFactory.Create(this, key, obj);
				_childs.Add(element);
			}
		}

		protected virtual void _PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			PropertyChanged?.Invoke(sender is Property ? sender : Tag, e);
		}

		internal IElement _parent;
		internal string _label;
		internal VerticalAlignment _label_valign = VerticalAlignment.Center;
		internal Grid _grid;
		internal List<IElement> _childs;
	}


	// Basic controls avail
	// 
	// Those will not only take care of displaying value correctly,
	// but will also take care of validating user input.
	// All controls MUST follow getter/setter pattern by supplying
	// both a Set() and Get() method.
	// 
	//TODO: Add validators to keep values in feasible limit

	internal static class ControlFactory
	{
		internal static FrameworkElement Create(object val)
		{
			if (val is bool)	return new BoolControl  ((bool)  val);
			if (val is byte)	return new ByteControl  ((byte)  val);
			if (val is int)		return new IntControl   ((int)   val);
			if (val is long)	return new LongControl  ((long)  val);
			if (val is float)	return new FloatControl ((float) val);
			if (val is str)		return new StrControl   ((str)   val);
			// Last resort: Simple .ToString
			return new StringControl(val != null ? val.ToString() : DetailsPanel.EMPTY);
		}
	}

	// Every control must implement this getter/setter pattern
	internal interface IValueContainer : INotifyPropertyChanged
	{
		//_ValueType Value { get; set; }
	}

	internal interface IValueContainer<_ValueType> : IValueContainer
	{
		_ValueType Value { get; set; }
	}

	internal class BoolControl : CheckBox, IValueContainer<bool>
	{
		//TODO: Rework to a nice toggle button w/ images

		internal BoolControl(bool val)
			: base()
		{
			Value = val;
		}

		public bool Value
		{
			get { return IsChecked.GetValueOrDefault(); }
			set { if (IsChecked != value) IsChecked = value; }
		}
		public event PropertyChangedEventHandler PropertyChanged;

		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (IsInitialized && e.Property == IsCheckedProperty)
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
		}
	}

	internal class FloatControl : MaskedTextBox<float>, IValueContainer<float>
	{
		internal FloatControl(float val)
			: base()
		{
			Mask = @"^[<S>]?([0-9]{1,3}(<T>[0-9]{3})|[0-9])*(<D>[0-9]{0,5})?$";
			Value = val;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (IsInitialized && e.Property == TextProperty)
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
		}
	}

	internal class ByteControl : MaskedTextBox<byte>, IValueContainer<byte> // Might change to wx.SpinCtrl later
	{
		internal ByteControl(byte val)
			: base()
		{
			Mask = @"^[0-9]{1,3}$";
			Value = val;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (IsInitialized && e.Property == TextProperty)
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
		}
	}

	public class IntControl : MaskedTextBox<int>, IValueContainer<int> // Might change to wx.SpinCtrl later
	{
		public IntControl() 
			: base()
		{
			Mask = @"^[<S>]?([0-9]{1,3}(<T>[0-9]{3})|[0-9])*$";
		}

		internal IntControl(int val)
			: base()
		{
			Value = val;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (IsInitialized && e.Property == TextProperty)
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
		}
	}

	internal class LongControl : MaskedTextBox<long>, IValueContainer<long> // Might change to wx.SpinCtrl later
	{
		internal LongControl(long val)
			: base()
		{
			Mask = @"^[<S>]?([0-9]{1,3}(<T>[0-9]{3})|[0-9])*$";
			Value = val;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (IsInitialized && e.Property == TextProperty)
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
		}
	}

	internal class StrControl : TextBox, IValueContainer<str>
	{
		internal StrControl(str val)
			: base()
		{
			Value = val;
			_ascii = val.IsAscii();
		}

		public str Value
		{
			get
			{
				if (Text == DetailsPanel.EMPTY)
					return null;
				if (_ascii && Text.Any(c => (c <= 127)))
					return new str(Encoding.ASCII.GetBytes(Text));
				return new str(Text);
			}
			set { Text = (value != null) ? value.ToString() : DetailsPanel.EMPTY; }
		}
		public event PropertyChangedEventHandler PropertyChanged;

		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (IsInitialized && e.Property == TextProperty)
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
		}

		internal bool _ascii;
	}

	internal class StringControl : TextBox, IValueContainer<string>
	{
		internal StringControl(string val)
			: base()
		{
			Value = val;
		}

		public string Value
		{
			get { return (Text != DetailsPanel.EMPTY) ? Text : null; }
			set { Text = (value != null) ? value : DetailsPanel.EMPTY; }
		}
		public event PropertyChangedEventHandler PropertyChanged;

		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (IsInitialized && e.Property == TextProperty)
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
		}
	}

	// Needed later to allow for modification by opening a color picker control, 
	// for now just a dumb display
	internal class ColorControl : Label, IValueContainer<byte[]>
	{
		internal ColorControl(P.Color color, bool read_only = false)
			: this(new byte[] { color.R, color.G, color.B, color.A }, read_only)
		{ }
		internal ColorControl(M.Color color, bool read_only = false)
			: this(new byte[] { color.R, color.G, color.B, color.A }, read_only)
		{ }
		internal ColorControl(byte[] color, bool read_only = false)
		{
			_readonly = read_only;

			Content = "";
			Width = new GridLength(100).Value;
			BorderBrush = M.Brushes.DarkGray;
			BorderThickness = new Thickness(1);
			Value = color;
		}

		public byte[] Value
		{
			get { return _value; }
			set
			{
				_value = value;
				M.Color c = M.Color.FromArgb(value[3], value[0], value[1], value[2]);
				Background = new M.SolidColorBrush(c);
			}
		}
#pragma warning disable CS0067 // The event 'ColorControl.PropertyChanged' is never used
		public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067

		internal byte[] _value;
		internal bool   _readonly;
	}

	// Needed later to allow for modification by opening a color picker control, 
	// for now just a dumb display
	internal class LinearColorControl : Label, IValueContainer<float[]>
	{
		internal LinearColorControl(P.LinearColor color, bool read_only = false)
			: this(new float[] { color.R, color.G, color.B, color.A }, read_only)
		{ }
		internal LinearColorControl(M.Color color, bool read_only = false)
			: this(new float[] { color.ScR, color.ScG, color.ScB, color.ScA }, read_only)
		{ }
		internal LinearColorControl(float[] color, bool read_only = false)
		{
			_readonly = read_only;

			Content = "";
			Width = new GridLength(100).Value;
			BorderBrush = M.Brushes.DarkGray;
			BorderThickness = new Thickness(1);
			Value = color;
		}

		public float[] Value
		{
			get { return _value; }
			set
			{
				_value = value;
				M.Color c = M.Color.FromScRgb(value[3], value[0], value[1], value[2]);
				Background = new M.SolidColorBrush(c);
			}
		}
#pragma warning disable CS0067 // The event 'ListViewControl.PropertyChanged' is never used
		public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067

		internal float[] _value;
		internal bool    _readonly;
	}


	internal class ListViewControl<_RowType> : ListView, IElement<ListViewControl<_RowType>.Collection>
	{
		//TODO: Add dedicated Value class which allows for more control 
		//      on how to display value, e.g. coloring
		//TODO: Also allow for adding framework elements like buttons,
		//      checkboxes, dropdowns and alike
		//TODO: Allow for changing elements (edit/add/remove)
		internal ListViewControl()
		{
			_gridview = new GridView() {
				AllowsColumnReorder = false,
			};

			HorizontalContentAlignment = HorizontalAlignment.Stretch;
			VerticalContentAlignment = VerticalAlignment.Stretch;
			ItemContainerStyle = FindResource("listviewitemStretch") as Style;
			MaxHeight = 400;
			View = _gridview;
		}

		internal ListViewControl(ColumnDefinitions columns = null)
			: this()
		{
			Columns = columns;
		}

		public virtual FrameworkElement Visual { get { return this; } }

		public bool HasLabel { get { return (_label != null); } }
		public string Label { get { return _label; } set { _label = value; } }
		public VerticalAlignment LabelVerticalAlign { get { return VerticalAlignment.Top; } }

		public bool HasValue { get { return true; } }
		public Collection Value
		{
			get { return ItemsSource as Collection; }
			set	{ ItemsSource = value; }
		}
#pragma warning disable CS0067 // The event 'ListViewControl.PropertyChanged' is never used
		public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067


		public class Collection : ObservableCollection<_RowType>
		{
			internal void RefreshRow(int index)
			{
				_RowType val = this[index];
				this[index] = default(_RowType);
				this[index] = val;
			}
		}


		public ColumnDefinitions Columns
		{
			get { return _columns; }
			set
			{
				_columns = value;

				_gridview.Columns.Clear();

				foreach (ColumnDefinition coldef in _columns)
				{
					GridViewColumn col = new GridViewColumn()
					{
						Header = coldef._header,
						Width = coldef._width,
						DisplayMemberBinding = coldef._binding,
						CellTemplate = coldef._template,
					};
					//TODO: Alignment
					//col.SetValue(HorizontalAlignmentProperty, coldef._align);
					//col.SetValue(HorizontalContentAlignmentProperty, coldef._align);

					_gridview.Columns.Add(col);
				}
			}
		}

		public class ColumnDefinitions : List<ColumnDefinition> { }

		internal class ColumnDefinition
		{
			internal ColumnDefinition(string header, double width, Binding binding, DataTemplate template = null, 
				HorizontalAlignment align = HorizontalAlignment.Left)
			{
				_header = header;
				_width = width;
				_binding = binding;
				_template = template;
				_align = align;
			}

			internal string _header;
			internal double _width;
			internal Binding _binding;
			internal DataTemplate _template;
			internal HorizontalAlignment _align;
		}


		internal string            _label;
		internal GridView          _gridview;
		internal ColumnDefinitions _columns;
	}


	internal class ColorSlot : ElementContainer<object>
	{
		public ColorSlot(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }

		internal override void _CreateVisual()
		{
			Label = "Color";
			_CreateChilds();
		}

		internal override void _CreateChilds()
		{
			StackPanel panel = new StackPanel() {
				Orientation = Orientation.Horizontal,
			};
			_visual = panel;

			byte slot_no = 0;
			if (_prop is P.ByteProperty)
				slot_no = (byte)(_prop as P.ByteProperty).Value;
			else if (_prop is byte)
				slot_no = (byte)_prop;
			else if (_prop is int)
				slot_no = (byte)(int)_prop;
			//else
			//	slot_no = (byte)0;
			panel.Children.Add(new ByteControl(slot_no));

			P.Property prop = MainWindow.CurrFile.Objects.FindByPathName("Persistent_Level:PersistentLevel.BuildableSubsystem");
			if (prop is P.Actor)
			{
				P.Entity entity = (prop as P.Actor).EntityObj;
				P.StructProperty stru;

				stru = entity.Value.Named("mColorSlotsPrimary", slot_no) as P.StructProperty;
				P.Color primary = (stru != null && stru.Value is P.Color) ? stru.Value as P.Color : null;

				stru = entity.Value.Named("mColorSlotsSecondary", slot_no) as P.StructProperty;
				P.Color secondary = (stru != null && stru.Value is P.Color) ? stru.Value as P.Color : null;

				if (primary != null && secondary != null)
				{
					panel.Children.Add(new ColorControl(primary));
					panel.Children.Add(new ColorControl(secondary));
				}
				else
				{
					ColorTable.Color color = ColorTable.INSTANCE.Find(slot_no);
					if (color != null)
					{
						panel.Children.Add(new ColorControl(color.Primary));
						panel.Children.Add(new ColorControl(color.Secondary));
					}
				}
			}
		}
	}


	// Actual value controls
	//
	// Those will combine label and one or more basic controls to fulfill a 
	// properties requirements, basically lowest level of IElement possible.
	//

	internal static class ValueControlFactory
	{
		internal static IElement Create(IElement parent, string label, object val, bool read_only = false)
		{
			if (!read_only)
			{
				if (val is bool)	return new SimpleValueControl<bool>          (parent, label, (bool)  val);
				if (val is byte)	return new SimpleValueControl<byte>          (parent, label, (byte)  val);
				if (val is int)		return new SimpleValueControl<int>           (parent, label, (int)   val);
				if (val is long)	return new SimpleValueControl<long>          (parent, label, (long)  val);
				if (val is float)	return new SimpleValueControl<float>         (parent, label, (float) val);
				if (val is str)		return new SimpleValueControl<str>			 (parent, label, (str)   val);
				if (val is string)	return new SimpleValueControl<string>		 (parent, label, (string)val);
				// Last resort: Empty string
				if (val != null)
					Log.Warning("! Label='{0}': Missing specialisation for {1}", label, val.GetType().Name);
				return                     new SimpleValueControl<string>        (parent, label, DetailsPanel.EMPTY);
			}
			else
			{
				if (val is bool)	return new ReadonlySimpleValueControl<bool>  (parent, label, (bool)  val);
				if (val is byte)	return new ReadonlySimpleValueControl<byte>  (parent, label, (byte)  val);
				if (val is int)		return new ReadonlySimpleValueControl<int>   (parent, label, (int)   val);
				if (val is long)	return new ReadonlySimpleValueControl<long>  (parent, label, (long)  val);
				if (val is float)	return new ReadonlySimpleValueControl<float> (parent, label, (float) val);
				if (val is str)		return new ReadonlySimpleValueControl<str>   (parent, label, (str)   val);
				if (val is string)	return new ReadonlySimpleValueControl<string>(parent, label, (string)val);
				// Last resort: Empty string
				if (val != null)
					Log.Warning("! Label='{0}': Missing specialisation for {1}", label, val.GetType().Name);
				return                     new ReadonlySimpleValueControl<string>(parent, label, DetailsPanel.EMPTY);
			}
		}
	}

	public abstract class ValueControl<_ValueType> : IElement<_ValueType>
	{
		public ValueControl(IElement parent, string label, object obj) 
		{
			_parent = parent;
			_visual = null;
			_label = label;
			_value = (obj != null) ? (_ValueType)obj : default(_ValueType);
		}

		public FrameworkElement Visual
		{
			get
			{
				if (_visual == null)
				{
					_CreateVisual();
					if (_visual is INotifyPropertyChanged)
						(_visual as INotifyPropertyChanged).PropertyChanged += _PropertyChanged;
				}
				return _visual;
			}
		}

		public bool HasLabel { get { return (_label != null)/*true*/; } }
		public string Label { get { return _label; } set { _label = value; } }
		public VerticalAlignment LabelVerticalAlign { get { return VerticalAlignment.Center; } }

		public bool HasValue { get { return (_value != null)/*true*/; } }
		public virtual _ValueType Value
		{
			get { return (_visual as IValueContainer<_ValueType>).Value; }
			set { (_visual as IValueContainer<_ValueType>).Value = _value; }
		}
		public event PropertyChangedEventHandler PropertyChanged;

		internal abstract void _CreateVisual();

		protected virtual void _PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			PropertyChanged?.Invoke(this, e);
		}

		internal IElement _parent;
		internal FrameworkElement _visual;
		internal string _label;
		internal _ValueType _value;
	}


	internal class SimpleValueControl<_ValueType> : ValueControl<_ValueType>
	{
		public SimpleValueControl(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }

		internal override void _CreateVisual()
		{
			_visual = ControlFactory.Create(_value);
		}
	}

	internal class ReadonlySimpleValueControl<_ValueType> : SimpleValueControl<_ValueType>
	{
		public ReadonlySimpleValueControl(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }

		internal override void _CreateVisual()
		{
			base._CreateVisual();
			_visual.IsEnabled = false;
		}
	}


	internal class MultiValueControl<_ValueType> : ValueControl<_ValueType[]> // Might subclass from IElementContainer instead
	{
		public MultiValueControl(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }

		public override _ValueType[] Value
		{
			get
			{
				if (_values == null)
					_values = new _ValueType[_childs.Count];
				int index = 0;
				foreach (FrameworkElement child in _childs)
				{
					if (child is IValueContainer<_ValueType>)//Temp workaround until real color control avail
						_values[index] = (child as IValueContainer<_ValueType>).Value;
					++index;
				}
				return _values;
			}
			set
			{
				int index = 0;
				foreach (_ValueType val in value)
				{
					if (_childs[index] is IValueContainer<_ValueType>)//Temp workaround until real color control avail
						(_childs[index] as IValueContainer<_ValueType>).Value = val;
					++index;
				}
			}
		}

		internal override void _CreateVisual()
		{
			_childs = new List<FrameworkElement>();
			_CreateChilds();

			StackPanel panel = new StackPanel() {
				Orientation = Orientation.Horizontal,
			};
			foreach (FrameworkElement child in _childs)
			{
				panel.Children.Add(child);
			}
		
			_visual = panel;
		}

		internal virtual void _CreateChilds()
		{
			foreach (_ValueType val in _value)
			{
				FrameworkElement child = ControlFactory.Create(val);
				(child as INotifyPropertyChanged).PropertyChanged += _PropertyChanged;
				_childs.Add(child);
			}
		}

		internal List<FrameworkElement> _childs;
		internal _ValueType[] _values;
	}

	internal class ReadonlyMultiValueControl<_ValueType> : MultiValueControl<_ValueType>
	{
		public ReadonlyMultiValueControl(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }

		internal override void _CreateChilds()
		{
			base._CreateChilds();
			foreach (FrameworkElement child in _childs)
				child.IsEnabled = false;
		}
	}


	internal class HexdumpControl : ValueControl<byte[]>
	{
		public HexdumpControl(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }

		internal override void _CreateVisual()
		{
			_visual = new TextBox() {
				Text = Helpers.Hexdump(_value, indent:0),
				FontFamily = new M.FontFamily("Consolas, FixedSys, Terminal"),
				FontSize = 12,
				MaxLines = 10,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				IsReadOnly = true,
				IsReadOnlyCaretVisible = true,
			};
		}
	}


	internal class ImageControl : ValueControl<byte[]>
	{
		public ImageControl(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }

		internal override void _CreateVisual()
		{
			// Build image
			_image = ImageHandler.ImageFromBytes(_value, depth:4);

			Grid grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(0, GridUnitType.Auto) });

			Label label = new Label() {
				Content = string.Format(Translate._("ImageControl.Label"), _image.PixelWidth, _image.PixelHeight, 4),
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Stretch,
				VerticalContentAlignment = VerticalAlignment.Center,
			};
			Grid.SetColumn(label, 0);
			grid.Children.Add(label);

			_button = new Button() {
				Content = Translate._("ImageControl.Button"),
				Width = 100,
				Height = 21,
			};
			_button.Click += _button_Click;
			Grid.SetColumn(_button, 1);
			grid.Children.Add(_button);

			_visual = grid;
		}

		private void _button_Click(object sender, RoutedEventArgs e)
		{
			new ImageDialog(null, Translate._("ImageDialog.Title"), _image).ShowDialog();
		}

		internal Button _button;
		internal BitmapSource _image;
	}


	internal class ListControl : Expando //<- For now, will be replaced by either ListView or DataView
	{
		public ListControl(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }

		internal override void _CreateChilds()
		{
			_childs = new List<IElement>();
			ICollection coll = Tag as ICollection;
			int index = 0;

			Header = Header + string.Format(" [{0}]", coll.Count);
			if (coll.Count == 0)
				IsEnabled = false;

			foreach (object obj in coll)
			{
				++index;
				string label = index.ToString();
				IElement element = MainFactory.Create(this, label, obj);
				_childs.Add(element);
			}
		}
	}


	internal class DictControl : Expando //<- For now, will be replaced by either ListView or DataView
	{
		public DictControl(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }

		internal override void _CreateChilds()
		{
			_childs = new List<IElement>();
			IDictionary dict = Tag as IDictionary;

			Header = Header + string.Format(" [{0}]", dict.Count);
			if (dict.Count == 0)
				IsEnabled = false;

			foreach (DictionaryEntry pair in dict)
			{
				string label = pair.Key.ToString();
				IElement element = MainFactory.Create(this, label, pair.Value);
				_childs.Add(element);
			}
		}
	}


	internal class TextWithIconDisplay : StackPanel
	{
		public TextWithIconDisplay()
		{ }
		public TextWithIconDisplay(P.InventoryItem inv_item)
		{
			_Setup(ItemTable.Find(inv_item));
		}
		public TextWithIconDisplay(ItemTable.Item tbl_item)
		{
			_Setup(tbl_item.Icon[MainWindow.CurrFile.Header.GetVersion()], tbl_item.DisplayName);
		}
		public TextWithIconDisplay(BitmapSource icon, string text)
		{
			_Setup(icon, text);
		}

		internal void _Setup(P.InventoryItem inv_item)
		{
			_Setup(ItemTable.Find(inv_item));
		}
		internal void _Setup(ItemTable.Item tbl_item)
		{
			_Setup(tbl_item.Icon[MainWindow.CurrFile.Header.GetVersion()], tbl_item.DisplayName);
		}
		internal void _Setup(BitmapSource icon, string text)
		{
			Orientation = Orientation.Horizontal;
			Width = double.NaN;
			Height = 20;
			HorizontalAlignment = HorizontalAlignment.Stretch;
			VerticalAlignment = VerticalAlignment.Center;
			Margin = new Thickness(0, 2, 0, 2);

			Image img = new Image() {
				Source = icon,
				Width = 20,
				Height = 20,
				Margin = new Thickness(0, 0, 5, 0),
			};
			Children.Add(img);

			TextBlock txt = new TextBlock() {
				Text = text,
				Width = double.NaN,
				Height = 20,
				VerticalAlignment = VerticalAlignment.Stretch,
			};
			Children.Add(txt);
		}
	}


	public class ItemCombobox : ComboBox, IValueContainer<str[]>
	{
		internal enum Parameter : int { Item=0, Instance=1 };

		internal ItemCombobox(str item, str instance, Collection coll)
			: this(new str[] { item, instance }, coll)
		{ }

		internal ItemCombobox(str[] item, Collection coll)
		{
			_version = MainWindow.CurrFile.Header.GetVersion();
			ItemsSource = _collection = coll;
			Value = item;
			SelectionChanged += _SelectionChanged;
		}

		public Collection AvailableItems
		{
			get
			{
				return _collection;
			}
			set
			{
				SelectionChanged -= _SelectionChanged;
				ItemsSource = _collection = value;
				Value = _item;
				SelectionChanged += _SelectionChanged;
			}
		}

		public str[] Value
		{
			get
			{
				return _item;
			}
			set
			{
				_item = value;
				if (_item != null && _item.Length == 2)
				{
					_selected = ItemTable.Find(_item[(int)Parameter.Item]);
					if (_selected != null)
					{
						ItemEntry entry = _collection.FirstOrDefault(e => e.Item == _selected);
						if (entry != null)
						{
							SelectedItem = entry;
							return;
						}
					}
				}
				SelectedIndex = 0;// Select '<empty>'
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void _SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ItemEntry item = SelectedItem as ItemEntry;
			_selected = (item != null) ? item.Item : null;
			if (_selected != null)
			{
				str item_name = new str(_selected.PathName);
				item_name.ToAscii();
				_item[(int)Parameter.Item]     = item_name;
				_item[(int)Parameter.Instance] = item.Instance;
			}
			else
			{
				_item[(int)Parameter.Item]     = _empty_str;
				_item[(int)Parameter.Instance] = _empty_str;
			}

			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
		}

		private str[]                       _item;
		private ItemTable.Item              _selected;
		private static VersionTable.Version _version;
		private Collection                  _collection;
		private static str                  _empty_str = new str(true);
		
		public class Collection : ObservableCollection<ItemEntry>
		{
			public Collection()
				: base()
			{ }

			public Collection(IEnumerable<ItemEntry> coll)
				: base(coll)
			{ }

			public static Collection FromItemTable(IEnumerable<ItemTable.Item> tbl)
			{
				Collection coll = new Collection();
				coll.Add(new ItemEntry(DetailsPanel.EMPTY));

				if (tbl != null && tbl.Count() > 0)
				{
					var grouped = tbl.GroupBy(i => i.Type);
					foreach (var group in grouped)
					{
						string type = group.Key.ToString();
						string tr = "DetailsPanel.ItemTable.Type." + type + "s";
						if (Translate.Has(tr))
							type = Translate._(tr);
						coll.Add(new ItemEntry(type));

						foreach (var grouped_item in group.OrderBy(i => i.DisplayName))
							if (!grouped_item.Blocked)
								coll.Add(new ItemEntry(grouped_item, _empty_str));
					}
				}

				return coll;
			}

			public static Collection FromSingleItem(ItemTable.Item item)
			{
				Collection coll = new Collection();
				coll.Add(new ItemEntry(DetailsPanel.EMPTY));

				string type = item.Type.ToString();
				string tr = "DetailsPanel.ItemTable.Type." + type + "s";
				if (Translate.Has(tr))
					type = Translate._(tr);
				coll.Add(new ItemEntry(type));

				coll.Add(new ItemEntry(item, _empty_str));

				return coll;
			}

			public static Collection FromInventoryStacks(P.Properties inv)
			{
				Collection coll = new Collection();
				coll.Add(new ItemEntry(DetailsPanel.EMPTY));

				if (inv != null)
				{
					// Normally, only called for equipment, so no grouping required
					var items = inv
						.ListOf<P.InventoryStack>()
						.Where(stack => (stack.Value != null) && (stack.Value.Count == 1))
						.Select(stack => (stack.Value[0] as P.StructProperty).Value as P.InventoryItem)
						;
					foreach (var i in items)
					{
						ItemTable.Item tbl_item = ItemTable.Find(i);
						if (tbl_item != null)
							coll.Add(new ItemEntry(tbl_item, i.PathName));
					}
				}

				return coll;
			}
		}

		public class ItemEntry
		{
			public string         Label     { get; set; }
			public ItemTable.Item Item      { get; set; }
			public str            Instance  { get; set; }
			public bool           HasIcon   { get { return (Item != null) || IsEmpty; } }
			public BitmapSource   Icon      { get { return (Item != null) ? Item.Icon[_version] : null; } }
			public bool           IsEnabled { get { return (Item != null) || IsEmpty; } }
			public bool           IsEmpty   { get { return (Label == DetailsPanel.EMPTY); } }

			public ItemEntry(ItemTable.Item item, str instance)
				: this(item.DisplayName, item, instance)
			{ }

			public ItemEntry(string label, ItemTable.Item item = null, str instance = null)
			{
				Label    = label;
				Item     = item;
				Instance = instance;
			}
		}
	}

	public class ItemControl : ValueControl<str[]>
	{
		public ItemControl(IElement parent, string label, object obj, ItemCombobox.Collection coll = null)
			: base(parent, label, obj)
		{
			_collection = coll;
			_no_change_event = false;
		}

		public ItemCombobox.Collection AvailableItems
		{
			get { return _collection; }
			set
			{
				_no_change_event = true;
				_cmb.AvailableItems = _collection = value;
				_no_change_event = true;
			}
		}

		internal override void _CreateVisual()
		{
			Border border = new Border() {
				Background = Brushes.Transparent,
				BorderThickness = new Thickness(1),
				BorderBrush = Brushes.DarkGray,
				Padding = new Thickness(0,0,1,0),
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch,
			};

			Grid grid = new Grid() {
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch,
			};
			border.Child = grid;

			_cmb = new ItemCombobox(_value, _collection);
			grid.Children.Add(_cmb);

			_visual = border;
		}

		protected override void _PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (!_no_change_event)
				base._PropertyChanged(sender, e);
		}

		private ItemCombobox.Collection _collection;
		private ItemCombobox            _cmb;
		private bool                    _no_change_event;
	}


	// A basic element container to ease implementation, mostly for those
	// varying properties like ObjectProperty or ArrayProperty which can
	// be expressed different based on their actual data stored
	//
	internal class ElementContainer<_PropType> : IElementContainer
		where _PropType : class
	{
		public ElementContainer(IElement parent, string label, object obj)
		{
			_parent = parent;
			_label = label;
			_prop = obj as _PropType;
		}

		public FrameworkElement Visual
		{
			get
			{
				if (_visual == null)
				{
					_CreateVisual();
					if (_visual is INotifyPropertyChanged)
						(_visual as INotifyPropertyChanged).PropertyChanged += _PropertyChanged;
				}
				return _visual;
			}
		}

		public virtual bool HasLabel { get { return !string.IsNullOrEmpty(_label); } }
		public virtual string Label { get { return _label; } set { _label = value; } }
		public VerticalAlignment LabelVerticalAlign { get { return VerticalAlignment.Center; } }

		public virtual bool HasValue { get { return true; } }
		//Value
		public event PropertyChangedEventHandler PropertyChanged;

		public virtual int Count { get { throw new NotImplementedException(); } }
		public virtual List<IElement> Childs { get { throw new NotImplementedException(); } }

		public virtual void Add(IElement element)
		{
			throw new NotImplementedException();
		}

		internal virtual void _CreateVisual()
		{
			// Last report: Expando
			if (_impl == null)
				_impl = new Expando(_parent, _label, _prop);

			_label = _impl.Label;
			_visual = _impl.Visual;
		}

		internal virtual void _CreateChilds()
		{
			throw new NotImplementedException();
		}

		protected virtual void _PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			PropertyChanged?.Invoke(sender is Property ? sender : _prop, e);
		}

		internal IElement _parent;
		internal string _label;
		internal _PropType _prop;
		internal FrameworkElement _visual;
		internal IElement _impl;
	}


	// Following actual property visualizers
	//
	// Types not listed here won't be visualized, which is a bit
	// cumbersome, but we can't just fall back into an Expando or such.
	//
	// When implementing, try to keep same order as with Properties.h^^
	//

	//Property -> Might be implemented, might be not, we'll see

	// Most basic property of all
	internal class ValueProperty<_ValueType> : SimpleValueControl<_ValueType>
	{
		public ValueProperty(IElement parent, string label, object obj)
			: base(parent, null, null)
		{
			_prop = obj as P.ValueProperty;
			if (_prop != null)
			{
				Label = _prop.Name.ToString();
				_value = (_ValueType) _prop.Value;
			}
			else
			{
				Label = null;
				_value = default(_ValueType);
			}
		}

		protected override void _PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			base._PropertyChanged(_prop, e);
		}

		internal ValueProperty _prop;
	}

	// Multiple properties as array
	internal class PropertyList : Expando
	{
		public PropertyList(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{
			_childs = new List<IElement>();
		}

		internal override void _CreateChilds()
		{
			P.PropertyList prop_list = Tag as P.PropertyList;
			foreach (Property prop in prop_list.Value)
			{
				string label = prop != null ? prop.ToString() : null;
				IElement element = MainFactory.Create(this, label, prop);
				_childs.Add(element);
			}
		}
	}

	// Simple types

	internal class BoolProperty : ValueProperty<byte>
	{
		public BoolProperty(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}

	internal class ByteProperty : ValueProperty<string>
	{
		public ByteProperty(IElement parent, string label, object obj) 
			: base(parent, label, null)
		{
			_prop = obj as ValueProperty;
			_value = _prop.Value.ToString();
		}
	}

	internal class IntProperty : ValueProperty<int>
	{
		public IntProperty(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}

	internal class FloatProperty : ValueProperty<float>
	{
		public FloatProperty(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}

	internal class StrProperty : ValueProperty<str>
	{
		public StrProperty(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}

	// Complex types

	internal class Header : Expando
	{
		public Header(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{
			_header = obj as P.Header;
		}

		internal override void _CreateChilds()
		{
			_childs.Add(ValueControlFactory.Create(this, "Type"        , _header.Type, true));

			//TODO: Allow to change save version back to v20 to allow for storing as old, uncompressed format
			_childs.Add(ValueControlFactory.Create(this, "SaveVersion" , _header.SaveVersion, true));

			int build_version = _header.GetBuildVersion();
			VersionTable.VersionEntry v = VersionTable.INSTANCE.Find(build_version);
			string build_v = v != null ? v.ToString() : string.Format("#{0}", build_version);
			_childs.Add(ValueControlFactory.Create(this, "BuildVersion", build_v, true));

			_childs.Add(_map_name = new SimpleValueControl<str>(this, "MapName", _header.MapName));

			_childs.Add(_map_options = new SimpleValueControl<str>(this, "MapOptions", _header.MapOptions));

			_childs.Add(_session_name = new SimpleValueControl<str>(this, "SessionName", _header.SessionName));

			DateTime dur = new DateTime();
			dur = dur.AddSeconds(_header.PlayDuration);
			_childs.Add(ValueControlFactory.Create(this, "PlayDuration", dur.ToString("HH:mm:ss"), true));

			DateTime saved = new DateTime();
			saved = saved.AddSeconds(_header.SaveDateTime / 10000000.0);
			saved = saved.ToLocalTime();//<- Note that time was created in GMT, so we've to adjust to local!
			_childs.Add(ValueControlFactory.Create(this, "SaveDateTime", saved.ToString("G"), true));

			_childs.Add(ValueControlFactory.Create(this, "Visibility"  , ((Visibilities)_header.Visibility).ToString(), true));
		}

		protected override void _PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			_header.MapName     = _map_name.Value;
			_header.MapOptions  = _map_options.Value;
			_header.SessionName = _session_name.Value;

			base._PropertyChanged(_header, e);
		}

		internal P.Header _header;
		internal SimpleValueControl<str> _map_name;
		internal SimpleValueControl<str> _map_options;
		internal SimpleValueControl<str> _session_name;

		internal enum Visibilities { Private=0, FriendsOnly=1 };
	}

	internal class Collected : Expando
	{
	//CLS(Collected) 
	//	//TODO: Find correct name, if any
	//	PUB_s(LevelName)
	//	PUB_s(PathName)
	//	READ
	//		LevelName = reader->ReadString();
	//		PathName = reader->ReadString();
	//	READ_END
	//	STR_(PathName)
	//CLS_END
		public Collected(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}

	internal class StructProperty : ElementContainer<P.StructProperty>
	{
	//CLS_(StructProperty,ValueProperty)
	//	PUB_ab(Unknown)
	//	bool IsArray;
	//	READ
	//		IsArray = false;
	//		str^ inner = reader->ReadString();
	//		Property^ acc = PropertyFactory::Construct(inner, this);
	//		if (acc == nullptr)
	//			throw gcnew ReadException(reader, String::Format("Unknown inner structure type '{0}'", inner));
	//		Unknown = ReadBytes(reader, 17);
	//		Value = acc->Read(reader);
	//	READ_END
	//	void ReadAsArray(IReader^ reader, int count)
	//	{
	//		IsArray = true;
	//		str^ inner = reader->ReadString();
	//		Unknown = ReadBytes(reader, 17);
	//		Properties^ props = gcnew Properties;
	//		for (int i = 0; i < count; ++i)
	//		{
	//			Property^ acc = PropertyFactory::Construct(inner, this);
	//			if (acc == nullptr)
	//				throw gcnew ReadException(reader, String::Format("Unknown inner structure type '{0}'", inner));
	//			props->Add(acc->Read(reader));
	//		}
	//		Value = props;
	//	}
	//CLS_END
		public StructProperty(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{
			//_label = _prop.Name.ToString();
			_label = null;
		}

		//public override string Label { get { return _prop.Name.ToString(); } }

		//public int Count { get { throw new NotImplementedException(); } }
		//public List<IElement> Childs { get { throw new NotImplementedException(); } }

		//public void Add(IElement element)
		//{
		//	throw new NotImplementedException();
		//}

		internal override void _CreateVisual()
		{
			if (_prop.Value != null && _prop.Index == 0 && !_prop.IsArray)
			{
				if (_prop.Unknown.IsNullOrEmpty())
				{
					// Replace it with type of actual value
					/*TODO:
					t = prop.Value.TypeName
					if t in globals():
						cls = globals()[t]
						cls(parent_pane, parent_sizer, prop.Name, prop.Value)
						return parent_pane, parent_sizer
					*/
					_impl = MainFactory.Create(_parent, _prop.Name.ToString(), _prop.Value as Property);
					_impl.PropertyChanged += _PropertyChanged;
				}
			}

			base._CreateVisual();
		}

		//internal override void _CreateChilds()
		//{
		//}
	}

	internal class Vector : MultiValueControl<float> //ValueControl<P.Vector>
	{
		public Vector(IElement parent, string label, object obj)
			: base(parent, label, null)
		{
			_prop = obj as P.Vector;
			_value = new float[] { _prop.X, _prop.Y, _prop.Z };
		}

		protected override void _PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			float[] vals = Value;
			_prop.X = vals[0];
			_prop.Y = vals[1];
			_prop.Z = vals[2];

			base._PropertyChanged(_prop, e);
		}

		internal P.Vector _prop;
	}

	internal class Rotator : MultiValueControl<float> //ValueControl<P.Vector>
	{
		public Rotator(IElement parent, string label, object obj)
			: base(parent, label, null)
		{
			_prop = obj as P.Rotator;
			_value = new float[] { _prop.Pitch, _prop.Yaw, _prop.Roll };
		}

		protected override void _PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			float[] vals = Value;
			_prop.Pitch = vals[0];
			_prop.Yaw   = vals[1];
			_prop.Roll  = vals[2];

			base._PropertyChanged(_prop, e);
		}

		internal P.Rotator _prop;
	}

	internal class Scale : Vector
	{
		public Scale(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class Box : Expando
	{
		public Box(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }

		internal override void _CreateChilds()
		{
			P.Box box = Tag as P.Box;

			_childs.Add(new MultiValueControl<float>(this, "Min", 
				new float[] { box.MinX, box.MinY, box.MinZ }));

			_childs.Add(new MultiValueControl<float>(this, "Max", 
				new float[] { box.MaxX, box.MaxY, box.MaxZ }));

			_childs.Add(ValueControlFactory.Create(this, "IsValid", box.IsValid));
		}

		protected override void _PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			P.Box box = Tag as P.Box;

			float[] min = (_childs[0] as MultiValueControl<float>).Value;
			box.MinX = min[0];
			box.MinY = min[1];
			box.MinZ = min[2];

			float[] max = (_childs[1] as MultiValueControl<float>).Value;
			box.MaxX = max[0];
			box.MaxY = max[1];
			box.MaxZ = max[2];

			base._PropertyChanged(box, e);
		}
	}

	internal class Color : MultiValueControl<byte> //ValueControl<P.Color>
	{
		public Color(IElement parent, string label, object obj)
			: base(parent, label + " [RGBA]", null)
		{
			_prop = obj as P.Color;
			_value = new byte[] { _prop.R, _prop.G, _prop.B, _prop.A };
		}

		internal override void _CreateChilds()
		{
			base._CreateChilds();
			_childs.Add(new ColorControl(_prop));
		}

		protected override void _PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			byte[] vals = Value;
			_prop.R = vals[0];
			_prop.G = vals[1];
			_prop.B = vals[2];
			_prop.A = vals[3];

			base._PropertyChanged(_prop, e);
		}

		internal P.Color _prop;
	}

	internal class ReadonlyColor : ReadonlyMultiValueControl<byte> //ValueControl<P.Color>
	{
		public ReadonlyColor(IElement parent, string label, object obj)
			: base(parent, label + " [RGBA]", null)
		{
			_prop = obj as P.Color;
			_value = new byte[] { _prop.R, _prop.G, _prop.B, _prop.A };
		}

		internal override void _CreateChilds()
		{
			base._CreateChilds();
			_childs.Add(new ColorControl(_prop, true));
		}

		protected override void _PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			byte[] vals = Value;
			_prop.R = vals[0];
			_prop.G = vals[1];
			_prop.B = vals[2];
			_prop.A = vals[3];

			base._PropertyChanged(_prop, e);
		}

		internal P.Color _prop;
	}

	internal class LinearColor : MultiValueControl<float> //ValueControl<P.LinearColor>
	{
		public LinearColor(IElement parent, string label, object obj)
			: base(parent, label + " [RGBA]", null)
		{
			_prop = obj as P.LinearColor;
			_value = new float[] { _prop.R, _prop.G, _prop.B, _prop.A };
		}

		internal override void _CreateChilds()
		{
			base._CreateChilds();
			_childs.Add(new LinearColorControl(_prop));
		}

		protected override void _PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			float[] vals = Value;
			_prop.R = vals[0];
			_prop.G = vals[1];
			_prop.B = vals[2];
			_prop.A = vals[3];

			base._PropertyChanged(_prop, e);
		}

		internal P.LinearColor _prop;
	}

	internal class ReadonlyLinearColor : ReadonlyMultiValueControl<float> //ValueControl<P.LinearColor>
	{
		public ReadonlyLinearColor(IElement parent, string label, object obj)
			: base(parent, label + " [RGBA]", null)
		{
			_prop = obj as P.LinearColor;
			_value = new float[] { _prop.R, _prop.G, _prop.B, _prop.A };
		}

		internal override void _CreateChilds()
		{
			base._CreateChilds();
			_childs.Add(new LinearColorControl(_prop, true));
		}

		protected override void _PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			float[] vals = Value;
			_prop.R = vals[0];
			_prop.G = vals[1];
			_prop.B = vals[2];
			_prop.A = vals[3];

			base._PropertyChanged(_prop, e);
		}

		internal P.LinearColor _prop;
	}

	internal class Transform : PropertyList //Expando
	{
	//CLS_(Transform,PropertyList)
	//	READ
	//		PropertyList^ obj = (PropertyList^) PropertyList::Read(reader);
	//		for (int i = 0; i < obj->Value->Count; ++i)
	//		{
	//			ValueProperty^ prop = (ValueProperty^) Value[i];
	//			if (*(prop->Name) == "Scale3D")
	//			{
	//				prop->Value = Scale::FromVector((Vector^)prop->Value);
	//				break;
	//			}
	//		}
	//		return obj;
	//	READ_END
	//CLS_END
		public Transform(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class Quat : MultiValueControl<float> //ValueControl<P.Quat>
	{
		public Quat(IElement parent, string label, object obj)
			: base(parent, label, null)
		{
			_prop = obj as P.Quat;
			_value = new float[] { _prop.X, _prop.Y, _prop.Z, _prop.W };
		}

		protected override void _PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			float[] vals = Value;
			_prop.X = vals[0];
			_prop.Y = vals[1];
			_prop.Z = vals[2];
			_prop.W = vals[3];

			base._PropertyChanged(_prop, e);
		}

		internal P.Quat _prop;
	}

	internal class Guid : StringControl
	{
		public Guid(IElement parent, string label, object obj)
			: base(DetailsPanel.EMPTY)
		{
			_guid = obj as P.Guid;
			if (_guid != null && _guid.Value is byte[])
			{
				byte[] b_arr = _guid.Value as byte[];
				if (b_arr.Length == 16)
				{
					StringBuilder sb = new StringBuilder(64);
					for (int i=0; i<4; ++i)
					{
						if (i > 0)
							sb.Append('-');
						sb.Append(BitConverter.ToUInt32(b_arr, i*4).ToString("X8"));
					}
					Value = sb.ToString();
					IsReadOnly = true;
				}
			}
		}

		private P.Guid _guid;
	}

	internal class RemovedInstanceArray : PropertyList
	{
		public RemovedInstanceArray(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class RemovedInstance : PropertyList
	{
		public RemovedInstance(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class PhaseCost : PropertyList
	{
		public PhaseCost(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class ItemAmount : PropertyList
	{
		public ItemAmount(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class ResearchCost : PropertyList
	{
		public ResearchCost(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class CompletedResearch : PropertyList
	{
		public CompletedResearch(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class ResearchRecipeReward : PropertyList
	{
		public ResearchRecipeReward(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class ItemFoundData : PropertyList
	{
		public ItemFoundData(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class RecipeAmountStruct : PropertyList
	{
		public RecipeAmountStruct(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class MessageData : PropertyList
	{
		public MessageData(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class SplinePointData : PropertyList
	{
		public SplinePointData(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class SpawnData : PropertyList
	{
		public SpawnData(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class FeetOffset : PropertyList
	{
		public FeetOffset(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class SplitterSortRule : PropertyList
	{
		public SplitterSortRule(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class SchematicCost : PropertyList
	{
		public SchematicCost(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class TimerHandle : PropertyList
	{
		public TimerHandle(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class InventoryStack : PropertyList
	{
		public InventoryStack(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class InventoryItem : Expando
	{
		public InventoryItem(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }

		internal override void _CreateChilds()
		{
			P.InventoryItem item = Tag as P.InventoryItem;

			//|	-> [InventoryItem] /Game/FactoryGame/Resource/Parts/IronPlateReinforced/Desc_IronPlateReinforced.Desc_IronPlateReinforced_C
			//|	  .Unknown = str:''
			//|	  .ItemName = str:'/Game/FactoryGame/Resource/Parts/IronPlateReinforced/Desc_IronPlateReinforced.Desc_IronPlateReinforced_C'
			//|	  .LevelName = str:''
			//|	  .PathName = str:''
			//|	  .Value =
			//|		-> [IntProperty] NumItems
			//|		  .Name = str:'NumItems'
			//|		  .Length = Int32:4
			//|		  .Index = Int32:0
			//|		  .Value = Int32:11
			string name = null;
			if (str.IsNullOrEmpty(item.ItemName))
				name = DetailsPanel.EMPTY;
			else
			{
				name = item.ItemName.LastName();
				if (name != null && Translate.Has(name))
					name = Translate._(name);
			}
			item_name = MainFactory.Create(this, "ItemName", name, true);
			_childs.Add(item_name);

			item_count = new ValueProperty<int>(this, "Count", item.Value);
			_childs.Add(item_count);
		}

		private IElement item_name;
		private ValueProperty<int> item_count;
	}

	internal class TimeTableStop : PropertyList
	{
		public TimeTableStop(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class RailroadTrackPosition : Expando
	{
		public RailroadTrackPosition(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }

		internal override void _CreateChilds()
		{
			P.RailroadTrackPosition pos = Tag as P.RailroadTrackPosition;
			_childs.Add(MainFactory.Create(this, "Offset", pos.Offset));
			_childs.Add(MainFactory.Create(this, "Forward", pos.Forward));
		}
	}

	internal class ObjectProperty : ElementContainer<P.ObjectProperty>
	{
	//CLS_(ObjectProperty,ValueProperty)
	//	// Note that ObjectProperty is somewhat special with having
	//	// two different faces: w/ .Name + .Value and w/o those.
	//	// (depending on its 'context' when loaded)
	//	PUB_s(LevelName)
	//	PUB_s(PathName)
	//	READ
	//		Read(reader, true);
	//	READ_END
	//	Property^ Read(IReader^ reader, bool null_check)
	//	{
	//		if (null_check)
	//			CheckNullByte(reader);
	//		LevelName = reader->ReadString();
	//		PathName = reader->ReadString();
	//		return this;
	//	}
	//	STR_(PathName)
	//CLS_END
		public ObjectProperty(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }

		//public int Count { get { throw new NotImplementedException(); } }
		//public List<IElement> Childs { get { throw new NotImplementedException(); } }

		//public void Add(IElement element)
		//{
		//	throw new NotImplementedException();
		//}

		internal override void _CreateVisual()
		{
			// Have seen 4 different combinations so far:
			// - (1) Only PathName valid, LevelName + Name + Value = empty (sub in an ArrayProperty)
			// - (2) PathName + LevelName, but Name + Value are empty (also with ArrayProperty)
			// - (2) PathName + Name valid, but LevelName + Value empty (sub in a StructProperty)
			// - (3) PathName, LevelName + Name, but empty Value (sub in an EntityObj)
			//
			//=> PathName  LevelName  Name  Value
			//      x          -       -      -
			//      x          x       -      -
			//      x          -       x      -
			//      x          x       x      -

			if (str.IsNullOrEmpty(_prop.LevelName) || _prop.LevelName.ToString() == "Persistent_Level")
			{
				if (str.IsNullOrEmpty(_prop.Name))
				{
					// Only PathName (... are we in an ArrayProperty?)
					_impl = MainFactory.Create(_parent, _label, _prop.PathName);
				}
				else
				{
					// PathName + Name, so Name is our label
					_impl = MainFactory.Create(_parent, _prop.Name.ToString(), _prop.PathName);
				}
				_impl.PropertyChanged += _PropertyChanged;
			}

			base._CreateVisual();
		}

		//internal void _CreateChilds()
		//{
		//}
	}

	internal class ArrayProperty : ElementContainer<P.ArrayProperty> //: Expando //: IElement //ValueProperty
	{
	//CLS_(ArrayProperty,ValueProperty)
	//	PUB_s(InnerType)
	//	READ
	//		InnerType = reader->ReadString();
	//		if (InnerType == "StructProperty")
	//		{
	//			CheckNullByte(reader);
	//			int count = reader->ReadInt();
	//			str^ name = reader->ReadString();
	//			str^ type = reader->ReadString();
	//			//assert _type == self.InnerType
	//			int length = reader->ReadInt();
	//			int index = reader->ReadInt();
	//			StructProperty^ stru = gcnew StructProperty(this);
	//			stru->Name = name;
	//			stru->Length = length;
	//			stru->Index = index;
	//			stru->ReadAsArray(reader, count);
	//			Value = stru;
	//		}
	//		else if (InnerType == "ObjectProperty")
	//		{
	//			CheckNullByte(reader);
	//			int count = reader->ReadInt();
	//			Properties^ objs = gcnew Properties;
	//			for (int i = 0; i < count; ++i)
	//			{
	//				ObjectProperty^ prop = gcnew ObjectProperty(this);
	//				objs->Add(prop->Read(reader, false));
	//			}
	//			Value = objs;
	//		}
	//		else if (InnerType == "IntProperty")
	//		{
	//			CheckNullByte(reader);
	//			int count = reader->ReadInt();
	//			Value = ReadInts(reader, count);
	//		}
	//		else if (InnerType == "ByteProperty")
	//		{
	//			CheckNullByte(reader);
	//			int count = reader->ReadInt();
	//			Value = ReadBytes(reader, count);
	//		}
	//		else
	//			throw gcnew ReadException(reader, String::Format("Unknown inner array type '{0}'", InnerType));
	//	READ_END
	//CLS_END
		public ArrayProperty(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }

		internal override void _CreateVisual()
		{
			if (_prop.Value is P.StructProperty)
			{
				P.StructProperty stru = _prop.Value as P.StructProperty;
				_impl = MainFactory.Create(_parent, null/*stru.Name.ToString()/*_label*/, stru);
			}
			else if (_prop.Value is int[])
			{
				_impl = new ListControl(_parent, _label, _prop.Value);
			}
			else if (_prop.Value is byte[])
			{
				_impl = new HexdumpControl(_parent, _label, _prop.Value);
			}
			else if (_prop.Value is ICollection)
			{
				// List<ObjectProperty> or List<str> (InnerType:EnumProperty|StrProperty)
				_impl = new ListControl(_parent, _prop.Name.ToString(), _prop.Value);
			}
			_impl.PropertyChanged += _PropertyChanged;

			base._CreateVisual();
		}
	}

	internal class EnumProperty : ValueProperty<str>
	{
	//CLS_(EnumProperty,ValueProperty)
	//	PUB_s(EnumName)
	//	READ
	//		EnumName = reader->ReadString();
	//		CheckNullByte(reader);
	//		Value = reader->ReadString();
	//	READ_END
	//	STR_(EnumName)
	//CLS_END
		public EnumProperty(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class NameProperty : StrProperty
	{
		public NameProperty(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class TextProperty : ValueProperty<str>
	{
	//CLS_(TextProperty,ValueProperty)
	//	PUB_ab(Unknown)
	//	READ
	//		CheckNullByte(reader);
	//		Unknown = ReadBytes(reader, 13);
	//		Value = reader->ReadString();
	//	READ_END
	//CLS_END
		public TextProperty(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class Entity : Expando //PropertyList
	{
	//public ref class Entity : PropertyList
	//{
	//public:
	//	Entity(Property^ parent, str^ level_name, str^ path_name, Properties^ children)
	//		: PropertyList(parent)
	//		, LevelName(level_name)
	//		, PathName(path_name)
	//		, Children(children)
	//		, Unknown(0)
	//		, Missing(nullptr)
	//		//, Private(nullptr)
	//	{ }
	//	PUB_s(LevelName)
	//	PUB_s(PathName)
	//	PUB(Children, Properties^)
	//	PUB_i(Unknown)
	//	PUB_ab(Missing)
	//	//PUB_o(Private, ...)
	//
	//	Property^ Read(IReader^ reader, int length)
	//	{
	//		int last_pos = reader->Pos;
	//		PropertyList::Read(reader);
	//		//TODO: There is an extra 'int' following, investigate!
	//		// Not sure if this is valid for all elements which are of type
	//		// PropertyList. For now,  we will handle it only here
	//		// Might this be the same "int" discovered with entities below???
	//		Unknown = reader->ReadInt();
	//		int bytes_read = reader->Pos - last_pos;
	//		if (bytes_read < 0)
	//			throw gcnew ReadException(reader, "Negative offset!");
	//		if (bytes_read != length)
	//			Missing = ReadBytes(reader, length - bytes_read);
	//		return this;
	//	}
	//	STR_(PathName)
	//};
		public Entity(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{
			// Ok to keep entities expanded
			IsExpanded = true;
		}
	}

	internal class NamedEntity : Entity
	{
	//public ref class NamedEntity : Entity
	//{
	//public:
	//	ref class Name : Property
	//	{
	//	public:
	//		Name(Property^ parent) 
	//			: Property(parent) 
	//		{ }
	//
	//		PUB_s(LevelName)
	//		PUB_s(PathName)
	//		READ
	//			LevelName = reader->ReadString();
	//			PathName = reader->ReadString();
	//		READ_END
	//	};
	//
	//	NamedEntity(Property^ parent, str^ level_name, str^ path_name, Properties^ children)
	//		: Entity(parent, level_name, path_name, children)
	//	{ }
	//
	//	Property^ Read(IReader^ reader, int length)
	//	{
	//		int last_pos = reader->Pos;
	//		LevelName = reader->ReadString();
	//		PathName = reader->ReadString();
	//		int count = reader->ReadInt();
	//		Children = gcnew Properties;
	//		for (int i = 0; i < count; ++i)
	//		{
	//			Name^ name = gcnew Name(this);
	//			Children->Add(name->Read(reader));
	//		}
	//		int bytes_read = reader->Pos - last_pos;
	//		if (bytes_read < 0)
	//			throw gcnew ReadException(reader, "Negative offset!");
	//		//if (bytes_read != length)
	//		//	Missing = ReadBytes(reader, length - bytes_read);
	//		Entity::Read(reader, length - bytes_read);
	//		return this;
	//	}
	//};
		public NamedEntity(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class Name : SimpleValueControl<str>
	{
		public Name(IElement parent, string label, object obj)
			: base(parent, label, null)
		{
			_name = obj as P.NamedEntity.Name;
			_value = _name.PathName;
		}

		protected override void _PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			_name.PathName = Value;

			base._PropertyChanged(_name.Parent, e);
		}

		private P.NamedEntity.Name _name;
	}

	internal class Object : Expando
	{
	//CLS(Object)
	//	//int Type;
	//	PUB_s(ClassName)
	//	PUB_s(LevelName)
	//	PUB_s(PathName)
	//	PUB_s(OuterPathName)
	//	PUB(EntityObj,Property^)
	//	READ
	//		//Type = 0;
	//		ClassName = reader->ReadString();
	//		LevelName = reader->ReadString();
	//		PathName = reader->ReadString();
	//		OuterPathName = reader->ReadString();
	//	READ_END
	//	Property^ ReadEntity(IReader^ reader)
	//	{
	//		int length = reader->ReadInt();
	//		Entity^ entity = gcnew Entity(this, nullptr, nullptr, nullptr);
	//		entity->Read(reader, length);
	//		EntityObj = entity;
	//		// EXPERIMENTAL
	//		//if self.Entity.Missing and Config.Get().deep_analysis.enabled:
	//		//	try:
	//		//		self.Entity.Private = self.__read_sub()
	//		//	except:
	//		//		self.Entity.Private = None
	//		//		# For now, just raise it to get more info on what went wrong
	//		//		if AppConfig.DEBUG:
	//		//			raise
	//		return this;
	//	}
	//	STR_(ClassName)
	//CLS_END
		public Object(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}

	internal class Actor : Expando
	{
	//CLS(Actor)
	//	//int Type;
	//	PUB_s(ClassName)
	//	PUB_s(LevelName)
	//	PUB_s(PathName)
	//	PUB_i(NeedTransform)
	//	PUB(Rotation,Quat^)
	//	PUB(Translate,Vector^)
	//	PUB(Scale,Scale^)
	//	PUB_i(WasPlacedInLevel)
	//	PUB(EntityObj,Property^)
	//	READ
	//		//Type = 1;
	//		ClassName = reader->ReadString();
	//		LevelName = reader->ReadString();
	//		PathName = reader->ReadString();
	//		NeedTransform = reader->ReadInt();
	//		Rotation = (Quat^) (gcnew Quat(this))->Read(reader);
	//		Translate = (Vector^) (gcnew Vector(this))->Read(reader);
	//		Scale = (Savegame::Properties::Scale^) (gcnew Savegame::Properties::Scale(this))->Read(reader);
	//		WasPlacedInLevel = reader->ReadInt();
	//	READ_END
	//	Property^ ReadEntity(IReader^ reader)
	//	{
	//		//if (ClassName == "/Script/FactoryGame.FGFoundationSubsystem")
	//		//	System::Diagnostics::Debugger::Break();

	//		int length = reader->ReadInt();
	//		NamedEntity^ entity = gcnew NamedEntity(this, nullptr, nullptr, nullptr);
	//		entity->Read(reader, length);
	//		EntityObj = entity;
	//		// EXPERIMENTAL
	//		//if self.Entity.Missing and Config.Get().deep_analysis.enabled:
	//		//	try:
	//		//		self.Entity.Private = self.__read_sub()
	//		//	except:
	//		//		self.Entity.Private = None
	//		//		# For now, just raise it to get more info on what went wrong
	//		//		if AppConfig.DEBUG:
	//		//			raise
	//		return this;
	//	}
	//	STR_(PathName)
	//CLS_END
		public Actor(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }
	}


	// Re-usable visualizers

	internal class ItemAmountList : ListViewControl<int?>
	{
		public ItemAmountList(List<P.ItemAmount> amounts)
			: base()
		{
			_amounts = amounts;
		}


		public override FrameworkElement Visual
		{
			get
			{
				if (Columns == null)
					_CreateColumns();
				if (Value == null)
					_CreateRows();
				return base.Visual;
			}
		}


		protected virtual RowConverter RowConv
		{
			get
			{
				if (_converter == null)
					_converter = new RowConverter(_amounts);
				return _converter;
			}
		}

		protected List<P.ItemAmount> _amounts;
		protected RowConverter       _converter;


		protected virtual void _CreateColumns()
		{
			Func<RowConverter.Parameter, Binding> binder = (param) => {
				return new Binding() {
					Converter = RowConv,
					ConverterParameter = param,
					Mode = BindingMode.OneWay,
				};
			};

			// Note that FrameworkElementFactory is somewhat dangerous, better use XamlLoader on a string value.
			//TODO: Convert to using TextWithIconDisplay
			FrameworkElementFactory factory = new FrameworkElementFactory(typeof(Image));
			factory.SetBinding(Image.SourceProperty, binder(RowConverter.Parameter.Icon));
			DataTemplate icon_tmpl = new DataTemplate() {
				VisualTree = factory,
			};
			factory = new FrameworkElementFactory(typeof(TextBlock));
			factory.SetBinding(TextBlock.TextProperty, binder(RowConverter.Parameter.Item));
			DataTemplate item_tmpl = new DataTemplate() {
				VisualTree = factory,
			};
			factory = new FrameworkElementFactory(typeof(TextBlock));
			factory.SetBinding(TextBlock.TextProperty, binder(RowConverter.Parameter.Amount));
			DataTemplate amount_tmpl = new DataTemplate() {
				VisualTree = factory,
			};
			//=> Move into Supplements.Helper ... or even CoreLib.Helpers?

			Columns = new ColumnDefinitions() {
				new ColumnDefinition("Slot", 30, binder(RowConverter.Parameter.Index), null, HorizontalAlignment.Right),
				new ColumnDefinition("", 32, null, icon_tmpl),
				new ColumnDefinition("Item", 200, null, item_tmpl),
				new ColumnDefinition("Amount", double.NaN, null, amount_tmpl, HorizontalAlignment.Right),
			};
		}

		protected virtual void _CreateRows()
		{
			Collection rows = new Collection();
			for (int i = 0; i < RowConv.RowCount; ++i)
				rows.Add(i);
			Value = rows;
		}


		protected class RowConverter : IValueConverter
		{
			internal enum Parameter { Index, Icon, Item, Amount };

			internal RowConverter(List<P.ItemAmount> items)
			{
				_Setup(items);
			}


			internal int RowCount { get { return _items.Count; } }


			public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			{
				_last_index = (int)value;
				switch ((Parameter)parameter)
				{
					case Parameter.Index:  return _GetIndex(_last_index);
					case Parameter.Icon:   return _GetIcon(_last_index);
					case Parameter.Item:   return _GetItem(_last_index);
					case Parameter.Amount: return _GetAmount(_last_index);
				}
				return null;
			}

			public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			{
				return Binding.DoNothing;
			}


			private void _Setup(List<P.ItemAmount> items)
			{
				_items = items;
				if (_items == null)
					throw new Exception("ItemAmountList: No items!");

				_last_index = -1;

				_version = MainWindow.CurrFile.Header.GetVersion();
			}


			#region Getter
			private object _GetIndex(int index)
			{
				return index.ToString();
			}

			private object _GetIcon(int index)
			{
				P.ItemAmount item = _items[index];
				string name = DetailsPanel.EMPTY;
				P.ObjectProperty itemclass = item.Value.Named("ItemClass") as P.ObjectProperty;
				if (itemclass != null && !str.IsNullOrEmpty(itemclass.PathName))
				{
					ItemTable.Item tbl_item = ItemTable.Find(itemclass.PathName);
					if (tbl_item != null)
						return tbl_item.Icon[_version];
				}
				return null;
			}

			private object _GetItem(int index)
			{
				P.ItemAmount item = _items[index];
				string name = DetailsPanel.EMPTY;
				P.ObjectProperty itemclass = item.Value.Named("ItemClass") as P.ObjectProperty;
				if (itemclass != null && !str.IsNullOrEmpty(itemclass.PathName))
				{
					ItemTable.Item tbl_item = ItemTable.Find(itemclass.PathName);
					if (tbl_item != null)
						name = tbl_item.DisplayName;
				}
				return name;
			}

			private object _GetAmount(int index)
			{
				P.ItemAmount item = _items[index];
				if (item.Value != null)
				{
					P.ValueProperty amount = item.Value.Named("amount") as P.ValueProperty;
					if (amount != null && amount.Value != null)
						return amount.Value.ToString();
				}
				return "";
			}
			#endregion


			private List<P.ItemAmount>   _items;
			private int                  _last_index;
			private VersionTable.Version _version;
		}
	}


	// Highly specialized visualizers
	//
	// Those will "condense" complex hierachies into a more readable form,
	// selected by .ClassName given.
	//

	internal abstract class SpecializedViewer : Expando
	{
		public SpecializedViewer(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{ }

		internal override void _CreateChilds(Dictionary<string,object> childs)
		{
			// Remove any "complex" child, such as sub-properties, lists, ...
			Dictionary<string,object> filtered = new Dictionary<string,object>();
			foreach (KeyValuePair<string,object> pair in childs)
			{
				if (!_excluded.Contains(pair.Key))
					filtered.Add(pair.Key, pair.Value);
			}
			base._CreateChilds(filtered);
		}

		// Will contain all property names which are to be handled in a more specialized way
		internal List<string> _excluded = new List<string>();
	}

	internal class FGInventoryComponent : SpecializedViewer
	{
		public FGInventoryComponent(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{
			_excluded.Add("EntityObj");
			Items = ItemTable.All;
		}

		internal ItemTable.Collection Items { get; set; }

		internal override void _CreateChilds()
		{
			base._CreateChilds();

			P.Object prop = Tag as P.Object;
			P.Entity entity = prop.EntityObj as P.Entity;

			// Add optional "mCanBeRearrange"
			P.BoolProperty rearrange = entity.Value.Named("mCanBeRearrange") as P.BoolProperty;
			if (rearrange != null)
				_childs.Add(MainFactory.Create(this, "Can be re-arranged", rearrange.Value));

			// Add optional "mAdjustedSizeDiff"
			P.ValueProperty inv_size = entity.Value.Named("mAdjustedSizeDiff") as P.ValueProperty;
			if (inv_size != null)
				_childs.Add(MainFactory.Create(this, "Extra slots", inv_size.Value));

			/*
				|-> [ArrayProperty] mArbitrarySlotSizes
				|  .InnerType = str:'IntProperty'
				|  .Value = list<Int32>(55):[0,]
			*/
			P.ArrayProperty arr = entity.Value.Named("mArbitrarySlotSizes") as P.ArrayProperty;
			if (arr == null)
				return;//TODO:
			LVC.Collection rows = new LVC.Collection();
			for (int i = 0; i < (arr.Value as int[]).Length; ++i)
				rows.Add(i);

			RowConverter converter = new RowConverter(this, prop);
			Func<RowConverter.Parameter, Binding> binder = (param) => {
				return new Binding() {
					Converter = converter,
					ConverterParameter = param,
				};
			};

			// Note that FrameworkElementFactory is somewhat dangerous, better use XamlLoader on a string value.
			FrameworkElementFactory factory = new FrameworkElementFactory(typeof(ContentControl));
			factory.SetBinding(ContentProperty, binder(RowConverter.Parameter.Item));
			DataTemplate item_tmpl = new DataTemplate() {
				VisualTree = factory,
			};
			factory = new FrameworkElementFactory(typeof(ContentControl));
			factory.SetBinding(ContentProperty, binder(RowConverter.Parameter.Count));
			DataTemplate count_tmpl = new DataTemplate() {
				VisualTree = factory,
			};
			//=> Move into Supplements.Helper ... or even CoreLib.Helpers?

			LVC.ColumnDefinitions columns = new LVC.ColumnDefinitions() {
				new LVC.ColumnDefinition("Slot", 30, binder(RowConverter.Parameter.Index), null, HorizontalAlignment.Right),
				new LVC.ColumnDefinition("Item", 200, null, item_tmpl),
				new LVC.ColumnDefinition("Count", double.NaN, null, count_tmpl, HorizontalAlignment.Right),
				new LVC.ColumnDefinition("Limit", 35, binder(RowConverter.Parameter.Limit), null, HorizontalAlignment.Right),
				new LVC.ColumnDefinition("Allowed", 200, binder(RowConverter.Parameter.Allowed)),
			};

			_lvc = new LVC() {
				Columns = columns,
				Value = rows,
			};

			_childs.Add(_lvc);
		}

		private ItemCombobox.Collection ComboboxItems
		{
			get
			{
				if (_items == null)
					_items = ItemCombobox.Collection.FromItemTable(Items);
				return _items;
			}
		}
		private ItemCombobox.Collection _items;

		protected class LVC : ListViewControl<int?> { }
		protected LVC _lvc;

		protected override void _PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (sender is int)
			{
				// Coming from RowConverter
				_lvc.Value.RefreshRow((int)sender);
			}

			base._PropertyChanged(sender, e);
		}

		internal class RowConverter : IValueConverter
		{
			internal enum Parameter { Index, Item, Count, Limit, Allowed };

			internal RowConverter(FGInventoryComponent owner, P.Object prop)
			{
				_owner = owner;
				_Setup(prop);
			}


			public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			{
				_last_index = (int)value;
				switch ((Parameter)parameter)
				{
					case Parameter.Index:   return _GetIndex(_last_index);
					case Parameter.Item:    return _GetItem(_last_index);
					case Parameter.Count:   return _GetCount(_last_index);
					case Parameter.Limit:   return _GetLimit(_last_index);
					case Parameter.Allowed: return _GetAllowed(_last_index);
				}
				return null;
			}

			public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			{
				// Done by monitoring change events, writing any change into InventoryItem directly
				return Binding.DoNothing;
			}


			private void _Setup(P.Object prop)
			{
				P.Entity entity = prop.EntityObj as P.Entity;

				P.ArrayProperty arr;
				P.StructProperty stru;

				/*
					|-> [ArrayProperty] mInventoryStacks
					|  .InnerType = str:'StructProperty'
					|  .Value =
					|	-> [StructProperty] mInventoryStacks
					|	  .Value =
					|		/ List with 55 elements:
					|		|-> [InventoryStack].Value[0-0]
				*/
				arr = entity.Value.Named("mInventoryStacks") as P.ArrayProperty;
				if (arr == null)
					throw new Exception("FGInventoryComponent: No inventory stack!");
				stru = arr.Value as P.StructProperty;
				if (stru == null || stru.Value == null)
					throw new Exception("FGInventoryComponent: Invalid inventory stack!");
				_stacks = stru.Value as P.Properties;

				/*
					|-> [ArrayProperty] mArbitrarySlotSizes
					|  .InnerType = str:'IntProperty'
					|  .Value = list<Int32>(55):[0,]
				*/
				arr = entity.Value.Named("mArbitrarySlotSizes") as P.ArrayProperty;
				if (arr == null)
					throw new Exception("FGInventoryComponent: No slot sizes!");
				_limits = arr.Value as int[];

				/*
					|-> [ArrayProperty] mAllowedItemDescriptors
					|  .InnerType = str:'ObjectProperty'
					|  .Value =
					|	/ List with 55 elements:
					|	|-> [ObjectProperty] 
					|	|  .LevelName = <empty>
					|	|  .PathName = <empty>
					|	|  .Length = Int32:0
					|	|  .Index = Int32:0
				*/
				arr = entity.Value.Named("mAllowedItemDescriptors") as P.ArrayProperty;
				if (arr == null)
					throw new Exception("FGInventoryComponent: No allowed item descriptors!");
				_allowed = arr.Value as P.Properties;

				if (_stacks.Count != _limits.Length || _limits.Length != _allowed.Count)
					throw new Exception("FGInventoryComponent: Mismatch in collection sizes!");

				_version = MainWindow.CurrFile.Header.GetVersion();

				_cache = new List<P.InventoryItem>(_limits.Length);
				for (int i = 0; i < _limits.Length; ++i)
					_cache.Add(null);

				_last_index = -1;
			}

			private P.InventoryItem _GetInventoryItem(int index)
			{
				if (index < 0 || index >= _cache.Count)
					return null;
				if (_cache[index] == null)
				{
					P.Properties vals = (_stacks[index] as P.InventoryStack).Value as P.Properties;
					if (vals.Count == 1)
					{
						P.StructProperty stru = vals[0] as P.StructProperty;
						if (stru.Name != null && stru.Name.ToString() == "Item")
							_cache[index] = stru.Value as P.InventoryItem;
					}
				}
				return _cache[index];
			}

			#region Getter
			private object _GetIndex(int index)
			{
				return index.ToString();
			}

			private object _GetItem(int index)
			{
				P.InventoryItem item = _GetInventoryItem(index);
				if (item != null)
				{
					ItemCombobox.Collection items = null;
					P.ObjectProperty limit = _allowed[index] as P.ObjectProperty;
					if (limit != null)
					{
						if (!str.IsNullOrEmpty(limit.PathName)
							&& limit.PathName.ToString() != "/Script/FactoryGame.FGItemDescriptor") // Placeholder?
						{
							ItemTable.Item limit_item = ItemTable.Find(limit.PathName);
							if (limit_item != null)
							{
								// Allow only the item listed ... or <empty>
								items = ItemCombobox.Collection.FromSingleItem(limit_item);
							}
						}
					}
					if (items == null)
						items = _owner.ComboboxItems;

					ItemCombobox ctrl = new ItemCombobox(item.ItemName, item.PathName, items) {
						Tag = index,
					};
					ctrl.PropertyChanged += Item_Changed;

					return ctrl;
				}
				return null;
			}
			private void Item_Changed(object sender, PropertyChangedEventArgs e)
			{
				ItemCombobox ctrl = sender as ItemCombobox;
				int index = (int)ctrl.Tag;

				_owner._PropertyChanged(index, null);
			}

			private object _GetCount(int index)
			{
				P.InventoryItem item = _GetInventoryItem(index);
				if (item != null && item.Value != null && item.Value is P.IntProperty)
				{
					P.IntProperty int_prop = item.Value as P.IntProperty;
					if (int_prop != null && int_prop.Value != null)
					{
						int? upper = (int?)_GetLimit(index);
						IntControl ctrl = new IntControl() {
							Width = 50,
							Margin = new Thickness(0),
							HorizontalAlignment = HorizontalAlignment.Right,
							Tag = index,
							LowerLimit = 1,
							UpperLimit = upper,
						};
						ctrl.Value = (int)int_prop.Value;
						ctrl.PropertyChanged += Count_Changed;
						return ctrl;
					}
				}
				return null;
			}
			private void Count_Changed(object sender, PropertyChangedEventArgs e)
			{
				IntControl ctrl = sender as IntControl;
				int count = ctrl.Value;
				int index = (int)ctrl.Tag;

				P.InventoryItem item = _GetInventoryItem(index);
				if (item != null && item.Value != null && item.Value is P.IntProperty)
				{
					P.IntProperty int_prop = item.Value as P.IntProperty;
					if (int_prop != null && int_prop.Value != null && (int)int_prop.Value != count)
					{
						int_prop.Value = count;
						_owner._PropertyChanged(null/*index*/, null);
					}
				}
			}

			private object _GetLimit(int index)
			{
				if (index < 0 || index >= _limits.Length)
					return null;
				int limit = _limits[index];
				if (limit == 0)
				{
					P.InventoryItem item = _GetInventoryItem(index);
					if (item != null && !str.IsNullOrEmpty(item.ItemName))
					{
						ItemTable.Item it_item = ItemTable.Find(item.ItemName);
						if (it_item != null)
							limit = (int)it_item.StackSize;
					}
				}
				return (limit != 0) ? (object)limit : null;
			}

			private object _GetAllowed(int index)
			{
				if (index < 0 || index >= _allowed.Count)
					return null;
				string item_limit = DetailsPanel.EMPTY;
				P.ObjectProperty limit = _allowed[index] as P.ObjectProperty;
				if (limit != null)
				{
					if (!str.IsNullOrEmpty(limit.PathName)
						&& limit.PathName.ToString() != "/Script/FactoryGame.FGItemDescriptor") // Placeholder?
					{
						item_limit = limit.PathName.LastName();
						if (item_limit != null && Translate.Has(item_limit))
							item_limit = Translate._(item_limit);
					}
				}
				return item_limit;
			}
			#endregion

			private FGInventoryComponent    _owner;
			private P.Properties            _stacks;
			private int[]                   _limits;
			private P.Properties            _allowed;
			private VersionTable.Version    _version;
			private List<P.InventoryItem>   _cache;
			private int                     _last_index;
		}
	}

	internal class FGInventoryComponentTrash : FGInventoryComponent
	{
		public FGInventoryComponentTrash(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{
			Items = ItemTable.Empty;
		}
	}

	internal class FGInventoryComponentEquipment : FGInventoryComponent
	{
		public FGInventoryComponentEquipment(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{
			// Distinguish between arms + back!
			Items = ItemTable.Equipment;
		}

		internal override void _CreateChilds()
		{
			P.Object prop = Tag as P.Object;
			P.Entity entity = prop.EntityObj as P.Entity;

			base._CreateChilds();

			// Let 'mActiveEquipmentIndex' take precedence over 'mEquipmentInSlot'
			bool handled = false;

			ItemCombobox.Collection coll = null;
			IEnumerable<P.InventoryItem> items = null;
			P.ArrayProperty arr = entity.Value.Named("mInventoryStacks") as P.ArrayProperty;
			if (arr != null && arr.Value != null && arr.Value is P.StructProperty)
			{
				P.StructProperty stru = arr.Value as P.StructProperty;
				P.Properties inv = stru.Value as P.Properties;
				coll = ItemCombobox.Collection.FromInventoryStacks(inv);
				items = inv
					.ListOf<P.InventoryStack>()
					.Where(stack => (stack.Value != null) && (stack.Value.Count == 1))
					.Select(stack => (stack.Value[0] as P.StructProperty).Value as P.InventoryItem)
					;
			}

			//TODO: Show active control even when no value found, creating new value 'on the fly'

			P.IntProperty index_prop = entity.Value.Named("mActiveEquipmentIndex") as P.IntProperty;
			if (index_prop != null && index_prop.Value is int)
			{
				int index = (int) index_prop.Value;
				if (items != null && items.Count() >= index)
				{
					P.InventoryItem item = items.ToList()[index];
					if (item != null)
					{
						handled = true;
						_itemctrl = new ItemControl(this, "Active equipment", item, coll);
						_childs.Insert(_childs.IndexOf(_lvc), _itemctrl);
					}
				}
			}

			if (!handled)
			{
				P.ObjectProperty equipped = entity.Value.Named("mEquipmentInSlot") as P.ObjectProperty;
				if (equipped != null)
				{
					// Select real instance from save using PathName
					P.Actor equipment = MainWindow.CurrFile.Objects.FindByPathName(equipped.PathName) as P.Actor;
					if (equipment != null)
					{
						str class_name = equipment.ClassName;
						P.InventoryItem item = items
							.FirstOrDefault(inv_item => (inv_item.ItemName == class_name))
							;
						if (item != null)
						{
							_itemctrl = new ItemControl(this, "Equipment in slot", item, coll);
							_childs.Insert(_childs.IndexOf(_lvc), _itemctrl);
						}
					}
				}
			}
		}

		protected override void _PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			base._PropertyChanged(sender, e);

			if (sender is int)
			{
				P.Object prop = Tag as P.Object;
				P.Entity entity = prop.EntityObj as P.Entity;

				ItemCombobox.Collection coll = null;
				P.ArrayProperty arr = entity.Value.Named("mInventoryStacks") as P.ArrayProperty;
				if (arr != null && arr.Value != null && arr.Value is P.StructProperty)
				{
					P.StructProperty stru = arr.Value as P.StructProperty;
					P.Properties inv = stru.Value as P.Properties;
					coll = ItemCombobox.Collection.FromInventoryStacks(inv);
					_itemctrl.AvailableItems = coll;
				}
			}
		}

		private ItemControl _itemctrl;
	}

	internal class FGMapManager : SpecializedViewer
	{
		public FGMapManager(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{
			_excluded.Add("NeedTransform");
			_excluded.Add("Rotation");
			_excluded.Add("Translate");
			_excluded.Add("Scale");
			_excluded.Add("WasPlacedInLevel");
			_excluded.Add("EntityObj");
		}

		internal override void _CreateChilds()
		{
			base._CreateChilds();

			P.Actor prop = Tag as P.Actor;
			P.Entity entity = prop.EntityObj as P.Entity;

			if (entity.Value.Count == 1)
			{
				P.ArrayProperty arr = entity.Value[0] as P.ArrayProperty;
				if (arr != null && !str.IsNullOrEmpty(arr.Name) && arr.Name.ToString() == "mFogOfWarRawData")
				{
					IElement element = new ImageControl(this, arr.Name.ToString(), arr.Value);
					_childs.Add(element);
				}
			}
		}
	}

	internal class FGFoundationSubsystem : SpecializedViewer
	{
		public FGFoundationSubsystem(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{
			_excluded.Add("NeedTransform");
			_excluded.Add("Rotation");
			_excluded.Add("Translate");
			_excluded.Add("Scale");
			_excluded.Add("WasPlacedInLevel");
			_excluded.Add("EntityObj");
		}

		internal override void _CreateChilds()
		{
			base._CreateChilds();

			P.Actor prop = Tag as P.Actor;
			P.Entity entity = prop.EntityObj as P.Entity;

			if (entity.Value.Count != 1)
				return;//TODO:
			P.MapProperty map_prop = entity.Value[0] as P.MapProperty;
			if (map_prop == null || str.IsNullOrEmpty(map_prop.Name) || map_prop.Name.ToString() != "mBuildings")
				return;//TODO:

			foreach (KeyValuePair<object, object> pair in map_prop.Value)
			{
				P.PropertyList props = pair.Value as P.PropertyList;
				List<P.ArrayProperty> entries = props.Value.ListOf<P.ArrayProperty>();
				if (entries == null || entries.Count != 1)
					continue;//TODO:
				P.ArrayProperty entry = entries[0];
				if (entry == null || entry.Value == null || str.IsNullOrEmpty(entry.Name) || entry.Name.ToString() != "Buildables")
					continue;//TODO:
				P.Properties objects = entry.Value as P.Properties;
				if (objects == null)
					continue;//TODO:

				RowConverter converter = new RowConverter(this, objects);
				Func<RowConverter.Parameter, Binding> binder = (param) => {
					return new Binding() {
						Converter = converter,
						ConverterParameter = param,
					};
				};

				LVC.Collection rows = new LVC.Collection();
				for (int i = 0; i < converter.RowCount; ++i)
					rows.Add(i);

				LVC.ColumnDefinitions columns = new LVC.ColumnDefinitions() {
					new LVC.ColumnDefinition("#", 50, binder(RowConverter.Parameter.Index), null, HorizontalAlignment.Right),
					new LVC.ColumnDefinition("Building", 300, binder(RowConverter.Parameter.Text)),//TODO: Make editable using template
				};

				LVC lvc = new LVC() {
					Columns = columns,
					Value = rows,
				};

				string label = string.Format("Chunk {0} ({1:#,#0} build{2})", 
					pair.Key, rows.Count, rows.Count==1 ? "" : "s");
				Expando expando = new Expando(this, label, null);
				expando._childs.Add(lvc);

				_childs.Add(expando);
			}
		}

		private class LVC : ListViewControl<int?> { }

		private class RowConverter : IValueConverter
		{
			internal enum Parameter { Index, Text };

			internal RowConverter(FGFoundationSubsystem owner, P.Properties objects)
			{
				_owner = owner;
				_Setup(objects);
			}


			internal int RowCount { get { return _objects.Count; } }


			public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			{
				_last_index = (int)value;
				switch ((Parameter)parameter)
				{
					case Parameter.Index: return _GetIndex(_last_index);
					case Parameter.Text:  return _GetText(_last_index);
				}
				return null;
			}

			public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			{
				//TODO: Update 'PathName' and inform owner
				return Binding.DoNothing;
			}


			private void _Setup(P.Properties objects)
			{
				_objects = objects.ListOf<P.ObjectProperty>();
			}


			#region Getter
			private object _GetIndex(int index)
			{
				return index.ToString();
			}

			private object _GetText(int index)
			{
				P.ObjectProperty obj = _objects[index];
				return !str.IsNullOrEmpty(obj.PathName) ? obj.PathName.LastName() : DetailsPanel.EMPTY;
			}
			#endregion


			private FGFoundationSubsystem  _owner;
			private List<P.ObjectProperty> _objects;
			private int                    _last_index;
		}
	}

	internal class FGRecipeManager : SpecializedViewer
	{
		public FGRecipeManager(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{
			_excluded.Add("NeedTransform");
			_excluded.Add("Rotation");
			_excluded.Add("Translate");
			_excluded.Add("Scale");
			_excluded.Add("WasPlacedInLevel");
			_excluded.Add("EntityObj");
		}

		internal override void _CreateChilds()
		{
			base._CreateChilds();

			P.Actor prop = Tag as P.Actor;
			P.NamedEntity entity = prop.EntityObj as P.NamedEntity;

			if (entity.Value.Count != 1)
				return;//TODO:
			P.ArrayProperty arr = entity.Value[0] as P.ArrayProperty;
			if (arr == null || str.IsNullOrEmpty(arr.Name) || arr.Name.ToString() != "mAvailableRecipes")
				return;//TODO:

			P.Properties recipes = arr.Value as P.Properties;
			if (recipes == null)
				return;//TODO:

			RowConverter converter = new RowConverter(this, recipes);
			Func<RowConverter.Parameter, Binding> binder = (param) => {
				return new Binding() {
					Converter = converter,
					ConverterParameter = param,
				};
			};

			LVC.Collection rows = new LVC.Collection();
			for (int i = 0; i < converter.RowCount; ++i)
				rows.Add(i);

			LVC.ColumnDefinitions columns = new LVC.ColumnDefinitions() {
				new LVC.ColumnDefinition("#", 35, binder(RowConverter.Parameter.Index), null, HorizontalAlignment.Right),
				new LVC.ColumnDefinition("Recipe", 300, binder(RowConverter.Parameter.Recipe)),//TODO: Make editable using template
			};

			LVC lvc = new LVC() {
				Columns = columns,
				Value = rows,
			};

			_childs.Add(lvc);
		}

		private class LVC : ListViewControl<int?> { }

		private class RowConverter : IValueConverter
		{
			internal enum Parameter { Index, Recipe };

			internal RowConverter(FGRecipeManager owner, P.Properties recipes)
			{
				_owner = owner;
				_Setup(recipes);
			}


			internal int RowCount { get { return _recipes.Count; } }


			public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			{
				_last_index = (int)value;
				switch ((Parameter)parameter)
				{
					case Parameter.Index:  return _GetIndex(_last_index);
					case Parameter.Recipe: return _GetRecipe(_last_index);
				}
				return null;
			}

			public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			{
				//TODO: Update 'PathName' and inform owner
				return Binding.DoNothing;
			}


			private void _Setup(P.Properties recipes)
			{
				_recipes = recipes.ListOf<P.ObjectProperty>();
			}


			#region Getter
			private object _GetIndex(int index)
			{
				return index.ToString();
			}

			private object _GetRecipe(int index)
			{
				P.ObjectProperty obj = _recipes[index];
				string name = DetailsPanel.EMPTY;
				if (!str.IsNullOrEmpty(obj.PathName))
				{
					name = obj.PathName.LastName();
					if (Translate.Has(name))
						name = Translate._(name);
				}
				return name;
			}
			#endregion


			private FGRecipeManager        _owner;
			private List<P.ObjectProperty> _recipes;
			private int                    _last_index;
		}
	}

	internal class BP_GamePhaseManager_C : SpecializedViewer
	{
		public BP_GamePhaseManager_C(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{
			_excluded.Add("NeedTransform");
			_excluded.Add("Rotation");
			_excluded.Add("Translate");
			_excluded.Add("Scale");
			_excluded.Add("WasPlacedInLevel");
			_excluded.Add("EntityObj");
		}

		internal override void _CreateChilds()
		{
			base._CreateChilds();

			P.Actor prop = Tag as P.Actor;
			P.NamedEntity entity = prop.EntityObj as P.NamedEntity;

			P.ValueProperty mGamePhase = entity.Value.Named("mGamePhase") as P.ValueProperty;
			_childs.Add(MainFactory.Create(this, "Game phase", mGamePhase != null ? mGamePhase.Value : "?"));

			P.ArrayProperty arr;
			P.StructProperty stru;

			/*
				|-> [ArrayProperty] mGamePhaseCosts
				|  .InnerType = str:'StructProperty'
				|  .Name = str:'mGamePhaseCosts'
				|  .Length = Int32:1.946
				|  .Index = Int32:0
				|  .Value =
				|	-> [StructProperty] mGamePhaseCosts
				|	  .Value =
				|		/ List with 3 elements:
				|		|-> [PhaseCost].Value[0-1]
			*/
			arr = entity.Value.Named("mGamePhaseCosts") as P.ArrayProperty;
			if (arr == null)
				return;//TODO:
			stru = arr.Value as P.StructProperty;
			if (stru == null || stru.Value == null)
				return;//TODO:
			List<P.PhaseCost> costs = stru.Value.ListOf<P.PhaseCost>();

			//TODO: Re-order using same order as GamePhase enum
			//TODO: Add missing "phases" (disabled expando?)
			for (int i = 0; i < costs.Count(); ++i)
			{
				/*
				|		|-> [PhaseCost].Value[0-1]
				|		|  .Value =
				|		|	/ List with 2 elements:
				|		|	|-> [ByteProperty] gamePhase
				|		|	|  .Unknown = str:'EGamePhase'
				|		|	|  .Name = str:'gamePhase'
				|		|	|  .Length = Int32:16
				|		|	|  .Index = Int32:0
				|		|	|  .Value = str:'EGP_MidGame'
				|		|	|-> [ArrayProperty] Cost
				|		|	|  .InnerType = str:'StructProperty'
				|		|	|  .Name = str:'Cost'
				|		|	|  .Length = Int32:438
				|		|	|  .Index = Int32:0
				|		|	|  .Value =
				|		|	|	-> [StructProperty] Cost
				|		|	|	  .Unknown = list(17):[0,]
				|		|	|	  .Name = str:'Cost'
				|		|	|	  .Length = Int32:366
				|		|	|	  .Index = Int32:0
				|		|	|	  .Value =
				|		|	|		/ List with 2 elements:
				|		|	|		|-> [ItemAmount].Value[0-1]
				*/
				P.PhaseCost phasecost = costs[i];
				if (phasecost.Value == null)
					continue;//TODO:

				P.ValueProperty gamephase = phasecost.Value.Named("gamePhase") as P.ValueProperty;
				if (gamephase == null || gamephase.Value == null)
					continue;//TODO:

				arr = phasecost.Value.Named("Cost") as P.ArrayProperty;
				if (arr == null)
					continue;//TODO:
				P.StructProperty cost = arr.Value as P.StructProperty;
				if (cost == null || cost.Value == null)
					continue;//TODO:
				List<P.ItemAmount> items = cost.Value.ListOf<P.ItemAmount>();
				if (items == null)
					continue;//TODO:

				ItemAmountList item_list = new ItemAmountList(items);

				string label = string.Format("Phase: {0}", gamephase.Value);
				Expando expando = new Expando(this, label, null);
				expando._childs.Add(item_list);
				expando.IsExpanded = true; // Ok to expand those

				_childs.Add(expando);
			}
		}

		private class LVC : ListViewControl<int?> { }

		private class RowConverter : IValueConverter
		{
			internal enum Parameter { Index, Icon, Item, Amount };

			internal RowConverter(P.PhaseCost phasecost)
			{
				_Setup(phasecost);
			}


			internal int RowCount { get { return _items.Count; } }


			public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			{
				_last_index = (int)value;
				switch ((Parameter)parameter)
				{
					case Parameter.Index:  return _GetIndex(_last_index);
					case Parameter.Icon:   return _GetIcon(_last_index);
					case Parameter.Item:   return _GetItem(_last_index);
					case Parameter.Amount: return _GetAmount(_last_index);
				}
				return null;
			}

			public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			{
				return Binding.DoNothing;
			}


			private void _Setup(P.PhaseCost phasecost)
			{
				P.ValueProperty gamephase = phasecost.Value.Named("gamePhase") as P.ValueProperty;
				if (gamephase == null || gamephase.Value == null)
					throw new Exception("BP_GamePhaseManager_C: No game phase!");

				P.ArrayProperty arr = phasecost.Value.Named("Cost") as P.ArrayProperty;
				if (arr == null)
					throw new Exception("BP_GamePhaseManager_C: No costs!");
				P.StructProperty cost = arr.Value as P.StructProperty;
				if (cost == null || cost.Value == null)
					throw new Exception("BP_GamePhaseManager_C: Invalid costs structure!");
				_items = (cost.Value as List<P.Property>).ListOf<P.ItemAmount>();
				if (_items == null)
					throw new Exception("BP_GamePhaseManager_C: No item amount!");

				_last_index = -1;

				_version = MainWindow.CurrFile.Header.GetVersion();
			}


			#region Getter
			private object _GetIndex(int index)
			{
				return index.ToString();
			}

			private object _GetIcon(int index)
			{
				P.ItemAmount item = _items[index];
				string name = DetailsPanel.EMPTY;
				P.ObjectProperty itemclass = item.Value.Named("ItemClass") as P.ObjectProperty;
				if (itemclass != null && !str.IsNullOrEmpty(itemclass.PathName))
				{
					ItemTable.Item tbl_item = ItemTable.Find(itemclass.PathName);
					if (tbl_item != null)
						return tbl_item.Icon[_version];
				}
				return null;
			}

			private object _GetItem(int index)
			{
				P.ItemAmount item = _items[index];
				string name = DetailsPanel.EMPTY;
				P.ObjectProperty itemclass = item.Value.Named("ItemClass") as P.ObjectProperty;
				if (itemclass != null && !str.IsNullOrEmpty(itemclass.PathName))
				{
					ItemTable.Item tbl_item = ItemTable.Find(itemclass.PathName);
					if (tbl_item != null)
						name = tbl_item.DisplayName;
				}
				return name;
			}

			private object _GetAmount(int index)
			{
				P.ItemAmount item = _items[index];
				if (item.Value != null)
				{
					P.ValueProperty amount = item.Value.Named("amount") as P.ValueProperty;
					if (amount != null && amount.Value != null)
						return (int)amount.Value;
				}
				return null;
			}
			#endregion


			private List<P.ItemAmount>   _items;
			private int                  _last_index;
			private VersionTable.Version _version;
		}
	}

	internal class BP_ResearchManager_C : SpecializedViewer
	{
		public BP_ResearchManager_C(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{
			_excluded.Add("NeedTransform");
			_excluded.Add("Rotation");
			_excluded.Add("Translate");
			_excluded.Add("Scale");
			_excluded.Add("WasPlacedInLevel");
			_excluded.Add("EntityObj");
		}

		internal override void _CreateChilds()
		{
			base._CreateChilds();

			P.Actor prop = Tag as P.Actor;
			P.NamedEntity entity = prop.EntityObj as P.NamedEntity;

			// Add ListView
			P.ArrayProperty arr;
			P.StructProperty stru;

			/*
				|-> [ArrayProperty] mResearchCosts
				|  .InnerType = str:'StructProperty'
				|  .Name = str:'mResearchCosts'
				|  .Length = Int32:9.660
				|  .Index = Int32:0
				|  .Value =
				|	-> [StructProperty] mResearchCosts
				|	  .Unknown = list(17):[0,]
				|	  .Name = str:'mResearchCosts'
				|	  .Length = Int32:9.576
				|	  .Index = Int32:0
				|	  .Value =
				|		/ List with 21 elements:
				|		|-> [ResearchCost].Value[0-1]
			*/
			arr = entity.Value.Named("mResearchCosts") as P.ArrayProperty;
			if (arr == null)
				return;//TODO:
			stru = arr.Value as P.StructProperty;
			if (stru == null || stru.Value == null)
				return;//TODO:

			RowConverter converter = new RowConverter(stru.Value as P.Properties);
			Func<RowConverter.Parameter, Binding> binder = (param) => {
				return new Binding() {
					Converter = converter,
					ConverterParameter = param,
				};
			};

			LVC.Collection rows = new LVC.Collection();
			for (int i = 0; i < converter.RowCount; ++i)
				rows.Add(i);

			// Note that FrameworkElementFactory is somewhat dangerous, better use XamlLoader on a string value.
			FrameworkElementFactory factory = new FrameworkElementFactory(typeof(TextBlock));
			factory.SetBinding(TextBlock.TextProperty, binder(RowConverter.Parameter.Research));
			DataTemplate research_tmpl = new DataTemplate() {
				VisualTree = factory,
			};
			factory = new FrameworkElementFactory(typeof(ContentControl));
			factory.SetBinding(ContentControl.ContentProperty, binder(RowConverter.Parameter.Item));
			DataTemplate item_tmpl = new DataTemplate() {
				VisualTree = factory,
			};
			factory = new FrameworkElementFactory(typeof(TextBlock));
			factory.SetBinding(TextBlock.TextProperty, binder(RowConverter.Parameter.Amount));
			DataTemplate amount_tmpl = new DataTemplate() {
				VisualTree = factory,
			};
			//=> Move into Supplements.Helper ... or even CoreLib.Helpers?

			LVC.ColumnDefinitions columns = new LVC.ColumnDefinitions() {
				new LVC.ColumnDefinition("#", 30, binder(RowConverter.Parameter.Index), null, HorizontalAlignment.Right),
				new LVC.ColumnDefinition("Research", 200, null, research_tmpl),
				new LVC.ColumnDefinition("Item", 200, null, item_tmpl),
				new LVC.ColumnDefinition("Amount", double.NaN, null, amount_tmpl, HorizontalAlignment.Right),
			};

			LVC lvc = new LVC() {
				Columns = columns,
				Value = rows,
			};

			_childs.Add(lvc);
			/*
			Expando expando = new Expando(this, "Research", null);
			expando._childs.Add(lvc);

			_childs.Add(expando);
			*/
		}

		private class LVC : ListViewControl<int?> { }

		private class RowConverter : IValueConverter
		{
			internal enum Parameter { Index, Research, Item, Amount };

			internal RowConverter(P.Properties research)
			{
				_Setup(research);
			}


			internal int RowCount { get { return _research.Count; } }


			public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			{
				_last_index = (int)value;
				switch ((Parameter)parameter)
				{
					case Parameter.Index:    return _GetIndex(_last_index);
					case Parameter.Research: return _GetResearch(_last_index);
					case Parameter.Item:     return _GetItem(_last_index);
					case Parameter.Amount:   return _GetAmount(_last_index);
				}
				return null;
			}

			public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			{
				return Binding.DoNothing;
			}


			private void _Setup(P.Properties research)
			{
				_research = research.ListOf<P.ResearchCost>();

				_last_index = -1;

				_version = MainWindow.CurrFile.Header.GetVersion();
			}

			private P.ItemAmount _GetItemAmount(int index)
			{
				P.ResearchCost researchcost = _research[index];
				string name = DetailsPanel.EMPTY;
				P.ArrayProperty arr = researchcost.Value.Named("Cost") as P.ArrayProperty;
				if (arr != null)
				{
					P.StructProperty cost = arr.Value as P.StructProperty;
					if (cost != null && cost.Value != null)
					{
						List<P.ItemAmount> items = cost.Value.ListOf<P.ItemAmount>();
						if (items != null && items.Count == 1) // There's only one item per research
							return items[0];
					}
				}
				return null;
			}

			#region Getter
			private object _GetIndex(int index)
			{
				return index.ToString();
			}

			private object _GetResearch(int index)
			{
				P.ResearchCost researchcost = _research[index];
				string name = DetailsPanel.EMPTY;
				P.ObjectProperty recipe = researchcost.Value.Named("researchRecipe") as P.ObjectProperty;
				//TODO: Access ResearchTable instance
				if (recipe != null && !str.IsNullOrEmpty(recipe.PathName))
				{
					name = recipe.PathName.LastName();
					if (Translate.Has(name))
						name = Translate._(name);
				}
				return name;
			}

			private object _GetItem(int index)
			{
				P.ItemAmount item_amount = _GetItemAmount(index);
				if (item_amount != null)
				{
					P.ObjectProperty itemclass = item_amount.Value.Named("ItemClass") as P.ObjectProperty;
					if (itemclass != null && !str.IsNullOrEmpty(itemclass.PathName))
					{
						ItemTable.Item tbl_item = ItemTable.Find(itemclass.PathName);
						if (tbl_item != null)
							return new TextWithIconDisplay(tbl_item);
					}
				}
				return null;
			}

			private object _GetAmount(int index)
			{
				P.ItemAmount item_amount = _GetItemAmount(index);
				if (item_amount != null)
				{
					P.ValueProperty amount = item_amount.Value.Named("amount") as P.ValueProperty;
					if (amount != null && amount.Value != null)
						return amount.Value;
				}
				return null;
			}
			#endregion


			private List<P.ResearchCost> _research;
			private int                  _last_index;
			private VersionTable.Version _version;
		}
	}

	internal class BP_BuildableSubsystem_C : SpecializedViewer
	{
		public BP_BuildableSubsystem_C(IElement parent, string label, object obj)
			: base(parent, label, obj)
		{
			_excluded.Add("NeedTransform");
			_excluded.Add("Rotation");
			_excluded.Add("Translate");
			_excluded.Add("Scale");
			_excluded.Add("WasPlacedInLevel");
			_excluded.Add("EntityObj");
		}

		internal override void _CreateChilds()
		{
			base._CreateChilds();

			P.Actor actor = Tag as P.Actor;
			_childs.Add(new FGWorldSettings(this, "", actor.EntityObj));
		}

		internal class FGWorldSettings : SpecializedViewer
		{
			internal FGWorldSettings(IElement parent, string label, object obj)
				: base(parent, label, obj)
			{ }

			internal override void _CreateVisual()
			{
				base._CreateVisual();
				(Visual as Expando).IsExpanded = true;
			}

			internal override void _CreateChilds()
			{
				P.NamedEntity entity = Tag as P.NamedEntity;
				if (entity == null)
					return;
				//var values = entity.Value.Names();

				var colors = entity.Value
					.Where(p => p is P.StructProperty && !str.IsNullOrEmpty((p as P.StructProperty).Name))
					.Cast<P.StructProperty>()
					.Where(s => s.Name.ToString() == "mColorSlotsPrimary")
					;
				foreach (P.StructProperty stru in colors)
				{
					_childs.Add(new ColorSlot(this, null, stru.Index));
				}

			}
		}
	}


	// More specialized visualizers
	//
	// Those are to be created explicitely using a Show... method!
	// Used for things like living entities, vehicles and such
	//

	internal class LivingEntity : Expando
	{
		public LivingEntity(LivingTree.Living living)
			: base(null, living.Title, living)
		{ }

		internal override void _CreateChilds()
		{
			LivingTree.Living living = Tag as LivingTree.Living;
			P.NamedEntity ent_named = living.Entity.EntityObj as P.NamedEntity;
			P.Actor blueprint = living.Blueprint;
			P.NamedEntity bp_named = (living.IsPlayer) ? blueprint.EntityObj as P.NamedEntity : null;

			P.Property prop;
			//P.Properties props;
			P.ArrayProperty arr;
			P.Object obj;
			P.Entity entity;
			IElement element;

			string[] excluded = new string[] { "ClassName", "LevelName", "PathName", "OuterPathName" };

			if (living.IsPlayer)
			{
				byte[] missing = bp_named.Missing;
				if (missing != null && missing.Length == 18)
				{
					string id = BitConverter.ToString(missing, 2).Replace("-", "");
					_childs.Add(MainFactory.Create(this, "Player-Id", id, true));
				}
			}

			// pl.C:Persistent_Level:PersistentLevel.BP_PlayerState_C_0.HealthComponent -> SG.O.Find* 
			//   => Object . EntityObj -> Entity . Value -> [0] mCurrentHealth;
			prop = MainWindow.CurrFile.Objects.FindByPathName(living.Entity.PathName.ToString() + ".HealthComponent");
			if (prop != null)
			{
				float health = 100;
				obj = prop as P.Object;
				entity = obj.EntityObj as P.Entity;
				if (entity != null)
				{
					prop = entity.Value.Named("mCurrentHealth");
					if (prop is P.FloatProperty)
						health = (float) (prop as P.FloatProperty).Value;
				}
				_childs.Add(MainFactory.Create(this, "Health [%]", health));
			}

			_childs.Add(MainFactory.Create(this, "Position", living.Entity.Translate));
			_childs.Add(MainFactory.Create(this, "Rotation", living.Entity.Rotation));
			_childs.Add(MainFactory.Create(this, "Scale"   , living.Entity.Scale));

			if (living.IsPlayer)
			{
				prop = ent_named.Value.Named("mFlashlightOn");
				if (prop != null)
					_childs.Add(MainFactory.Create(this, "Flashlight on?", prop));

				// What to do with those?
				//pl:
				//|-> [ObjectProperty] Persistent_Level:PersistentLevel.BP_BuildGun_C_0
				//|  .LevelName = str:'Persistent_Level'
				//|  .PathName = str:'Persistent_Level:PersistentLevel.BP_BuildGun_C_0'
				//|  .Name = str:'mBuildGun'
				//|  .Length = Int32:74
				//|  .Index = Int32:0
				//|  .Value = <empty>
				//|-> [ObjectProperty] Persistent_Level:PersistentLevel.BP_ResourceScanner_C_0
				//|  .LevelName = str:'Persistent_Level'
				//|  .PathName = str:'Persistent_Level:PersistentLevel.BP_ResourceScanner_C_0'
				//|  .Name = str:'mResourceScanner'
				//|  .Length = Int32:81
				//|  .Index = Int32:0
				//|  .Value = <empty>
				//|-> [ObjectProperty] Persistent_Level:PersistentLevel.Equip_ResourceMiner_C_0
				//|  .LevelName = str:'Persistent_Level'
				//|  .PathName = str:'Persistent_Level:PersistentLevel.Equip_ResourceMiner_C_0'
				//|  .Name = str:'mResourceMiner'
				//|  .Length = Int32:82
				//|  .Index = Int32:0
				//|  .Value = <empty>

				prop = bp_named.Value.Named("mHasReceivedInitialItems");
				if (prop != null)
					_childs.Add(MainFactory.Create(this, "Received initial items", prop));

				prop = bp_named.Value.Named("mLastSchematicTierInUI");
				if (prop != null)
					_childs.Add(MainFactory.Create(this, "Last schematic tier", prop));

				// pl:mLastSafeGroundPositions -> StructProperty -> [.Index] .Value -> Vector; (mLastSafeGroundPositionLoopHead ????)
				var grounds = ent_named.Value
					.Where(p => p is P.StructProperty)
					.Select(p => p as P.StructProperty)
					.Where(p => (p.Name != null) && (p.Name.ToString() == "mLastSafeGroundPositions"))
					.Select(p => p.Value as P.Vector)
					;
				_childs.Add(new LastSaveGroundPositions(this, null, grounds.ToList()));

				// bp:mHasSetupDefaultShortcuts -> BoolProperty + mHotbarShortcuts -> ArrayProperty -> [0-9] ObjectProperty.PathName -> SG.Obj.Find*;
				List<string> shortcuts = new List<string>();
				P.BoolProperty has_shortcuts = bp_named.Value.Named("mHasSetupDefaultShortcuts") as P.BoolProperty;
				if (has_shortcuts != null && has_shortcuts.Value is byte && (byte)(has_shortcuts.Value) == 1)
				{
					arr = bp_named.Value.Named("mHotbarShortcuts") as P.ArrayProperty;
					foreach (P.ObjectProperty shortcut in (arr.Value as P.Properties).ListOf<P.ObjectProperty>())
					{
						obj = MainWindow.CurrFile.Objects.FindByPathName(shortcut.PathName) as P.Object;
						entity = obj.EntityObj as P.Entity;
						P.ObjectProperty recipe = entity.Value.Named("mRecipeToActivate") as P.ObjectProperty;
						string recipe_name = (recipe != null) ? recipe.PathName.ToString() : null;
						shortcuts.Add(recipe_name);
					}
				}
				_childs.Add(new KeyboardShortcuts(this, null, shortcuts));
			}

			// pl.C:Persistent_Level:PersistentLevel.BP_PlayerState_C_0.inventory       + pl:mInventory      (why?);
			prop = MainWindow.CurrFile.Objects.FindByPathName(living.Entity.PathName.ToString() + ".inventory");
			if (prop == null)
				prop = MainWindow.CurrFile.Objects.FindByPathName(living.Entity.PathName.ToString() + ".mInventory");
			if (prop != null && prop is P.Object)
			{
				element = MainFactory.Create(this, null, prop);
				(element as SpecializedViewer)._excluded.AddRange(excluded);
				((element as Expando).Visual as Expando).Header = "Inventory";
				_childs.Add(element);
			}

			if (living.IsPlayer)
			{
				// pl.C:Persistent_Level:PersistentLevel.BP_PlayerState_C_0.ArmSlot;
				prop = MainWindow.CurrFile.Objects.FindByPathName(living.Entity.PathName.ToString() + ".ArmSlot");
				if (prop != null && prop is P.Object)
				{
					element = MainFactory.Create(this, null, prop);
					(element as SpecializedViewer)._excluded.AddRange(excluded);
					((element as Expando).Visual as Expando).Header = "Arm slots";
					_childs.Add(element);
				}

				// pl.C:Persistent_Level:PersistentLevel.BP_PlayerState_C_0.BackSlot;
				prop = MainWindow.CurrFile.Objects.FindByPathName(living.Entity.PathName.ToString() + ".BackSlot");
				if (prop != null && prop is P.Object)
				{
					element = MainFactory.Create(this, null, prop);
					(element as SpecializedViewer)._excluded.AddRange(excluded);
					((element as Expando).Visual as Expando).Header = "Back slot";
					_childs.Add(element);
				}

				// bp:mTutorialSubsystem -> ObjectProperty.PathName -> SG.Obj.Find*;
				//P.ObjectProperty tut = named.Value.Named("mTutorialSubsystem") as P.ObjectProperty;
				//if (tut != null)
				//	living.AddByPathName("Tutorial steps", tut.PathName.ToString());
				prop = bp_named.Value.Named("mTutorialSubsystem");
				if (prop != null)
				{
					if (prop is P.Object)
					{
						obj = prop as P.Object;
						entity = obj.EntityObj as P.Entity;
						prop = entity.Value.Named("mHasSeenIntroTutorial");
						if (prop != null)
							_childs.Add(MainFactory.Create(this, null, prop));
					}
					//else if (prop is P.ObjectProperty)
					//{
					//	P.ObjectProperty obj_prop = prop as P.ObjectProperty;
					//}
				}

				//bp:mNewRecipes -> ArrayProperty -> [0-N] ObjectProperty . PathName;
				//|-> [ArrayProperty] mNewRecipes
				//|  .InnerType = str:'ObjectProperty'
				//|  .Name = str:'mNewRecipes'
				//|  .Length = Int32:6.349
				//|  .Index = Int32:0
				//|  .Value =
				//|	/ List with 72 elements:
				//|	|-> [ObjectProperty] /Game/FactoryGame/Recipes/Equipment/Recipe_PortableMiner.Recipe_PortableMiner_C
				//|	|  .LevelName = <empty>
				//|	|  .PathName = str:'/Game/FactoryGame/Recipes/Equipment/Recipe_PortableMiner.Recipe_PortableMiner_C'
				//|	|  .Length = Int32:0
				//|	|  .Index = Int32:0
				//living.Add("Recipes", ...);

				// bp:mMessageData -> ArrayProperty -> StructProperty -> [0-N] MessageData;
				//|-> [ArrayProperty] mMessageData
				//|  .InnerType = str:'StructProperty'
				//|  .Name = str:'mMessageData'
				//|  .Length = Int32:16.501
				//|  .Index = Int32:0
				//|  .Value =
				//|	-> [StructProperty] mMessageData
				//|	  .Unknown = list<%s>(Byte):[0,]
				//|	  .Name = str:'mMessageData'
				//|	  .Length = Int32:16.420
				//|	  .Index = Int32:0
				//|	  .Value =
				//|		/ List with 76 elements:
				//|		|-> [MessageData].Value[0-1]
				//|		|  .Value =
				//|		|	/ List with 2 elements:
				//|		|	|-> [BoolProperty] WasRead
				//|		|	|  .Name = str:'WasRead'
				//|		|	|  .Length = Int32:0
				//|		|	|  .Index = Int32:0
				//|		|	|  .Value = Byte:0
				//|		|	|-> [ObjectProperty] /Game/FactoryGame/Interface/UI/Message/Tutorial/IntroTutorial/IntroTutorial_Greeting.IntroTutorial_Greeting_C
				//|		|	|  .LevelName = <empty>
				//|		|	|  .PathName = str:'/Game/FactoryGame/Interface/UI/Message/Tutorial/IntroTutorial/IntroTutorial_Greeting.IntroTutorial_Greeting_C'
				//|		|	|  .Name = str:'MessageClass'
				//|		|	|  .Length = Int32:118
				//|		|	|  .Index = Int32:0
				//|		|	|  .Value = <empty>
				//|		|	\ end of list
				//living.Add("Messages"        , pathname + ".");

				// pl.C:Persistent_Level:PersistentLevel.BP_PlayerState_C_0.TrashSlot;
				prop = MainWindow.CurrFile.Objects.FindByPathName(living.Entity.PathName.ToString() + ".TrashSlot");
				if (prop != null && prop is P.Object)
				{
					element = MainFactory.Create(this, null, prop);
					(element as SpecializedViewer)._excluded.AddRange(excluded);
					((element as Expando).Visual as Expando).Header = "Trash slot";
					_childs.Add(element);
				}

				// bp:mRememberedFirstTimeEquipmentClasses -> ArrayProperty -> [0-N] ObjectProperty.PathName -> SG.Obj.Find*;
				prop = bp_named.Value.Named("mRememberedFirstTimeEquipmentClasses");
				if (prop != null && prop is P.ArrayProperty)
					_childs.Add(new FirstTimeEquipped(this, null, prop));
			}
			else
			{
				//prop = ent_named.Value.Named("mSpline");
				//if (prop != null)
				//{
				//	element = MainFactory.Create(this, "Movement data?", prop);
				//	_childs.Add(element);
				//}
				//=> PathName not found within save, must be stored internally


			}
		}


		protected override void _PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			LivingTree.Living living = Tag as LivingTree.Living;
			base._PropertyChanged(living.IsPlayer ? living.Blueprint : living.Entity, e);
		}


		internal class LastSaveGroundPositions : PropertyList
		{
			public LastSaveGroundPositions(IElement parent, string label, List<P.Vector> vectors)
				: base(parent, label, vectors)
			{ }

			internal override void _CreateChilds()
			{
				Header = "Last save ground positions";

				_childs = new List<IElement>();
				List<P.Vector> vectors = Tag as List<P.Vector>;
				foreach (P.Vector vector in vectors)
				{
					string label = "Position";
					IElement element = MainFactory.Create(this, label, vector);
					_childs.Add(element);
				}
			}
		}

		internal class KeyboardShortcuts : PropertyList
		{
			public KeyboardShortcuts(IElement parent, string label, List<string> shortcuts)
				: base(parent, label, shortcuts)
			{ }

			internal override void _CreateChilds()
			{
				Header = "Keyboard shortcuts";

				int key = 0;
				_childs = new List<IElement>();
				List<string> shortcuts = Tag as List<string>;
				foreach (string shortcut in shortcuts)
				{
					key++;
					if (key > 9)
						key = 0;

					string label = string.Format("Key [{0}]", key);
					string pathname = DetailsPanel.EMPTY;
					if (shortcut != null)
					{
						pathname = shortcut.LastName();
						if (Translate.Has(pathname))
							pathname = Translate._(pathname);
					}

					IElement element = MainFactory.Create(this, label, pathname);
					//element.Label = label;
					_childs.Add(element);
				}
			}
		}

		internal class FirstTimeEquipped : Expando
		{
			public FirstTimeEquipped(IElement parent, string label, object obj)
				: base(parent, label, obj)
			{ }

			internal override void _CreateChilds()
			{
				P.ArrayProperty arr = Tag as P.ArrayProperty;

				_childs = new List<IElement>();
				List<P.ObjectProperty> coll = arr.Value.ListOf<P.ObjectProperty>();
				int index = 0;

				Header = "First-time equipped (skips animation)";
				if (coll.Count == 0)
					IsEnabled = false;

				foreach (P.ObjectProperty obj in coll)
				{
					++index;
					string label = index.ToString();
					string item_name = obj.PathName.LastName();
					if (Translate.Has(item_name))
						item_name = Translate._(item_name);

					IElement element = MainFactory.Create(this, label, item_name);
					_childs.Add(element);
				}
			}

		}

	}

	internal class Building : Expando
	{
		public Building(BuildingsTree.Building building)
			: base(null, building.Title, building)
		{ }

		internal override void _CreateChilds()
		{
			int save_version = MainWindow.CurrFile.Header.SaveVersion;

			BuildingsTree.Building tag = Tag as BuildingsTree.Building;
			P.Actor building = tag.Actor;
			P.NamedEntity ent_named = building.EntityObj as P.NamedEntity;

			// Find base type
			//
			string b_pathname = building.PathName.ToString();

			// Is this a passive or active actor?
			// - Passives: E.g. walls, conveyors, merger, ... -> Connectors only (up to 3 inputs, 1 output)
			// - Active  : E.g. constructors, miner, ...      -> Above (up to 4 inputs) plus invi(s), power, ...
			bool is_passive = (ent_named.Value.Named("mPowerInfo") == null);

			// Is this some sort of generator?
			bool is_generator = b_pathname.Contains("Generator");

			// Is this a simple conveyor?
			bool is_conveyor_belt = b_pathname.Contains("ConveyorBeltMk");
			bool is_conveyor_lift = b_pathname.Contains("ConveyorLiftMk");
			bool is_conveyor = is_conveyor_belt || is_conveyor_lift;

			// Is this a (stackable) conveyor pole?
			bool is_conveyor_pole = b_pathname.Contains("ConveyorPole_");
			bool is_conveyor_pole_st = b_pathname.Contains("ConveyorPoleStackable_");

			// Is this a simple power pole?
			bool is_powerpole = b_pathname.Contains("PowerPoleMk");

			// Is this a radar tower?
			bool is_radar_tower = b_pathname.Contains("RadarTower");

			// Is this a train-related building?
			bool is_train = b_pathname.Contains("Train");

			// Is this a train track?
			bool is_track = b_pathname.Contains("RailroadTrack");

			// Is this a track switch?
			bool is_switch = b_pathname.Contains("RailroadSwitch");


			// Start generating visuals
			//

			// Create list of values, processed ones are to be removed and 
			// remain - if any - will be logged as "dangling"
			var values = ent_named.Value.Names();
			Action<string> _eat_value = (name) => {
				if (values.Contains(name))
					values.Remove(name);
			};
			Func<string,P.Property> _try_value = (name) => {
				P.Property p = ent_named.Value.Named(name);
				if (p != null)
					_eat_value(name);
				return p;
			};

			P.Property prop;
			P.StructProperty stru;
			P.ObjectProperty objprop;
			P.Properties props;
			P.ArrayProperty arr;
			//P.Object obj;
			P.Entity entity;

			// Show transforms in separate expando
			_childs.Add(new Transforms(this, "Transform", building));

			//|-> [IntProperty] mBuildingID
			//|  .Name = str:'mBuildingID'
			//|  .Length = Int32:4
			//|  .Index = Int32:0
			//|  .Value = Int32:1
			prop = _try_value("mBuildingID");
			if (prop is P.IntProperty)
				_childs.Add(MainFactory.Create(this, "Chunk id", (prop as P.IntProperty).Value, true));

			//|-> [FloatProperty] mBuildTimeStamp
			//|  .Name = str:'mBuildTimeStamp'
			//|  .Length = Int32:4
			//|  .Index = Int32:0
			//|  .Value = Single:-434694
			//TODO: Find correct unit
			prop = _try_value("mBuildTimeStamp");
			if (prop is P.FloatProperty)
				_childs.Add(MainFactory.Create(this, "Built at", (prop as P.FloatProperty).Value, true));

			//|-> [ObjectProperty] /Game/FactoryGame/Recipes/Buildings/Recipe_ConstructorMk1.Recipe_ConstructorMk1_C
			//|  .LevelName = str:''
			//|  .PathName = str:'/Game/FactoryGame/Recipes/Buildings/Recipe_ConstructorMk1.Recipe_ConstructorMk1_C'
			//|  .Name = str:'mBuiltWithRecipe'
			//|  .Length = Int32:90
			//|  .Index = Int32:0
			//|  .Value = <empty>
			prop = _try_value("mBuiltWithRecipe");
			if (prop is P.ObjectProperty)
			{
				objprop = prop as P.ObjectProperty;
				string recipe = objprop.PathName.LastName();
				if (Translate.Has(recipe))
					recipe = Translate._(recipe);
				_childs.Add(MainFactory.Create(this, "Recipe used", recipe, true));
			}

			if (save_version >= 20)
			{
				// Use new coloring system, ...
				prop = _try_value("mColorSlot");
				_childs.Add(new ColorSlot(this, "Color slot", prop));

				// ... and remove the old values so they won't show up as dangling
				_eat_value("mPrimaryColor");
				_eat_value("mSecondaryColor");
			}
			else
			{
				// Display old color system

				//|-> [StructProperty] mPrimaryColor
				prop = _try_value("mPrimaryColor");
				if (prop is P.StructProperty)
				{
					stru = prop as P.StructProperty;
					if (stru != null && stru.Value is P.LinearColor)
						_childs.Add(MainFactory.Create(this, "Primary color", stru.Value));
				}

				//|-> [StructProperty] mSecondaryColor
				prop = _try_value("mSecondaryColor");
				if (prop is P.StructProperty)
				{
					stru = prop as P.StructProperty;
					if (stru != null && stru.Value is P.LinearColor)
						_childs.Add(MainFactory.Create(this, "Secondary color", stru.Value));
				}
			}

			//|-> [ArrayProperty] mDismantleRefund
			prop = _try_value("mDismantleRefund");
			if (prop is P.ArrayProperty)
			{
				arr = prop as P.ArrayProperty;
				_childs.Add(new DismantleRefund(this, null, arr.Value));
			}

			//|-> [BoolProperty] mDidFirstTimeUse
			//|  .Name = str:'mDidFirstTimeUse'
			//|  .Length = Int32:0
			//|  .Index = Int32:0
			//|  .Value = Byte:1
			prop = _try_value("mDidFirstTimeUse");
			if (prop is P.BoolProperty)
				_childs.Add(MainFactory.Create(this, "Is paused?", (prop as P.BoolProperty).Value, true));

			if (!is_passive)
			{
				// Active buildings might have an active recipe
				//TODO: "Identify" recipe-able buildings so we can show an 
				//      empty recipe if "mCurrentRecipe" is missing
				bool has_recipe = (ent_named.Value.Named("mExtractResourceNode") == null);

				if (has_recipe)
				{
					// Exclude train (docking) stations
					if (!is_train)
					{
						//|-> [ObjectProperty] /Game/FactoryGame/Recipes/Constructor/Recipe_Wire.Recipe_Wire_C
						//|  .LevelName = str:''
						//|  .PathName = str:'/Game/FactoryGame/Recipes/Constructor/Recipe_Wire.Recipe_Wire_C'
						//|  .Name = str:'mCurrentRecipe'
						//|  .Length = Int32:72
						//|  .Index = Int32:0
						//|  .Value = <empty>
						string recipe = DetailsPanel.EMPTY;
						prop = _try_value("mCurrentRecipe");
						if (prop is P.ObjectProperty)
						{
							objprop = prop as P.ObjectProperty;
							recipe = objprop.PathName.LastName();
							if (Translate.Has(recipe))
								recipe = Translate._(recipe);
						}
						_childs.Add(MainFactory.Create(this, "Current recipe", recipe, true));

						//|-> [FloatProperty] mCurrentManufacturingProgress
						//|  .Name = str:'mCurrentManufacturingProgress'
						//|  .Length = Int32:4
						//|  .Index = Int32:0
						//|  .Value = Single:0,003470778
						prop = _try_value("mCurrentManufacturingProgress");
						if (prop is P.FloatProperty)
						{
							float progress = ((float)(prop as P.FloatProperty).Value) * 100.0f;
							_childs.Add(MainFactory.Create(this, "Current progress [%]", progress, true));
						}
					}
				}
				else
				{
					// Instead of a recipe, miners will have extraction info instead
					//|-> [ObjectProperty] Persistent_Level:PersistentLevel.BP_ResourceNode89
					//|  .LevelName = str:'Persistent_Level'
					//|  .PathName = str:'Persistent_Level:PersistentLevel.BP_ResourceNode89'
					//|  .Name = str:'mExtractResourceNode'
					//|  .Length = Int32:76
					//|  .Index = Int32:0
					//|  .Value = <empty>
					string node = DetailsPanel.EMPTY;
					prop = _try_value("mExtractResourceNode");
					if (prop is P.ObjectProperty)
					{
						objprop = prop as P.ObjectProperty;
						node = objprop.PathName.LastName();
					}
					_childs.Add(MainFactory.Create(this, "Resource node", node, true));

					//|-> [FloatProperty] mCurrentExtractProgress
					//|  .Name = str:'mCurrentExtractProgress'
					//|  .Length = Int32:4
					//|  .Index = Int32:0
					//|  .Value = Single:0,04885578
					prop = _try_value("mCurrentExtractProgress");
					if (prop is P.FloatProperty)
					{
						float progress = ((float)(prop as P.FloatProperty).Value) * 100.0f;
						_childs.Add(MainFactory.Create(this, "Current progress [%]", progress, true));
					}
				}

				//|-> [BoolProperty] mIsProducing
				//|  .Name = str:'mIsProducing'
				//|  .Length = Int32:0
				//|  .Index = Int32:0
				//|  .Value = Byte:1
				prop = _try_value("mIsProducing");
				if (prop is P.BoolProperty)
					_childs.Add(MainFactory.Create(this, "Is producing?", (prop as P.BoolProperty).Value, true));

				//|-> [FloatProperty] mTimeSinceStartStopProducing
				//|  .Name = str:'mTimeSinceStartStopProducing'
				//|  .Length = Int32:4
				//|  .Index = Int32:0
				//|  .Value = Single:2918,587
				//TODO: Find correct unit
				prop = _try_value("mTimeSinceStartStopProducing");
				if (prop is P.FloatProperty)
					_childs.Add(MainFactory.Create(this, "Time since start", (prop as P.FloatProperty).Value, true));

				//|-> [BoolProperty] mIsProductionPaused
				//|  .Name = str:'mIsProductionPaused'
				//|  .Length = Int32:0
				//|  .Index = Int32:0
				//|  .Value = Byte:1
				//=> Seems only avail if paused?
				prop = _try_value("mIsProductionPaused");
				if (prop is P.BoolProperty)
					_childs.Add(MainFactory.Create(this, "Is paused?", (prop as P.BoolProperty).Value, true));
			}

			// Create list of childs, processed ones are to be removed and 
			// remain - if any - will be logged as "dangling"
			var childs = ent_named
				.Children
				.ListOf<P.NamedEntity.Name>()
				.Select(n => n.PathName.ToString())
				.ToList()
				;
			string building_pathname = building.PathName.ToString();
			Action<string> _eat = (path) => {
				string p = building_pathname + path;
				if (childs.Contains(p))
					childs.Remove(p);
			};
			Func<string, P.Property> _try = (path) => {
				P.Property p = MainWindow.CurrFile.Objects.FindByPathName(building_pathname + path, true);
				if (p != null)
					_eat(path);
				return p;
			};
			Action<string, string> _try_add_item_conn = (title, path) => {
				prop = _try(path);
				if (prop != null)
					_childs.Add(new FactoryConnection(this, title, prop));
			};
			Action<string, string> _try_add_inventory = (title, path) => {
				prop = _try(path);
				if (prop != null)
				{
					FGInventoryComponent invi = new FGInventoryComponent(this, null, prop);
					invi._label = null;
					invi._excluded.AddRange(_excluded_props);
					(invi.Visual as Expando).Header = title;
					_childs.Add(invi);
				}
			};
			Action<string, string> _try_add_power_conn = (title, path) => {
				prop = _try(path);
				if (prop != null)
					_childs.Add(new PowerConnection(this, title, prop));
			};
			Action<string, string> _try_add_train_platform = (title, path) => {
				prop = _try(path);
				if (prop != null)
					_childs.Add(new PlatformConnection(this, title, prop));
			};
			Action<string, string> _try_add_track_conn = (title, path) => {
				prop = _try(path);
				if (prop != null)
					_childs.Add(new TrackConnection(this, title, prop));
			};

			// Distinguish between conveyors, power poles and "more intelligent" 
			// buildings to ease things a bit in regards to no. of lookups
			if (is_conveyor)
			{
				_try_add_item_conn("Input", ".ConveyorAny0");
				_try_add_item_conn("Output", ".ConveyorAny1");

				prop = _try_value("mIsReversed");
				if (prop is P.BoolProperty)
					_childs.Add(MainFactory.Create(this, "Is Reversed?", (prop as P.BoolProperty).Value, true));

				//TODO: Add .Private, if any
			}
			else if (is_conveyor_pole || is_conveyor_pole_st)
			{
				// Has property 'mHeight' if height > 1 (300,500,700). So height=1 => 100 units => 100cm
				float height;
				prop = _try_value("mHeight");
				if (prop is P.FloatProperty)
					height = (float)(prop as P.FloatProperty).Value;
				else
					height = 100.0f;
				_childs.Add(MainFactory.Create(this, "Height [m]", height / 100.0f, true));
			}
			else if (is_track || is_switch)
			{
				if (is_track)
				{
					_try_add_track_conn("Previous", ".TrackConnection0");
					_try_add_track_conn("Next", ".TrackConnection1");
					_eat_value("mConnections");
					_eat_value("mConnections#1");

					//|-> [BoolProperty] mIsOwnedByPlatform
					//|  .Name = str:'mIsOwnedByPlatform'
					//|  .Length = Int32:0
					//|  .Index = Int32:0
					//|  .Value = Byte:1
					prop = _try_value("mIsOwnedByPlatform");
					if (prop is P.BoolProperty)
					{
						byte owned = (byte)(prop as P.BoolProperty).Value;
						_childs.Add(MainFactory.Create(this, "Owned by platform?", owned, true));
					}

					// Skipped for now:
					_eat_value("mSplineData");
				}
				else
				{
					prop = _try_value("mControlledConnection");
					if (prop is P.ObjectProperty)
					{
						prop = MainWindow.CurrFile.Objects.FindByPathName((prop as P.ObjectProperty).PathName, true);
						if (prop != null)
							_childs.Add(new TrackConnection(this, "Controlled Connection", prop));
					}
				}

				// Skipped for now:
				_eat_value("mTimeSinceStartStopProducing");
				_eat_value("mIsProducing");

				//TODO: Add .Private, if any
			}
			else if (is_radar_tower)
			{
				//- mMapText
				//|-> [TextProperty] mMapText
				//|  .Unknown = Byte:[ 18, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,  ]
				//|  .Name = str:'mMapText'
				//|  .Length = Int32:27
				//|  .Index = Int32:0
				//|  .Value = str:'Main Base'
				prop = _try_value("mMapText");
				if (prop is P.TextProperty)
					_childs.Add(MainFactory.Create(this, "Map text", (prop as P.TextProperty).Value, true));

				//|-> [IntProperty] mCurrentExpansionStep
				//|  .Name = str:'mCurrentExpansionStep'
				//|  .Length = Int32:4
				//|  .Index = Int32:0
				//|  .Value = Int32:10
				prop = _try_value("mCurrentExpansionStep");
				if (prop is P.IntProperty)
					_childs.Add(MainFactory.Create(this, "Current expansion step", (prop as P.IntProperty).Value, true));

				//|-> [FloatProperty] mTimeToNextExpansion
				//|  .Name = str:'mTimeToNextExpansion'
				//|  .Length = Int32:4
				//|  .Index = Int32:0
				//|  .Value = Single:-1
				prop = _try_value("mTimeToNextExpansion");
				if (prop is P.FloatProperty)
					_childs.Add(MainFactory.Create(this, "Time to next exp. step", (prop as P.FloatProperty).Value, true));

				//|-> [FloatProperty] mTimeSinceStartStopProducing
				//|  .Name = str:'mTimeSinceStartStopProducing'
				//|  .Length = Int32:4
				//|  .Index = Int32:0
				//|  .Value = Single:2918,587
				prop = _try_value("mTimeSinceStartStopProducing");
				if (prop is P.FloatProperty)
					_childs.Add(MainFactory.Create(this, "Time since start", (prop as P.FloatProperty).Value, true));
			}
			else
			{
				// Buildings can have up to 6 inputs (e.g. space elevator),
				// but start index varies between 0 and 1, so we do a 0-6 in total.
				for (int index = 0; index <= 6; ++index)
				{
					string idx = index.ToString();
					_try_add_item_conn("Input #" + idx, ".Input" + idx);
				}

				// Building can have either a "normal" or "fuel" input inventory
				// which is being fed by inputs above
				_try_add_inventory("Input inventory", ".InputInventory");
				_try_add_inventory("Input inventory", ".FuelInventory");

				// Buildings can have up to 3 outputs (e.g. splitter),
				// but start index varies between 0 and 1, so we do a 0-3 in total.
				for (int index = 0; index <= 3; ++index)
				{
					string idx = index.ToString();
					_try_add_item_conn("Output #" + idx, ".Output" + idx);
				}

				// Building can have an output inventory
				// which is being emptied by outputs above
				_try_add_inventory("Output inventory", ".OutputInventory");

				// Handle special types like storage containers and alike
				_try_add_inventory("Storage", ".StorageInventory");
				_try_add_inventory("Storage", ".inventory");// TrainDockingStation

				// Those were (or better should have been) expressed already
				_eat_value("mInventory");
				_eat_value("mFuelInventory");

				// Trunk and train station might carry this property:
				//|-> [BoolProperty] mIsInLoadMode
				//|  .Name = str:'mIsInLoadMode'
				//|  .Length = Int32:0
				//|  .Index = Int32:0
				//|  .Value = Byte:0
				prop = _try_value("mIsInLoadMode");
				if (prop is P.BoolProperty)
					_childs.Add(MainFactory.Create(this, "Is in load mode?", (prop as P.BoolProperty).Value, true));

				// Some machines do have an inventory potential, the OC slots ^^
				//|-> [FloatProperty] mCurrentPotential
				//|  .Name = str:'mCurrentPotential'
				//|  .Length = Int32:4
				//|  .Index = Int32:0
				//|  .Value = Single:1,88
				prop = _try_value("mCurrentPotential");
				if (prop is P.FloatProperty)
				{
					float potential = (((float)(prop as P.FloatProperty).Value) + 1.0f) * 100.0f;;
					_childs.Add(MainFactory.Create(this, "Current potential [%]", potential, true));
				}

				//|-> [FloatProperty] mPendingPotential
				//|  .Name = str:'mPendingPotential'
				//|  .Length = Int32:4
				//|  .Index = Int32:0
				//|  .Value = Single:1,88
				prop = _try_value("mPendingPotential");
				if (prop is P.FloatProperty)
				{
					float potential = (((float)(prop as P.FloatProperty).Value) + 1.0f) * 100.0f;;
					_childs.Add(MainFactory.Create(this, "Maximum potential [%]", potential, true));
				}

				_try_add_inventory("Overclocking slots", ".InventoryPotential");

				if (is_generator)
				{
					// Only with generators accepting varying fuels

					//|-> [BoolProperty] mHasFuleCached
					//|  .Name = str:'mHasFuleCached'
					//|  .Length = Int32:0
					//|  .Index = Int32:0
					//|  .Value = Byte:1
					prop = _try_value("mHasFuleCached");
					if (prop is P.BoolProperty)
						_childs.Add(MainFactory.Create(this, "Has cached fuel?", (prop as P.BoolProperty).Value, true));

					//|-> [FloatProperty] mCurrentFuelAmount
					//|  .Name = str:'mCurrentFuelAmount'
					//|  .Length = Int32:4
					//|  .Index = Int32:0
					//|  .Value = Single:194,6298
					prop = _try_value("mCurrentFuelAmount");
					if (prop is P.FloatProperty)
						_childs.Add(MainFactory.Create(this, "Current fuel amount", (prop as P.FloatProperty).Value, true));

					//|-> [ObjectProperty] /Game/FactoryGame/Resource/RawResources/Coal/Desc_Coal.Desc_Coal_C
					//|  .LevelName = str:''
					//|  .PathName = str:'/Game/FactoryGame/Resource/RawResources/Coal/Desc_Coal.Desc_Coal_C'
					//|  .Name = str:'mCurrentFuelClass'
					//|  .Length = Int32:75
					//|  .Index = Int32:0
					//|  .Value = <empty>
					prop = _try_value("mCurrentFuelClass");
					if (prop is P.ObjectProperty)
					{
						objprop = prop as P.ObjectProperty;
						string fuel = objprop.PathName.LastName();
						if (Translate.Has(fuel))
							fuel = Translate._(fuel);
						_childs.Add(MainFactory.Create(this, "Current fuel class", fuel, true));
					}
				}
				else if (is_train)
				{
					//|-> [Name]
					//|  .LevelName = str:'Persistent_Level'
					//|  .PathName = str:'Persistent_Level:PersistentLevel.Build_TrainStation_C_0.PlatformConnection0'
					//|  .PathName = str:'Persistent_Level:PersistentLevel.Build_TrainStation_C_0.PlatformConnection1'
					_try_add_train_platform("Platform connection #0", ".PlatformConnection0");
					_try_add_train_platform("Platform connection #1", ".PlatformConnection1");

					//|-> [ObjectProperty] Persistent_Level:PersistentLevel.BP_Locomotive_C_2
					//|  .LevelName = str:'Persistent_Level'
					//|  .PathName = str:'Persistent_Level:PersistentLevel.BP_Locomotive_C_2'
					//|  .Name = str:'mDockingLocomotive'
					//|  .Length = Int32:76
					//|  .Index = Int32:0
					//|  .Value = <empty>
					prop = _try_value("mDockingLocomotive");
					if (prop is P.ObjectProperty)
						_childs.Add(MainFactory.Create(this, "Docked locomotive", (prop as P.ObjectProperty).PathName, true));

					//|-> [ArrayProperty] mDockedPlatformList
					//|  .InnerType = str:'ObjectProperty'
					//|  .Name = str:'mDockedPlatformList'
					//|  .Length = Int32:92
					//|  .Index = Int32:0
					//|  .Value =
					//|	/ List with 1 elements:
					//|	|-> [ObjectProperty] Persistent_Level:PersistentLevel.Build_TrainDockingStation_C_3
					//|	|  .LevelName = str:'Persistent_Level'
					//|	|  .PathName = str:'Persistent_Level:PersistentLevel.Build_TrainDockingStation_C_3'
					//|	|  .Length = Int32:0
					//|	|  .Index = Int32:0
					//|	\ end of list
					prop = _try_value("mDockedPlatformList");
					if (prop is P.ArrayProperty)
					{
						arr = prop as P.ArrayProperty;
						if (arr.Value is P.Properties)
						{
							props = arr.Value as P.Properties;
							objprop = props[0] as P.ObjectProperty;
							_childs.Add(MainFactory.Create(this, "Docked platform", objprop.PathName, true));
						}
					}

					//This...
					//|-> [StructProperty] mTrackPosition
					//|  .Unknown = list<Byte>(17):[0,]
					//|  .Name = str:'mTrackPosition'
					//|  .Length = Int32:100
					//|  .Index = Int32:0
					//|  .Value =
					//|	-> [RailroadTrackPosition] 
					//|	  .ClassName = str:'Persistent_Level'
					//|	  .PathName = str:'Persistent_Level:PersistentLevel.Build_RailroadTrackIntegrated_C_0'
					//|	  .Offset = Single:799,9999
					//|	  .Forward = Single:1
					//|	  .Name = <empty>
					//|	  .Length = Int32:0
					//|	  .Index = Int32:0
					//|	  .Value = <empty>
					//...or should I use
					//|-> [ObjectProperty] Persistent_Level:PersistentLevel.Build_RailroadTrackIntegrated_C_0
					//|  .LevelName = str:'Persistent_Level'
					//|  .PathName = str:'Persistent_Level:PersistentLevel.Build_RailroadTrackIntegrated_C_0'
					//|  .Name = str:'mRailroadTrack'
					//|  .Length = Int32:92
					//|  .Index = Int32:0
					//|  .Value = <empty>
					//...phew
					_eat_value("mTrackPosition");
					prop = _try_value("mRailroadTrack");
					if (prop is P.ObjectProperty)
						_childs.Add(MainFactory.Create(this, "Railroad track", (prop as P.ObjectProperty).PathName, true));

					//|-> [BoolProperty] mIsOrientationReversed
					//|  .Name = str:'mIsOrientationReversed'
					//|  .Length = Int32:0
					//|  .Index = Int32:0
					//|  .Value = Byte:1
					prop = _try_value("mIsOrientationReversed");
					if (prop is P.BoolProperty)
						_childs.Add(MainFactory.Create(this, "Orientation reversed?", prop as P.BoolProperty, true));

					//|-> [BoolProperty] mIsProducing
					//|  .Name = str:'mIsProducing'
					//|  .Length = Int32:0
					//|  .Index = Int32:0
					//|  .Value = Byte:1
					prop = _try_value("mIsProducing");
					if (prop is P.BoolProperty)
						_childs.Add(MainFactory.Create(this, "Is producing?", (prop as P.BoolProperty).Value, true));

					//|-> [FloatProperty] mTimeSinceStartStopProducing
					//|  .Name = str:'mTimeSinceStartStopProducing'
					//|  .Length = Int32:4
					//|  .Index = Int32:0
					//|  .Value = Single:2918,587
					//TODO: Find correct unit, might be seconds, with =3600 running for an hour or more?
					prop = _try_value("mTimeSinceStartStopProducing");
					if (prop is P.FloatProperty)
						_childs.Add(MainFactory.Create(this, "Time since start", (prop as P.FloatProperty).Value, true));
				}

				//TODO: Add .Private, if any
			}

			// All buildings do have a power consumption info
			// *.powerInfo
			//
			//-> [Object] /Script/FactoryGame.FGPowerInfoComponent
			//  .ClassName = str:'/Script/FactoryGame.FGPowerInfoComponent'
			//  .LevelName = str:'Persistent_Level'
			//  .PathName = str:'Persistent_Level:PersistentLevel.Build_ConstructorMk1_C_118.powerInfo'
			//  .OuterPathName = str:'Persistent_Level:PersistentLevel.Build_ConstructorMk1_C_118'
			//  .EntityObj =
			//	-> [Entity] 
			//	  .LevelName = <empty>
			//	  .PathName = <empty>
			//	  .Children = <empty>
			//	  .Unknown = Int32:0
			//	  .Missing = <empty>
			//	  .Private = <empty>
			//	  .Value =
			//		/ List with N elements:
			//		|-> [FloatProperty] mTargetConsumption
			//		|  .Name = str:'mTargetConsumption'
			//		|  .Length = Int32:4
			//		|  .Index = Int32:0
			//		|  .Value = Single:0,1
			//		|  ...
			//		|-> [FloatProperty] mDynamicProductionCapacity
			//		|-> [FloatProperty] mBaseProduction
			//		\ end of list
			_eat_value("mPowerInfo");
			prop = _try(".powerInfo");
			if (prop is P.Object)
			{
				entity = (prop as P.Object).EntityObj as P.Entity;
				if (entity != null)
				{
					var names = entity.Value.Names();

					// * Crafting: Assembler, Constructor, Foundry, Locomotives, Manufacturer, Miner, OilPump, Smelter, ...
					// * Stations: TrainDockingStation, TrainStation, TruckStation, ...
					// * Remain  : JumpPad, LandingPad, RadarTower, DropPod (if energy is needed to open it), ...
					//|-> [FloatProperty] mTargetConsumption
					//|  .Name = str:'mTargetConsumption'
					//|  .Length = Int32:4
					//|  .Index = Int32:0
					//|  .Value = Single:25
					prop = entity.Value.Named("mTargetConsumption");
					if (prop is P.FloatProperty)
					{
						_childs.Add(MainFactory.Create(this, "Power consumption [MW]", (prop as P.FloatProperty).Value));
						names.Remove("mTargetConsumption");
					}

					// * Generators: Coal, Fuel, (Nuclear?)
					//|-> [FloatProperty] mDynamicProductionCapacity
					//|  .Name = str:'mDynamicProductionCapacity'
					//|  .Length = Int32:4
					//|  .Index = Int32:0
					//|  .Value = Single:150
					prop = entity.Value.Named("mDynamicProductionCapacity");
					if (prop is P.FloatProperty)
					{
						_childs.Add(MainFactory.Create(this, "Production capacity [MW]", (prop as P.FloatProperty).Value));
						names.Remove("mDynamicProductionCapacity");
					}

					// * Generators: GeoThermal
					//|-> [FloatProperty] mBaseProduction
					//|  .Name = str:'mBaseProduction'
					//|  .Length = Int32:4
					//|  .Index = Int32:0
					//|  .Value = Single:200
					prop = entity.Value.Named("mBaseProduction");
					if (prop is P.FloatProperty)
					{
						_childs.Add(MainFactory.Create(this, "Base production [MW]", (prop as P.FloatProperty).Value));
						names.Remove("mBaseProduction");
					}

					if (names.Count > 0 && Settings.VERBOSE)
					{
						Log.Warning("Building '{0}' has a {1} dangling .powerInfo entries:", building_pathname, names.Count);
						foreach (string name in names)
							Log.Warning("- {0}", name);
					}
				}
			}

			// Buildings can also have one of those
			_try_add_power_conn("Power connection", ".PowerInput");
			_try_add_power_conn("Power connection", ".PowerConnection");
			_try_add_power_conn("Power connection", ".FGPowerConnection");

			// Buildings with dynamic legs might also carry those cached offsets
			prop = _try(".FGFactoryLegs");
			if (prop is P.Object)
				_childs.Add(new FeetOffsets(this, "Feet offsets", prop));

			// Handle any type-specific child remaining
			if (is_passive)
			{
				//	|-> [ArrayProperty] mSortRules
				//	|  .InnerType = str:'StructProperty'
				//	|  .Name = str:'mSortRules'
				//	|  .Length = Int32:522
				//	|  .Index = Int32:0
				//	|  .Value =
				//	|	-> [StructProperty] mSortRules
				//	|	  .Unknown = list<Byte>(17):[0,]
				//	|	  .Name = str:'mSortRules'
				//	|	  .Length = Int32:438
				//	|	  .Index = Int32:0
				//	|	  .Value =
				//	|		/ List with 3 elements:
				prop = _try_value("mSortRules");
				if (prop is P.ArrayProperty)
					_childs.Add(new SortRules(this, "Sort rules", prop));

				// Some passive buildings will also carry snap-only data
				//
				//.SnapOnly0
				//- /Game/FactoryGame/Buildable/Building/Wall/Build_Wall_Conveyor_8x4_01.Build_Wall_Conveyor_8x4_01_C
				//- /Game/FactoryGame/Buildable/Building/Wall/Build_Wall_Conveyor_8x4_03_Steel.Build_Wall_Conveyor_8x4_03_Steel_C
				//- /Game/FactoryGame/Buildable/Building/Wall/Build_Wall_Conveyor_8x4_03.Build_Wall_Conveyor_8x4_03_C
				//- /Game/FactoryGame/Buildable/Building/Wall/Build_Wall_Conveyor_8x4_02_Steel.Build_Wall_Conveyor_8x4_02_Steel_C
				//- /Game/FactoryGame/Buildable/Factory/ConveyorPoleStackable/Build_ConveyorPoleStackable.Build_ConveyorPoleStackable_C
				//- /Game/FactoryGame/Buildable/Factory/ConveyorPole/Build_ConveyorPole.Build_ConveyorPole_C
				//.SnapOnly1
				//- /Game/FactoryGame/Buildable/Building/Wall/Build_Wall_Conveyor_8x4_01.Build_Wall_Conveyor_8x4_01_C
				//- /Game/FactoryGame/Buildable/Building/Wall/Build_Wall_Conveyor_8x4_02_Steel.Build_Wall_Conveyor_8x4_02_Steel_C
				//.SnapOnly2
				//- /Game/FactoryGame/Buildable/Building/Wall/Build_Wall_Conveyor_8x4_01.Build_Wall_Conveyor_8x4_01_C


				// Ignored for now:
				//
				//|-> [IntProperty] mCurrentInputIndex
				//|  .Name = str:'mCurrentInputIndex'
				//|  .Length = Int32:4
				//|  .Index = Int32:0
				//|  .Value = Int32:2
				_try_value("mCurrentInputIndex");
				// Same with mLastOutputIndex (smart + programmable splitters)
				_try_value("mLastOutputIndex");
			}
			else
			{
				// Active "buildings"
				//

				// Ignored for now:
				// n/a
			}

			if (values.Count > 0 && Settings.VERBOSE)
			{
				Log.Warning("Building '{0}' has a {1} dangling values:", building_pathname, values.Count);
				foreach (string value in values)
					Log.Warning("- {0}", value);
			}

			if (childs.Count > 0 && Settings.VERBOSE)
			{
				Log.Warning("Building '{0}' has a {1} dangling children:", building_pathname, childs.Count);
				foreach (string child in childs)
					Log.Warning("- {0}", child);
			}

		}


		protected override void _PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			BuildingsTree.Building building = Tag as BuildingsTree.Building;
			base._PropertyChanged(building.Actor, e);
		}


		internal class Transforms : Expando
		{
			public Transforms(IElement parent, string label, object obj)
				: base(parent, label, obj)
			{ }

			internal override void _CreateVisual()
			{
				base._CreateVisual();
				Header = "Transforms";
			}

			internal override void _CreateChilds()
			{
				P.Actor actor = Tag as P.Actor;

				_childs.Add(MainFactory.Create(this, "Position", actor.Translate));
				_childs.Add(MainFactory.Create(this, "Rotation", actor.Rotation));
				_childs.Add(MainFactory.Create(this, "Scale"   , actor.Scale));
				if (actor.ClassName.ToString().Contains("ConveyorLiftMk"))
				{
					P.Entity entity = actor.EntityObj as P.NamedEntity;
					P.Property prop = entity.Value.Named("mTopTransform");
					if (prop is P.StructProperty)
					{
						P.StructProperty stru = prop as P.StructProperty;
						P.Transform transforms = stru.Value as P.Transform;
						foreach (P.ValueProperty val in transforms.Value)
							_childs.Add(MainFactory.Create(this, "Top " + val.Name, val.Value));
					}
				}
			}
		}

		internal class DismantleRefund : Expando
		{
			public DismantleRefund(IElement parent, string label, object obj)
				: base(parent, label, obj)
			{ }

			internal override void _CreateChilds()
			{
				//|-> [ArrayProperty] mDismantleRefund
				//|  .InnerType = str:'StructProperty'
				//|  .Name = str:'mDismantleRefund'
				//|  .Length = Int32:450
				//|  .Index = Int32:0
				//|  .Value =
				//|	-> [StructProperty] mDismantleRefund
				P.StructProperty stru = Tag as P.StructProperty;

				_childs = new List<IElement>();
				List<P.ItemAmount> coll = stru.Value.ListOf<P.ItemAmount>();

				Header = "Dismantle refund";
				if (coll == null || coll.Count == 0)
					IsEnabled = false;

				ItemAmountList items = new ItemAmountList(coll);
				_childs.Add(items);
			}

		}

		internal class FactoryConnection : ReadonlySimpleValueControl<string>
		{
			public FactoryConnection(IElement parent, string label, object obj)
				: base(parent, label, null)
			{
				_object = obj as P.Object;//-> [Object] /Script/FactoryGame.FGFactoryConnectionComponent
			}

			//-> [Object] /Script/FactoryGame.FGFactoryConnectionComponent
			//  .ClassName = str:'/Script/FactoryGame.FGFactoryConnectionComponent'
			//  .LevelName = str:'Persistent_Level'
			//  .PathName = str:'Persistent_Level:PersistentLevel.Build_ConveyorAttachmentMerger_C_392.InPut3'
			//  .OuterPathName = str:'Persistent_Level:PersistentLevel.Build_ConveyorAttachmentMerger_C_392'
			//  .EntityObj =
			//	-> [Entity] 
			//	  .LevelName = <empty>
			//	  .PathName = <empty>
			//	  .Children = <empty>
			//	  .Unknown = Int32:0
			//	  .Missing = <empty>
			//	  .Value =
			//		/ List with 1 elements:
			//		|-> [ObjectProperty] Persistent_Level:PersistentLevel.Build_ConveyorBeltMk1_C_1329.ConveyorAny1
			//		|  .LevelName = str:'Persistent_Level'
			//		|  .PathName = str:'Persistent_Level:PersistentLevel.Build_ConveyorBeltMk1_C_1329.ConveyorAny1'
			//		|  .Name = str:'mConnectedComponent'
			//		|  .Length = Int32:100
			//		|  .Index = Int32:0
			//		|  .Value = <empty>
			//		\ end of list
			internal override void _CreateVisual()
			{
				_value = DetailsPanel.EMPTY;

				P.Entity entity = _object.EntityObj as P.Entity;
				if (entity.Value != null)
				{
					P.Property prop = entity.Value.Named("mConnectedComponent");
					if (prop is P.ObjectProperty)
					{
						P.ObjectProperty objprop = prop as P.ObjectProperty;
						string pathname = objprop.PathName.ToString();
						if (!string.IsNullOrEmpty(pathname))
						{
							//var names = pathname
							//	.Split('_')
							//	.Where(s => !_excludes.Contains(s))
							//	.ToList();
							//string id = names.Last();
							//names.Remove(id);
							//string name = string.Join("_", names);
							//_value = string.Format("{0} - {1}", string.Join("_", names), id);
							_value = pathname;
						}
					}
				}

				base._CreateVisual();

				//TODO: Check if connector is listed at all and in a valid "chunk" (see FGFoundationSubsystem)
			}

			private P.Object _object;
			private static string[] _excludes = new string[] { "Build", "BP", "C" };
		}

		internal class PowerConnection : Expando
		{
			public PowerConnection(IElement parent, string label, object obj)
				: base(parent, label, null)
			{
				_object = obj as P.Object;//-> [Object] /Script/FactoryGame.FGPowerConnectionComponent
			}

			//-> [Object] /Script/FactoryGame.FGPowerConnectionComponent
			//  .ClassName = str:'/Script/FactoryGame.FGPowerConnectionComponent'
			//  .LevelName = str:'Persistent_Level'
			//  .PathName = str:'Persistent_Level:PersistentLevel.Build_SmelterMk1_C_24.PowerConnection'
			//  .OuterPathName = str:'Persistent_Level:PersistentLevel.Build_SmelterMk1_C_24'
			//  .EntityObj =
			//	-> [Entity] 
			//	  .LevelName = <empty>
			//	  .PathName = <empty>
			//	  .Children = <empty>
			//	  .Unknown = Int32:0
			//	  .Missing = <empty>
			//	  .Private = <empty>
			//	  .Value =
			//		/ List with 2 elements:
			//		|-> [ArrayProperty] mWires
			//		|  .InnerType = str:'ObjectProperty'
			//		|  .Name = str:'mWires'
			//		|  .Length = Int32:85
			//		|  .Index = Int32:0
			//		|  .Value =
			//		|	/ List with 1 elements:
			//		|	|-> [ObjectProperty] Persistent_Level:PersistentLevel.Build_PowerLine_C_1395
			//		|	|  .LevelName = str:'Persistent_Level'
			//		|	|  .PathName = str:'Persistent_Level:PersistentLevel.Build_PowerLine_C_1395'
			//		|	|  .Length = Int32:0
			//		|	|  .Index = Int32:0
			//		|	\ end of list
			//		|-> [IntProperty] mCircuitID
			//		|  .Name = str:'mCircuitID'
			//		|  .Length = Int32:4
			//		|  .Index = Int32:0
			//		|  .Value = Int32:1
			//		\ end of list
			internal override void _CreateChilds()
			{
				Header = "Power connection(s)";

				P.Entity entity = _object.EntityObj as P.Entity;
				if (entity.Value != null)
				{
					P.Property prop = entity.Value.Named("mCircuitID");
					if (prop is P.IntProperty)
						_childs.Add(MainFactory.Create(this, "Circuit ID", (prop as P.IntProperty).Value, true));

					prop = entity.Value.Named("mWires");
					if (prop is P.ArrayProperty)
					{
						P.ArrayProperty arr = prop as P.ArrayProperty;
						List<P.ObjectProperty> list = (arr.Value as P.Properties).ListOf<P.ObjectProperty>();

						// Machines do have only one wire, but power poles can have multiples
						// So we do split here into simple and list-based visualisation based on 
						// parent by investigating objects .OuterPathName
						bool is_powerpole = _object.OuterPathName.ToString().Contains("PowerPoleMk");

						//		|  .Value =
						//		|	/ List with 1 elements:
						//		|	|-> [ObjectProperty] Persistent_Level:PersistentLevel.Build_PowerLine_C_1395
						//		|	|  .LevelName = str:'Persistent_Level'
						//		|	|  .PathName = str:'Persistent_Level:PersistentLevel.Build_PowerLine_C_1395'
						//		|	|  .Length = Int32:0
						//		|	|  .Index = Int32:0
						//		|	\ end of list
						if (!is_powerpole)
						{
							// Single
							string pathname = list[0].PathName.ToString();
							_childs.Add(MainFactory.Create(this, "Power line", pathname, true));
						}
						else
						{
							// List

							int index = 0;
							LVC.Collection rows = new LVC.Collection();
							foreach (P.ObjectProperty objprop in list)
							{
								++index;
								string label = index.ToString();

								string name = objprop.PathName.ToString();
								if (string.IsNullOrEmpty(name))
									name = DetailsPanel.EMPTY;

								rows.Add(new object[] {
									label,
									name,
								});
							}

							LVC.ColumnDefinitions columns = new LVC.ColumnDefinitions() {
								new LVC.ColumnDefinition("#", 25, new Binding(".[0]")),
								new LVC.ColumnDefinition("Power line", 300, new Binding(".[1]")),
							};
							LVC lvc = new LVC() {
								Columns = columns,
								Value = rows,
							};

							_childs.Add(lvc);
						}
					}
				}

				//TODO: Check if lines are listed at all and in a valid "circuit group"
			}

			internal class LVC : ListViewControl<object[]> { }

			private P.Object _object;
		}

		internal class SortRules : Expando
		{
			public SortRules(IElement parent, string label, object obj)
				: base(parent, label, null)
			{
				_array = obj as P.ArrayProperty;
			}

			internal override void _CreateChilds()
			{
				//|-> [ArrayProperty] mSortRules
				//|  .InnerType = str:'StructProperty'
				//|  .Name = str:'mSortRules'
				//|  .Length = Int32:522
				//|  .Index = Int32:0
				//|  .Value =
				//|	-> [StructProperty] mSortRules
				//|	  .Unknown = list<Byte>(17):[0,]
				//|	  .Name = str:'mSortRules'
				//|	  .Length = Int32:438
				//|	  .Index = Int32:0
				//|	  .Value =
				//|		/ List with 3 elements:
				P.StructProperty stru = _array.Value as P.StructProperty;

				_childs = new List<IElement>();
				List<P.SplitterSortRule> rules = stru.Value.ListOf<P.SplitterSortRule>();

				Header = "Sort rules";
				if (rules.Count == 0)
					IsEnabled = false;

				// Group by output index, and create a 3 separate expandos, one for each direction
				//
				// Example rule:
				//|		|-> [SplitterSortRule].Value[2]
				//|		|  .Value =
				//|		|	/ List with 2 elements:
				//|		|	|-> [ObjectProperty] /Script/FactoryGame.FGWildCardDescriptor     -> Any item
				//|		|	 or [ObjectProperty] /Script/FactoryGame.FGNoneDescriptor         -> Output closed
				//|		|	 or [ObjectProperty] /Script/FactoryGame.FGAnyUndefinedDescriptor -> Any item not specified in filters
				//|		|	 or [ObjectProperty] (empty .PathName)
				//|		|	 or [ObjectProperty] /Game/FactoryGame/Resource/RawResources/OreBauxite/Desc_OreBauxite.Desc_OreBauxite_C
				//|		|	|  .LevelName = str:''
				//|		|	|  .PathName = str:'/Script/FactoryGame.FGWildCardDescriptor'
				//|		|	|  .Name = str:'ItemClass'
				//|		|	|  .Length = Int32:49
				//|		|	|  .Index = Int32:0
				//|		|	|  .Value = <empty>
				//|		|	|-> [IntProperty] OutputIndex
				//|		|	|  .Name = str:'OutputIndex'
				//|		|	|  .Length = Int32:4
				//|		|	|  .Index = Int32:0
				//|		|	|  .Value = Int32:0
				//|		|	\ end of list
				_groups = rules.GroupBy(r => (int)(r.Value.Named("OutputIndex") as P.IntProperty).Value);
				foreach (var group in _groups)
				{
					string label = ((OutputIndex)group.Key).ToString();
					if (Translate.Has(label))
						label = Translate._(label);
					_AddView(label, group.Key, group.ToList());
				}
			}

			internal void _AddView(string label, int output, List<P.SplitterSortRule> rules)
			{
				Expando expando = new Expando(this, label, null);

				//TODO: Radio group: None, All, Items, with Items => LVC
				Radiogroup grp = new Radiogroup(this, "Rule", rules);
				expando.Childs.Add(grp);

				RowConverter converter = new RowConverter(this, output, rules);
				Func<RowConverter.Parameter, Binding> binder = (param) => {
					return new Binding() {
						Converter = converter,
						ConverterParameter = param,
					};
				};

				// Note that FrameworkElementFactory is somewhat dangerous, better use XamlLoader on a string value.
				FrameworkElementFactory factory = new FrameworkElementFactory(typeof(ContentControl));
				factory.SetBinding(ContentProperty, binder(RowConverter.Parameter.Item));
				DataTemplate item_tmpl = new DataTemplate() {
					VisualTree = factory,
				};
				factory = new FrameworkElementFactory(typeof(ContentControl));
				factory.SetBinding(ContentProperty, binder(RowConverter.Parameter.Button));
				DataTemplate btn_tmpl = new DataTemplate() {
					VisualTree = factory,
				};
				//=> Move into Supplements.Helper ... or even CoreLib.Helpers?

				LVC.Collection rows = new LVC.Collection();
				for (int i = 0; i < converter.RowCount; ++i)
					rows.Add(i);

				LVC.ColumnDefinitions columns = new LVC.ColumnDefinitions() {
					new LVC.ColumnDefinition("#", 25, binder(RowConverter.Parameter.Index)),
					new LVC.ColumnDefinition("Item", 250, null, item_tmpl),
					new LVC.ColumnDefinition("", 32, null, btn_tmpl),
				};

				LVC lvc = new LVC() {
					Columns = columns,
					Value = rows
				};
				expando.Childs.Add(lvc);

				//TODO: Add 'Add rule' button

				_childs.Add(expando);
			}

			internal void OnDeleteRule(int output, int rule)
			{
				//TODO:
			}

			internal class Radiogroup : MultiValueControl<bool>
			{
				public Radiogroup(IElement parent, string label, object obj)
					: base(parent, label, null)
				{
					_value = new bool[4] { false, false, false, false };
					List<P.SplitterSortRule> rules = obj as List<P.SplitterSortRule>;
					if (rules != null && rules.Count > 0)
					{
						P.ObjectProperty obj_prop = rules[0].Value.Named("ItemClass") as P.ObjectProperty;
						if (obj_prop != null && !str.IsNullOrEmpty(obj_prop.PathName))
						{
							string pathname = obj_prop.PathName.LastName();
							_value[0] = (pathname == "FGNoneDescriptor");
							_value[1] = (pathname == "FGWildCardDescriptor");
							_value[2] = (pathname == "FGAnyUndefinedDescriptor");
							_value[3] = !pathname.StartsWith("FG");
						}
					}
				}

				internal override void _CreateChilds()
				{
					Thickness margin = new Thickness(0, 1, 20, 1);
					_childs.Add(new RadioButton() { Content = Translate._("FGNoneDescriptor"), IsChecked = _value[0], Margin = margin });
					_childs.Add(new RadioButton() { Content = Translate._("FGWildCardDescriptor"), IsChecked = _value[1], Margin = margin });
					_childs.Add(new RadioButton() { Content = Translate._("FGAnyUndefinedDescriptor"), IsChecked = _value[2], Margin = margin });
					_childs.Add(new RadioButton() { Content = Translate._("ItemList"), IsChecked = _value[3], Margin = new Thickness(0, 1, 0, 1) });
				}

				protected override void _PropertyChanged(object sender, PropertyChangedEventArgs e)
				{
					for (int i = 0; i < _childs.Count; ++i)
						_value[i] = (_childs[i] as RadioButton).IsChecked.GetValueOrDefault();
					base._PropertyChanged(this, e);
				}
			}

			internal class LVC : ListViewControl<int?> { }

			private class RowConverter : IValueConverter
			{
				internal enum Parameter { Index, Item, Button };

				internal RowConverter(SortRules owner, int output, List<P.SplitterSortRule> rules)
				{
					_owner = owner;
					_output = output;
					_Setup(rules);
				}


				internal int RowCount { get { return _rules.Count; } }


				public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
				{
					_last_index = (int)value;
					switch ((Parameter)parameter)
					{
						case Parameter.Index:  return _GetIndex(_last_index);
						case Parameter.Item:   return _GetItem(_last_index);
						case Parameter.Button: return _GetButton(_last_index);
					}
					return null;
				}

				public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
				{
					return Binding.DoNothing;
				}


				private void _Setup(List<P.SplitterSortRule> rules)
				{
					_rules = rules;

					_last_index = -1;

					_version = MainWindow.CurrFile.Header.GetVersion();

					if (_all_items == null)
						_all_items = ItemCombobox.Collection.FromItemTable(ItemTable.All);
				}

				#region Getter
				private object _GetIndex(int index)
				{
					return index.ToString();
				}

				private object _GetItem(int index)
				{
					//|		|-> [SplitterSortRule].Value[2]
					//|		|  .Value =
					//|		|	/ List with 2 elements:
					//|		|	|-> [ObjectProperty] /Script/FactoryGame.FGWildCardDescriptor
					//|		|	 or [ObjectProperty] /Script/FactoryGame.FGNoneDescriptor
					//|		|	 or [ObjectProperty] /Script/FactoryGame.FGAnyUndefinedDescriptor
					//|		|	 or [ObjectProperty] (empty .PathName)
					//|		|	 or [ObjectProperty] /Game/FactoryGame/Resource/RawResources/OreBauxite/Desc_OreBauxite.Desc_OreBauxite_C
					//|		|	|  .LevelName = str:''
					//|		|	|  .PathName = str:'/Script/FactoryGame.FGWildCardDescriptor'
					//|		|	|  .Name = str:'ItemClass'
					//|		|	|  .Length = Int32:49
					//|		|	|  .Index = Int32:0
					//|		|	|  .Value = <empty>
					//|		|	|-> [IntProperty] OutputIndex
					//|		|	|  .Name = str:'OutputIndex'
					//|		|	|  .Length = Int32:4
					//|		|	|  .Index = Int32:0
					//|		|	|  .Value = Int32:0
					//|		|	\ end of list
					P.SplitterSortRule rule = _rules[index];
					P.ObjectProperty itemclass = rule.Value.Named("ItemClass") as P.ObjectProperty;
					if (itemclass != null)
					{	
						str pathname = itemclass.PathName;
						//TODO: Always create a combobox here?
						if (!str.IsNullOrEmpty(pathname) && !pathname.ToString().Contains(".FG"))
						{
							ItemCombobox cmb = new ItemCombobox(pathname, null, _all_items);
							cmb.SelectionChanged += _ItemChanged;
							return cmb;
						}
					}
					return null;
				}
				private void _ItemChanged(object sender, SelectionChangedEventArgs e)
				{
					//throw new NotImplementedException();
					//TODO:
				}

				private object _GetButton(int index)
				{
					Button btn = new Button() {
						Content = new Image() { Source = new BitmapImage(Helpers.GetResourceUri("Button.Delete.png")) },
						Width = 20,
						Height = 20,
						ToolTip = "Delete rule",//TODO: Translate._("?.Delete"),
						Tag = index,
					};
					btn.Click += _DeleteClick;
					return btn;
				}
				private void _DeleteClick(object sender, RoutedEventArgs e)
				{
					//throw new NotImplementedException();
					if (sender is Button)
						_owner.OnDeleteRule(_output, (int)((sender as Button).Tag));
				}
				#endregion


				private SortRules                      _owner;
				private int                            _output;
				private List<P.SplitterSortRule>       _rules;
				private int                            _last_index;
				private VersionTable.Version           _version;
				private static ItemCombobox.Collection _all_items;
			}

			private P.ArrayProperty _array;
			private IEnumerable<IGrouping<int, P.SplitterSortRule>> _groups;
		}

		internal class PlatformConnection : Expando
		{
			public PlatformConnection(IElement parent, string label, object obj)
				: base(parent, label, null)
			{
				_object = obj as P.Object;
			}

			//-> [Object] /Script/FactoryGame.FGTrainPlatformConnection
			//  .ClassName = str:'/Script/FactoryGame.FGTrainPlatformConnection'
			//  .LevelName = str:'Persistent_Level'
			//  .PathName = str:'Persistent_Level:PersistentLevel.Build_TrainStation_C_0.PlatformConnection0'
			//  .OuterPathName = str:'Persistent_Level:PersistentLevel.Build_TrainStation_C_0'
			//  .EntityObj =
			//	-> [Entity] 
			//	  .LevelName = <empty>
			//	  .PathName = <empty>
			//	  .Children = <empty>
			//	  .Unknown = Int32:0
			//	  .Missing = <empty>
			//	  .Private = <empty>
			//	  .Value =
			//		/ List with 1 elements:
			//		|-> [BoolProperty] mComponentDirection
			//		|  .Name = str:'mComponentDirection'
			//		|  .Length = Int32:0
			//		|  .Index = Int32:0
			//		|  .Value = Byte:1
			//		|-> [ObjectProperty] Persistent_Level:PersistentLevel.Build_TrainDockingStation_C_2
			//		|  .LevelName = str:'Persistent_Level'
			//		|  .PathName = str:'Persistent_Level:PersistentLevel.Build_TrainDockingStation_C_2'
			//		|  .Name = str:'platformOwner'
			//		|  .Length = Int32:88
			//		|  .Index = Int32:0
			//		|  .Value = <empty>
			//		|-> [ObjectProperty] Persistent_Level:PersistentLevel.Build_TrainDockingStation_C_2.PlatformConnection0
			//		|  .LevelName = str:'Persistent_Level'
			//		|  .PathName = str:'Persistent_Level:PersistentLevel.Build_TrainDockingStation_C_2.PlatformConnection0'
			//		|  .Name = str:'mConnectedTo'
			//		|  .Length = Int32:108
			//		|  .Index = Int32:0
			//		|  .Value = <empty>
			//		\ end of list
			internal override void _CreateChilds()
			{
				Header = "Platform connection";

				P.Entity entity = _object.EntityObj as P.Entity;
				if (entity.Value != null)
				{
					P.Property prop = entity.Value.Named("platformOwner");
					if (prop is P.IntProperty)
					{
						P.ObjectProperty objprop = prop as P.ObjectProperty;
						string pathname = objprop.PathName.ToString();
						if (!string.IsNullOrEmpty(pathname))
							_childs.Add(MainFactory.Create(this, "Owner", pathname, true));
					}

					prop = entity.Value.Named("mComponentDirection");
					if (prop is P.BoolProperty)
					{
						byte pos = (byte) (prop as P.BoolProperty).Value;
						_childs.Add(MainFactory.Create(this, "Direction", pos, true));
					}

					prop = entity.Value.Named("mConnectedTo");
					if (prop is P.ObjectProperty)
					{
						P.ObjectProperty objprop = prop as P.ObjectProperty;
						string pathname = objprop.PathName.ToString();
						if (!string.IsNullOrEmpty(pathname))
							_childs.Add(MainFactory.Create(this, "Connected to", pathname, true));
					}
				}
			}

			private P.Object _object;
		}

		internal class TrackConnection : Expando
		{
			public TrackConnection(IElement parent, string label, object obj)
				: base(parent, label, null)
			{
				_object = obj as P.Object;
			}

			//-> [Object] /Script/FactoryGame.FGRailroadTrackConnectionComponent
			//  .ClassName = str:'/Script/FactoryGame.FGRailroadTrackConnectionComponent'
			//  .LevelName = str:'Persistent_Level'
			//  .PathName = str:'Persistent_Level:PersistentLevel.Build_RailroadTrack_C_0.TrackConnection0'
			//  .OuterPathName = str:'Persistent_Level:PersistentLevel.Build_RailroadTrack_C_0'
			//  .EntityObj =
			//	-> [Entity] 
			//	  .LevelName = <empty>
			//	  .PathName = <empty>
			//	  .Children = <empty>
			//	  .Unknown = Int32:0
			//	  .Missing = <empty>
			//	  .Private = <empty>
			//	  .Value =
			//		/ List with 2 elements:
			//		|-> [ArrayProperty] mConnectedComponents
			//		|  .InnerType = str:'ObjectProperty'
			//		|  .Name = str:'mConnectedComponents'
			//		|  .Length = Int32:104
			//		|  .Index = Int32:0
			//		|  .Value =
			//		|	/ List with 1 elements:
			//		|	|-> [ObjectProperty] Persistent_Level:PersistentLevel.Build_RailroadTrack_C_43.TrackConnection0
			//		|	|  .LevelName = str:'Persistent_Level'
			//		|	|  .PathName = str:'Persistent_Level:PersistentLevel.Build_RailroadTrack_C_43.TrackConnection0'
			//		|	|  .Length = Int32:0
			//		|	|  .Index = Int32:0
			//		|	\ end of list
			//		|-> [IntProperty] mSwitchPosition
			//		|  .Name = str:'mSwitchPosition'
			//		|  .Length = Int32:4
			//		|  .Index = Int32:0
			//		|  .Value = Int32:0
			//		\ end of list
			internal override void _CreateChilds()
			{
				Header = "Track connection";

				P.Entity entity = _object.EntityObj as P.Entity;
				if (entity.Value != null)
				{
					P.Property prop = entity.Value.Named("mConnectedComponents");
					if (prop is P.ArrayProperty)
					{
						P.ArrayProperty arr = prop as P.ArrayProperty;
						if (arr.Value is P.Properties)
						{
							P.Properties props = arr.Value as P.Properties;
							int index = 0;
							foreach (P.Property p in props)
							{
								P.ObjectProperty objprop = p as P.ObjectProperty;
								string pathname = objprop.PathName.ToString();
								if (!string.IsNullOrEmpty(pathname))
								{
									string label = string.Format("Connection #{0}", index);
									_childs.Add(MainFactory.Create(this, label, pathname, true));
									++index;
								}
							}
						}
					}

					prop = entity.Value.Named("mSwitchPosition");
					if (prop is P.IntProperty)
					{
						int pos = (int) (prop as P.IntProperty).Value;
						//_childs.Add(MainFactory.Create(this, "Switch position", (pos == 0 ? "Left" : "Right"), true));
						_childs.Add(MainFactory.Create(this, "Switch position", pos, true));
					}
				}
			}

			private P.Object _object;
		}

		internal class FeetOffsets : Expando
		{
			public FeetOffsets(IElement parent, string label, object obj)
				: base(parent, label, obj)
			{ }

			internal override void _CreateChilds()
			{
				Header = "Feet offsets";

				P.Object obj = Tag as P.Object;

				// Buildings with dynamic legs will also carry those offsets
				//
				//-> [Object] /Script/FactoryGame.FGFactoryLegsComponent
				//  .ClassName = str:'/Script/FactoryGame.FGFactoryLegsComponent'
				//  .LevelName = str:'Persistent_Level'
				//  .PathName = str:'Persistent_Level:PersistentLevel.Build_ConstructorMk1_C_118.FGFactoryLegs'
				//  .OuterPathName = str:'Persistent_Level:PersistentLevel.Build_ConstructorMk1_C_118'
				//  .EntityObj =
				//	-> [Entity] 
				//	  .LevelName = <empty>
				//	  .PathName = <empty>
				//	  .Children = <empty>
				//	  .Unknown = Int32:0
				//	  .Missing = <empty>
				//	  .Private = <empty>
				//	  .Value =
				//		/ List with 1 elements:
				//		|-> [ArrayProperty] mCachedFeetOffset
				//		|  .InnerType = str:'StructProperty'
				//		|  .Name = str:'mCachedFeetOffset'
				//		|  .Length = Int32:665
				//		|  .Index = Int32:0
				//		|  .Value =
				//		|	-> [StructProperty] mCachedFeetOffset
				//		|	  .Unknown = list<Byte>(17):[0,]
				//		|	  .Name = str:'mCachedFeetOffset'
				//		|	  .Length = Int32:580
				//		|	  .Index = Int32:0
				//		|	  .Value =
				//		|		/ List with 4 elements:
				//		|		|-> [FeetOffset].Value[3]
				P.Entity entity = obj.EntityObj as P.Entity;
				if (entity == null || entity.Value == null || entity.Value.Count == 0)
					return;
				P.ArrayProperty arr = entity.Value[0] as P.ArrayProperty;
				if (arr == null)
					return;
				P.StructProperty stru = arr.Value as P.StructProperty;
				if (stru == null)
					return;
				var feets = (stru.Value as P.Properties).ListOf<P.FeetOffset>();

				if (feets.Count() == 0)
					IsEnabled = false;

				// SaveVersion 20+ changed properties contained:
				// - 'FeetName' was replaced with 'FeetIndex'
				// - 'ShouldShow' was removed
				//
				//|	  .Value =
				//|		/ List with 4 elements:
				//|		...
				//|		\ end of list
				//
				LVC.ColumnDefinitions columns = null;
				LVC.Collection rows = new LVC.Collection();
				P.Property prop;

				if (MainWindow.CurrFile.Header.SaveVersion < 20)
				{
					//|		|-> [FeetOffset].Value[3]
					//|		|  .Value =
					//|		|	/ List with 3 elements:
					//|		|	|-> [NameProperty] FeetName
					//|		|	|  .Name = str:'FeetName'
					//|		|	|  .Length = Int32:12
					//|		|	|  .Index = Int32:0
					//|		|	|  .Value = str:'foot_01'
					//|		|	|-> [FloatProperty] OffsetZ
					//|		|	|  .Name = str:'OffsetZ'
					//|		|	|  .Length = Int32:4
					//|		|	|  .Index = Int32:0
					//|		|	|  .Value = Single:-10,00708
					//|		|	|-> [BoolProperty] ShouldShow
					//|		|	|  .Name = str:'ShouldShow'
					//|		|	|  .Length = Int32:0
					//|		|	|  .Index = Int32:0
					//|		|	|  .Value = Byte:1
					//|		|	\ end of list
					foreach (P.FeetOffset ofs in feets)
					{
						string label = "?";
						prop = ofs.Value.Named("FeetName");
						if (prop is P.NameProperty)
							label = ((prop as P.NameProperty).Value as str).ToString();

						string offset = "?";
						prop = ofs.Value.Named("OffsetZ");
						if (prop is P.FloatProperty)
							offset = ((float)(prop as P.FloatProperty).Value).ToString("F7");

						string show = "?";
						prop = ofs.Value.Named("ShouldShow");
						if (prop is P.BoolProperty)
							show = ((byte)(prop as P.BoolProperty).Value) != 0 ? "Yes" : "No";

						rows.Add(new object[] {
							label,
							offset,
							show,
						});
					}

					columns = new LVC.ColumnDefinitions() {
						new LVC.ColumnDefinition("Name", 100, new Binding(".[0]")),
						new LVC.ColumnDefinition("Offset", 150, new Binding(".[1]"), null, HorizontalAlignment.Right),
						new LVC.ColumnDefinition("Should show?", 100, new Binding(".[2]")),
					};
				}
				else
				{
					//|		|-> [FeetOffset].Value[2]
					//|		|  .Value =
					//|		|	/ List with 2 elements:
					//|		|	|-> [ByteProperty] FeetIndex
					//|		|	|  .Unknown = str:'None'
					//|		|	|  .Name = str:'FeetIndex'
					//|		|	|  .Length = Int32:1
					//|		|	|  .Index = Int32:0
					//|		|	|  .Value = Byte:1
					//|		|	|-> [FloatProperty] OffsetZ
					//|		|	|  .Name = str:'OffsetZ'
					//|		|	|  .Length = Int32:4
					//|		|	|  .Index = Int32:0
					//|		|	|  .Value = Single:0
					//|		|	\ end of list
					foreach (P.FeetOffset ofs in feets)
					{
						string label = "?";
						prop = ofs.Value.Named("FeetIndex");
						if (prop is P.ByteProperty)
							label = ((byte)(prop as P.ByteProperty).Value).ToString();

						string offset = "?";
						prop = ofs.Value.Named("OffsetZ");
						if (prop is P.FloatProperty)
							offset = ((float)(prop as P.FloatProperty).Value).ToString("F7");

						rows.Add(new object[] {
							label,
							offset,
						});
					}

					columns = new LVC.ColumnDefinitions() {
						new LVC.ColumnDefinition("Index", 50, new Binding(".[0]")),
						new LVC.ColumnDefinition("Offset", 150, new Binding(".[1]"), null, HorizontalAlignment.Right),
					};
				}

				LVC lvc = new LVC() {
					Columns = columns,
					Value = rows,
				};
				if (rows.Count == 0)
					lvc.IsEnabled = false;

				_childs.Add(lvc);
			}

			internal class LVC : ListViewControl<object[]> { }
		}

		private static string[] _excluded_props = new string[] { "ClassName", "LevelName", "PathName", "OuterPathName" };

	}


	#region EXPERIMENTAL
	// EXPERIMENTAL viewers
	//

	// Container for private, class-related data extracted from .Missing array with deep analysis options being enabled.
	// Both reading and visualizing might cause an unwanted exception or even crash our program, so be careful!
	internal class PrivateData : PropertyList
	{
		public PrivateData(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}


	// * /Game/FactoryGame/Character/Player/...

	//class BP_PlayerState_C(Accessor):
	//	pass #TODO: Data yet unknown -> Keep .Missing and create report


	// * /Game/FactoryGame/-Shared/Blueprint/...

	// Stores a list of circuit classes, e.g.
	//		.PathName = Persistent_Level:PersistentLevel.CircuitSubsystem.FGPowerCircuit_15
	// with .Index being the same as appended to .PathName (in this case =15)
	//
	// Those are the ones listed in
	//		/Script/FactoryGame/FGPowerCircuit/*
	internal class BP_CircuitSubsystem_C : Collected
	{
	//CLS_(BP_CircuitSubsystem_C, Collected)
	//	PUB_i(Index)
	//	READ
	//		Index = reader->ReadInt();
	//		//LevelName = reader.readStr()
	//		//PathName = reader.readInt()
	//		return Collected::Read(reader);
	//	READ_END
	//CLS_END
		public BP_CircuitSubsystem_C(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}

	// Contains path to player state which this tied to this game state, e.g.:
	//		.PathName = Persistent_Level:PersistentLevel.BP_PlayerState_C_0
	internal class BP_GameState_C : Collected
	{
	//CLS_(BP_GameState_C, Collected)
	//CLS_END
		public BP_GameState_C(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}

	//class BP_RailroadSubsystem_C(Accessor):
	//	pass #TODO: Yet no data avail to inspect -> Keep .Missing and create report

	//class BP_GameMode_C(Accessor):
	//	pass #TODO: Yet no data avail to inspect -> Keep .Missing and create report


	// * /Game/FactoryGame/Buildable/Factory/...

	// Describes an item on a belt:
	//		.PathName = /Game/FactoryGame/Resource/Parts/Fuel/Desc_Fuel.Desc_Fuel_C
	// with its offset along belt's "movement vector"(?):
	//		.Translate = [Vector] 0 / 0 / 300,8046000
	// (X+Y always empty?)
	internal class Build_ConveyorBelt : Expando
	{
	//CLS_(Build_ConveyorBelt, Property)
	//	PUB_i(Index)
	//	PUB_s(ItemName)
	//	PUB(Translate, Vector^)
	//	READ
	//		Index = reader->ReadInt();
	//		ItemName = reader->ReadString();
	//		// Might be a translation for object?
	//		//self.Unknown = reader.readStruct(TYPE_UNKNOWN_12)#reader.readNByte(12)
	//		Translate = gcnew Vector(this);
	//		Translate->Read(reader); // At least no error reading :D
	//	READ_END
	//CLS_END
		public Build_ConveyorBelt(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}

	internal class Build_ConveyorBeltMk1_C : Build_ConveyorBelt
	{
	//CLS_(Build_ConveyorBeltMk1_C, Build_ConveyorBelt)
	//CLS_END
		public Build_ConveyorBeltMk1_C(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}

	internal class Build_ConveyorBeltMk2_C : Build_ConveyorBelt
	{
	//CLS_(Build_ConveyorBeltMk2_C, Build_ConveyorBelt)
	//CLS_END
		public Build_ConveyorBeltMk2_C(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}

	internal class Build_ConveyorBeltMk3_C : Build_ConveyorBelt
	{
	//CLS_(Build_ConveyorBeltMk3_C, Build_ConveyorBelt)
	//CLS_END
		public Build_ConveyorBeltMk3_C(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}

	internal class Build_ConveyorBeltMk4_C : Build_ConveyorBelt
	{
	//CLS_(Build_ConveyorBeltMk4_C, Build_ConveyorBelt)
	//CLS_END
		public Build_ConveyorBeltMk4_C(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}

	internal class Build_ConveyorBeltMk5_C : Build_ConveyorBelt
	{
	//CLS_(Build_ConveyorBeltMk5_C, Build_ConveyorBelt)
	//CLS_END
		public Build_ConveyorBeltMk5_C(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}

	internal class Build_ConveyorBeltMk6_C : Build_ConveyorBelt
	{
	//CLS_(Build_ConveyorBeltMk6_C, Build_ConveyorBelt)
	//CLS_END
		public Build_ConveyorBeltMk6_C(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}


	// Lift has exact same data layout as belts, but this might change
	// with future releases so an extra base class is used here
	// Describes an item on a lift:
	//		.PathName = /Game/FactoryGame/Resource/Parts/Fuel/Desc_Fuel.Desc_Fuel_C
	// with its offset along lift's "movement vector"(?):
	//		.Translate = [Vector] 0 / 0 / 300,8046000
	// (X+Y always empty?)
	internal class Build_ConveyorLift : Expando
	{
	//CLS_(Build_ConveyorLift, Property)
	//	PUB_i(Index)
	//	PUB_s(ItemName)
	//	PUB(Translate, Vector^)
	//	READ
	//		Index = reader->ReadInt();
	//		ItemName = reader->ReadString();
	//		Translate = gcnew Vector(this);
	//		Translate->Read(reader);
	//	READ_END
	//CLS_END
		public Build_ConveyorLift(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}

	internal class Build_ConveyorLiftMk1_C : Build_ConveyorLift
	{
	//CLS_(Build_ConveyorLiftMk1_C, Build_ConveyorLift)
	//CLS_END
		public Build_ConveyorLiftMk1_C(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}

	internal class Build_ConveyorLiftMk2_C : Build_ConveyorLift
	{
	//CLS_(Build_ConveyorLiftMk2_C, Build_ConveyorLift)
	//CLS_END
		public Build_ConveyorLiftMk2_C(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}

	internal class Build_ConveyorLiftMk3_C : Build_ConveyorLift
	{
	//CLS_(Build_ConveyorLiftMk3_C, Build_ConveyorLift)
	//CLS_END
		public Build_ConveyorLiftMk3_C(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}

	internal class Build_ConveyorLiftMk4_C : Build_ConveyorLift
	{
	//CLS_(Build_ConveyorLiftMk4_C, Build_ConveyorLift)
	//CLS_END
		public Build_ConveyorLiftMk4_C(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}

	internal class Build_ConveyorLiftMk5_C : Build_ConveyorLift
	{
	//CLS_(Build_ConveyorLiftMk5_C, Build_ConveyorLift)
	//CLS_END
		public Build_ConveyorLiftMk5_C(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}

	internal class Build_ConveyorLiftMk6_C : Build_ConveyorLift
	{
	//CLS_(Build_ConveyorLiftMk6_C, Build_ConveyorLift)
	//CLS_END
		public Build_ConveyorLiftMk6_C(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}


	// Contains exactly 2 connectors this line connects. 
	// Could be either power poles or machines, e.g.:
	//		.PathName = Persistent_Level:PersistentLevel.Build_PowerPoleMk1_C_960.PowerConnection
	// and
	//		.PathName = Persistent_Level:PersistentLevel.Build_PowerPoleMk1_C_935.PowerConnection
	// (There might be more connection "types" in future, e.g. logistical ones as with Factorio?)
	internal class Build_PowerLine_C : Collected
	{
	//[FixedCount(2)]
	//CLS_(Build_PowerLine_C, Collected)
	//CLS_END
		public Build_PowerLine_C(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }

		//internal override void _CreateChilds()
		//{
		//	P.Collected c = Tag as P.Collected;
		//
		//	foreach (P.Property prop in c.V )
		//}
	}


	// * /Game/FactoryGame/Buildable/Vehicle/...

	internal class BP_Vehicle : Expando
	{
	//CLS_(BP_Vehicle, Property)
	//	PUB_s(Node)
	//	PUB_ab(Unknown)
	//	READ
	//		// Seems like some animation data?
	//		Node = reader->ReadString();
	//		//TODO: Crack those 53 bytes
	//		Unknown = ReadBytes(reader, 53);
	//	READ_END
	//CLS_END
		public BP_Vehicle(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}

	internal class BP_Tractor_C : BP_Vehicle
	{
	//CLS_(BP_Tractor_C, BP_Vehicle)
	//CLS_END
		public BP_Tractor_C(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}

	internal class BP_Truck_C : BP_Vehicle
	{
	//CLS_(BP_Truck_C, BP_Vehicle)
	//CLS_END
		public BP_Truck_C(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}

	internal class BP_Explorer_C : BP_Vehicle
	{
	//CLS_(BP_Explorer_C, BP_Vehicle)
	//CLS_END
		public BP_Explorer_C(IElement parent, string label, object obj) 
			: base(parent, label, obj)
		{ }
	}
	#endregion

}
