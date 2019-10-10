using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

using Savegame.Properties;

namespace SatisfactorySavegameTool.Dialogs
{
	/// <summary>
	/// Interaction logic for IncidentReportDialog.xaml
	/// </summary>
	public partial class IncidentReportDialog : Window
	{
		public static void Show(string filename, UnknownPropertyException exc)
		{
			Show(null, filename, exc);
		}

		public static void Show(Window parent, string filename, UnknownPropertyException exc)
		{
			if (Config.Root.incident_reports.enabled)
			{
				new IncidentReportDialog(parent, filename, exc).ShowDialog();
			}
			else
			{
				string msg = string.Format(Translate._("IncidentReportDialog.Message"), filename, exc.PropertyType, exc.ErrorPos)
						   + "\n\n"
						   + Translate._("IncidentReportDialog.Hint")
						   ;
				MessageBox.Show(msg, Translate._("IncidentReportDialog.Title"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
		}


		private IncidentReportDialog(Window parent, string filename, UnknownPropertyException exc)
			: base()
		{
			InitializeComponent();

			Icon = Application.Current.MainWindow.Icon;

			if (parent == null)
				parent = Application.Current.MainWindow;
			Owner = parent;

			if (Config.Root.HasSection("dialogs") && Config.Root.dialogs.HasSection("incident_report"))
			{
				dynamic section = Config.Root.dialogs.incident_report;
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

			_msg = exc.Message;

			Message.Text = string.Format(Translate._("IncidentReportDialog.Message"), filename, exc.PropertyType, exc.ErrorPos);

			long offset = (exc.ErrorPos - Settings.INCIDENT_OFFSET) & (~0xF);
			long length = Settings.INCIDENT_LENGTH;
			_ReadData(filename, offset, length);
			SnapshotData.Text = CoreLib.Helpers.Hexdump(_data, 16, indent:0, rel_offset:offset);
		}

		private void Send_Click(object sender, RoutedEventArgs e)
		{
			(Application.Current as App).SendReport("Incident", _msg, _data);
			Close();
		}

		private void Close_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			if (!Config.Root.HasSection("dialogs"))
				Config.Root.AddSection("dialogs");

			dynamic section;
			if (!Config.Root.dialogs.HasSection("incident_report"))
			{
				section = Config.Root.dialogs.AddSection("incident_report");
				section.AddItem("pos_x" , (int)Left);
				section.AddItem("pos_y" , (int)Top);
				section.AddItem("size_x", (int)Width);
				section.AddItem("size_y", (int)Height);
			}
			else
			{
				section = Config.Root.dialogs.incident_report;
				section.pos_x  = (int)Left;
				section.pos_y  = (int)Top;
				section.size_x = (int)Width;
				section.size_y = (int)Height;
			}

			base.OnClosing(e);
		}

		private void _ReadData(string filename, long offset, long length)
		{
			_data = null;

			Header header = null;
			try
			{
				Savegame.Savegame savegame = new Savegame.Savegame(filename);
				header = savegame.PeekHeader();
			}
			catch
			{
			}

			if (header != null)
			{
				if (header.SaveVersion < 21)
				{
					_data = CoreLib.Helpers.GetFileContents(filename, offset, length);
				}
				else
				{
					Reader.FileReader filereader = null;
					Reader.CloudsaveReader cloudreader = null;
					try
					{
						filereader = new Reader.FileReader(filename, null);
						int header_length = header.GetLength();
						if (filereader.Seek(header_length, Reader.IReader.Positioning.Start) == header_length)
						{
							cloudreader = new Reader.CloudsaveReader(filereader, null);
							offset += 4; // Take leading 'size' into account
							if (cloudreader.Seek(offset, Reader.IReader.Positioning.Start) == offset)
								_data = cloudreader.ReadBytes((int)length);
						}
					}
					catch
					{
					}
					finally
					{
						if (cloudreader != null)
							cloudreader.Close();
						if (filereader != null)
							filereader.Close();
					}
				}
			}
		}

		private string _msg;
		private byte[] _data;
	}
}
