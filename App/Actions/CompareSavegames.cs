using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

using CoreLib;

using P = Savegame.Properties;

using SatisfactorySavegameTool.Supplements;
using SatisfactorySavegameTool.Dialogs;
using A = SatisfactorySavegameTool.Actions.Attributes;

using ICSharpCode.TreeView;


namespace SatisfactorySavegameTool.Actions.Compare
{

	// Compare two saves, highlighting differences in a TBD manner
	//
	[A.Name("[Action.Compare.Name]"), A.Description("[Action.Compare.Description]")/*, A.Icon("?")*/]
	public class CompareSavegames : IAction
	{
		public CompareSavegames(Savegame.Savegame savegame)
		{
			_left_save = savegame;

			//_InitComparators();
		}

		public void Run()
		{
#if !DEVENV
			OpenFileDialog dlg = new OpenFileDialog();
			dlg.Title = Translate._("Action.Compare.Load2ndGamefile.Title");//"Select savegame to load";
			dlg.InitialDirectory = Config.Root.core.defaultpath;
			dlg.Filter = Translate._("Action.Compare.Load2ndGamefile.Filter");//"Savegames (*.sav)|*.sav|All files (*.*)|*.*";
			if (dlg.ShowDialog().GetValueOrDefault(false) == false || !File.Exists(dlg.FileName))
				return;
			string filename_2nd = dlg.FileName;
#else
			string filename_2nd = @"C:\Users\SillyBits\AppData\Local\FactoryGame\Saved\SaveGames\common\NF-Start-100979.sav";
			//string filename_2nd = @"C:\Users\SillyBits\AppData\Local\FactoryGame\Saved\SaveGames\common\First trial ... Paused for now.sav";
			//string filename_2nd = @"D:\src\tools\satisfactory-savegame-tool-ng-20190823\__internal\NF-Start-100979.sav";
#endif

			_progress = new ProgressDialog(null, Translate._("Action.Compare.Progress.Title"));
			_callback = _progress.Events;

			var task = Task.Run(async() => await RunAsync(filename_2nd));
			while (!task.IsCompleted)
			{
				Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
				System.Threading.Thread.Yield();
				Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
			}
			bool? outcome = task.Result;

			_callback.Stop("", "");
			_callback = null;
			_progress = null;

			if (outcome == null)
			{
				MessageBox.Show(Application.Current.MainWindow, 
					Translate._("Action.Compare.ErrorLoading"), 
					Translate._("Action.Compare.Name"));
			}
			else if (outcome.Value == true)
			{
				MessageBox.Show(Application.Current.MainWindow, 
					Translate._("Action.Compare.NoDifferences"), 
					Translate._("Action.Compare.Name"));
			}
			else
			{
				MessageBoxResult res = MessageBox.Show(Application.Current.MainWindow, 
					Translate._("Action.Compare.HasDifferences"), 
					Translate._("Action.Compare.Name"), 
					MessageBoxButton.YesNo, MessageBoxImage.Information);
				if (res == MessageBoxResult.Yes)
					DifferencesDialog.Show(_differences);
			}
		}

		public async Task<bool?> RunAsync(string filename_2nd)
		{
			return await Task.Run(() => {
				//_progress.CounterFormat = Translate._("Action.Validate.Compare.CounterFormat");
				_progress.Interval = 1024*1024;//1024 * 128;
				//var load_task = Task.Run(async() => await _LoadSave(filename_2nd));
				//load_task.WaitWithDispatch(Application.Current.Dispatcher);
				//_right_save = load_task.Result;
				_right_save = _LoadSave(filename_2nd);
				if (_right_save == null)
					return (bool?)null;

				_differences = new DifferenceModel(_left_save, _right_save);

				_progress.CounterFormat = Translate._("Action.Compare.Progress.CounterFormat");
				_progress.Interval = 1000;// -1;
				//var task = Task.Run(async() => await RunAsync());
				//task.Wait();//WithDispatch(Application.Current.Dispatcher);
				//bool outcome = task.Result;
				Log.Info("Starting up comparison ...");
				DateTime start_time = DateTime.Now;

				bool outcome = _CompareAll();

				DateTime end_time = DateTime.Now;
				TimeSpan ofs = end_time - start_time;
				Log.Info("Comparison took {0}", ofs);

				return outcome;
			});
		}


		internal Savegame.Savegame _LoadSave(string filename)
		{
			Log.Info("Loading 2nd savegame ...");
			DateTime start_time = DateTime.Now;

			Savegame.Savegame savegame = new Savegame.Savegame(filename);
			try
			{
				savegame.Load(_callback);

				DateTime end_time = DateTime.Now;
				TimeSpan ofs = end_time - start_time;
				Log.Info("Loading took {0}", ofs);
			}
			catch (Exception exc)
			{
				savegame = null;
				Log.Error("Loading 2nd save failed", exc);
			}

			return savegame;
		}


