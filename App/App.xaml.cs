using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;

using CoreLib;

namespace SatisfactorySavegameTool
{
    /// <summary>
    /// Interaction logic for "App.xaml"
    /// </summary>
    public partial class App : Application
    {
		public static string APPNAME              = "SatisfactorySavegameTool";
		public static string APPPATH              = null;
		public static string RESOURCES            = "Resources";
		public static string RESOURCEPATH         = null;
		public static string TRANSLATIONFILE_MAIN = "Translation.res";
		public static string TRANSLATIONFILE_FG   = "FactoryGame.res";
		public static string LANGUAGE             = null;
		public static string EXPORTS              = "exports";
		public static string EXPORTPATH           = null;
		//public static string ...


		protected Logger _logger = null;
		protected ConfigFile _config = null;
		protected LanguageHandler _languages = null;

		protected override void OnStartup(StartupEventArgs e)
		{
			string path;
#if DEBUG
			path = @"E:\GitHub\satisfactory-savegame-tool-ng\App";
#else
			/*TODO:
			path = Process.GetCurrentProcess().StartInfo.WorkingDirectory;
			if (path == "" || !Directory.Exists(path))
			{
				path = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
			}
			if (path == "" || !Directory.Exists(path))
			{
				path = @"E:\GitHub\satisfactory-savegame-tool-ng";
			}*/
			path = @"E:\GitHub\satisfactory-savegame-tool-ng\App";
#endif

			APPPATH      = path;
			RESOURCEPATH = Path.Combine(path, RESOURCES);
			EXPORTPATH   = Path.Combine(path, EXPORTS);

			_logger = new Logger(Path.Combine(APPPATH, "logs"), APPNAME, Logger.Level.Debug);
			_config = new ConfigFile(APPPATH, APPNAME);
			_languages = new LanguageHandler(RESOURCEPATH, null, 
				new string[] { TRANSLATIONFILE_MAIN, TRANSLATIONFILE_FG });

			// Select suitable language for translations
			string langid = null;
			if (Config.Root.HasSection("core") && Config.Root.core.HasItem("language"))
			{
				Log.Info("Language: {0}", Thread.CurrentThread.CurrentUICulture.Name);
				#if DEBUG
				langid = "en-US";
				#else
				langid = Config.Root.core.language;
				#endif
			}
			if (string.IsNullOrEmpty(langid))
				langid = Thread.CurrentThread.CurrentUICulture.Name;
			_languages.SelectLanguage(langid);
			Thread.CurrentThread.CurrentUICulture = new CultureInfo(langid);
			LANGUAGE = langid;

			// Create defaultpath setting if not set up yet
			if (!Config.Root.HasSection("core") 
				|| !Config.Root.core.HasItem("defaultpath") 
				|| Config.Root.core.defaultpath == "")
			{
				string subpath = Path.Combine("FactoryGame", "Saved", "SaveGames");

				path = Environment.GetEnvironmentVariable("LOCALAPPDATA");
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

				//TODO: Query registry if empty still

				if (path != null)
					Config.Root.core.defaultpath = path;
			}

			base.OnStartup(e);
		}

		protected override void OnExit(ExitEventArgs e)
		{
			base.OnExit(e);

			_logger.Shutdown();
			_logger = null;

			_config.Shutdown();
			_config = null;
		}

	}
}
