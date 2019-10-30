using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

using CoreLib;

using FileHandler;

using P = Savegame.Properties;

using SatisfactorySavegameTool.Actions;
using A = SatisfactorySavegameTool.Actions.Attributes;
using SatisfactorySavegameTool.Supplements;


namespace ActionExamples
{
	// This example action shows how to run a method on a save to gather some information.
	// After completion, the report is being stored to file using a path from the tool's settings.
	//
	[A.Name("Export child classes"), A.Description("Export all classes found within .Children")]
	public class ExportChildClasses : IAction
	{
		public ExportChildClasses(Savegame.Savegame savegame)
		{
			_savegame = savegame;
			_runner   = new HierarchyRunner(savegame, false);
			_classes  = new Dictionary<string, List<string>>();
		}


		public void Run()
		{
			Helper.WaitAsync(RunAsync);
		}

		public async Task RunAsync()
		{
			await Task.Run(() => {
				Log.Info("Exporting ...");
				DateTime start_time = DateTime.Now;

				_runner.Run("Exporting ...", _Process);

				_WriteFile();

				DateTime end_time = DateTime.Now;
				TimeSpan ofs = end_time - start_time;
				Log.Info("Export took {0}", ofs);
			});
		}


		private void _Process(P.Property prop)
		{
			//-> [Actor] Persistent_Level:PersistentLevel.Char_Player_C_0
			//  .ClassName = str:'/Game/FactoryGame/Character/Player/Char_Player.Char_Player_C'
			//  .PathName = str:'Persistent_Level:PersistentLevel.Char_Player_C_0'
			//  .EntityObj =
			//	-> [NamedEntity] 
			//	  .Children =
			//		/ List with 5 elements:
			//		|-> [Name]
			//		|  .LevelName = str:'Persistent_Level'
			//		|  .PathName = str:'Persistent_Level:PersistentLevel.Char_Player_C_0.TrashSlot'
			//		|  ...
			//		|-> [Name]
			//		|  .LevelName = str:'Persistent_Level'
			//		|  .PathName = str:'Persistent_Level:PersistentLevel.Char_Player_C_0.inventory'
			//		\ end of list

			P.Actor actor = prop as P.Actor;
			if (actor == null)
				return;

			if (str.IsNullOrEmpty(actor.ClassName))
				return;
			string actor_class = actor.ClassName.ToString();

			P.NamedEntity entity = actor.EntityObj as P.NamedEntity;
			if (entity == null)
				return;

			var childs = entity.GetChilds();
			if (!childs.ContainsKey("Children"))
				return;

			P.Properties props = childs["Children"] as P.Properties;
			if (props == null || props.Count == 0)
				return;

			List<P.NamedEntity.Name> names = props.ListOf<P.NamedEntity.Name>();
			foreach (P.NamedEntity.Name name in names)
			{
				string pathname = "";
				if (!str.IsNullOrEmpty(name.PathName))
					pathname = name.PathName.LastName();

				List<string> classes;
				if (!_classes.ContainsKey(pathname))
				{
					classes = new List<string>();
					_classes.Add(pathname, classes);
				}
				else
				{
					classes = _classes[pathname];
				}
				if (!classes.Contains(actor_class))
					classes.Add(actor_class);
			}
		}

		private void _WriteFile()
		{
			string filename =  Path.Combine(SatisfactorySavegameTool.Settings.EXPORTPATH, Path.GetFileName(_savegame.Filename) + "-childs.export");
			using (StreamWriter w = File.CreateText(filename))
			{
				w.WriteLine("All classes found within .Children:");
				w.WriteLine("===================================");
				w.WriteLine("");

				List<string> keys = _classes.Keys();
				keys.Sort();
				foreach (string key in keys)
				{
					w.WriteLine("." + key);
					List<string> classes = _classes[key];
					classes.Sort();
					classes.ForEach(c => w.WriteLine(" - " + c));
					w.WriteLine("");
				}

				w.Flush();
				w.Close();
			}
		}


		private Savegame.Savegame                _savegame;
		private HierarchyRunner                  _runner;
		private Dictionary<string, List<string>> _classes;
	}

}