		internal bool _CompareAll()
		{
			int total = _left_save.TotalElements + _right_save.TotalElements;
			Log.Info("Comparing a ~{0} elements ...", total);
			_cbStart(total, Translate._("Action.Compare.Progress"), "");

			DifferenceNode node;
			DifferenceNode sub;
			List<DifferenceNode> nodes = new List<DifferenceNode>();
			int differences = 0;


			node = _Compare(_left_save.Header, _right_save.Header);
			if (node != null)
			{
				node.SetTitle("Header");
				_differences.Add(node);
				++differences;
			}


			List<P.Property> left_objects  = new List<P.Property>(_left_save.Objects);
			List<P.Property> right_objects = new List<P.Property>(_right_save.Objects);
			while (left_objects.Count > 0)
			{
				P.Property left = left_objects[0];
				left_objects.RemoveAt(0);

				_cbUpdate(null, left.ToString());

				P.Property right = _FindMatch(left, right_objects);
				if (right == null)
					sub = new DifferenceNode(left.ToString(), left, null);
				else
				{
					right_objects.Remove(right);
					sub = _Compare(left, right);
				}
				if (sub != null)
				{
					sub.SetTitle(left.ToString());
					nodes.Add(sub);
				}
			}
			foreach (P.Property right in right_objects)
				nodes.Add(new DifferenceNode(right.ToString(), null, right));
			if (nodes.Count > 0)
			{
				differences += nodes.Count;
				foreach (DifferenceNode child in nodes)
					_differences.Add(child);
				nodes.Clear();
			}


			DifferenceNode collected = new DifferenceNode("Collected", "", "");
			List<P.Property> left_coll  = new List<P.Property>(_left_save.Collected);
			List<P.Property> right_coll = new List<P.Property>(_right_save.Collected);
			while (left_coll.Count > 0)
			{
				P.Property left = left_coll[0];
				left_coll.RemoveAt(0);

				_cbUpdate(null, left.ToString());

				P.Property right = _FindMatch(left, right_coll);
				if (right == null)
					sub = new DifferenceNode(left.ToString(), left, null);
				else
				{
					right_coll.Remove(right);
					sub = _Compare(left, right);
				}
				if (sub != null)
				{
					sub.SetTitle(left.ToString());
					collected.Add(sub);
				}
			}
			foreach (P.Property right in right_coll)
				collected.Add(right.ToString(), null, right);
			if (collected.ChildCount > 0)
			{
				differences += collected.ChildCount;
				foreach (DifferenceNode child in collected.Children)
					_differences.Add(child);
			}


			//if (_left_save.Missing != null && _left_save.Missing.Length > 0)
			//{
			//	//...
			//}


			_cbStop(Translate._("Action.Done"), "");
			Log.Info("... done comparing, differences={0}", differences);

			return (differences == 0);
		}

		internal P.Property _FindMatch(P.Property left, List<P.Property> right_coll)
		{
			string left_pathname = left.GetPathName();
			if (string.IsNullOrEmpty(left_pathname))
				return null;

			foreach (P.Property right in right_coll)
			{
				string right_pathname = right.GetPathName();
				if (left_pathname.Equals(right_pathname))
					return right;
			}

			return null;
		}

		internal DifferenceNode _Compare(P.Property left, P.Property right)
		{
			DifferenceNode node = new DifferenceNode(left.ToString(), left, right);
			DifferenceNode sub;

			if ((left == null || right == null) && (left != right))
				return node;

			Dictionary<string, object> left_childs  = new Dictionary<string, object>(left.GetChilds());
			Dictionary<string, object> right_childs = new Dictionary<string, object>(right.GetChilds());

			while (left_childs.Count > 0)
			{
				var pair = left_childs.First();

				object left_sub = pair.Value;
				left_childs.Remove(pair.Key);

				object right_sub = null;
				if (right_childs.ContainsKey(pair.Key))
				{
					right_sub = right_childs[pair.Key];
					right_childs.Remove(pair.Key);
				}

				if (_blacklisted.Contains(pair.Key))
					continue;

				if (right_sub == null && left_sub != null)
					sub = new DifferenceNode(null, left_sub, null);
				else if (left_sub is P.Property)
					sub = _Compare(left_sub as P.Property, right_sub as P.Property);
				else 
					sub = _CompareObject(left_sub, right_sub);
				if (sub != null)
				{
					sub.SetTitle(pair.Key);
					node.Add(sub);
				}
			}

			foreach (var pair in right_childs)
				if (!_blacklisted.Contains(pair.Key))
					node.Add(pair.Key, null, pair.Value);

			return (node.ChildCount > 0) ? node : null;
		}


