using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using SatisfactorySavegameTool.Dialogs;

using CoreLib;

using Savegame;
using Savegame.Properties;

namespace SatisfactorySavegameTool.Panels
{

	public class DetailsPanel : StackPanel
	{
		internal static readonly string EMPTY = Translate._("DetailsPanel.Empty");

		public DetailsPanel()
			: base()
		{ }


		public void ShowProperty(Property prop)
		{
			_ClearAll();

			Log.Info("Adding property {0}", prop);
			Expando exp;
			if (prop == null)
			{
				exp = _Add(null, Translate._("DetailsPanel.Empty"), prop);
				exp.IsEnabled = false;
			}
			else
			{
				exp = _Add(null, null, prop);
				exp.IsExpanded = true;
			}
			Children.Add(exp);
		}


		internal void _ClearAll()
		{
			Children.Clear();
		}

		internal Expando _Add(Expando parent, string name, Property prop)
		{
			string label;
			ValueControl ctrl;

			if (prop != null)
			{
				// Those are to be moved into explicit type handlers???
				if (prop is ArrayProperty)
				{
					ArrayProperty array_p = prop as ArrayProperty;
					if (array_p.Name.ToString() == "mFogOfWarRawData")
					{
						parent.AddRow(new ImageControl(array_p.Name.ToString(), (byte[]) array_p.Value));
						return parent;
					}
					//...more to come? We'll see
				}

				// Those are to be moved into explicit type handlers???
				if (prop is StructProperty)
				{
					StructProperty struct_p = prop as StructProperty;
					if (struct_p.Value != null && struct_p.Index == 0 && !struct_p.IsArray)
					{
						bool process = (struct_p.Unknown == null) || (struct_p.Unknown.Length == 0);
						if (!process)
						{
							int sum = 0;
							foreach(byte b in struct_p.Unknown)
								sum += b;
							process = (sum == 0);
						}
						if (process)
						{
							// Replace it with type of actual value
							/*TODO:
							t = prop.Value.TypeName
							if t in globals():
								cls = globals()[t]
								cls(parent_pane, parent_sizer, prop.Name, prop.Value)
								return parent_pane, parent_sizer
							*/
							ctrl = ControlFactory.Create(struct_p.Name.ToString(), struct_p.Value as Property);
							if (ctrl != null)
							{
								parent.AddRow(ctrl);
								return parent;
							}
						}
					}
				}

				/*TODO:
				t = prop.TypeName
				if t in globals():
					cls = globals()[t]
					cls(parent_pane, parent_sizer, name, prop)
					return parent_pane, parent_sizer
				*/
				ctrl = ControlFactory.Create(name, prop);
				if (ctrl != null)
				{
					parent.AddRow(ctrl);
					return parent;
				}
			}

			label = (prop != null) ? prop.ToString() : name;
			Expando exp = new Expando(parent, label);

			if (prop != null)
			{
				Dictionary<string,object> childs = prop.GetChilds();
				if (childs.Count == 0)
					exp.IsEnabled = false;
				else
					_AddRecurs(exp, childs);
			}

			return exp;
		}

