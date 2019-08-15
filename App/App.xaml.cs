using System;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
		protected Logger _logger = null;
		protected ConfigFile _config = null;
		protected LanguageHandler _languages = null;
		protected VersionTable _versions = null;

		protected override void OnStartup(StartupEventArgs e)
		{
			_logger    = new Logger(Settings.LOGPATH, Settings.APPNAME, Logger.Level.Debug);
			_config    = new ConfigFile(Settings.APPPATH, Settings.APPNAME);
			_languages = new LanguageHandler(Settings.RESOURCEPATH, null, TRANSLATIONFILES);

			// Setup error reporting
			_InitErrorReporting();

			Splashscreen.ShowSplash("Starting up...");
			MainWindow = null;

			Settings.Init();

			_versions = new VersionTable();
			// Add more, like research table

			base.OnStartup(e);
		}

		protected override void OnExit(ExitEventArgs e)
		{
			base.OnExit(e);

			_languages = null;

			_config.Shutdown();
			_config = null;

			_logger.Shutdown();
			_logger = null;
		}


		private void _InitErrorReporting()
		{
			// Catching those based on idea in https://stackoverflow.com/a/46804709
			AppDomain.CurrentDomain.UnhandledException += _AppDomainUnhandledException;
			DispatcherUnhandledException += _DispatcherUnhandledException;
			TaskScheduler.UnobservedTaskException += _TaskSchedulerUnobservedTaskException;
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


		internal void SaveConfig()
		{
			_config.Flush();
		}

		internal void SendReport()
		{
			//TODO: Flush current log, save to zip and upload to XXX
		}


		private const  string   TRANSLATIONFILE_MAIN = "Translation.res";
		private const  string   TRANSLATIONFILE_FG   = "FactoryGame.res";
		private static string[] TRANSLATIONFILES     = new string[] { TRANSLATIONFILE_MAIN, TRANSLATIONFILE_FG };

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

		// Path to where exports are to be stored - configured by user
		public const  string EXPORTS              = "exports";
		public static string EXPORTPATH           = null;


		// Localisation related - configured by user
		public static string LANGUAGE             = null;


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

			string path;
#if DEBUG
			path = @"E:\GitHub\satisfactory-savegame-tool-ng\App";
#else
			//TODO:
			path = Process.GetCurrentProcess().StartInfo.WorkingDirectory;
			if (path == "" || !Directory.Exists(path))
			{
				path = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
			}
			if (path == "" || !Directory.Exists(path))
			{
				path = @"E:\GitHub\satisfactory-savegame-tool-ng";
			}
#endif
			APPPATH = path;

			// Setup paths which do rely on our main path
			RESOURCEPATH = Path.Combine(APPPATH, RESOURCES);
			LOGPATH      = Path.Combine(APPPATH, LOGS);


		}

		// - Second, init remain based on actual config instance
		internal static void Init()
		{
			// Pick suitable default language if setting is missing
			Splashscreen.SetMessage("Setting up language");
			if (!Config.Root.HasSection("core") 
				|| !Config.Root.core.HasItem("language")
				|| string.IsNullOrEmpty(Config.Root.core.language))
			{
#if DEBUG
				Config.Root.core.language = "en-US";
#else
				Config.Root.core.language = Thread.CurrentThread.CurrentUICulture.Name;
#endif
			}
			// Setup actual language for translations
			Splashscreen.SetMessage("Loading language resources");
			LANGUAGE = Config.Root.core.language;
			LanguageHandler.LANG.SelectLanguage(LANGUAGE);
			Thread.CurrentThread.CurrentUICulture = new CultureInfo(LANGUAGE);
			

			// Create defaultpath setting if not set up yet
			Splashscreen.SetMessage("Setting up default savegame path");
			if (!Config.Root.HasSection("core") 
				|| !Config.Root.core.HasItem("defaultpath") 
				|| Config.Root.core.defaultpath == "")
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


			EXPORTPATH   = Path.Combine(APPPATH, EXPORTS);//TODO: Use config value



		}

	}

}
