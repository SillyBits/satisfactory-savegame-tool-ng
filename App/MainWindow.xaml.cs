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
			_UpdateMenuStates();
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
		protected void _UpdateMenuStates()
		{
			bool has_save = (CurrFile != null);
			bool modified = false; //TODO: has_save && CurrFile.IsModified;

			File_Save.IsEnabled = File_SaveAs.IsEnabled = modified;
			File_Close.IsEnabled = has_save;
			File_Export.IsEnabled = File_Import.IsEnabled = false;//TODO:
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
			_CloseGamefile();

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
			CurrFile = null;
			_UpdateMenuStates();
		}

		private void File_Export_Click(object sender, RoutedEventArgs e)
		{
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


		private void Help_Changelog_Click(object sender, RoutedEventArgs e)
		{
			var dlg = new ChangelogDialog(this, Translate._("ChangelogDialog.Title"));
			dlg.ShowDialog();
		}

		private void Help_About_Click(object sender, RoutedEventArgs e)
		{
			var dlg = new AboutDialog(this, Translate._("AboutDialog.Title"));
			dlg.ShowDialog();
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
			Property prop = tvi.Tag as Property;
			Details.ShowProperty(prop);
		}
#endregion

#region Handling for savegame
		public Savegame.Savegame CurrFile;

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
				progress.Interval = 1000;
				//TreeView.CreateTree(CurrFile, progress.Events);
				TreeView.CreateTrees(CurrFile, progress.Events);
				Log.Info("... finished creating tree");

				DateTime end_time = DateTime.Now;
				TimeSpan ofs = end_time - start_time;
				Log.Info("Loading took {0}", ofs);

				Dispatcher.Invoke(() => { _UpdateMenuStates(); });
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
