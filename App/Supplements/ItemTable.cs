using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using FileHandler;


namespace SatisfactorySavegameTool.Supplements
{
	public class ItemTable
	{
		public static ItemTable INSTANCE;

		public ItemTable()
		{
			_items = new List<Item>();

			_Load();

			INSTANCE = this;
		}


		public Item Find(str path)
		{
			if (!str.IsNullOrEmpty(path))
				return Find(path.ToString());
			return null;
		}
		public Item Find(string path)
		{
			return _items.Find(r => r.PathName == path);
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
					exp = null, blocked = null, stacksize = null;
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
					}
				}
				if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
					throw new Exception("INVALID ITEM TABLE FILE!");

				Item item = new Item(name, path, clazz, bp, native, type, stacksize, (exp != null), (blocked != null));

				_items.Add(item);
			}

		}

		private List<Item> _items;

		public enum ItemType
		{
			Unknown = 0,
			Item,
			Equipment,
			Resource,
			Invalid = -1
		}

		public class Item
		{
			public string		Name;
			public string		PathName;
			public string		Blueprint;
			public string		Native;
			public ItemType		Type;
			public EStackSize	StackSize;
			public bool			Experimental;
			public bool			Blocked;

			internal Item(string name, string path, string clazz, string blueprint, string native, string type,
				string stacksize, bool exp_only, bool blocked)
			{
				Name         = name;
				PathName     = "/Game/FactoryGame/" + path + "." + (clazz == null ? name + "_C" : clazz);
				Blueprint    = blueprint;
				Native       = native;
				Experimental = exp_only;
				Blocked      = blocked;

				if (string.IsNullOrEmpty(type))
					Type = ItemType.Unknown;
				else if (!Enum.TryParse(type, out Type))
					Type = ItemType.Invalid;

				if (string.IsNullOrEmpty(stacksize))
				{
					if (Type != ItemType.Equipment && !Blocked)
						throw new Exception("Stacksize missing for non-equipment entry");
					StackSize = EStackSize.SS_ONE;
				}
				else if (!Enum.TryParse(stacksize, out StackSize))
					throw new Exception(string.Format("Invalid stacksize {0} for item {0}", stacksize, name));
			}

		}

	}

}