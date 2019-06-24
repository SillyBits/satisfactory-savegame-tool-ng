using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
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
	
	public class TreePanel : TabControl
	{
		public TreePanel()
			: base()
		{
			Func<string,string,BasicTree,TabItem> createTab = (title,icon,tree) => {
				TabItem tab = new TabItem();
				StackPanel sp = new StackPanel() {
					Orientation = Orientation.Horizontal,
				};
				sp.Children.Add(new Image() {
					Source = new BitmapImage(new Uri(Path.Combine(App.RESOURCEPATH, icon))),
					Width = 20,
					Height = 20,
				});
				sp.Children.Add(new Label() {
					Content = Translate._(title),
				});
				tab.Header = sp;
				tab.Content = tree;
				return tab;
			};

			//TODO: Suitable icon for 'simple'
			_treeSimple = new SimpleTree();
			_tabSimple = new TabItem() { Header = Translate._("TreePanel.Tab.Simple"), };
			_tabSimple.Content = _treeSimple;
			AddChild(_tabSimple);

			_treeClasses = new ClassesTree();
			_tabClasses = createTab("TreePanel.Tab.Classes", "Icon.TreePanel.Classes.png", _treeClasses);
			AddChild(_tabClasses);

			_treePaths = null;//new PathTree();
			_tabPaths = createTab("TreePanel.Tab.Paths", "Icon.TreePanel.Paths.png", _treePaths);
			AddChild(_tabPaths);
		}

		public void CreateTrees(Savegame.Savegame savegame, ICallback callback)
		{
			_treeSimple.CreateTree(savegame, callback);
			_treeClasses.CreateTree(savegame, callback);
			//_treePaths.CreateTree(savegame, callback);

			Dispatcher.Invoke(() => SelectedItem = _tabClasses);
		}

		public void ClearTrees()
		{
			_treeSimple.ClearTree();
			_treeClasses.ClearTree();
			//_treePaths.ClearTree();
		}


		internal TabItem _tabSimple;
		internal BasicTree _treeSimple;

		internal TabItem _tabClasses;
		internal BasicTree _treeClasses;

		internal TabItem _tabPaths;
		internal BasicTree _treePaths;

		protected override void OnSelectionChanged(SelectionChangedEventArgs e)
		{
			base.OnSelectionChanged(e);

			if (e.AddedItems.Count != 1)
				return;
			TabItem tab = e.AddedItems[0] as TabItem;
			if (tab == null)
				return;
			BasicTree tree = tab.Content as BasicTree;
			if (tree == null)
				return;
			RoutedPropertyChangedEventArgs<object> ev = 
				new RoutedPropertyChangedEventArgs<object>(null, null, 
					TreeView.SelectedItemChangedEvent);
			tree.RaiseEvent(ev);
		}
	}


	public abstract class BasicTree : TreeView
	{
		public BasicTree()
			: base()
		{
			SetValue(VirtualizingPanel.IsVirtualizingProperty, true);
			SetValue(VirtualizingPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);

			SelectedItemChanged += (Application.Current.MainWindow as SatisfactorySavegameTool.MainWindow).TreeView_SelectedItemChanged;

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
			//int extra = 150; // ClassesTree
			int extra = NoOfExtraElements;

			Dispatcher.Invoke(() => {
				Items.Clear();
				_callback.Start(savegame.TotalElements + extra, Translate._("MainWindow.LoadGamefile.Progress.Title.2"), "");
			});

			_count = 0;

			TreeViewItem root = _AddItem(null, System.IO.Path.GetFileName(savegame.Filename), null);
			Dispatcher.Invoke(() => {
				root.Tag = savegame.Header;
			});

			_CreateTree(savegame, root);

			Dispatcher.Invoke(() => {
				root.IsExpanded = true;
				root.IsSelected = true;
				_callback.Stop("", "");
			});
		}

		public void ClearTree()
		{
			Items.Clear();
		}


		internal abstract int NoOfExtraElements { get; }
		internal abstract void _CreateTree(Savegame.Savegame savegame, TreeViewItem root);


		internal ICallback _callback;
		internal int _count;

		internal TreeViewItem _AddItem(TreeViewItem parent, string label, Property prop = null)
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


		protected override void OnContextMenuOpening(ContextMenuEventArgs e)
		{
			TreeViewItem tvi = e.Source as TreeViewItem;
			if (tvi == null)
			{
				e.Handled = true;
				return;
			}
			if (!tvi.IsSelected)
				tvi.IsSelected = true;

			Property prop = tvi.Tag as Property;
			if (prop == null)
			{
				e.Handled = true;
				return;
			}

			base.OnContextMenuOpening(e);
		}

		private void Contextmenu_Inspect_Click(object sender, RoutedEventArgs e)
		{
			TreeViewItem tvi = SelectedItem as TreeViewItem;
			if (tvi == null)
				return;

			Property prop = tvi.Tag as Property;
			if (prop == null)
				return;

			StringBuilder sb = new StringBuilder();
			Dumper.WriteFunc writer = (s) => { sb.Append(s); };
			Dumper.Dump(prop, writer);

			ShowRawTextDialog.Show(Translate._("Dialog.PropertyDump.Title"), sb.ToString());
		}

	}


	public class SimpleTree : BasicTree
	{
		public SimpleTree()
			: base()
		{ }

		internal override int NoOfExtraElements { get { return 3; } }

		internal override void _CreateTree(Savegame.Savegame savegame, TreeViewItem root)
		{
			String label = string.Format(Translate._("TreePanel.Tree.Objects"), savegame.Objects.Count);
			TreeViewItem objects = _AddItem(root, label, null);
			foreach (Property prop in savegame.Objects)
				_AddItem(objects, prop.ToString(), prop);

			label = string.Format(Translate._("TreePanel.Tree.Collected"), savegame.Collected.Count);
			TreeViewItem collected = _AddItem(root, label, null);
			foreach (Property prop in savegame.Collected)
				_AddItem(collected, prop.ToString(), prop);

			//if self.__savegame.Missing:
			//	label = "Missing"
			//	self.__add(self.root, label, self.__savegame.Missing)
		}

	}


	public class ClassesTree : BasicTree
	{
		public ClassesTree()
			: base()
		{ }

		internal override int NoOfExtraElements { get { return 150; } }

		internal override void _CreateTree(Savegame.Savegame savegame, TreeViewItem root)
		{
			_classes = new Dictionary<string,TreeViewItem>();

			foreach (Property prop in savegame.Objects)
				_AddClassRecurs(root, "/", prop);

			//foreach (Property prop in savegame.Collected)
			//	AddClassRecurs(root, "/", (Savegame.Properties.Object) prop);

			//if self.__savegame.Missing:
			//	label = "Missing"
			//	self.__add(self.root, label, self.__savegame.Missing)
		}

		internal TreeViewItem _AddClassRecurs(TreeViewItem parent, string path, Savegame.Properties.Property prop)
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
				class_item = _AddOrGetClass(parent, fullname, classname);
				return _AddClassRecurs(class_item, fullname, prop);
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
					class_item = _AddOrGetClass(parent, fullname, classnames[0]);

					// Ignore [1] or add both?
					if (classnames[0] + "_C" != classnames[1])
					{
						fullname += classnames[1];
						class_item = _AddOrGetClass(class_item, fullname, classnames[1]);
					}
					//TODO: Add BP_... and FG...

					label = PathName;
					label = label.Substring(label.IndexOf('.') + 1);
					return _AddItem(class_item, label, prop);
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
			return _AddItem(parent, label, prop);
		}

		internal TreeViewItem _AddOrGetClass(TreeViewItem parent, string fullname, string classname)
		{
			if (_classes.ContainsKey(fullname))
				return _classes[fullname];
			TreeViewItem class_item = _AddItem(parent, classname);
			_classes.Add(fullname, class_item);
			return class_item;
		}

		internal Dictionary<string,TreeViewItem> _classes;

	}

}
