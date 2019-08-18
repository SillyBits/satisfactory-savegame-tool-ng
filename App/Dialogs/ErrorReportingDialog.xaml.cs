using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

using Savegame.Properties;

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


			string stack_trace = null;
			try
			{
				var trace = new StackTrace(exc);
				if (trace != null)
					stack_trace = trace.ToString();
			}
			catch { }
			if (stack_trace == null)
				stack_trace = exc.StackTrace;

			string text = msg 
				+ "\n\n" 
				+ "Message: " + exc.Message + "\n"
				+ "TargetSite: " + exc.TargetSite.Name + ", " + exc.TargetSite.Module.FullyQualifiedName + "\n"
				+ "Source: " + exc.Source + "\n"
				+ "StackTrace:\n" 
				+ stack_trace + "\n"
				;
			TextCtrl.Text = text;

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
			Application.Current.Shutdown();
		}

	}
}
