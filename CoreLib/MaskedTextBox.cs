using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;//TEMP
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;


namespace CoreLib
{

	public class MaskedTextBox<_ValueType> : TextBox
		where _ValueType : struct
	{

		public _ValueType Value
		{
			get
			{
				_ValueType value;
				if (!_parse(Text, out value))
					value = default(_ValueType);
				return value;
			}
			set
			{
				Text = _format(value);
			}
		}

		public string Mask
		{
			get
			{
				return _mask;
			}
			set
			{
				if (_mask != value)
				{
					_mask = value;
					// Replace any placeholder with actual chars from current UI culture (and escape any special regex-char)
					if (_mask.Contains("<S>") || _mask.Contains("<T>") || _mask.Contains("<D>"))
						_mask = _mask.Replace("<S>", _signs).Replace("<T>", _thousands).Replace("<D>", _decimalpt).Replace(".", "\\.");
					_regex = new Regex(_mask, RegexOptions.CultureInvariant|RegexOptions.IgnoreCase|RegexOptions.IgnorePatternWhitespace);
				}
			}
		}


		public MaskedTextBox()
		{
			LostFocus        += _LostFocus;
			PreviewTextInput += _PreviewTextInput;
		}

		private void _LostFocus(object sender, System.Windows.RoutedEventArgs e)
		{
			// Trigger formatting
			Value = Value;
		}

		private void _PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
		{
			e.Handled = !_IsValidInput(e.Text);
		}

		private bool _IsValidInput(string text)
		{
			if (_allowed != null && !_allowed.IsMatch(text))
				return false;
			if (_regex != null)
			{
				// also validate resulting string
				string result = Text.Substring(0, CaretIndex) + text + Text.Substring(CaretIndex);
				if (!_regex.IsMatch(result))
					return false;
			}
			return true;
		}

		private bool _IsValid()
		{
			if (_regex == null)
				return false;
			return _regex.IsMatch(Text);
		}

		private static string              _signs;
		private static string              _thousands;
		private static string              _decimalpt;
		private static Regex               _allowed;
		private static Formatters.Delegate _format;
		private static Parsers.Delegate    _parse;
		private string                     _mask;
		private Regex                      _regex;

		static MaskedTextBox()
		{
			Type type = typeof(_ValueType);

			// Get some culture-specific chars
			var fmt = CultureInfo.CurrentUICulture.NumberFormat;
			_signs     = fmt.NegativeSign + fmt.PositiveSign;
			_thousands = fmt.NumberGroupSeparator;
			_decimalpt = fmt.NumberDecimalSeparator;

			// Limit input (if possible)
			string allowed = null;
			if (type == typeof(float) || type == typeof(double))
				allowed = "[" + _signs + _thousands + _decimalpt + "0-9]";
			else if (type == typeof(byte))
				allowed = "[0-9]";
			else if (type == typeof(uint) || type == typeof(ulong))
				allowed = "[" + _thousands + "0-9]";
			else if (type == typeof(sbyte))
				allowed = "[" + _signs + "0-9]";
			else if (type == typeof(int) || type == typeof(long))
				allowed = "[" + _signs + _thousands + "0-9]";
			if (allowed != null)
			{
				// Make sure special chars are escaped
				allowed = allowed.Replace(".", "\\.");
				_allowed = new Regex(allowed, RegexOptions.CultureInvariant|RegexOptions.IgnoreCase|RegexOptions.IgnorePatternWhitespace);
			}

			// Explicit links to specialized methods
			MethodInfo mi = Helpers.PickStaticMethod(typeof(Formatters), "Format", new Type[] { type });
			if (mi == null)
				throw new InvalidOperationException(string.Format("No matching formatter for type '{0}'", type.Name));
			_format = (Formatters.Delegate) mi.CreateDelegate(typeof(Formatters.Delegate));

			mi = Helpers.PickStaticMethod(typeof(Parsers), "Parse", new Type[] { typeof(string), type.MakeByRefType() });
			if (mi == null)
				throw new InvalidOperationException(string.Format("No matching parser for type '{0}'", type.Name));
			_parse = (Parsers.Delegate) mi.CreateDelegate(typeof(Parsers.Delegate));
		}


		internal static class Formatters
		{
			internal delegate string Delegate(_ValueType value);

			internal static string Format(float value)
			{
				return value.ToString("F5", CultureInfo.CurrentUICulture);
			}

			internal static string Format(double value)
			{
				return value.ToString("F5", CultureInfo.CurrentUICulture);
			}

			//internal static string Format(byte value) => Generic version

			internal static string Format(int value)
			{
				return value.ToString("#,#0", CultureInfo.CurrentUICulture);
			}

			internal static string Format(long value)
			{
				return value.ToString("#,#0", CultureInfo.CurrentUICulture);
			}

			internal static string Format(_ValueType value)
			{
				return value.ToString();
			}
		}

		internal static class Parsers
		{
			internal delegate bool Delegate(string text, out _ValueType value);

			internal static bool Parse(string text, out float value)
			{
				return float.TryParse(text, NumberStyles.Float|NumberStyles.AllowThousands, CultureInfo.CurrentUICulture, out value);
			}

			internal static bool Parse(string text, out double value)
			{
				return double.TryParse(text, NumberStyles.Float|NumberStyles.AllowThousands, CultureInfo.CurrentUICulture, out value);
			}

			internal static bool Parse(string text, out byte value)
			{
				return byte.TryParse(text, NumberStyles.Integer/*|NumberStyles.AllowThousands*/, CultureInfo.CurrentUICulture, out value);
			}

			internal static bool Parse(string text, out int value)
			{
				return int.TryParse(text, NumberStyles.Integer|NumberStyles.AllowThousands, CultureInfo.CurrentUICulture, out value);
			}

			internal static bool Parse(string text, out long value)
			{
				return long.TryParse(text, NumberStyles.Integer|NumberStyles.AllowThousands, CultureInfo.CurrentUICulture, out value);
			}

			internal static bool Parse(string text, out _ValueType value)
			{
				value = default(_ValueType);
				return false;
			}
		}

	}

}
