using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

using CoreLib;

using P = Savegame.Properties;

using SatisfactorySavegameTool.Dialogs;


namespace SatisfactorySavegameTool.Supplements
{

	/// <summary>
	/// For traversing savegame tree, triggering passed action on every property visited.
	/// 
	/// This uses an indeterminate progress bar as number of nodes being visited is somewhat unknown.
	/// Could add another loop for getting an accurate count first, but such would be a huge increase 
	/// in overall time, so skipped for now until there are ppl complaining 'bout :D
	/// </summary>
	public class HierarchyRunner
	{
		/// <summary>
		/// Runner constructor
		/// </summary>
		/// <param name="savegame">Savegame to run on</param>
		/// <param name="deep_run">Whether or not to visit child properties too</param>
		public HierarchyRunner(Savegame.Savegame savegame, bool deep_run)
		{
			_savegame = savegame;
			_deep_run = deep_run;
		}


		/// <summary>
		/// Action delegate used.
		/// Depending on whether  or not deep_run=true was used, you've to visit childs yourself.
		/// </summary>
		/// <param name="prop">Property visited</param>
		public delegate void Runner(P.Property prop);


		/// <summary>
		/// Traverses tree synchronously.
		/// Use this if you need to know end of operation (e.g. to unblock UI).
		/// </summary>
		/// <param name="title">Title for progress dialog</param>
		/// <param name="action">Action to trigger on nodes visited</param>
		public void Run(string title, Runner action)
		{
			_progress = new ProgressDialog(Application.Current.MainWindow, title);
			_progress.CounterFormat = Translate._("HierarchyRunner.Progress.CounterFormat");
			_progress.Interval = 1000;

			Task task = Task.Run(async() => await RunAsync(action, _progress.Events));
			while (!task.IsCompleted)
			{
				Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
				System.Threading.Thread.Yield();
				Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
			}

			_progress = null;
		}

		/// <summary>
		/// Traverse tree asynchronously.
		/// Used for more background-like operations where end of operation isn't important.
		/// </summary>
		/// <param name="action">Action to trigger on nodes visited</param>
		/// <param name="callback">Callback to trigger with each node visited (optional)</param>
		public async Task RunAsync(Runner action, ICallback callback = null)
		{
			// Actual action runner
			await Task.Run(() => {
				Log.Debug("Traversing tree ...");

				_callback = callback;
				if (_callback != null)
					_callback.Start(-1, Translate._("HierarchyRunner.Progress.Title"), "");

				_count = 0;

				if (!_deep_run)
				{
					if (_callback != null)
						_callback.Update(++_count, null, _savegame.Header.ToString());
					action(_savegame.Header);
				}
				else
					_TraverseProperty(_savegame.Header, action);

				foreach (P.Property prop in _savegame.Objects)
				{
					if (!_deep_run)
					{
						if (_callback != null)
							_callback.Update(++_count, null, prop.ToString());
						action(prop);
					}
					else
						_TraverseProperty(prop, action);
				}

				foreach (P.Property prop in _savegame.Collected)
				{
					if (!_deep_run)
					{
						if (_callback != null)
							_callback.Update(++_count, null, prop.ToString());
						action(prop);
					}
					else
						_TraverseProperty(prop, action);
				}

				if (_callback != null)
					_callback.Stop("", "");
				_callback = null;

				Log.Debug("... done traversing, visited a total of {0:#,#0} objects", _count);
			});
		}

		private void _TraverseProperty(P.Property prop, Runner action)
		{
			if (prop == null)
				return;

			if (_callback != null)
				_callback.Update(++_count, null, prop.ToString());

			action(prop);

			Dictionary<string, object> childs = prop.GetChilds();
			foreach (string name in childs.Keys)
			{
				object sub = childs[name];
				if (sub is P.Property)
					_TraverseProperty(sub as P.Property, action);
				else
					_TraverseObject(sub, action);
			}
		}

		private void _TraverseObject(object obj, Runner action)
		{
			if (obj == null)
				return;

			if (_callback != null)
				_callback.Update(++_count, null, obj.ToString());

			if (obj is IDictionary)
			{
				IDictionary coll = obj as IDictionary;
				foreach (object key in coll.Keys)
				{
					object sub = coll[key];
					if (sub is P.Property)
						_TraverseProperty(sub as P.Property, action);
					else
						_TraverseObject(sub, action);
				}
			}
			else if (obj is ICollection)
			{
				ICollection coll = obj as ICollection;
				foreach (object sub in coll)
				{
					if (sub is P.Property)
						_TraverseProperty(sub as P.Property, action);
					else
						_TraverseObject(sub, action);
				}
			}
//#if DEBUG
//			else
//			{
//				Type objtype = obj.GetType();
//				if (objtype.BaseType != typeof(ValueType) && objtype != typeof(str))
//					Log.Warning("Traversing: No handler for: {0} = {1}", objtype.Name, obj.ToString());
//			}
//#endif
		}


		private Savegame.Savegame _savegame;
		private bool              _deep_run;
		private ProgressDialog    _progress;
		private ICallback         _callback;
		private long              _count;

	}

}