		internal void _AddRecurs(Expando parent, Dictionary<string,object> childs)
		{
			// Sort children first by both their "type" and name
			var names = childs.Keys.OrderBy((s) => s);
			List<string> simple = new List<string>();
			List<string> simple2 = new List<string>();
			List<string> props = new List<string>();
			List<string> sets = new List<string>();
			List<string> last = new List<string>();
			foreach (string name in names)
			{
				object sub = childs[name];
				if (sub is System.Collections.ICollection)//isinstance(sub, (list,dict)):
				{
					if (name == "Missing")
						last.Add(name);
					else if (name == "Unknown")//and isinstance(sub, list)):
						last.Add(name);
					else
						sets.Add(name);
				}
				else if (sub is Property)
				{
					//Property prop = sub as Property;
					//if (prop.TypeName in globals)
					//if (sub is Entity)
					//	simple2.Add(name);
					//else
					//	props.Add(name);
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

			foreach (string name in order)//childs.Keys)
			{
				object sub = childs[name];
				Log.Info("_AddRecurs: {0}", sub != null ? sub.GetType() : sub);

				//TODO: Those are to be moved into explicit type handlers???
				// Do some testing on property name here as some do need special handling, e.g.
				// - Length : Must be readonly as this will be calculated based on properties stored.
				// - Missing: Should show a (readonly) hex dump
				if (name == "Length")
				{
					parent.AddRow(new ReadonlySimpleValueControl<int>(name, (int)sub));
				}
				else if (name == "Missing")
				{
					parent.AddRow(new HexdumpControl(name, sub as byte[]));
				}
				else if (name == "Unknown")// && (sub is Array || sub is System.Collections.IEnumerable))
				{
					// There are several .Unknown properties, dump only list-based ones
					parent.AddRow(new HexdumpControl(name, sub as byte[]));
				}
				else if (name == "WasPlacedInLevel" || name == "NeedTransform")
				{
					parent.AddRow(name, new BoolControl((int)sub));
				}
				else if (sub is System.Collections.ICollection)
				{
					System.Collections.ICollection e = sub as System.Collections.ICollection;
					string label = string.Format("{0} [{1}]", name, e.Count);
					Expando sub_exp = _Add(parent, label, null);
					if (e.Count == 0)
						sub_exp.IsEnabled = false;
					else
						foreach (object obj in e)
						{
							if (obj is Property)
								_Add(sub_exp, name/*"*" + obj.ToString()*/, (Property)obj);
							//else?
						}
				}
				else if (sub is Property)
				{
					_Add(parent, name, sub as Property);
				}
				else
				{
					parent.AddRow(ControlFactory.CreateSimple(name, sub));
				}
			}

		}
	}

	internal class Expando : Expander
	{
		internal Expando(Expando parent, string label)
		{
			Header = label;
			HorizontalContentAlignment = HorizontalAlignment.Stretch;

			_grid = new Grid();
			_grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength( 0, GridUnitType.Auto ) });
			_grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength( 1, GridUnitType.Star ) });
			_grid.Margin = new Thickness(10, 4, 5, 4);//LTRB
			_grid.Background = Brushes.Transparent;

			Border b = new Border() {
				BorderBrush = Brushes.DarkGray,
				BorderThickness = new Thickness(1, 0, 0, 1),//LTRB
				Margin = new Thickness(10, 0, 0, 0),//LTRB
			};
			b.Child = _grid;
			Content = b;

