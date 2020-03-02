using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

using CoreLib;

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
			Splashscreen.SetMessage("Creating main window");

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

#if !DEVENV
			if (Config.Root.HasSection("update_check") && 
				Config.Root.update_check.HasItem("check_for_updates") && 
				Config.Root.update_check.check_for_updates)
			{
				Splashscreen.SetMessage("Checking for updates");
				_RunUpdateChecker(true);
			}
#endif

			_SetupMRU();

			Splashscreen.SetMessage("Registering plugins");
			ActionFactory.AddToMenu(actions_menu, Actions_Click);

			_SetStatusbar();

			Details.Modified += Details_Modified;

			// Finally, update menu states
			_UpdateUIState();

			// Close splash
			Splashscreen.HideSplash();
        }

		protected override void OnClosing(CancelEventArgs e)
		{
			if (!_CloseGamefile())
			{
				// User decided to save before closing, so cancel closing
				e.Cancel = true;
			}
			else
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
			}

			base.OnClosing(e);
		}
#endregion

#region Menu "stuff"
		protected void _UpdateUIState()
		{
			bool has_save = (CurrFile != null);
			bool modified = has_save && CurrFile.Modified;

			string title = Translate._("MainWindow.Title");
			if (has_save)
			{
				title += " - " + Path.GetFileName(CurrFile.Filename);
				if (modified)
					title += string.Format(" ({0})", Translate._("MainWindow.Gamefile.modified"));
			}
			Title = title;

			File_Save.IsEnabled = File_SaveAs.IsEnabled = modified;
			File_Close.IsEnabled = has_save;
			File_Export.IsEnabled = has_save;
			File_Import.IsEnabled = false;//TODO:

			actions_menu.IsEnabled = has_save;


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
			StatBarText.Text = (text != null) ? text : Translate._("MainWindow.Statusbar.Ready");
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
						_LoadGamefile(@"C:\Users\SillyBits\AppData\Local\FactoryGame\Saved\SaveGames\common\NF-Start-103400.sav");
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
			dlg.InitialDirectory = Config.Root.core.defaultpath;
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
			Export.Run(CurrFile);
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
			if (!_CloseGamefile())
				return;

			Application.Current.Shutdown();
		}


		private void Edit_Options_Click(object sender, RoutedEventArgs e)
		{
			var dlg = new OptionsDialog();
			dlg.Show();
		}


		private void Actions_Click(object sender, RoutedEventArgs e)
		{
			if (sender is MenuItem)
			{
				MenuItem item = sender as MenuItem;
				string name = item.Tag as string;
				IAction action = ActionFactory.Create(name, CurrFile);
				if (action != null)
				{
					_BlockUI(true);
					//_SetStatusbar(string.Format(Translate._("Action.Validate.Progress.Statusbar"), CurrFile.Filename));

					try
					{
						action.Run();
					}
					finally
					{
						//_SetStatusbar();
						_BlockUI(false);
					}
				}
			}
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

		private void Help_UpdateCheck_Click(object sender, RoutedEventArgs e)
		{
			_RunUpdateChecker(false);
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

		private void Details_Modified(Property prop)
		{
			CurrFile.Modified = true;
			_UpdateUIState();
		}
#endregion

#region Handling for savegame
		public static Savegame.Savegame CurrFile;

		private async void _LoadGamefile(string filename)
		{
			if (!_CloseGamefile())
				return;

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
				string save_version = string.Format("Build {0}", header.GetBuildVersion());
				var ret = MessageBox.Show(string.Format(Translate._("MainWindow.LoadGamefile.PeekHeader.Warn"), save_version), 
					Translate._("MainWindow.Title"), MessageBoxButton.YesNo, MessageBoxImage.Question);
				if (ret == MessageBoxResult.No)
					return;
			}

			_BlockUI(true);
			_SetStatusbar(string.Format(Translate._("MainWindow.LoadGamefile.Progress.Statusbar"), filename));
			ProgressDialog progress = new ProgressDialog(this, Translate._("MainWindow.LoadGamefile.Progress.Title"));

			try
			{
				await Task.Run(() => {
					DateTime start_time = DateTime.Now;

					Log.Info("Loading file '{0}'", filename);
					progress.CounterFormat = Translate._("MainWindow.LoadGamefile.Progress.CounterFormat");
					progress.Interval = 1024*1024;//1024 * 128;
					CurrFile.Load(progress.Events);
					//throw new UnknownPropertyException("Hard failure :D", "WTFProperty", 0x10001L);
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
			}
			catch (UnknownPropertyException exc)
			{
				CurrFile = null;

				IncidentReportDialog.Show(filename, exc);
			}
			catch (Reader.ReadException exc)
			{
				CurrFile = null;

				IncidentReportDialog.Show(filename, exc);
			}
			finally
			{
				progress = null;
				_SetStatusbar();
				_BlockUI(false);

				_UpdateUIState();
			}
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

			if (!CurrFile.Modified)
				TreeView.ResetModified();

			progress = null;
			_SetStatusbar();
			_BlockUI(false);

			_UpdateUIState();
		}

		private bool _CloseGamefile()
		{
			if (CurrFile != null && CurrFile.Modified)
			{
				var ret = MessageBox.Show(Translate._("MainWindow.Gamefile.UnsavedChanges"), 
					Translate._("MainWindow.Title"), MessageBoxButton.YesNo, MessageBoxImage.Question);
				if (ret == MessageBoxResult.No)
					return false;
			}

			CurrFile = null;

			return true;
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
				string filename = Config.Root.mru.files[index];
				if (!File.Exists(filename))
				{
					var ret = MessageBox.Show(string.Format(Translate._("MainWindow.MRU.NotFound"), filename), 
						Translate._("MainWindow.Title"), MessageBoxButton.YesNo, MessageBoxImage.Question);
					if (ret == MessageBoxResult.Yes)
					{
						Config.Root.mru.files.RemoveAt(index);
						_UpdateMRU();
					}
					return;
				}
				_LoadGamefile(filename);
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

		private async void _RunUpdateChecker(bool background_check)
		{
			// Do update check, with or without a progress dialog
			ProgressDialog progress = null;
			if (!background_check)
			{
				progress = new ProgressDialog(this, " ");
				progress.CounterFormat = "";
				progress.Interval = 0;
				progress.Events.Start(-1, Translate._("MainWindow.UpdateCheck.Progress.Title"), " ");
			}

			string changelog = null;
			Task<string> check = Task.Run(() => {
				string vers_url = string.Format("{0}?u={1}&v={2}",
					Helpers.Unpack("yygpKSi20tc3STNKsUw008vPy8nMS9UtTi0qSy3SS87JL03RL8vxM0jzjAop8IhMTDL2MPDITc/QB0oXZ+bn6WbmpeXrVeTmAAA="),
					Settings.USER_KEY, Settings.APPVERSION.Replace(" ", "_"));
				UpdateChecker checker = new UpdateChecker(vers_url, progress != null? progress.Events : null, Logger.LOG);
				string url = checker.Check(Settings.APPTITLE, Settings.APPVERSION, out changelog);
				checker.Close();
				checker = null;
				return url;
			});
			await check;
			string dl_url = check.Result;

			if (progress != null)
				progress.Close();
			progress = null;

			if (string.IsNullOrEmpty(dl_url))
			{
				if (!background_check)
					MessageBox.Show(Translate._("MainWindow.UpdateCheck.NoUpdateAvail"), Translate._("MainWindow.UpdateCheck.Title"));
				return;
			}

			// Prompt user to continue installation
			bool update = false;
			if (string.IsNullOrEmpty(changelog))
			{
				var ret = MessageBox.Show(Translate._("MainWindow.UpdateCheck.NewVersionAvail"), 
					Translate._("MainWindow.UpdateCheck.Title"), MessageBoxButton.YesNo, MessageBoxImage.Question);
				update = (ret == MessageBoxResult.Yes);
			}
			else
			{
				string[] buttons = new string[] { Translate._("Yes"), Translate._("No"),
					Translate._("MainWindow.UpdateCheck.NewVersionAvail") };
				update = ShowHtmlResDialog.Show(Translate._("MainWindow.UpdateCheck.Title"), changelog, buttons);
			}
			if (!update)
				return;
			
			// Download installer package, ...
			string dest_file = Path.Combine(Settings.APPPATH, Path.GetFileName(dl_url));
			using (Stream strm = File.Create(dest_file))
			{
				Downloader dl = new Downloader(dl_url, null, Logger.LOG);
				if (!dl.Download(strm))
				{
					MessageBox.Show(Translate._("MainWindow.UpdateCheck.DownloadFailed"), Translate._("MainWindow.UpdateCheck.Title"));
					return;
				}
			}
			// ..., and start installer in silent mode (installer will wait until program has closed)
			ProcessStartInfo info = new ProcessStartInfo() {
				FileName = dest_file,
				Arguments = "/S",
				WorkingDirectory = Settings.APPPATH,
				UseShellExecute = false,
				ErrorDialog = false,
			};
			Process process = Process.Start(info);
			if (process == null || process.HasExited)
			{
				MessageBox.Show(Translate._("MainWindow.UpdateCheck.InstallFailed"), Translate._("MainWindow.UpdateCheck.Title"));
				if (process != null)
					process.Dispose();
				return;
			}

			// End program and let installer do its job
			Close();
		}

	}

}