		internal void _cbStart(long total, string status, string info)
		{
			_count = 0;
			if (_callback != null) 
				_callback.Start(total, status, info);
		}

		internal void _cbUpdate(string status, string info)
		{
			_count++;
			if (_callback != null) 
				_callback.Update(_count, status, info);
		}

		internal void _cbStop(string status, string info)
		{
			if (_callback != null) 
				_callback.Stop(status, info);
		}


		internal Savegame.Savegame _left_save, _right_save;
		internal ProgressDialog _progress;
		internal ICallback _callback;
		internal long _count;
		internal DifferenceModel _differences;
		private static string[] _blacklisted = { "Private" };


		#region Comparison helpers

		internal DifferenceNode _CompareObject(object left, object right)
		{
			Func<bool,DifferenceNode> state = (b) => {
				return b ? null : new DifferenceNode(null, left, right);
			};

			if (left == null || right == null)
				return state(left == right);
			if (!left.GetType().Equals(right.GetType()))
				return state(false);

			if (left is byte)
				return state(((int)(byte)left - (int)(byte)right) == 0);
			if (left is int)
				return state(((int)left - (int)right) == 0);
			if (left is long)
				return state(((long)left - (long)right) == 0);
			if (left is float)
				return state(((float)left - (float)right).IsNearZero(1e-10f));
			//if (left is double)
			//	return state(((double)left - (double)right).IsNearZero(1e-10));

			if (left is IDictionary)
			{
				DifferenceNode node = new DifferenceNode(null, left, right);
				DifferenceNode sub;

				Func<IDictionary,IDictionary> clone = (src) => {
					Dictionary<object,object> dst = new Dictionary<object, object>();
					foreach (object key in src.Keys)
						dst.Add(key, src[key]);
					return dst;
				};
				IDictionary left_coll  = clone(left as IDictionary);
				IDictionary right_coll = clone(right as IDictionary);
				
				foreach (object key in left_coll.Keys)
				{
					object left_sub  = left_coll[key];
					object right_sub = null;
					if (right_coll.Contains(key))
					{
						right_sub = right_coll[key];
						right_coll.Remove(key);
					}

					if (right_sub == null && left_sub != null)
						sub = new DifferenceNode(null, left_sub, null);
					else if (left_sub is P.Property)
						sub = _Compare(left_sub as P.Property, right_sub as P.Property);
					else
						sub = _CompareObject(left_sub, right_sub);
					if (sub != null)
					{
						sub.SetTitle(key.ToString());
						node.Add(sub);
					}
				}
				foreach (object key in right_coll)
					node.Add(key.ToString(), null, right_coll[key]);

				return node.ChildCount > 0 ? node : null;
			}

			if (left is ICollection)
			{
				DifferenceNode node = new DifferenceNode(null, left, right);
				DifferenceNode sub;

				Func<ICollection,List<object>> clone = (src) => {
					List<object> dst = new List<object>();
					foreach (object val in src)
						dst.Add(val);
					return dst;
				};
				List<object> left_coll  = clone(left as ICollection);
				List<object> right_coll = clone(right as ICollection);

				for (int index = 0; index < left_coll.Count; ++index)
				{
					object left_sub  = left_coll[index];
					object right_sub = null;
					if (index < right_coll.Count)
					{
						right_sub = right_coll[index];
						right_coll[index] = null;
					}

					if (right_sub == null && left_sub != null)
						sub = new DifferenceNode(null, left_sub, null);
					else if (left_sub is P.Property)
						sub = _Compare(left_sub as P.Property, right_sub as P.Property);
					else
						sub = _CompareObject(left_sub, right_sub);
					if (sub != null)
					{
						sub.SetTitle(index.ToString());
						node.Add(sub);
					}
				}
				for (int index = 0; index < right_coll.Count; ++index)
				{
					object right_sub = right_coll[index];
					if (right_sub != null)
						node.Add(index.ToString(), null, right_sub);
				}

				return node.ChildCount > 0 ? node : null;
			}

			return null;
		}

		#endregion

	}


	public class DifferenceModel
	{
		public DifferenceNode    Root          { get; set; }
		public Savegame.Savegame LeftSavegame  { get; private set; }
		public Savegame.Savegame RightSavegame { get; private set; }

