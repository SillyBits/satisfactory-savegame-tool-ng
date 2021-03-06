﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;


using FileHandler;

using P = Savegame.Properties;


namespace SatisfactorySavegameTool.Supplements
{

	/// <summary>
	/// Commonly used extension methods
	/// </summary>
	public static class Extensions
	{
		public static List<_KeyType> Keys<_KeyType,_ValueType>(this Dictionary<_KeyType,_ValueType> dict)
		{
			return dict.ToList().Select(pair => pair.Key).ToList();
		}


		// Refresh given element by forcing its dispatcher to render
		// (will eat performance like crazy, so advised to do this only with vital elements)
		private static readonly Action EmptyDelegate = delegate { };
		public static void Refresh(this UIElement uiElement)
		{
			uiElement.Dispatcher.Invoke(DispatcherPriority.Render, EmptyDelegate);
		}

		// Wait on task, but keep dispatcher actively working while waiting
		// (will slow down tasks performance a bit, but still way better than a frozen UI)
		public static void WaitWithDispatch(this Task task, Dispatcher dispatcher)
		{
			while (!task.IsCompleted)
			{
				dispatcher.Invoke(() => { }, DispatcherPriority.Render);
				System.Threading.Thread.Yield();
				dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
			}
		}


		public static bool IsNullOrEmpty(this byte[] arr) { return _IsNullOrEmpty(arr); }
		public static bool IsNullOrEmpty(this int[] arr)  { return _IsNullOrEmpty(arr); }
		private static bool _IsNullOrEmpty<_ValueType>(_ValueType[] arr)
			where _ValueType : IConvertible
		{
			if (arr == null || arr.Length == 0)
				return true;
			double sum = arr.Sum((v) => v.ToDouble(System.Globalization.CultureInfo.InvariantCulture));
			return (sum == 0);
		}


		// Used to get game version info from savegame header
		public static VersionTable.VersionEntry GetVersionEntry(this P.Header header)
		{
			int build_version = header.GetBuildVersion();
			return VersionTable.INSTANCE.Find(build_version);
		}

		public static int GetBuildVersion(this P.Header header)
		{
			int build_version = header.BuildVersion;
			if (header.SaveVersion < 21)
				build_version += 34682;
			return build_version;
		}

		public static VersionTable.Version GetVersion(this P.Header header)
		{
			int build_version = header.GetBuildVersion();
			VersionTable.VersionEntry version = VersionTable.INSTANCE.Find(build_version);
			if (version == null)
				return VersionTable.Version.Unknown;
			return version.Release;
		}


		// Used to extract last portion of a string which can be split using given separator
		// (e.g. "/Script/FactoryGame.FGInventoryComponent" -> "FGInventoryComponent")
		public static string LastName(this str name, char separator = '.')
		{
			if (str.IsNullOrEmpty(name))
				return null;
			return LastName(name.ToString(), separator);
		}

		public static string LastName(this string name, char separator = '.')
		{
			if (!string.IsNullOrEmpty(name))
			{
				string[] names = name.Split(separator);
				if (names.Length >= 2)
					return names.Last();
			}
			return null;
		}


		// Get all named value properties
		public static List<string> Names(this P.Properties props)
		{
			return props
				.Where(prop => (prop is P.ValueProperty) && !str.IsNullOrEmpty((prop as P.ValueProperty).Name))
				.Select(prop => {
					P.ValueProperty val = prop as P.ValueProperty;
					string s = val.Name.ToString();
					if (val.Index != 0)
						s += "#" + val.Index;
					return s;
				})
				.ToList()
				;
		}

		// Find a value property by name
		public static P.Property Named(this P.Properties props, string name, int index = -1)
		{
			if (index < 0)
			{
				int pos = name.IndexOf('#');
				if (pos > 0)
				{
					string idx = name.Substring(pos + 1);
					name = name.Substring(0, pos);
					if (!int.TryParse(idx, out index))
						return null;
				}
			}
			if (index >= 0)
				return props.FirstOrDefault(prop => {
					if (prop is P.ValueProperty)
					{
						P.ValueProperty val = prop as P.ValueProperty;
						str prop_name = val.Name;
						if (!str.IsNullOrEmpty(prop_name))
							return (val.Index == index) && (prop_name.ToString() == name);
					}
					return false;
				});

			return props.FirstOrDefault(prop => {
				if (prop is P.ValueProperty)
				{
					str prop_name = (prop as P.ValueProperty).Name;
					if (!str.IsNullOrEmpty(prop_name))
						return prop_name.ToString() == name;
				}
				return false;
			});
		}

