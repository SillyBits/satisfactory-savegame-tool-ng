using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

using CoreLib;

using SatisfactorySavegameTool.Supplements;


namespace SatisfactorySavegameTool.Dialogs
{
	/// <summary>
	/// Interaction logic for ErrorReportingDialog.xaml
	/// </summary>
	public partial class ErrorReportingDialog : Window
	{
		public static void Show(string msg, Exception exc)
		{
			Show(null, msg, exc);
		}

		public static void Show(Window parent, string msg, Exception exc)
		{
			new ErrorReportingDialog(parent, msg, exc).ShowDialog();
		}


		private ErrorReportingDialog(Window parent, string msg, Exception exc)
			: base()
		{
			InitializeComponent();

			Icon = Application.Current.MainWindow.Icon;

			if (parent == null)
				parent = Application.Current.MainWindow;
			Owner = parent;

			if (Config.Root.HasSection("dialogs") && Config.Root.dialogs.HasSection("error_report"))
			{
				dynamic section = Config.Root.dialogs.error_report;
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

			TextCtrl.Text = exc.ToLongString();

			// Not sure which state we're in, so better be careful with accessing config
			try { SendBtn.IsEnabled = Config.Root.crash_reports.enabled; }
			catch { }
		}

		private void Send_Click(object sender, RoutedEventArgs e)
		{
			(Application.Current as App).SendReport("Crash", TextCtrl.Text);
			Application.Current.Shutdown();
		}

		private void Exit_Click(object sender, RoutedEventArgs e)
		{
			MainWindow.CurrFile = null;
			Application.Current.Shutdown();
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			if (!Config.Root.HasSection("dialogs"))
				Config.Root.AddSection("dialogs");

			dynamic section;
			if (!Config.Root.dialogs.HasSection("error_report"))
			{
				section = Config.Root.dialogs.AddSection("error_report");
				section.AddItem("pos_x" , (int)Left);
				section.AddItem("pos_y" , (int)Top);
				section.AddItem("size_x", (int)Width);
				section.AddItem("size_y", (int)Height);
			}
			else
			{
				section = Config.Root.dialogs.error_report;
				section.pos_x  = (int)Left;
				section.pos_y  = (int)Top;
				section.size_x = (int)Width;
				section.size_y = (int)Height;
			}

			base.OnClosing(e);
		}

	}
}
