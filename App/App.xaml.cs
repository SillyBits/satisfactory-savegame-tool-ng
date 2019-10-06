using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

using CoreLib;

using SatisfactorySavegameTool.Supplements;

namespace SatisfactorySavegameTool
{
    /// <summary>
    /// Interaction logic for "App.xaml"
    /// </summary>
    public partial class App : Application
    {
		protected Uploader        _uploader;
		protected Logger          _logger;
		protected ConfigFile      _config;
		protected LanguageHandler _languages;

		protected VersionTable    _versions;
		protected ColorTable      _colors;
		protected ItemTable       _items;
		protected RecipeTable     _recipes;
		protected ResearchTable   _researchs;
		protected SchematicTable  _schematics;


		protected override void OnStartup(StartupEventArgs e)
		{
			_InitErrorReporting();

#if DEBUG
			_logger    = new Logger(Settings.LOGPATH, Settings.APPNAME, Logger.Level.Debug);
#else
			_logger    = new Logger(Settings.LOGPATH, Settings.APPNAME);
#endif
			_config    = new ConfigFile(Settings.APPPATH, Settings.APPNAME);
			_languages = new LanguageHandler(Settings.RESOURCEPATH, null, TRANSLATIONFILES);


			// Before continuing, check and adjust log level
			if (Config.Root.HasSection("logging") && Config.Root.logging.HasItem("verbose"))
			{
				Settings.VERBOSE = Config.Root.logging.verbose;
				if (Settings.VERBOSE)
					_logger.SetLogLevel(Logger.Level.Debug);
			}

			Settings.Init();

			// Stuff which can be run in parallel
			Task load_data_tables = Task.Run(async() => await _LoadDataTables());
			Task.WaitAll(load_data_tables);

			base.OnStartup(e);
		}

		protected override void OnExit(ExitEventArgs e)
		{
			base.OnExit(e);

			_languages = null;

			if (_config != null)
				_config.Shutdown();
			_config = null;

			if (_logger != null)
				_logger.Shutdown();
			_logger = null;

			_ShutdownErrorReporting();
		}


		private void _InitErrorReporting()
		{
			// Setup error reporting
			_uploader = new Uploader( Helpers.Unpack("yygpKSi20tc3STNKsUw008vPy8nMS9UtTi0qSy3SS87JL03Rz3NxNSs18ckqLs+rMnMJrzIyzUoCAA==")
									, Helpers.Unpack("CwxNNgmpysgJzsrISTTIiAgxcjNKzA4K9jOwMEsJ9TQID3GLDMn1y04xzvAJd0vJSAkvMQ0NT7cFAA==")
									, new string[] { Settings.APPNAME, Settings.APPVERSION }
									, Path.Combine(Settings.LOGPATH, "Uploader"));

			// Catching those based on idea in https://stackoverflow.com/a/46804709
			AppDomain.CurrentDomain.UnhandledException += _AppDomainUnhandledException;
			DispatcherUnhandledException += _DispatcherUnhandledException;
			TaskScheduler.UnobservedTaskException += _TaskSchedulerUnobservedTaskException;

			_uploader.Start();
		}

		private void _ShutdownErrorReporting()
		{
			// Remove event handlers first!
			AppDomain.CurrentDomain.UnhandledException -= _AppDomainUnhandledException;
			DispatcherUnhandledException -= _DispatcherUnhandledException;
			TaskScheduler.UnobservedTaskException -= _TaskSchedulerUnobservedTaskException;

			if (_uploader != null)
			{
				if (_uploader.HasPendingWork)
				{
					Splashscreen.ShowSplash("Waiting for uploader to finish ...");

					string msg = "";
					while (_uploader.HasPendingWork)
					{
						msg += ".";
						Splashscreen.SetMessage(msg);
						Thread.Sleep(250);
					}

					Splashscreen.HideSplash();
				}

				_uploader.Close();
			}
			_uploader = null;
		}

		private void _AppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			_ReportException(sender, (Exception)e.ExceptionObject);
		}

		private void _DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
		{
			_ReportException(sender, e.Exception);
			e.Handled = true;
		}

