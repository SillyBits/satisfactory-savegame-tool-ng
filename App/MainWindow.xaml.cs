using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

using CoreLib;
using CoreLib.PubSub;

using Savegame;
using Savegame.Properties;

using SatisfactorySavegameTool.Actions;
using SatisfactorySavegameTool.Dialogs;

#pragma warning disable CS1998 // async method called from synchronous context


namespace SatisfactorySavegameTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

#region Window core
		public MainWindow()
			: base()
        {
            InitializeComponent();

			_Init();
		}

		protected void _Init()
		{
			// Load options

			// - Position window
			if (Config.Root.HasSection("window"))
			{
				if (Config.Root.window.HasItem("state") 
					&& Config.Root.window.state == WindowState.Maximized.ToString())
				{
					WindowState = WindowState.Maximized;
				}
				else
				{
					if (Config.Root.window.HasItem("pos_x") && Config.Root.window.pos_x != -1)
					{
						Left   = Config.Root.window.pos_x;
						Top    = Config.Root.window.pos_y;
						Width  = Config.Root.window.size_x;
						Height = Config.Root.window.size_y;
					}
					else
					{
						WindowStartupLocation = WindowStartupLocation.CenterScreen;
					}
				}

				// - Position splitter
				if (Config.Root.window.HasItem("splitter"))
				{
					if (Config.Root.window.splitter != -1)
						MainGrid.ColumnDefinitions[0].Width = new GridLength(Config.Root.window.splitter);
				}
			}

			_SetupMRU();

			// Finally, update menu states
			_UpdateUIState();
        }

		protected override void OnClosing(CancelEventArgs e)
		{
			// Save window state

			//TODO:
			//if (!Config.Root.HasSection("window"))
			//	Config.Root.AddSection("window");

			Config.Root.window.state = WindowState.ToString();

			Config.Root.window.pos_x  = (int) Left  ;
			Config.Root.window.pos_y  = (int) Top   ;
			Config.Root.window.size_x = (int) Width ;
			Config.Root.window.size_y = (int) Height;

			Config.Root.window.splitter = (int) MainGrid.ColumnDefinitions[0].ActualWidth;

			base.OnClosing(e);
		}
#endregion

