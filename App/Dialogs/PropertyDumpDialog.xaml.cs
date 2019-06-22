using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using Savegame.Properties;

namespace SatisfactorySavegameTool.Dialogs
{
	/// <summary>
	/// Interaction logic for ImageDialog.xaml
	/// </summary>
	public partial class PropertyDumpDialog : Window
	{
		public PropertyDumpDialog(Window parent, string title, Property prop)
			: base()
		{
			InitializeComponent();

			Owner = parent;
			Title = title;

			StringBuilder sb = new StringBuilder();
			Dumper.WriteFunc writer = (s) => { sb.Append(s); };
			Dumper.Dump(prop, writer);

			text.Text = sb.ToString();
		}


		private void Close_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}
