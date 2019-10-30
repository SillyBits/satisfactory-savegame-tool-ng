using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using CoreLib;

using FileHandler;

using P = Savegame.Properties;

using SatisfactorySavegameTool.Actions;
using A = SatisfactorySavegameTool.Actions.Attributes;
using SatisfactorySavegameTool.Dialogs;
using SatisfactorySavegameTool.Supplements;


namespace ActionExamples
{
	// This example action shows how to run a method on a save to gather some information,
	// in this case all the class names in a save.
	// After completion, the tool's raw text dialog is being used to show the results.
	//
	[A.Name("Export class names"), A.Description("Export all class names")]
	public class ExportClassNames : IAction
	{
		public ExportClassNames(Savegame.Savegame savegame)
		{
			_runner       = new HierarchyRunner(savegame, false);
			_classnames   = new List<string>();
			_report_sb    = new StringBuilder();
			_report_depth = 0;
		}


		public void Run()
		{
			Helper.WaitAsync(RunAsync);

			ShowRawTextDialog.Show("", _report_sb.ToString());
			_report_sb.Clear();
		}

		public async Task RunAsync()
		{
			await Task.Run(() => {
				Log.Info("Starting up process ...");
				DateTime start_time = DateTime.Now;

				_runner.Run("Exporting ...", _ProcessProperty);

				_CreateReport();

				DateTime end_time = DateTime.Now;
				TimeSpan ofs = end_time - start_time;
				Log.Info("Processing took {0}", ofs);
			});
		}


		private void _Process(object obj)
		{
			if (obj == null)
				return;

			if (obj is P.Property)
			{
				_ProcessProperty(obj as P.Property);
				return;
			}

			if (obj is IDictionary)
			{
				IDictionaryEnumerator e = (obj as IDictionary).GetEnumerator();
				while (e.MoveNext())
					_Process(e.Value);
				return;
			}

			if (obj is ICollection)
			{
				IEnumerator e = (obj as ICollection).GetEnumerator();
				while (e.MoveNext())
					_Process(e.Current);
				return;
			}
		}

		private void _ProcessProperty(P.Property prop)
		{
			Dictionary<string, object> childs = prop.GetChilds();
			foreach (string name in childs.Keys)
			{
				object sub = childs[name];
				if (sub == null)
					continue;

				if (name == "ClassName")
				{
					_AddClassName(sub.ToString());
				}
				else if (sub is str)
				{
					string s = sub.ToString();
					if (s.StartsWith("/Game/FactoryGame/") && s.EndsWith("_C"))
						_AddClassName(s);
				}

				if (sub is P.Property)
					_ProcessProperty(sub as P.Property);
				else
					_Process(sub);
			}
		}


		private void _AddClassName(string classname)
		{
			if (!_classnames.Contains(classname))
				_classnames.Add(classname);
		}


		private void _CreateReport()
		{
			Log.Info("Creating report ...");

			_AddToReport("ClassName");
			_report_depth++;
			_classnames.Sort();
			foreach (string name in _classnames)
				_AddToReport(name);
			_report_depth--;
		}

		private void _AddToReport(string s)
		{
			_report_sb.Append('\t', _report_depth);
			_report_sb.AppendLine(s);
		}


		private HierarchyRunner _runner;
		private List<string>    _classnames;
		private StringBuilder   _report_sb;
		private int             _report_depth;

	}

}
