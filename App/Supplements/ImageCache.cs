using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;


using CoreLib;

using FileHandler;

using PakHandler;
using PakHandler.Structures;


namespace SatisfactorySavegameTool.Supplements
{

	public static class ImageCache
	{

		public static PakLoader GetPak(VersionTable.Version version)
		{
			return _Cache[version]._Loader;
		}

		public static void LoadCaches(ICallback callback = null)
		{
			if (!_ValidatePaths())
				return;

			Task.Run(async() => {
				_loading = true;
				await _LoadCaches(null);
				_loading = false;
			});
		}

		public static void CloseCaches()
		{
			while (_loading)
			{
				Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
				System.Threading.Thread.Yield();
				Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
			}

			foreach (PakCache cache in _Cache.Values)
			{
				if (cache != null)
					cache.Close();
			}

			_Cache.Clear();
		}

		public static BitmapSource GetImage(str object_name, int desired_resolution, VersionTable.Version version)
		{
			if (object_name == null)
				return null;
			return GetImage(object_name.ToString(), desired_resolution, version);
		}
		public static BitmapSource GetImage(string object_name, int desired_resolution, VersionTable.Version version)
		{
			//TODO: Check for background activity, and wait until this cache is loaded finally
			while (_loading)
			{
				Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
				System.Threading.Thread.Yield();
				Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
			}

			if (!_Cache.ContainsKey(version))
				return null;
			return _Cache[version].GetImage(object_name, desired_resolution);
		}


		private static bool _ValidatePaths()
		{
			// Find paths to .pak's if not set yet
			string ea_path  = Config.Root.pak_loader.ea_path;
			string exp_path = Config.Root.pak_loader.exp_path;

			if (!File.Exists(ea_path) || !File.Exists(exp_path))
			{
				// Try MuiCache first
				ea_path = exp_path = null;
				try
				{
					RegistryKey key = Helpers.OpenRegKey(Registry.ClassesRoot,
						"Local Settings/Software/Microsoft/Windows/Shell/MuiCache");
					if (key == null)
						key = Helpers.OpenRegKey(Registry.CurrentUser,
							"Software/Classes/Local Settings/Software/Microsoft/Windows/Shell/MuiCache");

					if (key != null)
					{
						var keys = key.GetValueNames().Where(k => k.ToLowerInvariant().Contains("satisfactory"));
						key.Dispose();

						if (keys.Count() >= 2)
						{
							//F:\Epic Games\SatisfactoryEarlyAccess\FactoryGame\Binaries\Win64\FactoryGame-Win64-Shipping.exe

							ea_path = keys.FirstOrDefault(k => k.ToLowerInvariant().Contains("earlyaccess"));
							if (File.Exists(ea_path))
							{
								ea_path = Path.GetDirectoryName(ea_path);
								ea_path = Path.Combine(ea_path, "..", "..", "Content", "Paks", "FactoryGame-WindowsNoEditor.pak");
								ea_path = Path.GetFullPath(ea_path);
							}

							exp_path = keys.FirstOrDefault(k => k.ToLowerInvariant().Contains("experimental"));
							if (File.Exists(exp_path))
							{
								exp_path = Path.GetDirectoryName(exp_path);
								exp_path = Path.Combine(exp_path, "..", "..", "Content", "Paks", "FactoryGame-WindowsNoEditor.pak");
								exp_path = Path.GetFullPath(exp_path);
							}

							if (!File.Exists(ea_path) || !File.Exists(exp_path))
								ea_path = exp_path = null;
						}
					}
				}
				catch
				{
					ea_path = exp_path = null;
				}
			}

			if (!File.Exists(ea_path) || !File.Exists(exp_path))
			{
				// Find epic launcher location by using its "program data"
				// => HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Epic Games\EpicGamesLauncher 
				//    - AppDataPath:REG_SZ -> C:\ProgramData\Epic\EpicGamesLauncher\Data\

				// No good? Install dir guessing -.-
				// => HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\EpicGames\Unreal Engine
				//    - INSTALLDIR:REG_SZ -> F:\Epic Games\
				// But have to guess SatisEarly+Exp paths

				// Still no good? Last resort: Firewall settings, but those are tricky to access/read
			}

			return (File.Exists(ea_path) || File.Exists(exp_path));
		}