		// Find name of value instance passed, or null if not found
		public static string FindName(this P.Properties props, object value)
		{
			string name = null;
			foreach (P.Property prop in props)
			{
				if (prop is P.ValueProperty && prop == value)
				{
					P.ValueProperty v = (prop as P.ValueProperty);
					if (!str.IsNullOrEmpty(v.Name))
						name = v.Name.ToString();
					break;
				}
			}
			return null;
		}
		public static string FindName(this Dictionary<string,object> childs, object value)
		{
			string name = null;
			foreach (var pair in childs)
			{
				if (pair.Value == value)
				{
					name = pair.Key;
					break;
				}
			}
			return null;
		}


		// Convert a Properties list into another type, e.g. List<InventoryStack>
		public static List<_PropertyType> ListOf<_PropertyType>(this object props)
		{
			if (props is P.Properties)
				return (props as P.Properties).Cast<_PropertyType>().ToList();
			throw new ArgumentException("Invalid list object passed!");
		}


		// Find a property by it's path name
		public static P.Property FindByPathName(this P.Properties props, str pathname, bool case_insensitive = false)
		{
			return props.FindByPathName(pathname.ToString(), case_insensitive);
		}

		public static P.Property FindByPathName(this P.Properties props, string pathname, bool case_insensitive = false)
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


		// Tries to get .ClassName from property passed, or null if property has no such value
		public static string GetClassName(this P.Property prop)
		{
			if (prop is P.Actor)
				return (prop as P.Actor).ClassName.ToString();
			if (prop is P.Object)
				return (prop as P.Object).ClassName.ToString();
			if (prop is P.RailroadTrackPosition)
				return (prop as P.RailroadTrackPosition).ClassName.ToString();

			return null;
		}

		// Tries to get .PathName from property passed, or null if property has no such value
		public static string GetPathName(this P.Property prop)
		{
			if (prop is P.Actor)
				return (prop as P.Actor).PathName.ToString();
			if (prop is P.Object)
				return (prop as P.Object).PathName.ToString();
			if (prop is P.Collected)
				return (prop as P.Collected).PathName.ToString();
			if (prop is P.InventoryItem)
				return (prop as P.InventoryItem).PathName.ToString();
			if (prop is P.ObjectProperty)
				return (prop as P.ObjectProperty).PathName.ToString();
			if (prop is P.RailroadTrackPosition)
				return (prop as P.RailroadTrackPosition).PathName.ToString();
			if (prop is P.Entity)
				return (prop as P.Entity).PathName.ToString();
			if (prop is P.NamedEntity.Name)
				return (prop as P.NamedEntity.Name).PathName.ToString();

			return null;
		}

		// Tries to get .Name from property passed, or null if property has no such value
		public static string GetName(this P.Property prop)
		{
			if (prop is P.ValueProperty)
			{
				str s = (prop as P.ValueProperty).Name;
				if (s != null)
					return s.ToString();
			}

			return null;
		}


		// Tries to get a pretty name from property passed in, or null if none found
		public static string PrettyName(this object val)
		{
			string name = null;
			if (val is P.ValueProperty)
			{
				P.ValueProperty val_prop = val as P.ValueProperty;
				if (val_prop.Name != null)
					name = val_prop.Name.ToString();
			}
			if (string.IsNullOrEmpty(name) && val is P.Property)
			{
				P.Property prop = val as P.Property;
				name = prop.GetPathName();
				if (!string.IsNullOrEmpty(name))
					name = name.LastName();
				else if (prop.Parent != null)
				{
					name = prop.Parent.GetChilds().FindName(prop);
					//TODO: Could add further checking by traversing parent properties
					//      and checking Properties- resp. PropertyList-related childs
					//if (string.IsNullOrEmpty(name)) ...
				}
			}
			return name;
		}


		// Check float for being with range of value given
		public static bool IsNear(this float val, float near, float range = 100.0f * float.Epsilon)
		{
			return (near - range <= val) && (val <= near + range);
		}

		// Check float for being near 0
		public static bool IsNearZero(this float val, float range = 100.0f * float.Epsilon)
		{
			return (-range <= val) && (val <= +range);
		}

		// Check vector for being identity
		public static bool IsIdentity(this P.Vector val)
		{
			return val.X.IsNear(1.0f) && val.Y.IsNear(1.0f) && val.Z.IsNear(1.0f);
		}

		// Check scale for being identity
		public static bool IsIdentity(this P.Scale val)
		{
			return (val as P.Vector).IsIdentity();
		}

	}

}