#region Menu "stuff"
		protected void _UpdateUIState()
		{
			bool has_save = (CurrFile != null);
			bool modified = false; //TODO: has_save && CurrFile.IsModified;

			File_Save.IsEnabled = File_SaveAs.IsEnabled = modified;
			File_Close.IsEnabled = has_save;
			Actions_Validate.IsEnabled = has_save;

			#if DEBUG
			File_Export.IsEnabled = has_save;//Only for me :D
			File_Import.IsEnabled = false;
			#else
			File_Export.IsEnabled = File_Import.IsEnabled = false;//TODO:
			#endif

			if (!has_save)
			{
				Details.ShowProperty(null);
				TreeView.ClearTrees();
			}
		}

		protected override void OnKeyUp(KeyEventArgs e)
		{
			// Lazy old-style keyboard shortcuts 
			// ... but way shorter than this Key-/CommandBinding shananigans -.-
			base.OnKeyUp(e);
			if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
			{
				char c = char.ToUpper(e.Key.ToString()[0]);
				switch (c)
				{
					case 'O':
						#if DEBUG
						_LoadGamefile(@"C:\Users\SillyBits\AppData\Local\FactoryGame\Saved\SaveGames\NF-Start-100979.sav");
						#else
						File_Open_Click(e.OriginalSource, null);
						#endif
						break;					
					case 'S': File_Save_Click(e.OriginalSource, null); break;
					case 'W': File_Close_Click(e.OriginalSource, null); break;
				}
			}
		}


		private void File_Open_Click(object sender, RoutedEventArgs e)
		{

			OpenFileDialog dlg = new OpenFileDialog();
			dlg.Title = Translate._("MainWindow.LoadGamefile.Title");//"Select savegame to load";
			dlg.InitialDirectory = Config.Root.core.defaultpath;
			dlg.Filter = Translate._("MainWindow.LoadGamefile.Filter");//"Savegames (*.sav)|*.sav|All files (*.*)|*.*";
			if (dlg.ShowDialog().GetValueOrDefault(false) == true && File.Exists(dlg.FileName))
			{
				string filename = dlg.FileName;
				_AddToMRU(filename);
				_LoadGamefile(filename);
			}
		}

		private void File_Save_Click(object sender, RoutedEventArgs e)
		{
		}

		private void File_SaveAs_Click(object sender, RoutedEventArgs e)
		{
		}

		private void File_Close_Click(object sender, RoutedEventArgs e)
		{
			_CloseGamefile();

			_UpdateUIState();
		}

		private void File_Export_Click(object sender, RoutedEventArgs e)
		{
			string export_file;
			if (Config.Root.core.HasItem("exportpath"))
			{
				string path = Config.Root.core.exportpath;
				if (path == "")
					path = Path.Combine(App.APPPATH, App.EXPORTS);
				string filename = Path.GetFileName(CurrFile.Filename) + ".export";
				export_file = Path.Combine(path, filename);
			}
			else
			{
				export_file = CurrFile.Filename + ".export";
			}
			_ExportGamefile(export_file);
		}

		private void File_Import_Click(object sender, RoutedEventArgs e)
		{
		}

		private void File_MRU_Click(object sender, RoutedEventArgs e)
		{
			_HandleMRU(sender, e);
		}

		private void File_Exit_Click(object sender, RoutedEventArgs e)
		{
			_CloseGamefile();

			Application.Current.Shutdown();
		}


		private void Edit_Options_Click(object sender, RoutedEventArgs e)
		{
		}


		private void Actions_Validate_Click(object sender, RoutedEventArgs e)
		{
			ValidateSavegame.Run(CurrFile);
		}


		private void Help_Changelog_Click(object sender, RoutedEventArgs e)
		{
			string filename = Path.Combine(App.RESOURCEPATH, App.LANGUAGE, "Changelog.res");
			string content = File.ReadAllText(filename);
			ShowHtmlResDialog.Show(Translate._("Dialog.Changelog.Title"), content);
		}

		private void Help_About_Click(object sender, RoutedEventArgs e)
		{
			string filename = Path.Combine(App.RESOURCEPATH, App.LANGUAGE, "About.res");
			string content = File.ReadAllText(filename);
			ShowHtmlResDialog.Show(Translate._("Dialog.About.Title"), content);
		}
#endregion

#region Control events
		public void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			TreeView tv = sender as TreeView;
			if (tv == null)
				return;
			TreeViewItem tvi = tv.SelectedItem as TreeViewItem;
			if (tvi == null)
				return;
			if (tvi.Tag is Property)
			{
				Property prop = tvi.Tag as Property;
				Details.ShowProperty(prop);
			}
			else if (tvi.Tag is Panels.LivingTree.Living)
			{
				Panels.LivingTree.Living living = tvi.Tag as Panels.LivingTree.Living;
				Details.ShowLiving(living);
			}
		}
#endregion

