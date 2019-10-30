using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

using CoreLib;
using A = CoreLib.Attributes;


namespace SatisfactorySavegameTool.Actions
{

	/// <summary>
	/// Action interface.
	/// Derive your actions from this interface to allow for being recognized as an action.
	/// Use the attributes in Actions.Attributes to define its name, together with an optional 
	/// description and icon.
	/// </summary>
	public interface IAction
	{
		//public Cons(Savegame.Savegame savegame);

		void Run();
		//public async Task<...> RunAsync(/*ICallback callback*/);
	}


	// Factory used to gather all actions avail, incl. those found within plugins.
	public class ActionFactory : BaseFactory<IAction>
	{
		public static IAction Create(string name, Savegame.Savegame savegame)
		{
			IAction action = null;
			if (INSTANCE.IsKnown(name))
				action = INSTANCE[name, savegame];
			return action;
		}

		// Add found actions to 'Actions' menu
		public static void AddToMenu(MenuItem menu, RoutedEventHandler handler)
		{
			INSTANCE._AddToMenu(menu, handler);
		}

		protected static ActionFactory INSTANCE = new ActionFactory();

		protected List<string> _plugins;

		protected override void _CreateLookup()
		{
			// Setup signature of constructor were looking for
			// (see interface comment)
			Type[] cons = { typeof(Savegame.Savegame) };

			Assembly assembly = Assembly.GetExecutingAssembly();

			// Creating actual lookup on built-in actions
			_CreateLookup(assembly, cons);

			// Add plugins
			_plugins = new List<string>();

			string dir = Settings.PLUGINPATH;
			if (Directory.Exists(dir))
			{
				Log.Debug("Enumerating plugins in '{0}'", dir);

				foreach (string file in Directory.EnumerateFiles(dir, "*.dll"))
				{
					Assembly ass = Assembly.LoadFile(file);
					if (ass != null)
					{
						Log.Debug("- " + ass.FullName);

						_plugins.Add(ass.GetName().Name);
						_AddToLookup(ass, true);
					}
					else
					{
						Log.Warning("- Failed to load plugin '{0}'", file);
					}
				}
			}
		}

		protected void _AddToMenu(MenuItem menu, RoutedEventHandler handler)
		{
			Func<MenuItem,string,MenuItem> add_plugin = (m,name) => {
				string header = name;
				if (Translate.Has(header))
					header = Translate._(header);

				MenuItem action = new MenuItem() {
					Header = name,
					Tag = name,
				};

				m.Items.Add(action);

				return action;
			};
			Action<MenuItem,string,ConstructorInfo> add_action = (m,name,ci) => {
				if (ci == null)
					return;

				Type cls = ci.DeclaringType;
				if (!Attributes.Name.Has(cls))
					return;

				string header = Attributes.Name.Get(cls);
				if (header[0] == '[')
					header = Translate._(header.Substring(1, header.Length - 2));

				MenuItem action = new MenuItem() {
					Name = name.Replace('\\', '_'),
					Header = header,
					Tag = name,
				};
				if (Attributes.Description.Has(cls))
				{
					string tooltip = Attributes.Description.Get(cls);
					if (tooltip[0] == '[')
						tooltip = Translate._(tooltip.Substring(1, tooltip.Length - 2));
					action.ToolTip = tooltip;
				}
				if (Attributes.Icon.Has(cls))
					action.Icon = Attributes.Icon.Get(cls);

				action.Click += handler;

				m.Items.Add(action);
			};

			menu.Items.Clear();

			var all = GetKnown();
			var builtin = all.Where(pair => !pair.Key.Contains('\\'));
			foreach (var pair in builtin)
				add_action(menu, pair.Key, pair.Value);

			var plugins = all.Except(builtin);
			if (plugins.Count() > 0)
			{
				var groups = plugins.GroupBy(pair => pair.Key.Split('\\')[0]);
				if (groups.Count() > 0)
				{
					menu.Items.Add(new Separator());
					foreach (var group in groups)
					{
						MenuItem plugin = add_plugin(menu, group.Key);
						foreach (var pair in group)
							add_action(plugin, pair.Key, pair.Value);
					}
				}
			}
		}

	}

}


namespace SatisfactorySavegameTool.Actions.Attributes
{
	// Attributes used with specifying actions

	/// <summary>
	/// Action name (mandatory).
	/// Use [] to add as a localised resource.
	/// </summary>
	public class Name : A.StringAttr
	{
		public Name(string name)
		{
			Value = name;
		}

		public static bool Has(Type type)
		{
			return Has<Name>(type);
		}

		public static string Get(Type type)
		{
			return Get<Name>(type);
		}
	}

	/// <summary>
	/// Action description (optional).
	/// Use [] to add as a localised resource.
	/// </summary>
	public class Description : A.StringAttr
	{
		public Description(string desc)
		{
			Value = desc;
		}

		public static bool Has(Type type)
		{
			return Has<Description>(type);
		}

		public static string Get(Type type)
		{
			return Get<Description>(type);
		}
	}

	/// <summary>
	/// Action icon (optional).
	/// (A relative ref into the plugins assembly).
	/// </summary>
	public class Icon : A.ImageRefAttr
	{
		public Icon(string res)
			: base(res)
		{ }
	}

}
