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
using SatisfactorySavegameTool.Supplements;


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
			Application.Current.MainWindow = this;

            InitializeComponent();

			_Init();
		}

		protected void _Init()
		{
			// Load options
			Splashscreen.SetMessage("Creating main window");

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

			_SetStatusbar();

			// Finally, update menu states
			_UpdateUIState();

			// Close splash
			Splashscreen.HideSplash();
        }

		protected override void OnClosing(CancelEventArgs e)
		{
			// Save window state
			dynamic wnd;
			if (!Config.Root.HasSection("window"))
			{
				wnd = Config.Root.AddSection("window");
				wnd.AddItem("state"   , WindowState.ToString());
				wnd.AddItem("pos_x"   , (int)Left);
				wnd.AddItem("pos_y"   , (int)Top);
				wnd.AddItem("size_x"  , (int)Width);
				wnd.AddItem("size_y"  , (int)Height);
				wnd.AddItem("splitter", (int)MainGrid.ColumnDefinitions[0].ActualWidth);
			}
			else
			{
				wnd = Config.Root.window;
				wnd.state    = WindowState.ToString();
				wnd.pos_x    = (int)Left;
				wnd.pos_y    = (int)Top;
				wnd.size_x   = (int)Width;
				wnd.size_y   = (int)Height;
				wnd.splitter = (int)MainGrid.ColumnDefinitions[0].ActualWidth;
			}

			base.OnClosing(e);
		}
#endregion

