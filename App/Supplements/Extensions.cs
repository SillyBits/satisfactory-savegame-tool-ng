using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;


using P = Savegame.Properties;


namespace SatisfactorySavegameTool.Supplements
{

	/// <summary>
	/// Commonly used extension methods
	/// </summary>
	public static class Extensions
	{

		private static readonly Action EmptyDelegate = delegate { };
		public static void Refresh(this UIElement uiElement)
		{
			uiElement.Dispatcher.Invoke(DispatcherPriority.Render, EmptyDelegate);
		}


		internal static bool IsNullOrEmpty(this byte[] arr) { return _IsNullOrEmpty(arr); }
		internal static bool IsNullOrEmpty(this int[] arr)  { return _IsNullOrEmpty(arr); }
		private static bool _IsNullOrEmpty<_ValueType>(_ValueType[] arr)
			where _ValueType : IConvertible
		{
			if (arr == null || arr.Length == 0)
				return true;
			double sum = arr.Sum((v) => v.ToDouble(System.Globalization.CultureInfo.InvariantCulture));
			return (sum == 0);
		}


		// Used to get game version info from savegame header
		internal static VersionTable.VersionEntry GetVersionEntry(this P.Header header)
		{
			int build_version = header.BuildVersion + 34682;
			return VersionTable.INSTANCE.Find(build_version);
		}

		internal static int GetBuildVersion(this P.Header header)
		{
			return header.BuildVersion + 34682;
		}

		internal static VersionTable.Version GetVersion(this P.Header header)
		{
			int build_version = header.BuildVersion + 34682;
			VersionTable.VersionEntry version = VersionTable.INSTANCE.Find(build_version);
			if (version == null)
				return VersionTable.Version.Unknown;
			return version.Release;
		}


		// Used to extract last portion of a string which can be split using given separator
		// (e.g. "/Script/FactoryGame.FGInventoryComponent" -> "FGInventoryComponent")
		internal static string LastName(this str name, char separator = '.')
		{
			if (str.IsNullOrEmpty(name))
				return null;
			return LastName(name.ToString(), separator);
		}

		internal static string LastName(this string name, char separator = '.')
		{
			if (!string.IsNullOrEmpty(name))
			{
				string[] names = name.Split(separator);
				if (names.Length >= 2)
					return names.Last();
			}
			return null;
		}


		// Find a value property by name
		internal static P.Property Named(this P.Properties props, string name)
		{
			return props.Find(prop => {
				if (prop is P.ValueProperty)
				{
					str prop_name = (prop as P.ValueProperty).Name;
					if (!str.IsNullOrEmpty(prop_name))
						return prop_name.ToString() == name;
				}
				return false;
			});
		}


		// Convert a Properties list into another type, e.g. List<InventoryStack>
		internal static List<_PropertyType> ListOf<_PropertyType>(this object props)
		{
			if (props is P.Properties)
				return (props as P.Properties).Cast<_PropertyType>().ToList();
			throw new ArgumentException("Invalid list object passed!");
		}


		// Find a property by it's path name
		internal static P.Property FindByPathName(this P.Properties props, str pathname, bool case_insensitive = false)
		{
			return props.FindByPathName(pathname.ToString(), case_insensitive);
		}

		internal static P.Property FindByPathName(this P.Properties props, string pathname, bool case_insensitive = false)
		{
			if (case_insensitive)
			{
				string _pathname = pathname.ToLower();
				return props.Find(prop => {
					return ((prop is P.Object) && (prop as P.Object).PathName.ToString().ToLower() == _pathname) 
						|| ((prop is P.Actor ) && (prop as P.Actor ).PathName.ToString().ToLower() == _pathname)
						;
				});
			}

			return props.Find(prop => {
				return ((prop is P.Object) && (prop as P.Object).PathName.ToString() == pathname) 
					|| ((prop is P.Actor ) && (prop as P.Actor ).PathName.ToString() == pathname)
					;
			});
		}


		// Check float for being with range of value given
		internal static bool IsNear(this float val, float near, float range = float.Epsilon)
		{
			return (near - range <= val) && (val <= near + range);
		}

		// Check float for being near 0
		internal static bool IsNearZero(this float val, float range = 10 * float.Epsilon)
		{
			return (-range <= val) && (val <= +range);
		}

		// Check vector for being identity
		internal static bool IsIdentity(this P.Vector val)
		{
			return val.X.IsNear(1.0f) && val.Y.IsNear(1.0f) && val.Z.IsNear(1.0f);
		}

		// Check scale for being identity
		internal static bool IsIdentity(this P.Scale val)
		{
			return (val as P.Vector).IsIdentity();
		}

	}

}