			if (parent != null)
				parent.AddRow(this);
		}

		internal void AddRow(UIElement element)
		{
			int index = _AddRow();

			Grid.SetRow(element, index);
			Grid.SetColumn(element, 0);
			Grid.SetColumnSpan(element, 2);
			_grid.Children.Add(element);
		}

		internal void AddRow(string label, UIElement element)
		{
			int index = _AddRow();

			Label ctrl = new Label() {
				Content = label + ":",
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Stretch,
				VerticalContentAlignment = VerticalAlignment.Center,
			};
			Thickness t = ctrl.Padding;
			t.Top = t.Bottom = 0;
			ctrl.Padding = t;
			Grid.SetRow(ctrl, index);
			Grid.SetColumn(ctrl, 0);
			_grid.Children.Add(ctrl);

			Grid.SetRow(element, index);
			Grid.SetColumn(element, 1);
			_grid.Children.Add(element);
		}

		internal void AddRow(ValueControl vc)
		{
			AddRow(vc.Label, vc.Ctrl);
		}

		internal int _AddRow()
		{
			RowDefinition rowdef = new RowDefinition() {
				Height = new GridLength(0, GridUnitType.Auto),
			};
			_grid.RowDefinitions.Add(rowdef);
			return _grid.RowDefinitions.Count - 1;
		}

		internal Grid _grid;
	}


	// Every control must implement this getter/setter pattern
	internal interface IValueContainer<_ValueType>
	{
		_ValueType Value { get; set; }
	}


	// Basic controls avail
	// 
	// Those will not only take care of displaying value correctly,
	// but will also take care of validating user input.
	// All controls MUST follow getter/setter pattern by supplying
	// both a Set() and Get() method.
	// 
	//TODO: Add validators to keep values in feasible limit

	internal class BoolControl : CheckBox, IValueContainer<int>
	{
		internal BoolControl(int val)
			: base()
		{
			//Width = new GridLength(100).Value;
			HorizontalAlignment = HorizontalAlignment.Left;
			VerticalAlignment = VerticalAlignment.Center;
			Margin = new Thickness(0, 2, 0, 2);
			Value = val;
		}

		public int Value
		{
			get { return (IsChecked.GetValueOrDefault() ? 1 : 0); }
			set { IsChecked = (value != 0); }
		}
	}

	internal class FloatControl : TextBox, IValueContainer<float>
	{
		internal readonly string _format = "{0:F7}"; //Translate._("");

		internal FloatControl(float val)
			: base()
		{
			Width = new GridLength(100).Value;
			HorizontalAlignment = HorizontalAlignment.Left;
			TextAlignment = TextAlignment.Right;
			Value = val;
		}

		public float Value
		{
			get
			{
				float f;
				if (!float.TryParse(Text, out f))
					throw new FormatException("Input for float value is invalid");
				return f;
			}
			set { Text = string.Format(_format, value); }
		}
	}

	internal class IntControl : TextBox, IValueContainer<int> // Might change to wx.SpinCtrl later
	{
		internal readonly string _format = "{0:#,#}"; //Translate._("");

		internal IntControl(int val)
			: base()
		{
			Width = new GridLength(val > 1e10 ? 150 : 100).Value;
			HorizontalAlignment = HorizontalAlignment.Left;
			TextAlignment = TextAlignment.Right;
			Value = val;
		}

		public int Value
		{
			get
			{
				int i;
				if (!int.TryParse(Text, out i))
					throw new FormatException("Input for integer value is invalid");
				return i;
			}
			set	{ Text = string.Format(_format, value); }
		}
	}

	internal class StrControl : TextBox, IValueContainer<string>
	{
		internal StrControl(string val)
			: base()
		{
			//Width = new GridLength(100, GridUnitType.Auto).Value;
			//Height = new GridLength(0, GridUnitType.Auto).Value;
			HorizontalAlignment = HorizontalAlignment.Stretch;
			VerticalAlignment = VerticalAlignment.Stretch;
			Value = val;
		}

		public string Value
		{
			get { return Text; }
			set	{ Text = value; }
		}
	}

	/*
	class ColorControl(wx.ColourPickerCtrl):
		"""
		Shows current color, opening wx.ColourDialog if clicked
		"""
		def __init__(self, parent, val:wx.Colour):
			super().__init__(parent)
			#self.Disable()# Let's hope this won't ruin visualization
			self.Unbind(wx.EVT_BUTTON)
			self.Set(val)

		def Set(self, val): self.Colour = val
		def Get(self):      return self.Colour
	# As disabling will change color to gray, we will use a simple 
	# display until we've added modifying savegames and saving.
	 */
	internal class ColorDisplay : Label, IValueContainer<Savegame.Properties.Color>
	{
		internal ColorDisplay(Savegame.Properties.Color color)
		{
			Width = new GridLength(200).Value;
			BorderBrush = System.Windows.Media.Brushes.DarkGray;
			BorderThickness = new Thickness(1);
			Value = color;
		}

		public Savegame.Properties.Color Value
		{
			get { return _value; }
			set {
				_value = value;
				System.Windows.Media.Color c = 
					System.Windows.Media.Color.FromArgb(value.A, value.R, value.G, value.B);
				Background = new SolidColorBrush(c);
				Content = string.Format("R:{0} / G:{1} / B:{2} / A:{3}", value.R, value.G, value.B, value.A);
			}
		}

		internal Savegame.Properties.Color _value;
	}

	internal class LinearColorDisplay : Label, IValueContainer<LinearColor>
	{
		internal LinearColorDisplay(LinearColor color)
		{
			Width = new GridLength(200).Value;
			BorderBrush = System.Windows.Media.Brushes.DarkGray;
			BorderThickness = new Thickness(1);
			Value = color;
		}

		public LinearColor Value
		{
			get { return _value; }
			set {
				_value = value;
				System.Windows.Media.Color c = 
					System.Windows.Media.Color.FromScRgb(value.A, value.R, value.G, value.B);
				Background = new SolidColorBrush(c);
				Content = string.Format("R:{0:F7} / G:{1:F7} / B:{2:F7} / A:{3:F7}", value.R, value.G, value.B, value.A);
			}
		}

		internal LinearColor _value;
	}


	// Actual value controls
	//
	// Those will combine label and one or more basic 
	// controls to fulfill a properties requirements

	internal abstract class ValueControl
	{
		internal ValueControl(object val)
		{
			_value = val;
		}

		internal string Label;
		internal UIElement Ctrl;

		internal object _value;
	}

	internal abstract class ValueControl<_ValueType> : ValueControl
	{
		internal ValueControl(object val) 
			: base(val)
		{ }

		internal virtual _ValueType Value
		{
			get { return (Ctrl as IValueContainer<_ValueType>).Value; }
			set { (Ctrl as IValueContainer<_ValueType>).Value = (_ValueType) _value; }
		}
	}


	internal class SimpleValueControl<_ValueType> : ValueControl<_ValueType>
	{
		internal SimpleValueControl(string label, object val)
			: base(val)
		{
			Label = label;
			Ctrl = ControlFactory.Create(val);
		}
	}

	internal class ReadonlySimpleValueControl<_ValueType> : SimpleValueControl<_ValueType>
	{
		internal ReadonlySimpleValueControl(string label, object val)
			: base(label, val)
		{
			Ctrl.IsEnabled = false;
		}
	}


	internal class HexdumpControl : ValueControl<byte[]>
	{
		internal HexdumpControl(string label, byte[] val)
			: base(val)
		{
			Label = label;
			Ctrl = new TextBox() {
				Text = CoreLib.Helpers.Hexdump(val, indent:0),
				FontFamily = new FontFamily("Consolas, FixedSys, Terminal"),
				FontSize = 12,
				//BorderBrush = Brushes.DarkGray,
				//BorderThickness = new Thickness(1),//LTRB
			};
		}

	}


	internal class ImageControl : ValueControl<byte[]>
	{
		internal ImageControl(string label, byte[] val)
			: base(val)
		{
			Label = label;

			// Build image
			_image = ImageHandler.ImageFromBytes(val, depth:4);

			Grid grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(0, GridUnitType.Auto) });

			_label = new Label() {
				Content = string.Format(Translate._("ImageControl.Label"), _image.PixelWidth, _image.PixelHeight, 4),
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Stretch,
				VerticalContentAlignment = VerticalAlignment.Center,
			};
			Grid.SetColumn(_label, 0);
			grid.Children.Add(_label);

			_button = new Button() {
				Content = Translate._("ImageControl.Button"),
				Width = 100,
				Height = 21,
			};
			_button.Click += _button_Click;
			Grid.SetColumn(_button, 1);
			grid.Children.Add(_button);

			Ctrl = grid;
		}

		private void _button_Click(object sender, RoutedEventArgs e)
		{
			var dlg = new ImageDialog(Application.Current.MainWindow, Translate._("ImageDialog.Title"), _image);
			dlg.ShowDialog();
			dlg.Close();
		}

		internal Label _label;
		internal Button _button;
		internal BitmapSource _image;

	}


	internal class VectorControl : ValueControl<object>
	{
		internal VectorControl(string label, Savegame.Properties.Vector val)
			: base(val)
		{
			Label = label;

			StackPanel panel = new StackPanel() {
				Orientation = Orientation.Horizontal,
			};

			panel.Children.Add(new FloatControl(val.X));
			panel.Children.Add(new FloatControl(val.Y));
			panel.Children.Add(new FloatControl(val.Z));

			Ctrl = panel;
		}
	}


	internal class ColorControl : ValueControl<Savegame.Properties.Color>
	{
		internal ColorControl(string label, Savegame.Properties.Color val)
			: base(val)
		{
			Label = label;
			Ctrl = new ColorDisplay(val);
			//TODO: Add separate fields for RGBA
		}
	}

	internal class LinearColorControl : ValueControl<LinearColor>
	{
		internal LinearColorControl(string label, LinearColor val)
			: base(val)
		{
			Label = label;
			Ctrl = new LinearColorDisplay(val);
			//TODO: Add separate fields for RGBA
		}
	}


	internal class QuatControl : ValueControl<object>
	{
		internal QuatControl(string label, Quat val)
			: base(val)
		{
			Label = label;

			StackPanel panel = new StackPanel() { Orientation = Orientation.Horizontal };
			panel.Children.Add(new FloatControl(val.A));
			panel.Children.Add(new FloatControl(val.B));
			panel.Children.Add(new FloatControl(val.C));
			panel.Children.Add(new FloatControl(val.D));

			Ctrl = panel;
		}
	}


	internal class ObjectControl : SimpleValueControl<string>
	{
		internal ObjectControl(string label, ObjectProperty val)
			: base(label, val.Name != null ? val.Name.ToString() : DetailsPanel.EMPTY)
		{ }
	}


	internal class EnumControl : SimpleValueControl<string>
	{
		internal EnumControl(string label, EnumProperty val)
			: base(label, val.Value.ToString())
		{ }
	}


	internal class NameControl : SimpleValueControl<string>
	{
		internal NameControl(string label, NameProperty val)
			: base(label, val.Value.ToString())
		{ }
	}


	internal class TextControl : SimpleValueControl<string>
	{
		internal TextControl(string label, TextProperty val)
			: base(label, val.Value.ToString())
		{ }
	}


	internal static class ControlFactory
	{
		internal static UIElement Create(object val)
		{
			if (val is bool)
				return new BoolControl((int) val);
			else if (val is int)
				return new IntControl((int) val);
			else if (val is float)
				return new FloatControl((float) val);
			return new StrControl(val != null ? val.ToString() : DetailsPanel.EMPTY);
		}

		internal static ValueControl CreateSimple(string label, object val, bool read_only = false)
		{
			if (!read_only)
			{
				if (val is bool)
					return new SimpleValueControl<bool>(label, val);
				else if (val is int)
					return new SimpleValueControl<int>(label, val);
				else if (val is float)
					return new SimpleValueControl<float>(label, val);
				return new SimpleValueControl<string>(label, val);
			}
			else
			{
				if (val is bool)
					return new ReadonlySimpleValueControl<bool>(label, val);
				else if (val is int)
					return new ReadonlySimpleValueControl<int>(label, val);
				else if (val is float)
					return new ReadonlySimpleValueControl<float>(label, val);
				return new ReadonlySimpleValueControl<string>(label, val);
			}
		}

		internal static ValueControl Create(string label, Property prop)
		{
			if (prop is BoolProperty)
				return CreateSimple(label, (prop as BoolProperty).Value);
			if (prop is ByteProperty)
				return CreateSimple(label, (prop as ByteProperty).Value);
			if (prop is IntProperty)
				return CreateSimple(label, (prop as IntProperty).Value);
			if (prop is FloatProperty)
				return CreateSimple(label, (prop as FloatProperty).Value);
			if (prop is StrProperty)
				return CreateSimple(label, (prop as StrProperty).Value);
			if (prop is Savegame.Properties.Vector)
				return new VectorControl(label, prop as Savegame.Properties.Vector);
			if (prop is Rotator)
				return new VectorControl(label, prop as Savegame.Properties.Vector);
			if (prop is Scale)
				return new VectorControl(label, prop as Savegame.Properties.Vector);
			if (prop is Savegame.Properties.Color)
				return new ColorControl(label, prop as Savegame.Properties.Color);
			if (prop is LinearColor)
				return new LinearColorControl(label, prop as LinearColor);
			if (prop is Quat)
				return new QuatControl(label, prop as Quat);
			if (prop is ObjectProperty)
				return new ObjectControl(label, prop as ObjectProperty);
			if (prop is EnumProperty)
				return new EnumControl(label, prop as EnumProperty);
			if (prop is NameProperty)
				return new NameControl(label, prop as NameProperty);
			if (prop is TextProperty)
				return new TextControl(label, prop as TextProperty);
			return null;
		}

	}

}