#region Menu "stuff"
		protected void _UpdateUIState()
		{
			bool has_save = (CurrFile != null);
#if DEBUG
			bool modified = true;
#else
			bool modified = false; //TODO: has_save && CurrFile.IsModified;
#endif

			File_Save.IsEnabled = File_SaveAs.IsEnabled = modified;
			File_Close.IsEnabled = has_save;
			File_Export.IsEnabled = has_save;
			File_Import.IsEnabled = false;//TODO:

			Actions_Validate.IsEnabled = has_save;


			if (!has_save)
			{
				Details.ShowProperty(null);
				TreeView.ClearTrees();
			}

			TreeView.IsEnabled = has_save;
		}

		protected void _BlockUI(bool state)
		{
			MainMenuBar.IsEnabled = !state;
		}

		protected void _SetStatusbar(string text = null)
		{
			StatBarText.Text = (text != null) ? text : Translate._("MainWindow.Menu.Statusbar.Ready");
			StatBarText.Refresh();
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
			_SaveGamefile(CurrFile.Filename);
		}

		private void File_SaveAs_Click(object sender, RoutedEventArgs e)
		{
			SaveFileDialog dlg = new SaveFileDialog();
			dlg.Title = Translate._("MainWindow.SaveGamefile.Title");
			dlg.InitialDirectory = Settings.EXPORTPATH;
			dlg.DefaultExt = Translate._("MainWindow.SaveGamefile.DefaultExt");
			dlg.Filter = Translate._("MainWindow.SaveGamefile.Filter");
			if (dlg.ShowDialog().GetValueOrDefault(false) == true)
			{
				_SaveGamefile(dlg.FileName);
			}
		}

		private void File_Close_Click(object sender, RoutedEventArgs e)
		{
			_CloseGamefile();

			_UpdateUIState();
		}

		private void File_Export_Click(object sender, RoutedEventArgs e)
		{
			string filename = Path.GetFileName(CurrFile.Filename) + ".export";
			string export_file = Path.Combine(Config.Root.core.exportpath, filename);

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
			var dlg = new OptionsDialog();
			dlg.Show();
		}


		private void Actions_Validate_Click(object sender, RoutedEventArgs e)
		{
			_BlockUI(true);
			_SetStatusbar(string.Format(Translate._("Action.Validate.Progress.Statusbar"), CurrFile.Filename));
			ValidateSavegame.Run(CurrFile);
			_SetStatusbar();
			_BlockUI(false);
		}


		private void Help_Changelog_Click(object sender, RoutedEventArgs e)
		{
			string filename = Path.Combine(Settings.RESOURCEPATH, Settings.LANGUAGE, "Changelog.res");
			string content = File.ReadAllText(filename);
			ShowHtmlResDialog.Show(Translate._("Dialog.Changelog.Title"), content);
		}

		private void Help_About_Click(object sender, RoutedEventArgs e)
		{
			string filename = Path.Combine(Settings.RESOURCEPATH, Settings.LANGUAGE, "About.res");
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
			Panels.TreeNode node = tv.SelectedItem as Panels.TreeNode;
			if (node == null)
				return;
			if (node.Tag is Property)
			{
				Property prop = node.Tag as Property;
				Details.ShowProperty(prop);
			}
			else if (node.Tag is Panels.LivingTree.Living)
			{
				Panels.LivingTree.Living living = node.Tag as Panels.LivingTree.Living;
				Details.ShowLiving(living);
			}
			else if (node.Tag is Panels.BuildingsTree.Building)
			{
				Panels.BuildingsTree.Building building = node.Tag as Panels.BuildingsTree.Building;
				Details.ShowBuilding(building);
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

			// Setup savegame
			CurrFile = new Savegame.Savegame(filename);

			// Activate features as configured by user
			CurrFile.EnableDeepAnalysis(Config.Root.deep_analysis.enabled);

			// Peek header first, to ensure the savegame version is fully supported
			Header header = CurrFile.PeekHeader();
			if (header == null)
			{
				Log.Error("Failed to peek header");
				MessageBox.Show(string.Format(Translate._("MainWindow.LoadGamefile.PeekHeader.Failed"), filename), 
					Translate._("MainWindow.Title"), MessageBoxButton.OK, MessageBoxImage.Stop);
				return;
			}
			if (header.GetVersionEntry() == null)
			{
				// Log this version info in case tool crashes
				Log.Warning("Save is newer than this tool supports: Build={0}, SaveVersion={1}, SaveType={2}", 
					header.GetBuildVersion(), header.SaveVersion, header.Type);
				VersionTable.VersionEntry max_version = VersionTable.INSTANCE.GetMax();
				string save_version = string.Format("Build {0}", header.GetBuildVersion());
				var ret = MessageBox.Show(string.Format(Translate._("MainWindow.LoadGamefile.PeekHeader.Warn"), max_version, save_version), 
					Translate._("MainWindow.Title"), MessageBoxButton.YesNo, MessageBoxImage.Question);
				if (ret == MessageBoxResult.No)
					return;
			}

			_BlockUI(true);
			_SetStatusbar(string.Format(Translate._("MainWindow.LoadGamefile.Progress.Statusbar"), filename));
			ProgressDialog progress = new ProgressDialog(this, Translate._("MainWindow.LoadGamefile.Progress.Title"));

			await Task.Run(() => {
				DateTime start_time = DateTime.Now;

				Log.Info("Loading file '{0}'", filename);
				progress.CounterFormat = Translate._("MainWindow.LoadGamefile.Progress.CounterFormat");
				progress.Interval = 1024*1024;//1024 * 128;
				CurrFile.Load(progress.Events);
				Log.Info("Finished loading");
				Log.Info("... loaded a total of {0} elements", CurrFile.TotalElements);

				Log.Info("Creating trees ...");
				//progress.CounterFormat = Translate._("MainWindow.LoadGamefile.Progress.CounterFormat.2");
				progress.Interval = 1000;
				//TreeView.CreateTree(progress.Events);
				TreeView.CreateTrees(progress.Events);
				Log.Info("... finished creating trees");

				DateTime end_time = DateTime.Now;
				TimeSpan ofs = end_time - start_time;
				Log.Info("Loading took {0}", ofs);
			});

			progress = null;
			_SetStatusbar();
			_BlockUI(false);

			_UpdateUIState();
		}

		private async void _SaveGamefile(string filename)
		{
			_BlockUI(true);

			if (File.Exists(filename))
			{
				// Move to zip, ...
				string path = Path.GetDirectoryName(filename);
				string backupfile = Path.Combine(path, 
					Path.GetFileNameWithoutExtension(filename) + "-" + DateTime.Now.ToString("yyyyMMdd-hhmmss") + ".zip");
				_SetStatusbar(string.Format(Translate._("MainWindow.SaveGamefile.Backup.Statusbar"), filename, Path.GetFileName(backupfile)));
				await Task.Run(() => Compressor.CompressToFile(backupfile, filename, path));
			}

			_SetStatusbar(string.Format(Translate._("MainWindow.SaveGamefile.Progress.Statusbar"), filename));
			ProgressDialog progress = new ProgressDialog(this, Translate._("MainWindow.SaveGamefile.Progress.Title"));

			await Task.Run(() => {
				DateTime start_time = DateTime.Now;

				Log.Info("Saving file '{0}'", filename);
				progress.CounterFormat = Translate._("MainWindow.SaveGamefile.Progress.CounterFormat");
				progress.Interval = 1000;
				CurrFile.SaveAs(progress.Events, filename);
				Log.Info("Finished saving");
				Log.Info("... saved a total of {0} elements", CurrFile.TotalElements);

				DateTime end_time = DateTime.Now;
				TimeSpan ofs = end_time - start_time;
				Log.Info("Saving took {0}", ofs);
			});

			progress = null;
			_SetStatusbar();
			_BlockUI(false);

			_UpdateUIState();
		}

		private void _CloseGamefile()
		{
			//TODO: Add modification check

			CurrFile = null;
		}

		private async void _ExportGamefile(string export_file)
		{
			_BlockUI(true);
			_SetStatusbar(string.Format(Translate._("MainWindow.ExportGamefile.Progress.Statusbar"), 
				Path.GetFileName(CurrFile.Filename), export_file));
			ProgressDialog progress = new ProgressDialog(this, Translate._("MainWindow.ExportGamefile.Progress.Title"));
			progress.CounterFormat = Translate._("MainWindow.ExportGamefile.Progress.CounterFormat");
			progress.Interval = 1000;

			int count = 0;

			await Task.Run(() => {
				Log.Info("Exporting file '{0}'\n"
					   + "-> to          '{1}'", 
					   CurrFile.Filename, export_file);
				progress.Events.Start(CurrFile.TotalElements, Translate._("MainWindow.ExportGamefile.Progress.Title"), "");

				DateTime start_time = DateTime.Now;

				StreamWriter sw = File.CreateText(export_file);

				sw.Write("/ Header\n");
				//Dumper.Indent(1, 9);
				++count;
				progress.Events.Update(count, null, CurrFile.Header.ToString());
				Savegame.Properties.Dumper.Dump(CurrFile.Header, sw.Write);
				//Dumper.Unindent(1);
				sw.Write("\\ Header\n");

				sw.Write("/ Objects\n");
				//Dumper.Indent(1, 9);
				foreach (Property prop in CurrFile.Objects)
				{
					++count;
					progress.Events.Update(count, null, prop.ToString());
					Savegame.Properties.Dumper.Dump(prop, sw.Write);
				}
				//Dumper.Unindent(1);
				sw.Write("\\ Objects\n");

				sw.Write("/ Collected\n");
				//Dumper.Indent(1, 9);
				foreach (Property prop in CurrFile.Collected)
				{
					++count;
					progress.Events.Update(count, null, prop.ToString());
					Savegame.Properties.Dumper.Dump(prop, sw.Write);
				}
				//Dumper.Unindent(1);
				sw.Write("\\ Collected\n");

				sw.Close();

				DateTime end_time = DateTime.Now;
				TimeSpan ofs = end_time - start_time;
				Log.Info("Finished exporting, took {0}", ofs);

				progress.Events.Stop(Translate._("MainWindow.ExportGamefile.Done"), "");
			});

			_SetStatusbar();
			_BlockUI(false);

			MessageBox.Show(Translate._("MainWindow.ExportGamefile.Done"), Translate._("MainWindow.Title"));
		}
#endregion

#region Handling for MRU menu
		protected void _SetupMRU()
		{
			if (!Config.Root.HasSection("mru"))
			{
				Config.Root.AddSection("mru");
				Config.Root.mru.AddListItem("files");
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
