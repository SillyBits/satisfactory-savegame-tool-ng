using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using System.Xml;

using FileHandler;

using P = Savegame.Properties;


namespace SatisfactorySavegameTool.Supplements
{
	public class ItemTable
	{
		public static ItemTable INSTANCE;

		public ItemTable()
		{
			_all = new Collection();

			_Load();

			INSTANCE = this;
		}


		public static Item Find(P.InventoryItem item)
		{
			if (item != null && !str.IsNullOrEmpty(item.ItemName))
				return Find(item.ItemName.ToString());
			return null;
		}

		public static Item Find(str path)
		{
			if (!str.IsNullOrEmpty(path))
				return Find(path.ToString());
			return null;
		}

		public static Item Find(string path)
		{
			return INSTANCE._all.Find(r => r.PathName == path);
		}


		// All items available, regardless of type, slot, whatsoever
		public static Collection All
		{
			get
			{
				return INSTANCE._all;
			}
		}

		// Cached subsets
		public static Collection Items
		{
			get
			{
				if (INSTANCE._items == null)
					INSTANCE._items = new Collection(INSTANCE._all.Where(i => i.Type == ItemType.Item));
				return INSTANCE._items;
			}
		}
		public static Collection Equipment
		{
			get
			{
				if (INSTANCE._equipment_all == null)
					INSTANCE._equipment_all = new Collection(INSTANCE._all.Where(i => i.Type == ItemType.Equipment));
				return INSTANCE._equipment_all;
			}
		}
		public static Collection EquipmentArms
		{
			get
			{
				if (INSTANCE._equipment_arms == null)
					INSTANCE._equipment_arms = new Collection(INSTANCE._all.Where(i => i.Type == ItemType.Equipment && i.Slot == EEquipmentSlot.ES_ARMS));
				return INSTANCE._equipment_arms;
			}
		}
		public static Collection EquipmentBack
		{
			get
			{
				if (INSTANCE._equipment_back == null)
					INSTANCE._equipment_back = new Collection(INSTANCE._all.Where(i => i.Type == ItemType.Equipment && i.Slot == EEquipmentSlot.ES_BACK));
				return INSTANCE._equipment_back;
			}
		}
		public static Collection Resources
		{
			get
			{
				if (INSTANCE._resources == null)
					INSTANCE._resources = new Collection(INSTANCE._all.Where(i => i.Type == ItemType.Resource));
				return INSTANCE._resources;
			}
		}
		public static Collection Empty
		{
			get
			{
				if (INSTANCE._empty == null)
					INSTANCE._empty = new Collection();
				return INSTANCE._empty;
			}
		}


		private void _Load()
		{
			string filename = Path.Combine(Settings.RESOURCEPATH, "ItemTable.xml");

			XmlDocument xml = new XmlDocument();
			try
			{
				xml.Load(filename);
			}
			catch (Exception exc)
			{
				string msg = string.Format("Error loading item table {0}:\n", filename)
							+ exc.Message.ToString() + "\n"
							+ exc.Source.ToString() + "\n"
							+ exc.StackTrace.ToString() + "\n"
							;
				Log.Error(msg);
				return;
			}
			if (xml.ChildNodes.Count != 2)
				throw new Exception("INVALID ITEM TABLE FILE!");

			XmlNode node = xml.ChildNodes[0];
			if (node.Name.ToLower() != "xml")
				throw new Exception("INVALID ITEM TABLE FILE!");

			node = xml.ChildNodes[1];
			if (node.Name.ToLower() != "items")
				throw new Exception("INVALID ITEM TABLE FILE!");

			foreach (XmlNode childnode in node.ChildNodes)
			{
				XmlElement element = childnode as XmlElement;
				if (element == null)
					continue; // Skip comments and such

				string name = null, path = null, clazz = null, bp = null, native = null, type = null,
					exp = null, blocked = null, stacksize = null, slot = null;
				foreach (XmlAttribute attr in element.Attributes)
				{
					switch (attr.Name)
					{
						case "name":		name      = attr.Value; break;
						case "path":		path      = attr.Value; break;
						case "class":		clazz     = attr.Value; break;
						case "bp":			bp        = attr.Value; break;
						case "native":		native    = attr.Value; break;
						case "type":		type      = attr.Value; break;
						case "exp":			exp       = attr.Value; break;
						case "blocked":		blocked   = attr.Value; break;
						case "stacksize":	stacksize = attr.Value; break;
						case "slot":		slot      = attr.Value; break;
					}
				}
				if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
					throw new Exception("INVALID ITEM TABLE FILE!");

				Item item = new Item(name, path, clazz, bp, native, type, stacksize, slot, (exp != null), (blocked != null));

				_all.Add(item);
			}
		}

		private Collection _all, _items, _equipment_all, _equipment_arms, _equipment_back, _resources, _empty;

		[Flags]
		public enum ItemType
		{
			Unknown   = 0,
			Item      = 1,
			Equipment = 2,
			Resource  = 4,
		}

		public class Item
		{
			public string			Name;
			public string			DisplayName;
			public string			PathName;
			public string			Blueprint;
			public string			Native;
			public ItemType			Type;
			public EStackSize		StackSize;
			public EEquipmentSlot	Slot;
			public bool				Experimental;
			public bool				Blocked;

			public Icons Icon
			{
				get
				{
					if (_icons == null)
					{
						_icons = new Icons();
						if (Config.Root.pak_loader.enabled)
						{
							_icons[VersionTable.Version.Experimental] = ImageCache.GetImage(PathName, 32, VersionTable.Version.Experimental);
							_icons[VersionTable.Version.EarlyAccess]  = ImageCache.GetImage(PathName, 32, VersionTable.Version.EarlyAccess);
						}
					}
					return _icons;
				}
			}


			internal Item(string name, string path, string clazz, string blueprint, string native, string type,
				string stacksize, string slot, bool exp_only, bool blocked)
			{
				Name         = name;
				PathName     = "/Game/FactoryGame/" + path + "." + (clazz == null ? name + "_C" : clazz);
				Blueprint    = blueprint;
				Native       = native;
				Experimental = exp_only;
				Blocked      = blocked;

				if (string.IsNullOrEmpty(type) || !Enum.TryParse(type, out Type))
					Type = ItemType.Unknown;

				if (string.IsNullOrEmpty(stacksize))
				{
					if (Type != ItemType.Equipment && !Blocked)
						throw new Exception("Stacksize missing for non-equipment entry");
					StackSize = EStackSize.SS_ONE;
				}
				else if (!Enum.TryParse(stacksize, out StackSize))
					throw new Exception(string.Format("Invalid stacksize {0} for item {1}", stacksize, name));

				if (string.IsNullOrEmpty(slot))
				{
					if (Type == ItemType.Equipment)
						throw new Exception("Slot missing for equipment entry");
					Slot = EEquipmentSlot.ES_NONE;
				}
				else if (!Enum.TryParse(slot, out Slot))
					throw new Exception(string.Format("Invalid slot {0} for item {1}", slot, name));

				DisplayName = (clazz == null) ? name + "_C" : clazz;
				if (Translate.Has(DisplayName))
					DisplayName = Translate._(DisplayName);
			}

			public class Icons
			{
				public BitmapSource this[VersionTable.Version version]
				{
					get { return _icons[(int)version]; }
					set { _icons[(int)version] = value; }
				}

				private BitmapSource[] _icons = new BitmapSource[2];
			}

			private Icons _icons;
		}

		public class Collection : List<Item>
		{
			internal Collection() 
				: base()
			{ }

			internal Collection(IEnumerable<Item> coll)
				: base(coll)
			{ }
		}

	}

}