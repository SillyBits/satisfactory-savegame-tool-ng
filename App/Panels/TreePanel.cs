using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

using CoreLib;
using Savegame.Properties;

using SatisfactorySavegameTool.Dialogs;

namespace SatisfactorySavegameTool.Panels
{
	/*
	 * TODO:
	 * 
	 * - Add better tree style handling
	 * 
	 */

	public class TreePanel : TreeView
	{
		public TreePanel()
			: base()
		{
			ContextMenu = new ContextMenu();

			MenuItem item = new MenuItem() {
				Header = Translate._("TreePanel.Context.Inspect"),
			};
			item.Click += Contextmenu_Inspect_Click;
			ContextMenu.Items.Add(item);
		}

		public void CreateTree(Savegame.Savegame savegame, ICallback callback)
		{
			_callback = callback;

			//int extra = 3; // SimpleTree
			int extra = 150; // ClassesTree

			Dispatcher.Invoke(() => {
				Items.Clear();
				_callback.Start(savegame.TotalElements + extra, Translate._("MainWindow.LoadGamefile.Progress.Title.2"), "");
			});

			_count = 0;

			TreeViewItem root = AddItem(null, System.IO.Path.GetFileName(savegame.Filename), null);
			Dispatcher.Invoke(() => {
				root.Tag = savegame.Header;
			});

			//CreateSimpleTree(savegame, root);
			CreateClassesTree(savegame, root);

			Dispatcher.Invoke(() => {
				root.IsExpanded = true;
				root.IsSelected = true;
				_callback.Stop("", "");
			});
		}


		private void CreateSimpleTree(Savegame.Savegame savegame, TreeViewItem root)
		{
			TreeViewItem objects = AddItem(root, string.Format("Objects ({0} elements)", savegame.Objects.Count), null);
			foreach (Property prop in savegame.Objects)
				AddItem(objects, prop.ToString(), prop);

			TreeViewItem collected = AddItem(root, string.Format("Collected ({0} elements)", savegame.Collected.Count), null);
			foreach (Property prop in savegame.Collected)
				AddItem(collected, prop.ToString(), prop);

			//if self.__savegame.Missing:
			//	label = "Missing"
			//	self.__add(self.root, label, self.__savegame.Missing)
		}

		private void CreateClassesTree(Savegame.Savegame savegame, TreeViewItem root)
		{
			_classes = new Dictionary<string,TreeViewItem>();

			foreach (Property prop in savegame.Objects)
				AddClassRecurs(root, "/", prop);

			//foreach (Property prop in savegame.Collected)
			//	AddClassRecurs(root, "/", (Savegame.Properties.Object) prop);

			//if self.__savegame.Missing:
			//	label = "Missing"
			//	self.__add(self.root, label, self.__savegame.Missing)
		}

		private TreeViewItem AddClassRecurs(TreeViewItem parent, string path, Savegame.Properties.Property prop)
		{
			string classname, fullname, label;
			TreeViewItem class_item;

			string ClassName, PathName;
			if (prop.TypeName == "Object")
			{
				Savegame.Properties.Object obj = (Savegame.Properties.Object) prop;
				ClassName = obj.ClassName.ToString();
				PathName = obj.PathName.ToString();
			}
			else if (prop.TypeName == "Actor")
			{
				Actor actor = (Actor) prop;
				ClassName = actor.ClassName.ToString();
				PathName = actor.PathName.ToString();
			}
			else
				throw new Exception(string.Format("Can't handle {0}", prop));

			string remain = ClassName.Substring(path.Length);
			if (remain.Contains('/'))
			{
				classname = remain.Split('/')[0];
				fullname = path + classname + "/";
				//if not fullname in self.__classes:
				//	class_item = self.__add(parent_item, classname)
				//	self.__classes[fullname] = class_item
				//else:
				//	class_item = self.__classes[fullname]
				class_item = AddOrGetClass(parent, fullname, classname);
				return AddClassRecurs(class_item, fullname, prop);
			}
			if (remain.Contains('.'))
			{
				string[] classnames = remain.Split('.');
				if (classnames.Length == 2)
				{
					/*
					if (classnames[0] + "_C" == classnames[1])
					{
						// Ignore [1]
						//return self.__add(parent_item, classnames[0], prop)
						fullname = path + classnames[0] + ".";
						classname = classnames[0];
						class_item = AddOrGetClass(parent, fullname, classnames[0]);
					}
					else
					{
						// Add both?
						fullname = path + classnames[0] + ".";
						class_item = AddOrGetClass(parent, fullname, classnames[0]);
					
						fullname += classnames[1];
						class_item = AddOrGetClass(class_item, fullname, classnames[1]);
					}
					*/
					fullname = path + classnames[0] + ".";
					class_item = AddOrGetClass(parent, fullname, classnames[0]);

					// Ignore [1] or add both?
					if (classnames[0] + "_C" != classnames[1])
					{
						fullname += classnames[1];
						class_item = AddOrGetClass(class_item, fullname, classnames[1]);
					}

					label = PathName;
					label = label.Substring(label.IndexOf('.') + 1);
					return AddItem(class_item, label, prop);
				}
				Log.Warning("AddClassRecurd: What to do with '{0}'?", ClassName);
			/*
				fullname = parent_class + classname + "."
				if not fullname in self.__classes:
					class_item = self.__add(parent_item, classname)
					self.__classes[fullname] = class_item
				else:
					class_item = self.__classes[fullname]
				return self.__add_class_recurs(class_item, fullname, prop)
			*/
			}

			/*
			if prop.ClassName.startswith("/Script/") and remain:
				fullname = prop.ClassName
				if not fullname in self.__classes:
					class_item = self.__add(parent_item, remain)
					self.__classes[fullname] = class_item
				else:
					class_item = self.__classes[fullname]
				parent_item = class_item
			*/
	
			// At the end of our path, now add property
			//return self.__add(parent_item, remain, prop)
			//label = prop.PathName.split(".")[1:]
			label = PathName;
			label = label.Substring(label.IndexOf('.') + 1);
			return AddItem(parent, label, prop);
		}

		private TreeViewItem AddOrGetClass(TreeViewItem parent, string fullname, string classname)
		{
			if (_classes.ContainsKey(fullname))
				return _classes[fullname];
			TreeViewItem class_item = AddItem(parent, classname);
			_classes.Add(fullname, class_item);
			return class_item;
		}

		private Dictionary<string,TreeViewItem> _classes;


		private ICallback _callback;
		private int _count;

		private TreeViewItem AddItem(TreeViewItem parent, string label, Property prop = null)
		{
			_count ++;
			return Dispatcher.Invoke(() => {
				TreeViewItem item = new TreeViewItem();
				item.Header = label;
				item.Tag = prop;
				if (parent != null)
					parent.Items.Add(item);
				else
					Items.Add(item);
				_callback.Update(_count, null, label);
				return item;
			});
		}


		private void Contextmenu_Inspect_Click(object sender, RoutedEventArgs e)
		{
			//throw new NotImplementedException();
			TreeViewItem tvi = SelectedItem as TreeViewItem;
			if (tvi != null)
			{
				Property prop = tvi.Tag as Property;
				if (prop != null)
				{
					var dlg = new PropertyDumpDialog(Application.Current.MainWindow, "", prop);
					dlg.ShowDialog();
				}
			}
		}

	}
}
