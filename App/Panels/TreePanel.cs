using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
	 */
	
	public class TreePanel : TabControl
	{
		public TreePanel()
			: base()
		{
			// Create initial trees config if missing
			if (!Config.Root.HasSection("trees"))
			{
				Section trees = Config.Root.AddSection("trees");
				trees.AddItem("simple"   , false);
				trees.AddItem("classes"  , true);
				trees.AddItem("paths"    , false);
				trees.AddItem("living"   , true);
				trees.AddItem("buildings", true);

				dynamic order = trees.AddListItem("order");
				order.Add("simple");
				order.Add("classes");
				order.Add("paths");
				order.Add("living");
				order.Add("buildings");
			}

			_Tabs = new List<TabItem>();
			foreach(string tree in Config.Root.trees.order)
			{
				Item config_item = Config.Root.trees.Items[tree];
				if ((bool)config_item.Value)
				{
					string name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(tree);
					string icon = "Icon.TreePanel." + name + ".png";
					string title = "TreePanel.Tab." + name;

					Type type = Type.GetType("SatisfactorySavegameTool.Panels." + name + "Tree");
					if (type == null)
					{
						Log.Error("Unknown tree '{0}' in config file!", name);
						continue;
					}
					BasicTree instance = Activator.CreateInstance(type) as BasicTree;
					if (instance == null)
					{
						Log.Error("Unable to create instance of tree '{0}'!", name);
						continue;
					}

					StackPanel sp = new StackPanel() {
						Orientation = Orientation.Horizontal,
					};
					sp.Children.Add(new Image() {
						Source = new BitmapImage(Helpers.GetResourceUri(icon)),
						Width = 20,
						Height = 20,
					});
					sp.Children.Add(new Label() {
						Content = Translate._(title),
					});

					TabItem tab = new TabItem() {
						Header = sp,
						Content = instance,
					};
					_Tabs.Add(tab);
					AddChild(tab);
				}
			}
		}

		public void CreateTrees(ICallback callback)
		{
			foreach (TabItem tab in _Tabs)
			{
				BasicTree tree = Dispatcher.Invoke(() => { return tab.Content as BasicTree; });
				tree.CreateTree(callback);
			}

			if (_Tabs.Count > 0)
				Dispatcher.Invoke(() => SelectedItem = _Tabs.First().Content);
		}

		public void ClearTrees()
		{
			foreach (TabItem tab in _Tabs)
				(tab.Content as BasicTree).ClearTree();
		}


		private List<TabItem> _Tabs;

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


	public class TreeModel
	{
		public TreeNodes Nodes { get; private set; }

		public TreeModel()
		{
			Nodes = new TreeNodes();
		}
	}

	public class TreeNodes : ObservableCollection<TreeNode>
	{
		public TreeNode Add(string title, object tag)
		{
			TreeNode node = new TreeNode(title, tag);
			Add(node);
			return node;
		}
	}

	public class TreeNode : INotifyPropertyChanged
	{
		public string    Title      { get; private set; }
		public object    Tag        { get; private set; }
		public TreeNodes Childs     { get; private set; }

		public bool IsEnabled
		{
			get { return _enabled; }
			set	{ _enabled = value; _Notify("IsEnabled"); }
		}

		public bool IsExpanded
		{
			get { return _expanded; }
			set	{ _expanded = value; _Notify("IsExpanded"); }
		}

		public bool IsSelected
		{
			get { return _selected; }
			set	{ _selected = value; _Notify("IsSelected"); }
		}

		public event PropertyChangedEventHandler PropertyChanged;


		public TreeNode(string title, object tag)
		{
			Title = title;
			Tag = tag;
			Childs = new TreeNodes();

			_enabled = true;
			_expanded = false;
			_selected = false;
		}

		public TreeNode Add(string title, object tag)
		{
			TreeNode node = new TreeNode(title, tag);
			Childs.Add(node);
			return node;
		}

		private void _Notify(string prop)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
		}

		private bool _enabled;
		private bool _expanded;
		private bool _selected;
	}


	public abstract class BasicTree : TreeView
	{
		public BasicTree()
			: base()
		{
			SetValue(VirtualizingPanel.IsVirtualizingProperty, true);
			SetValue(VirtualizingPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);

			Model = new TreeModel();
			DataContext = Model;
			ItemsSource = Model.Nodes;

			ItemTemplate = FindResource("treeTemplate") as HierarchicalDataTemplate;
			ItemContainerStyle = FindResource("treeitemStyle") as Style;
		}


		public void CreateTree(ICallback callback)
		{
			_callback = callback;

			long extra = NoOfExtraElements;

			Dispatcher.Invoke(() => {
				SelectedItemChanged -= (Application.Current.MainWindow as MainWindow).TreeView_SelectedItemChanged;
				SelectedItemChanged += (Application.Current.MainWindow as MainWindow).TreeView_SelectedItemChanged;
				_CreateContextMenu();
				Model.Nodes.Clear();
			});

			_callback.Start(MainWindow.CurrFile.TotalElements + extra, Translate._("MainWindow.LoadGamefile.Progress.Title.2"), "");
			_count = 0;

			TreeNode root = _AddItem(null, Path.GetFileName(MainWindow.CurrFile.Filename), MainWindow.CurrFile.Header);
			_CreateTree(root);

			root.IsExpanded = true;
			root.IsSelected = true;

			_callback.Stop("", "");
		}

		public void ClearTree()
		{
			Model.Nodes.Clear();
		}


		internal abstract int NoOfExtraElements { get; }

		internal abstract void _CreateTree(TreeNode root);


		public TreeModel Model { get; private set; }
		internal ICallback _callback;
		internal long _count;

		internal TreeNode _AddItem(TreeNode parent, string label, object tag = null)
		{
			_count ++;
			_callback.Update(_count, null, label);

			return Dispatcher.Invoke(() => {
				TreeNode node;
				if (parent == null)
					node = Model.Nodes.Add(label, tag);
				else
					node = parent.Add(label, tag);
				return node;
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
			if (!(e.OriginalSource is TextBlock))
			{
				e.Handled = true;
				return;
			}
			TreeNode node = (e.OriginalSource as TextBlock).DataContext as TreeNode;
			if (node == null)
			{
				e.Handled = true;
				return;
			}
			if (!node.IsSelected)
				node.IsSelected = true;

			if (node.Tag is P.Property) // || ...)
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
			TreeNode node = SelectedItem as TreeNode;
			if (node == null)
				return;

			P.Property prop = node.Tag as P.Property;
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

		internal override void _CreateTree(TreeNode root)
		{
			string label = string.Format(Translate._("TreePanel.Tree.Objects"), MainWindow.CurrFile.Objects.Count);
			TreeNode objects = _AddItem(root, label, null);
			foreach (P.Property prop in MainWindow.CurrFile.Objects)
				_AddItem(objects, prop.ToString(), prop);

			label = string.Format(Translate._("TreePanel.Tree.Collected"), MainWindow.CurrFile.Collected.Count);
			TreeNode collected = _AddItem(root, label, null);
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

		internal override void _CreateTree(TreeNode root)
		{
			_classes = new Dictionary<string,TreeNode>();

			foreach (P.Property prop in MainWindow.CurrFile.Objects)
				_AddClassRecurs(root, "/", prop);

			//foreach (Property prop in MainWindow.GetSavegame().Collected)
			//	AddClassRecurs(root, "/", (Savegame.Properties.Object) prop);

			//if self.__savegame.Missing:
			//	label = "Missing"
			//	self.__add(self.root, label, self.__savegame.Missing)
		}

		internal TreeNode _AddClassRecurs(TreeNode parent, string path, Savegame.Properties.Property prop)
		{
			string classname, fullname, label;
			TreeNode class_item;

			string ClassName, PathName;
			if (prop is P.Actor)//.TypeName == "Actor")
			{
				P.Actor actor = (P.Actor) prop;
				ClassName = actor.ClassName.ToString();
				PathName = actor.PathName.ToString();
			}
			else if (prop is P.Object)//.TypeName == "Object")
			{
				P.Object obj = (P.Object) prop;
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

		internal TreeNode _AddOrGetClass(TreeNode parent, string fullname, string classname)
		{
			if (_classes.ContainsKey(fullname))
				return _classes[fullname];
			TreeNode class_item = _AddItem(parent, classname);
			_classes.Add(fullname, class_item);
			return class_item;
		}

		internal Dictionary<string, TreeNode> _classes;

	}


	public class PathsTree : BasicTree
	{
		public PathsTree()
			: base()
		{ }

		internal override int NoOfExtraElements { get { return 150; } }

		internal override void _CreateTree(TreeNode root)
		{
			_paths = new Dictionary<string, TreeNode>();

			foreach (P.Property prop in MainWindow.CurrFile.Objects)
				_AddTreeRecurs(root, "", prop);

			foreach (P.Property prop in MainWindow.CurrFile.Collected)
				_AddTreeRecurs(root, "", prop);
		}

		internal TreeNode _AddTreeRecurs(TreeNode parent, string path, P.Property prop)
		{
			string pathname, fullname, label;
			TreeNode path_item;

			string PathName;
			if (prop is P.Actor)
			{
				P.Actor actor = (P.Actor) prop;
				PathName = actor.PathName.ToString();
			}
			else if (prop is P.Object)
			{
				P.Object obj = (P.Object) prop;
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

		internal TreeNode _AddOrGetPath(TreeNode parent, string fullname, string pathname)
		{
			if (_paths.ContainsKey(fullname))
				return _paths[fullname];
			TreeNode path_item = _AddItem(parent, pathname);
			_paths.Add(fullname, path_item);
			return path_item;
		}

		internal Dictionary<string, TreeNode> _paths;

	}


	public class LivingTree : BasicTree
	{
		public LivingTree()
			: base()
		{ }

		internal override int NoOfExtraElements { get { return 0; } }

		internal override void _CreateTree(TreeNode root)
		{
			_classes = new Dictionary<string, TreeNode>();

			var living = MainWindow.CurrFile.Objects
				.Where(p => p is P.Actor)
				.Cast<P.Actor>()
				.Where(a => a.ClassName.ToString().StartsWith("/Game/FactoryGame/Character/"))
				;

			TreeNode players = _AddItem(root, "Players");
			var subset = living.Where(a => a.ClassName.ToString().StartsWith("/Game/FactoryGame/Character/Player/BP_PlayerState"));
			foreach (P.Actor prop in subset)
				_AddPlayer(players, prop);

			TreeNode enemies = _AddItem(root, "Enemies");
			subset = living.Where(a => a.ClassName.ToString().StartsWith("/Game/FactoryGame/Character/Creature/Enemy"));
			foreach (P.Actor prop in subset)
				_AddEnemy(enemies, prop);

			TreeNode wildlife = _AddItem(root, "Wildlife");
			subset = living.Where(a => a.ClassName.ToString().StartsWith("/Game/FactoryGame/Character/Creature/Wildlife"));
			foreach (P.Actor prop in subset)
				_AddWildlife(wildlife, prop);

			players.IsExpanded = true;
			enemies.IsExpanded = true;
			wildlife.IsExpanded = true;
		}

		internal void _AddPlayer(TreeNode parent, P.Actor blueprint)
		{
			P.NamedEntity named = blueprint.EntityObj as P.NamedEntity;
			P.ObjectProperty player_obj = named.Value.Named("mOwnedPawn") as P.ObjectProperty;
			if (player_obj == null)
			{
				string pl = blueprint.PathName.LastName();
				string short_title = string.Format("Player #{0} [INVALID]", pl.Split('_').Last());
				TreeNode p = _AddItem(parent, short_title, null);
				p.IsEnabled = false;
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
					TreeNode p = _AddItem(parent, short_title, null);
					p.IsEnabled = false;
				}
				else
				{
					Living living = new Living(title, player, blueprint);
					_AddItem(parent, short_title, living);
				}
			}
		}

		internal void _AddEnemy(TreeNode parent, P.Actor entity)
		{
			// Add grouping
			//From: /Game/FactoryGame/Character/Creature/Enemy/Crab/BabyCrab/Char_BabyCrab.Char_BabyCrab_C
			//  To:                                            Crab/BabyCrab/Char_BabyCrab.Char_BabyCrab_C
			string[] groups = entity
				.ClassName.ToString()
				.Replace("/Game/FactoryGame/Character/Creature/Enemy/", "")
				.Split('/');
			TreeNode group = _AddOrGetClass(parent, groups[0]);
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

		internal void _AddWildlife(TreeNode parent, P.Actor entity)
		{
			// Add grouping
			//From: /Game/FactoryGame/Character/Creature/Wildlife/SpaceRabbit/Char_SpaceRabbit.Char_SpaceRabbit_C
			//  To:                                               SpaceRabbit/Char_SpaceRabbit.Char_SpaceRabbit_C
			string group_name = entity
				.ClassName.ToString()
				.Replace("/Game/FactoryGame/Character/Creature/Wildlife/", "")
				.Split('/').First();
			TreeNode group = _AddOrGetClass(parent, group_name);

			string name = entity.PathName.LastName();
			string classname = entity.ClassName.LastName();
			if (Translate.Has(classname))
				classname = Translate._(classname);
			string short_title = string.Format("{0} #{1}", classname, name.Split('_').Last());
			string title = short_title + string.Format(" ({0})", name);

			Living living = new Living(title, entity, null);
			_AddItem(group, short_title, living);
		}

		internal TreeNode _AddOrGetClass(TreeNode parent, string name)
		{
			if (!_classes.ContainsKey(name))
			{
				TreeNode class_item = _AddItem(parent, name);
				//class_item.IsExpanded = true;
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


		internal Dictionary<string, TreeNode> _classes;


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


	public class BuildingsTree : BasicTree
	{
		public BuildingsTree()
			: base()
		{ }

		internal override int NoOfExtraElements { get { return 0; } }

		internal override void _CreateTree(TreeNode root)
		{
			_classes = new Dictionary<string, TreeNode>();

			var factory_set = MainWindow.CurrFile.Objects
				.Where(p => p is P.Actor)
				.Cast<P.Actor>()
				.Where(a => a.ClassName.ToString().StartsWith("/Game/FactoryGame/Buildable/Factory/"))
				;
			TreeNode factories = _AddItem(root, "Factories");
			foreach (P.Actor prop in factory_set)
				_AddFactory(factories, prop);

			var building_set = MainWindow.CurrFile.Objects
				.Where(p => p is P.Actor)
				.Cast<P.Actor>()
				.Where(a => a.ClassName.ToString().StartsWith("/Game/FactoryGame/Buildable/Building/"))
				;
			TreeNode buildings = _AddItem(root, "Buildings");
			foreach (P.Actor prop in building_set)
				_AddBuilding(buildings, prop);

			factories.IsExpanded = true;
			buildings.IsExpanded = true;
		}

		internal void _AddFactory(TreeNode parent, P.Actor actor)
		{
			string[] groups = actor
				.ClassName.ToString()
				.Replace("/Game/FactoryGame/Buildable/Factory/", "")
				.Split('/');
			TreeNode group = _AddOrGetClass(parent, groups[0]);
			if (groups.Length == 3)
				group = _AddOrGetClass(group, groups[1]);

			string objname = actor.PathName.LastName();
			var names = objname
				.Split('_')
				.Where(s => !_excludes.Contains(s))
				.ToList();
			string id = names.Last();
			names.Remove(id);
			string name = string.Join("_", names);

			string short_title = string.Format("{0} #{1}", name, id);
			string title = short_title + string.Format(" ({0})", objname);

			Building building = new Building(title, actor, true);
			_AddItem(group, short_title, building);
		}

		internal void _AddBuilding(TreeNode parent, P.Actor actor)
		{
			string[] groups = actor
				.ClassName.ToString()
				.Replace("/Game/FactoryGame/Buildable/Building/", "")
				.Split('/');
			TreeNode group = _AddOrGetClass(parent, groups[0]);
			if (groups.Length == 3)
				group = _AddOrGetClass(group, groups[1]);

			string objname = actor.PathName.LastName();
			var names = objname
				.Split('_')
				.Where(s => !_excludes.Contains(s))
				.ToList();
			string id = names.Last();
			names.Remove(id);
			string name = string.Join("_", names);

			string short_title = string.Format("{0} #{1}", name, id);
			string title = short_title + string.Format(" ({0})", objname);

			Building building = new Building(title, actor, false);
			_AddItem(group, short_title, building);
		}

		//TODO: Add icon capability?
		internal TreeNode _AddOrGetClass(TreeNode parent, string name)
		{
			if (!_classes.ContainsKey(name))
			{
				string title = name;
				if (Translate.Has(title))
					title = Translate._(title);
				TreeNode class_item = _AddItem(parent, title);
				//class_item.IsExpanded = true;
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


		private Dictionary<string, TreeNode> _classes;
		private static string[] _excludes = new string[] { "Build", "BP", "C" };


		internal class Building
		{
			internal string  Title;
			internal P.Actor Actor;
			internal bool    IsFactory;

			internal Building(string title, P.Actor actor, bool is_factory)
			{
				Title = title;
				Actor = actor;
				IsFactory = is_factory;
			}
		}

	}

}