		public DifferenceModel(Savegame.Savegame left, Savegame.Savegame right)
		{
			Root          = new DifferenceNode(@"\", "", "");
			LeftSavegame  = left;
			RightSavegame = right;

			_classes      = new Dictionary<string,DifferenceNode>();
		}

		public void Add(DifferenceNode node)
		{
			P.Property prop = ((node.Left != null) ? node.Left : node.Right) as P.Property;
			if (prop != null)
			{
				DifferenceNode class_node = _AddClassRecurs(Root, "/", prop);
				if (class_node != null)
				{
					class_node.Add(node);
					return;
				}
			}

			Root.Add(node);
		}

		//HINT: Methods following are copied from TreePanel.cs, ensure to update those if TreePanel.cs changes!
		private DifferenceNode _AddClassRecurs(DifferenceNode parent, string path, P.Property prop)
		{
			string classname, fullname, label;
			DifferenceNode class_item;

			string ClassName, PathName;
			if (prop is P.Actor)
			{
				P.Actor actor = prop as P.Actor;
				ClassName = actor.ClassName.ToString();
				PathName = actor.PathName.ToString();
			}
			else if (prop is P.Object)
			{
				P.Object obj = prop as P.Object;
				ClassName = obj.ClassName.ToString();
				PathName = obj.PathName.ToString();
			}
			else
			{
				return null;
			}

			string remain = ClassName.Substring(path.Length);
			if (remain.Contains('/'))
			{
				classname = remain.Split('/')[0];
				fullname = path + classname + "/";
				class_item = _AddOrGetClass(parent, fullname, classname);
				return _AddClassRecurs(class_item, fullname, prop);
			}
			if (remain.Contains('.'))
			{
				string[] classnames = remain.Split('.');
				if (classnames.Length == 2)
				{
					label = PathName;
					label = label.Substring(label.LastIndexOf('.') + 1);

					// Before adding more sub-classes, check for both BP_... and FG... condition
					if ("BP_" + label != classnames[0] && "FG_" + label != classnames[0])
					{
						fullname = path + classnames[0] + ".";
						class_item = _AddOrGetClass(parent, fullname, classnames[0]);

						// Ignore [1] or add both?
						if (classnames[0] + "_C" != classnames[1])
						{
							fullname += classnames[1];
							class_item = _AddOrGetClass(class_item, fullname, classnames[1]);
						}

						// To collect things following into a sub node ('BP_PlayerState_C_0' with data below):
						//		.PathName = str:'Persistent_Level:PersistentLevel.BP_PlayerState_C_0.FGRecipeShortcut_#'
						// with # = [0,9]
						// Or following ('Char_Player_C_0' with data below):
						//		.PathName = str:'Persistent_Level:PersistentLevel.Char_Player_C_0.BackSlot'
						//		.PathName = str:'Persistent_Level:PersistentLevel.Char_Player_C_0.ArmSlot'
						// Will also take care of showing actual entity in case we're showing
						// something like an inventory:
						//		.PathName = str:'Persistent_Level:PersistentLevel.Char_Player_C_0.inventory'
						string[] labels = PathName.Split('.');
						if (labels.Length == 3)
						{
							fullname += "." + labels[1];
							class_item = _AddOrGetClass(class_item, fullname, labels[1]);
						}
					}
					else
					{
						class_item = parent;
					}

					//return _AddItem(class_item, label, null, null);
					return class_item;
				}
				Log.Warning("AddClassRecurs: What to do with '{0}'?", ClassName);
			}

			// At the end of our path, add property
			//label = PathName;
			//label = label.Substring(label.IndexOf('.') + 1);
			//return _AddItem(parent, label, null, null);
			return parent;
		}

		private DifferenceNode _AddOrGetClass(DifferenceNode parent, string fullname, string classname)
		{
			if (_classes.ContainsKey(fullname))
				return _classes[fullname];
			DifferenceNode class_item;
			if (parent == null)
				class_item = Root.Add(classname, "", "");
			else
				class_item = parent.Add(classname, "", "");
			_classes.Add(fullname, class_item);
			return class_item;
		}

		private Dictionary<string,DifferenceNode> _classes;
	}

	public class DifferenceNode : SharpTreeNode
	{
		public override object Text
		{
			get
			{
				string t = _title;
				if (t != null && Children.Count > 0)
					t += string.Format(" [{0}]", Children.Count);
				return t;
			}
		}

		public override bool CanCopy(SharpTreeNode[] nodes) { return false; }
		public override bool CanPaste(IDataObject data) { return false; }
		public override bool CanDelete(SharpTreeNode[] nodes) { return false; }
		public override bool IsCheckable { get { return false; } }

		public object        Left { get; private set; }
		public object        Right { get; private set; }
		public int           ChildCount { get { return (Children != null) ? Children.Count : 0; } }


		public DifferenceNode(string title, object left, object right)
			: base()
		{
			_title    = title;
			Left      = left;
			Right     = right;

			LazyLoading = false;
			IsHidden = false;
		}

		public void SetTitle(string title)
		{
			_title = title;
		}

		public DifferenceNode Add(string title, object left, object right)
		{
			return Add(new DifferenceNode(title, left, right));
		}

		public DifferenceNode Add(DifferenceNode node)
		{
			Children.Add(node);
			return node;
		}


		private string _title;
	}


}