#region Handling for savegame
		public static Savegame.Savegame CurrFile;

		private async void _LoadGamefile(string filename)
		{
			_CloseGamefile();

			if (!File.Exists(filename))
			{
				Log.Info("Tried to load a file which didn't exist anymore!");
				return;
			}

			ProgressDialog progress = new ProgressDialog(this, Translate._("MainWindow.LoadGamefile.Progress.Title"));

			await Task.Run(() => {
				DateTime start_time = DateTime.Now;

				CurrFile = new Savegame.Savegame(filename);

				Log.Info("Loading file '{0}'", filename);
				progress.CounterFormat = Translate._("MainWindow.LoadGamefile.Progress.CounterFormat");
				CurrFile.Load(progress.Events);//, self.treeview)
				Log.Info("Finished loading");
				Log.Info("... loaded a total of {0} elements", CurrFile.TotalElements);

				Log.Info("Creating tree ...");
				progress.CounterFormat = Translate._("MainWindow.LoadGamefile.Progress.CounterFormat.2");
				//progress.Interval = 1000;
				//TreeView.CreateTree(progress.Events);
				TreeView.CreateTrees(progress.Events);
				Log.Info("... finished creating tree");

				DateTime end_time = DateTime.Now;
				TimeSpan ofs = end_time - start_time;
				Log.Info("Loading took {0}", ofs);

				Dispatcher.Invoke(() => { _UpdateUIState(); });
			});
		}

		private async void _SaveGamefile(string filename)
		{
		}

		private void _CloseGamefile()
		{
			//TODO: Add modification check

			CurrFile = null;
		}

		private async void _ExportGamefile(string export_file)
		{
			ProgressDialog progress = new ProgressDialog(this, "Exporting save ..."/*Translate._("MainWindow.LoadGamefile.Progress.Title")*/);
			progress.CounterFormat = "{0} / {1} elements";//Translate._("MainWindow.LoadGamefile.Progress.CounterFormat.2");
			progress.Interval = 100;

			int count = 0;

			await Task.Run(() => {
				Log.Info("Exporting file '{0}'\n"
					   + "-> to          '{1}'", 
					   CurrFile.Filename, export_file);
				((ICallback)progress.Events).Start(CurrFile.TotalElements, "Exporting ...", "");

				DateTime start_time = DateTime.Now;

				StreamWriter sw = File.CreateText(export_file);

				sw.Write("/ Header\n");
				//Dumper.Indent(1, 9);
				++count;
				((ICallback)progress.Events).Update(count, null, CurrFile.Header.ToString());
				Dumper.Dump(CurrFile.Header, sw.Write);
				sw.Flush();
				//Dumper.Unindent(1);
				sw.Write("\\ Header\n");

				sw.Write("/ Objects\n");
				//Dumper.Indent(1, 9);
				foreach (Property prop in CurrFile.Objects)
				{
					++count;
					((ICallback)progress.Events).Update(count, null, prop.ToString());
					Dumper.Dump(prop, sw.Write);
					sw.Flush();
				}
				//Dumper.Unindent(1);
				sw.Write("\\ Objects\n");

				sw.Write("/ Collected\n");
				//Dumper.Indent(1, 9);
				foreach (Property prop in CurrFile.Collected)
				{
					++count;
					((ICallback)progress.Events).Update(count, null, prop.ToString());
					Dumper.Dump(prop, sw.Write);
					sw.Flush();
				}
				//Dumper.Unindent(1);
				sw.Write("\\ Collected\n");

				sw.Close();

				DateTime end_time = DateTime.Now;
				TimeSpan ofs = end_time - start_time;
				Log.Info("Finished exporting, took {0}", ofs);

				((ICallback)progress.Events).Stop("Done", "");
			});

			progress.Events.Destroy();
			MessageBox.Show("Done");
		}
#endregion

#region Handling for MRU menu
		protected void _SetupMRU()
		{
			if (!Config.HasSection("mru"))
			{
				File_MRU.IsEnabled = false;
				return;
			}

			_UpdateMRU();
			File_MRU.IsEnabled = true;
		}

		protected void _UpdateMRU()
		{
			MenuItem item;

			// Remove any old item beforehand
			while (File_MRU.Items.Count > 2)
			{
				item = File_MRU.Items[File_MRU.Items.Count - 1] as MenuItem;
				item.Click -= File_MRU_Click;
				File_MRU.Items.Remove(item);
			}

			// Add elements available
			foreach(string file in Config.Root.mru.files)
			{
				int index = File_MRU.Items.Count - 2;

				item = new MenuItem();
				item.Header = string.Format("_{0}. {1}", index, file);
				item.Tag = index;
				item.Click += File_MRU_Click;

				File_MRU.Items.Add(item);
			}

			File_MRU.UpdateLayout();
		}

		protected void _HandleMRU(object sender, RoutedEventArgs e)
		{
			MenuItem item = sender as MenuItem;
			int index = (int) item.Tag;

			if (index == -1)
			{
				// Clear all
				Config.Root.mru.files.Clear();
				_UpdateMRU();
			}
			else
			{
				_LoadGamefile(Config.Root.mru.files[index]);
			}
		}

		protected void _AddToMRU(string filename)
		{
			foreach(string file in Config.Root.mru.files)
			{
				if (file == filename)
					return;
			}
			Config.Root.mru.files.Add(filename);
			_UpdateMRU();
		}
#endregion

	}

}