		private static async Task _LoadCaches(ICallback callback)
		{
			Task early = Task.Run(() => { _CreateCache(VersionTable.Version.EarlyAccess,  callback); });
			Task exp   = Task.Run(() => { _CreateCache(VersionTable.Version.Experimental, callback); });
			await Task.WhenAll(early, exp);
		}

		private static bool _CreateCache(VersionTable.Version version, ICallback callback)
		{
			if (!_Cache.ContainsKey(version))
			{
				// Find path to pak by version given
				string pakfile = null;
				try
				{
					if (version == VersionTable.Version.EarlyAccess)
						pakfile = Config.Root.pak_loader.ea_path;
					else if (version == VersionTable.Version.Experimental)
						pakfile = Config.Root.pak_loader.exp_path;
				}
				catch
				{
				}
				if (pakfile == null || !File.Exists(pakfile))
					return false;

				PakCache cache = new PakCache(version);
				if (!cache.Load(pakfile, callback))
					return false;

				_Cache.Add(version, cache);
			}

			return true;
		}

		private static Dictionary<VersionTable.Version, PakCache> _Cache = new Dictionary<VersionTable.Version, PakCache>();
		private static volatile bool _loading = false;
	}

	internal class PakCache
	{
		internal PakCache(VersionTable.Version version)
		{
			_Version = version;
			_CachePath = null;
			_Loader = null;
			_Cache = new Dictionary<string, BitmapSource>();
		}

		~PakCache()
		{
			Close();
		}


		internal bool Load(string pakfile, ICallback callback)
		{
			// Before loading .pak, check cache folder and load all files
			if (callback != null)
				callback.Update((long)0, "Loading image cache", null);

			_CachePath = null;
			try
			{
				_CachePath = Path.Combine(Config.Root.pak_loader.cachepath, _Version.ToString());
				if (!Directory.Exists(_CachePath))
					Directory.CreateDirectory(_CachePath);
			}
			catch
			{
				_CachePath = null;
			}
			if (_CachePath == null)
				return false;

			var files = Directory.EnumerateFiles(_CachePath);
			foreach (string file in files)
			{
				if (callback != null)
					callback.Update((long)0, null, file);

				BitmapSource bitmap = ImageHandler.LoadImageFromFile(file);
				if (bitmap == null)
					continue;

				// Freeze and add to cache
				bitmap.Freeze();
				string name = Path.GetFileNameWithoutExtension(file);
				_Cache.Add(name, bitmap);
			}

			if (File.Exists(pakfile))
			{
				_Loader = new PakLoader(pakfile);
				if (_Loader == null)
					return false;

				if (callback != null)
					callback.Start((long)0, "Loading " + pakfile, "");

				if (!_Loader.Load(callback))
					return false;

				if (_Loader.Index.Count == 0)
					return false;
			}

			if (callback != null)
				callback.Stop("", "Done");

			return true;
		}

		internal void Close()
		{
			_Cache.Clear();

			if (_Loader != null)
			{
				_Loader.Close();
				_Loader = null;
			}
		}