		private void _TaskSchedulerUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			_ReportException(sender, e.Exception);
		}

		private void _ReportException(object sender, Exception exc)
		{
			string msg = "Catched an unhandled exception raised from: " + sender.ToString();
			_logger.Error(msg, exc);

			try
			{
				// Not sure which state we're in, so better be careful with creating this dialog
				Dialogs.ErrorReportingDialog.Show(msg, exc);
			}
			catch { }
		}


		private async Task _LoadDataTables()
		{
			Task[] loaders = new Task[] {
				Task.Run(() => _versions   = new VersionTable()),
				Task.Run(() => _colors     = new ColorTable()),
				Task.Run(() => _items      = new ItemTable()),
				Task.Run(() => _recipes    = new RecipeTable()),
				Task.Run(() => _researchs  = new ResearchTable()),
				Task.Run(() => _schematics = new SchematicTable()),
			};
			await Task.WhenAll(loaders);
		}


		internal void SaveConfig()
		{
			_config.Flush();
		}

		internal void SendReport(string type, string report, byte[] data = null)
		{
#if DEVENV
			// Do nothing while in a development environment
#else
			// Gather relevant data
			Dictionary<string,byte[]> content = new Dictionary<string, byte[]>();
			content.Add(type + "-" + Settings.USER_ID + ".txt", Encoding.ASCII.GetBytes(report));
			if (type == "Crash" || type == "Incident")
				content.Add("Latest.log", _logger.GetSnapshot(false));
			if (type == "Crash")
				content.Add("Latest.cfg", Helpers.GetFileContents(_config.Filename));
			if (data != null)
				content.Add("Data.bin", data);
			_uploader.Send(Compressor.CompressToArray(content));
#endif
		}


		private const  string   TRANSLATIONFILE_MAIN = "Translation.res";
		private const  string   TRANSLATIONFILE_FG   = "FactoryGame.res";
		private static string[] TRANSLATIONFILES     = { TRANSLATIONFILE_MAIN, TRANSLATIONFILE_FG };

	}


	public static class Settings
	{
		// Application values
		public static string APPNAME              = null;
		public static string APPTITLE             = null;
		public static string APPVERSION           = null;
		public static string APPPATH              = null;

		// Path to our resources
		public const  string RESOURCES            = "Resources";
		public static string RESOURCEPATH         = null;

		// Path to where logs are to be stored
		public const  string LOGS                 = "logs";
		public static string LOGPATH              = null;
		public static bool   VERBOSE              = false;

		// Path to where plugins are to be stored
		public const  string PLUGINS              = "plugins";
		public static string PLUGINPATH           = null;

		// Path to where exports are to be stored - configured by user
		public const  string EXPORTS              = "exports";
		public static string EXPORTPATH           = null;


		// Anonymous user id, used for reporting crashes and such and to allow for grouping such reports
		public static string USER_ID              = null;
		// User key (MUST be a 32 bytes)
		public static byte[] USER_KEY             = null;


		// Localisation related - configured by user
		public static string LANGUAGE             = null;


		// Incident related - size of snapshot to take
		public const int INCIDENT_OFFSET          = 512;
		public const int INCIDENT_LENGTH          = INCIDENT_OFFSET + 256;


		// Creating actual settings is a 2-step approach:
		// - First, init static ones (e.g. those coming from assembly file itself)
		static Settings()
		{
			Assembly ass = Assembly.GetExecutingAssembly();

			APPNAME = ass.GetName().Name;

			//[assembly: AssemblyTitle("Satisfactory Savegame Tool")]
			var title = ass.GetCustomAttribute<AssemblyTitleAttribute>();
			if (title != null)
				APPTITLE = title.Title;
			else
				APPTITLE = "Satisfactory Savegame Tool";

			//[assembly: AssemblyCopyright("Copyright © 2019 SillyBits")]

			//[assembly: AssemblyInformationalVersion("0.1 alpha")]
			var versions = ass.GetCustomAttributes<AssemblyInformationalVersionAttribute>() 
							as AssemblyInformationalVersionAttribute[];
			AssemblyInformationalVersionAttribute attr = versions.FirstOrDefault();
			if (attr != null)
				APPVERSION = attr.InformationalVersion;
			else
				APPVERSION = FileVersionInfo.GetVersionInfo(ass.Location).ProductVersion;

#if DEVENV
			APPPATH = @"E:\GitHub\satisfactory-savegame-tool-ng\App";
#else
			APPPATH = Path.GetDirectoryName(ass.Location);
#endif

			// Setup paths which do rely on our main path
			RESOURCEPATH = Path.Combine(APPPATH, RESOURCES);
			LOGPATH      = Path.Combine(APPPATH, LOGS);
			PLUGINPATH   = Path.Combine(APPPATH, PLUGINS);

			// Ensure log path exists
			// If this fails, user might have insufficient rights, or even worse happened
			try
			{
				if (!Directory.Exists(LOGPATH))
					Directory.CreateDirectory(LOGPATH);
			}
			catch (Exception exc)
			{
				string msg = string.Format("A fatal error has occurred while initializing!\n"
										 + "\n"
										 + "Catched an exception\n"
										 + "\n"
										 + "{0}\n"
										 + "\n"
										 + "while trying to create log files folder\n"
										 + "\n"
										 + "{1}\n"
										 + "\n"
										 + "\n"
										 + "Consult the FaQ for further assistance."
										 , exc.Message
										 , LOGPATH);
				MessageBox.Show(msg, "Fatal error", MessageBoxButton.OK, MessageBoxImage.Stop);
				Process.GetCurrentProcess().Kill();
			}
		}

		// - Second, init remain based on actual config instance
		internal static void Init()
		{
			Splashscreen.ShowSplash("Starting up...");
			Application.Current.MainWindow = null;

			// Note version
			Log.Info("{0}, version {1}", APPNAME, APPVERSION);


			if (!Config.Root.HasSection("core"))
				Config.Root.AddSection("core");


			// Pick suitable default language if setting is missing
			Splashscreen.SetMessage("Setting up language");
			if (!Config.Root.core.HasItem("language"))
				Config.Root.core.AddItem("language");
			if (string.IsNullOrEmpty(Config.Root.core.language))
				Config.Root.core.language = Thread.CurrentThread.CurrentUICulture.Name;
			// Setup actual language for translations
			Splashscreen.SetMessage("Loading language resources");
			LANGUAGE = Config.Root.core.language;
			LanguageHandler.LANG.SelectLanguage(LANGUAGE);
			Thread.CurrentThread.CurrentUICulture = new CultureInfo(LANGUAGE);


			// Create defaultpath setting if not set up yet
			Splashscreen.SetMessage("Setting up default savegame path");
			if (!Config.Root.core.HasItem("defaultpath"))
				Config.Root.core.AddItem("defaultpath");
			if (string.IsNullOrEmpty(Config.Root.core.defaultpath) || !Directory.Exists(Config.Root.core.defaultpath))
			{
				string subpath = Path.Combine("FactoryGame", "Saved", "SaveGames");

				string path = Environment.GetEnvironmentVariable("LOCALAPPDATA");
				if (path != null)
				{
					path = Path.Combine(path, subpath);
					if (!Directory.Exists(path))
						path = null;
				}

				if (path == null)
				{
					path = Environment.GetEnvironmentVariable("APPDATA");
					if (path != null)
					{
						path = Path.Combine(path, "..", subpath);
						if (!Directory.Exists(path))
							path = null;
					}
				}

				if (path == null)
				{
					path = Environment.GetEnvironmentVariable("USERPROFILE");
					if (path != null)
					{
						path = Path.Combine(path, "ApplicationData", "Local", subpath);
						if (!Directory.Exists(path))
							path = null;
					}
				}

				Config.Root.core.defaultpath = path;
			}


			// Read user id from registry, resp. create one if none was found in registry
			string id = null;
			try
			{
				using (RegistryKey key = Helpers.OpenRegKey(Registry.CurrentUser,
					"SOFTWARE/Epic Games/Unreal Engine/Identifiers"))
				{
					if (key != null)
						id = key.GetValue("AccountId") as string;
				}
			}
			catch { }
			if (id == null)
				id = Environment.UserName + Environment.MachineName;
			if (id != null)
			{
				// Create anonymous id on info found
				using (SHA256 sha = SHA256.Create())
				{
					byte[] arr = Encoding.ASCII.GetBytes(id);
					USER_KEY = sha.ComputeHash(arr);
					USER_ID  = BitConverter.ToString(USER_KEY).Replace("-", "");
				}
			}


			// Setup export path
			if (!Config.Root.core.HasItem("exportpath"))
				Config.Root.core.AddItem("exportpath", "");
			if (string.IsNullOrEmpty(Config.Root.core.exportpath) || !Directory.Exists(Config.Root.core.exportpath))
				Config.Root.core.exportpath = Path.Combine(APPPATH, EXPORTS);
			EXPORTPATH = Config.Root.core.exportpath;


			// Create any missing config before continuing
			if (!Config.Root.HasSection("update_check"))
				Config.Root.AddSection("update_check");
			if (!Config.Root.update_check.HasItem("check_for_updates"))
				Config.Root.update_check.AddItem("check_for_updates", true);

			if (!Config.Root.HasSection("deep_analysis"))
				Config.Root.AddSection("deep_analysis");
			if (!Config.Root.deep_analysis.HasItem("enabled"))
				Config.Root.deep_analysis.AddItem("enabled", false);

			if (!Config.Root.HasSection("crash_reports"))
				Config.Root.AddSection("crash_reports");
			if (!Config.Root.crash_reports.HasItem("enabled"))
				Config.Root.crash_reports.AddItem("enabled", false);

			if (!Config.Root.HasSection("incident_reports"))
				Config.Root.AddSection("incident_reports");
			if (!Config.Root.incident_reports.HasItem("enabled"))
				Config.Root.incident_reports.AddItem("enabled", false);

			if (!Config.Root.HasSection("online_mapping"))
				Config.Root.AddSection("online_mapping");
			if (!Config.Root.online_mapping.HasItem("enabled"))
				Config.Root.online_mapping.AddItem("enabled", false);
		}

	}

}
