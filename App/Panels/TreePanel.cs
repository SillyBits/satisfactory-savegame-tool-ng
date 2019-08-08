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
using P = Savegame.Properties;

using SatisfactorySavegameTool.Dialogs;
using SatisfactorySavegameTool.Supplements;


namespace SatisfactorySavegameTool.Panels
{
	/*
	 * TODO:
	 * 
	 * - Add options to select which trees to build, and which one to show initially
	 * 
	 * - Add better tree style handling
	 * 
	 * - Building trees in background an option? (all besides the initial one)
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

			////TODO: Suitable icon for 'simple'
			//_treeSimple = new SimpleTree();
			//_tabSimple = new TabItem() { Header = Translate._("TreePanel.Tab.Simple"), };
			//_tabSimple.Content = _treeSimple;
			//AddChild(_tabSimple);

			_treeClasses = new ClassesTree();
			_tabClasses = createTab("TreePanel.Tab.Classes", "Icon.TreePanel.Classes.png", _treeClasses);
			AddChild(_tabClasses);

			//_treePaths = new PathTree();
			//_tabPaths = createTab("TreePanel.Tab.Paths", "Icon.TreePanel.Paths.png", _treePaths);
			//AddChild(_tabPaths);

			_treeLiving = new LivingTree();
			_tabLiving = createTab("TreePanel.Tab.Living", "Icon.TreePanel.Living.png", _treeLiving);
			AddChild(_tabLiving);
		}

		public void CreateTrees(ICallback callback)
		{
			//if (_treeSimple != null) _treeSimple.CreateTree(callback);
			if (_treeClasses != null) _treeClasses.CreateTree(callback);
			//if (_treePaths != null) _treePaths.CreateTree(callback);
			if (_treeLiving != null) _treeLiving.CreateTree(callback);

			Dispatcher.Invoke(() => SelectedItem = _tabClasses);
		}

		public void ClearTrees()
		{
			//if (_treeSimple != null) _treeSimple.ClearTree();
			if (_treeClasses != null) _treeClasses.ClearTree();
			//if (_treePaths != null) _treePaths.ClearTree();
			if (_treeLiving != null) _treeLiving.ClearTree();
		}


		//internal TabItem _tabSimple;
		//internal BasicTree _treeSimple;

		internal TabItem _tabClasses;
		internal BasicTree _treeClasses;

		//internal TabItem _tabPaths;
		//internal BasicTree _treePaths;

		internal TabItem _tabLiving;
		internal BasicTree _treeLiving;


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

			_CreateContextMenu();
		}


		public void CreateTree(ICallback callback)
		{
			_callback = callback;

			long extra = NoOfExtraElements;

			Dispatcher.Invoke(() => {
				Items.Clear();
				_callback.Start(MainWindow.CurrFile.TotalElements + extra, Translate._("MainWindow.LoadGamefile.Progress.Title.2"), "");
			});

			_count = 0;

			TreeViewItem root = _AddItem(null, System.IO.Path.GetFileName(MainWindow.CurrFile.Filename), null);
			Dispatcher.Invoke(() => {
				root.Tag = MainWindow.CurrFile.Header;
			});

			_CreateTree(root);

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
		internal abstract void _CreateTree(TreeViewItem root);


		internal ICallback _callback;
		internal long _count;

		internal TreeViewItem _AddItem(TreeViewItem parent, string label, object tag = null)
		{
			_count ++;
			return Dispatcher.Invoke(() => {
				TreeViewItem item = new TreeViewItem();
				item.Header = label;
				item.Tag = tag;
				if (parent != null)
					parent.Items.Add(item);
				else
					Items.Add(item);
				_callback.Update(_count, null, label);
				return item;
			});
		}


		protected virtual void _CreateContextMenu()
		{
			ContextMenu = new ContextMenu();

			MenuItem item = new MenuItem() {
				Header = Translate._("TreePanel.Context.Inspect"),
			};
			item.Click += Contextmenu_Inspect_Click;
			ContextMenu.Items.Add(item);
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

			if (tvi.Tag is P.Property) // || ...)
			{
			}
			else
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

			P.Property prop = tvi.Tag as P.Property;
			if (prop == null)
				return;

			StringBuilder sb = new StringBuilder();
			P.Dumper.WriteFunc writer = (s) => { sb.Append(s); };
			P.Dumper.Dump(prop, writer);

			ShowRawTextDialog.Show(Translate._("Dialog.PropertyDump.Title"), sb.ToString());
		}

	}


	public class SimpleTree : BasicTree
	{
		public SimpleTree()
			: base()
		{ }

		internal override int NoOfExtraElements { get { return 3; } }

		internal override void _CreateTree(TreeViewItem root)
		{
			String label = string.Format(Translate._("TreePanel.Tree.Objects"), MainWindow.CurrFile.Objects.Count);
			TreeViewItem objects = _AddItem(root, label, null);
			foreach (P.Property prop in MainWindow.CurrFile.Objects)
				_AddItem(objects, prop.ToString(), prop);

			label = string.Format(Translate._("TreePanel.Tree.Collected"), MainWindow.CurrFile.Collected.Count);
			TreeViewItem collected = _AddItem(root, label, null);
			foreach (P.Property prop in MainWindow.CurrFile.Collected)
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

		internal override int NoOfExtraElements
		{
			get
			{
				// A 25% as rough estimate should be ok
				return MainWindow.CurrFile.Objects.Count / 4;
			}
		}

		internal override void _CreateTree(TreeViewItem root)
		{
			_classes = new Dictionary<string,TreeViewItem>();

			foreach (P.Property prop in MainWindow.CurrFile.Objects)
				_AddClassRecurs(root, "/", prop);

			//foreach (Property prop in MainWindow.GetSavegame().Collected)
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
			if (prop is P.Actor)//.TypeName == "Actor")
			{
				P.Actor actor = (P.Actor) prop;
				ClassName = actor.ClassName.ToString();
				PathName = actor.PathName.ToString();
			}
			else if (prop is P.Object)//.TypeName == "Object")
			{
				Savegame.Properties.Object obj = (Savegame.Properties.Object) prop;
				ClassName = obj.ClassName.ToString();
				PathName = obj.PathName.ToString();
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

					return _AddItem(class_item, label, prop);
				}
				Log.Warning("AddClassRecurs: What to do with '{0}'?", ClassName);
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


	public class PathTree : BasicTree
	{
		public PathTree()
			: base()
		{ }

		internal override int NoOfExtraElements { get { return 150; } }

		internal override void _CreateTree(TreeViewItem root)
		{
			_paths = new Dictionary<string,TreeViewItem>();

			foreach (P.Property prop in MainWindow.CurrFile.Objects)
				_AddTreeRecurs(root, "", prop);

			foreach (P.Property prop in MainWindow.CurrFile.Collected)
				_AddTreeRecurs(root, "", prop);
		}

		internal TreeViewItem _AddTreeRecurs(TreeViewItem parent, string path, P.Property prop)
		{
			string pathname, fullname, label;
			TreeViewItem path_item;

			string PathName;
			if (prop is P.Actor)
			{
				P.Actor actor = (P.Actor) prop;
				PathName = actor.PathName.ToString();
			}
			else if (prop is P.Object)
			{
				Savegame.Properties.Object obj = (P.Object) prop;
				PathName = obj.PathName.ToString();
			}
			else if (prop is P.Collected)
			{
				P.Collected coll = (P.Collected) prop;
				PathName = coll.PathName.ToString();
			}
			else
				throw new Exception(string.Format("Can't handle {0}", prop));

			string remain = PathName.Substring(path.Length);
			if (remain.Contains(':'))
			{
				pathname = remain.Split(':')[0];
				fullname = path + pathname + ":";
				path_item = _AddOrGetPath(parent, fullname, pathname);
				return _AddTreeRecurs(path_item, fullname, prop);
			}
			if (remain.Contains('.'))
			{
				string[] pathnames = remain.Split('.');
				if (pathnames.Length == 2)
				{
					label = PathName;
					label = label.Substring(label.LastIndexOf('.') + 1);

					// Before adding more sub-classes, check for both BP_... and FG... condition
					if ("BP_" + label != pathnames[0] && "FG_" + label != pathnames[0])
					{
						fullname = path + pathnames[0] + ".";
						path_item = _AddOrGetPath(parent, fullname, pathnames[0]);
					}
					else
					{
						path_item = parent;
					}

					return _AddItem(path_item, label, prop);
				}
				else if (pathnames.Length == 3)
				{
					label = PathName;
					label = label.Substring(label.LastIndexOf('.') + 1);

					fullname = path + pathnames[0] + ".";
					path_item = _AddOrGetPath(parent, fullname, pathnames[0]);

					fullname = path + pathnames[1] + ".";
					path_item = _AddOrGetPath(parent, fullname, pathnames[1]);

					return _AddItem(path_item, label, prop);
				}
				Log.Warning("AddClassRecurs: What to do with '{0}'?", PathName);
			}

	
			// At the end of our path, now add property
			label = PathName;
			label = label.Substring(label.IndexOf('.') + 1);
			return _AddItem(parent, label, prop);
		}

		internal TreeViewItem _AddOrGetPath(TreeViewItem parent, string fullname, string pathname)
		{
			if (_paths.ContainsKey(fullname))
				return _paths[fullname];
			TreeViewItem path_item = _AddItem(parent, pathname);
			_paths.Add(fullname, path_item);
			return path_item;
		}

		internal Dictionary<string,TreeViewItem> _paths;

	}


	public class LivingTree : BasicTree
	{
		public LivingTree()
			: base()
		{ }

		internal override int NoOfExtraElements { get { return 0; } }

		internal override void _CreateTree(TreeViewItem root)
		{
			_classes = new Dictionary<string,TreeViewItem>();

			var living = MainWindow.CurrFile.Objects
				.Where(p => p is P.Actor)
				.Cast<P.Actor>()
				.Where(a => a.ClassName.ToString().StartsWith("/Game/FactoryGame/Character/"))
				;

			TreeViewItem players = _AddItem(root, "Players");
			var subset = living.Where(a => a.ClassName.ToString().StartsWith("/Game/FactoryGame/Character/Player/BP_PlayerState"));
			foreach (P.Actor prop in subset)
				_AddPlayer(players, prop);

			TreeViewItem enemies = _AddItem(root, "Enemies");
			subset = living.Where(a => a.ClassName.ToString().StartsWith("/Game/FactoryGame/Character/Creature/Enemy"));
			foreach (P.Actor prop in subset)
				_AddEnemy(enemies, prop);

			TreeViewItem wildlife = _AddItem(root, "Wildlife");
			subset = living.Where(a => a.ClassName.ToString().StartsWith("/Game/FactoryGame/Character/Creature/Wildlife"));
			foreach (P.Actor prop in subset)
				_AddWildlife(wildlife, prop);

			Dispatcher.Invoke(() => {
				players.IsExpanded = true;
				enemies.IsExpanded = true;
				wildlife.IsExpanded = true;
			});
		}

		internal void _AddPlayer(TreeViewItem parent, P.Actor blueprint)
		{
			P.NamedEntity named = blueprint.EntityObj as P.NamedEntity;
			P.ObjectProperty player_obj = named.Value.Named("mOwnedPawn") as P.ObjectProperty;
			if (player_obj == null)
			{
				string pl = blueprint.PathName.LastName();
				string short_title = string.Format("Player #{0} [INVALID]", pl.Split('_').Last());
				TreeViewItem p = _AddItem(parent, short_title, null);
				Dispatcher.Invoke(() => p.IsEnabled = false);
			}
			else
			{
				string pathname = player_obj.PathName.ToString();
				string name = pathname.LastName();

				string short_title = string.Format("Player #{0}", name.Split('_').Last());
				string title = short_title + string.Format(" ({0})", name);
				P.Actor player = MainWindow.CurrFile.Objects.FindByPathName(pathname) as P.Actor;

				if (player == null)
				{
					short_title += " [NO ACTOR]";
					TreeViewItem p = _AddItem(parent, short_title, null);
					Dispatcher.Invoke(() => p.IsEnabled = false);
				}
				else
				{
					Living living = new Living(title, player, blueprint);
					_AddItem(parent, short_title, living);
				}
			}
		}

		internal void _AddEnemy(TreeViewItem parent, P.Actor entity)
		{
			// Add grouping
			//From: /Game/FactoryGame/Character/Creature/Enemy/Crab/BabyCrab/Char_BabyCrab.Char_BabyCrab_C
			//  To:                                            Crab/BabyCrab/Char_BabyCrab.Char_BabyCrab_C
			string[] groups = entity
				.ClassName.ToString()
				.Replace("/Game/FactoryGame/Character/Creature/Enemy/", "")
				.Split('/');
			TreeViewItem group = _AddOrGetClass(parent, groups[0]);
			if (groups.Length == 3)
				group = _AddOrGetClass(group, groups[1]);

			string name = entity.PathName.LastName();
			string classname = entity.ClassName.LastName();
			if (Translate.Has(classname))
				classname = Translate._(classname);
			string short_title = string.Format("{0} #{1}", classname, name.Split('_').Last());
			string title = short_title + string.Format(" ({0})", name);

			Living living = new Living(title, entity, null);
			_AddItem(group, short_title, living);
		}

		internal void _AddWildlife(TreeViewItem parent, P.Actor entity)
		{
			// Add grouping
			//From: /Game/FactoryGame/Character/Creature/Wildlife/SpaceRabbit/Char_SpaceRabbit.Char_SpaceRabbit_C
			//  To:                                               SpaceRabbit/Char_SpaceRabbit.Char_SpaceRabbit_C
			string group_name = entity
				.ClassName.ToString()
				.Replace("/Game/FactoryGame/Character/Creature/Wildlife/", "")
				.Split('/').First();
			TreeViewItem group = _AddOrGetClass(parent, group_name);

			string name = entity.PathName.LastName();
			string classname = entity.ClassName.LastName();
			if (Translate.Has(classname))
				classname = Translate._(classname);
			string short_title = string.Format("{0} #{1}", classname, name.Split('_').Last());
			string title = short_title + string.Format(" ({0})", name);

			Living living = new Living(title, entity, null);
			_AddItem(group, short_title, living);
		}

		internal TreeViewItem _AddOrGetClass(TreeViewItem parent, string name)
		{
			if (!_classes.ContainsKey(name))
			{
				TreeViewItem class_item = _AddItem(parent, name);
				Dispatcher.Invoke(() => class_item.IsExpanded = true);
				_classes.Add(name, class_item);
			}
			return _classes[name];
		}

		protected override void _CreateContextMenu()
		{
			/* NO context menu for now
			ContextMenu = new ContextMenu();

			MenuItem item = new MenuItem() {
				Header = Translate._("TreePanel.Context.Inspect"),
			};
			item.Click += Contextmenu_Inspect_Click;
			ContextMenu.Items.Add(item);
			*/
		}


		internal Dictionary<string,TreeViewItem> _classes;


		internal class Living
		{
			internal string  Title;
			internal P.Actor Entity;
			internal P.Actor Blueprint;

			internal bool IsPlayer { get { return Blueprint != null; } }

			internal Living(string title, P.Actor entity, P.Actor blueprint)
			{
				Title = title;
				Entity = entity;
				Blueprint = blueprint;
			}
		}

	}

}