		public BitmapSource GetImage(string object_name, int desired_resolution)
		{
			// Receiving something like
			//		/Game/FactoryGame/Resource/Environment/Berry/Desc_Berry.Desc_Berry_C
			// which must be transformed into
			//		Name: Desc_Berry_C
			// and
			//		Path: FactoryGame/Content/FactoryGame/Resource/Environment/Berry/Desc_Berry
			if (!object_name.StartsWith("/Game/"))
				return null;
			int pos = object_name.LastIndexOf('.');
			if (pos < 0)
				return null;

			// Generate name for cached entry (used for both key and filename)
			string name = object_name.Substring(pos + 1) + "-" + desired_resolution;

			// Check cache here
			BitmapSource bitmap = null;
			_Cache.TryGetValue(name, out bitmap);
			if (bitmap == null && _Loader != null)
			{
				// Not cached yet, so build path needed
				string path = object_name
					.Substring(0, pos)
					.Replace("/Game/", "FactoryGame/Content/")
					;
				// Load asset, this will contain texture info in its import table
				FObject obj = _Loader.ReadObject(path);
				if (obj == null)
					return null;

				// Below should yield something like:
				//		Berry_256
				//		Berry_64
				// If not, bail out
				var textures = obj.Summary.Imports
					.Where(i => (i.ClassName.Name == "Texture2D"))
					.Select(i => i.ObjectName.Name)
					;
				if (textures.Count() == 0)
					return null;

				// Pick best match (256 first, if not found use max. avail)
				string picked = null;
				if (desired_resolution <= 256)
					picked = textures.FirstOrDefault(t => t.Contains("_256"));
				if (picked == null)
					picked = textures.Max();
				if (picked == null)
					return null; // Shouldn't have happened :-/

				// Next up: Transform our intermediate path
				//		FactoryGame/Content/FactoryGame/Resource/Environment/Berry/Desc_Berry
				// into a full path like
				//		FactoryGame/Content/FactoryGame/Resource/Environment/Berry/UI/Berry_256
				//		FactoryGame/Content/FactoryGame/Resource/Environment/Berry/UI/Berry_64
				// to be able to access
				//		FactoryGame/Content/FactoryGame/Resource/Environment/Berry/UI/Berry_256.uasset
				//		FactoryGame/Content/FactoryGame/Resource/Environment/Berry/UI/Berry_256.uexp
				// resp.
				//		FactoryGame/Content/FactoryGame/Resource/Environment/Berry/UI/Berry_64.uasset
				//		FactoryGame/Content/FactoryGame/Resource/Environment/Berry/UI/Berry_64.uexp
				pos = path.LastIndexOf('/');
				if (pos < 0)
					return null;
				path = path.Substring(0, pos + 1) + "UI/" + picked;

				// Try to load
				FTexture2D texture = _Loader.ReadTexture(path);
				if (texture == null)
				{
					// Might be stored at a different location as seen with A.I. limiters.
					// Istead of
					//		FactoryGame/Content/FactoryGame/Resource/Parts/CircuitBoardHighSpeed/Desc_CircuitBoardHighSpeed/UI/...
					// its stored at 
					//		FactoryGame/Content/FactoryGame/Resource/Parts/AIlimiter/UI/AILimiter_256
					textures = obj.Summary.Imports
						.Where(i => (i.ObjectName.Name.EndsWith(picked)))
						.Select(i => i.ObjectName.Name)
						;
					if (textures.Count() > 0)
					{
						path = textures
							.First()
							.Replace("/Game/", "FactoryGame/Content/")
							;

						// Another try
						texture = _Loader.ReadTexture(path);
					}
				}
				if (texture == null)
					return null;

				// Try to find an image with desired resolution
				bitmap = texture.GetImage(desired_resolution, desired_resolution);
				if (bitmap == null)
				{
					// Get biggest image avail and scale down to size desired
					PakHandler.Structures.Size biggest = texture
						.GetDimensions()
						.OrderByDescending(d => (long)d.SizeX * d.SizeY)
						.First()
						;
					bitmap = texture.GetImage(biggest.SizeX, biggest.SizeY);
					if (bitmap == null)
						return null;

					double scale = desired_resolution / (double)biggest.SizeX;
					bitmap = new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
				}

				// Freeze image
				bitmap.Freeze();

				// Add image to caches (lossless PNG for now)
				string filename = Path.Combine(_CachePath, name + ".png");
				ImageHandler.SaveImageToFile(bitmap, filename);

				_Cache.Add(name, bitmap);
			}

			// Finally, return image
			return bitmap;
		}


		internal VersionTable.Version _Version;
		internal string _CachePath;
		internal PakLoader _Loader;
		internal Dictionary<string, BitmapSource> _Cache;

	}

}
