using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace CoreLib
{
	//
	// Config handling using XML approach
	// Similar to System.Configuration, but more light-weighted and 
	// flexible by utilizing DynamicObject which allows for stuff like:
	//
	//     Setting a windows position:
    //         Left   = Config.Root.window.pos_x;
    //         Top    = Config.Root.window.pos_y;
    //         Width  = Config.Root.window.size_x;
    //         Height = Config.Root.window.size_y;
	//
	//     Handling MRU lists, like setting up a menu:
	//         foreach (string file in Config.Root.mru.files) 
	//         {
    //             item = new MenuItem();
	//             ...
	//         }
	//     and adding new files:
	//         Config.Root.mru.files.Add(<newly opened file>)
	//
	// Only downside with using DynamicObject: There is no syntax help 
	// in means of auto-completion and such. But a small price to pay
	// compared to possibilities avail.
	//
	//
	// 'Sections' can contain other 'Section's or 'Item's.
	// 'Item' can contain an optional attribute 'type' and defaults to 'String' if omitted.
	// Besides single 'Item', using the attributes following will denote a 'ListItem': is-list="<flag>"[ type="<type>"]
	// Values in this list are ordered using the attribute 'id' (which MUST be of numerical type).
	//

	/*
	 * TODO:
	 * 
	 * - Add locking to be thread-safe, best would be to use either
	 *     lock(_node.OwnerDocument) {...}
	 *   or
	 *     lock(ConfigFile.CONFIG) {...}
	 * 
	 * - Add creators/removers for Section and [List]Item
	 * 
	 */

	// Items do store a name and value
	public class Item : DynamicObject
	{
		internal Item()
		{
			_dirty = false;
		}
		// Stays public for now until a solution is found regarding Activator.CreateInstance
		public Item(XmlNode node)
			: this()
		{
			_node = node;
			_Load();
		}

		public virtual string Name
		{
			get { return _node.Name; }
		}
		public virtual object Value
		{
			get { return _node.InnerText; }
			set
			{
				_node.InnerText = value.ToString();
				_dirty = true;
			}
		}

		internal XmlNode _node;
		internal bool _dirty;

		internal virtual void _Load()
		{
			// Nothing to be done here
		}
		internal virtual bool _Save()
		{
			if (!_dirty)
				return false;
			_dirty = false;
			return true;
		}
	}

	internal class Item<_ValueType> : Item
	{
		// Stays public for now until a solution is found regarding Activator.CreateInstance
		public Item(XmlNode node)
			: base(node)
		{ }

		public override object Value
		{
			get { return _value; }
			set
			{
				// Explicit use of this type of cast to force 
				// an exception if value can't be converted.
				_value = (_ValueType) value;
				_dirty = true;
			}
		}

		internal _ValueType _value;

		internal override void _Load()
		{
			// Find better way of converting
			_value = (_ValueType) Convert.ChangeType(_node.InnerText, typeof(_ValueType));
		}
		internal override bool _Save()
		{
			if (!_dirty)
				return false;
			_node.InnerText = _value.ToString();
			_dirty = false;
			return true;
		}
	}

	// ListItems do store a name and N values
	internal class ListItem : Item, IEnumerable
	{
		// Stays public for now until a solution is found regarding Activator.CreateInstance
		public ListItem(XmlNode node)
			: base(node)
		{ }

		public override object Value
		{
			get { return this; }
			set { throw new NotSupportedException(); }
		}
		public virtual int Count
		{
			get { return _values.Count; }
		}
		public virtual object this[object index]
		{
			get
			{
				int idx = (int) index;
				if (idx < 0 || idx >= _values.Count)
					throw new IndexOutOfRangeException();
				return _values[idx];
			}
			set
			{
				int idx = (int) index;
				if (idx < 0 || idx >= _values.Count)
					throw new IndexOutOfRangeException();
				_values[idx] = value;
				_dirty = true;
			}
		}

		public virtual object Add(object value)
		{
			return _Add(value);
		}
		public virtual void Remove(object value)
		{
			_Remove<object>(value);
		}
		public virtual void RemoveAt(object index)
		{
			_RemoveAt<object>(index);
		}
		public virtual void Clear()
		{
			_Clear<object>();
		}

		internal IList _values;

		internal override void _Load()
		{
			_Load<object>();
		}
		internal override bool _Save()
		{
			return _Save<object>();
		}

		internal virtual void _Load<_ValueType>()
		{
			// Even if nodes are to be ordered same as in the section read from file,
			// users may haveed ited the file and re-ordered those. So ensure proper ordering.
			// And as we're using Linq already, lets filter out comments and other 'junk'.
			var sorted = _node.ChildNodes.Cast<XmlNode>()
				.Where((node) => node is XmlElement)
				.OrderBy((node) => int.Parse(node.Attributes["id"].InnerText));

			_values = new List<_ValueType>(sorted.Count());
			foreach (XmlNode node in sorted)
			{
				if (node.Name != "entry")
					throw new Exception("EXPECTED 'entry' ELEMENT!");
				// Find better way of converting
				_ValueType value = (_ValueType) Convert.ChangeType(node.InnerText, typeof(_ValueType));
				_values.Add(value);
			}
		}
		internal virtual bool _Save<_ValueType>()
		{
			if (!_dirty)
				return false;

			if (_node.ChildNodes.Count != _values.Count)
			{
				// Entries were added/removed

				while (_node.HasChildNodes)
					_node.RemoveChild(_node.LastChild);

				string name = Name;
				int index = 0;
				foreach (_ValueType value in _values)
				{
					XmlElement child = _node.OwnerDocument.CreateElement("entry");
					child.SetAttribute("id", index.ToString());
					child.InnerText = value.ToString();
					_node.AppendChild(child);
					++index;
				}
			}
			else
			{
				// Just update values stored

				for (int index = 0; index < _values.Count; ++index)
					_node.ChildNodes[index].InnerText = _values[index].ToString();
			}

			_dirty = false;
			return true;
		}

		internal virtual object _Add<_ValueType>(_ValueType value)
		{
			_dirty = true;
			return _values.Add(value);
		}
		internal virtual void _Remove<_ValueType>(_ValueType value)
		{
			_values.Remove(value);
			_dirty = true;
		}
		internal virtual void _RemoveAt<_ValueType>(object index)
		{
			int idx = (int) index;
			if (idx < 0 || idx >= _values.Count)
				throw new IndexOutOfRangeException();
			_values.RemoveAt(idx);
			_dirty = true;
		}
		internal virtual void _Clear<_ValueType>()
		{
			_values.Clear();
			_dirty = true;
		}

		// DynamicObject
		public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
		{
			result = this[indexes[0]];
			return true;
		}
		public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value)
		{
			this[indexes[0]] = value;
			return true;
		}
		public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
		{
			switch(binder.Name)
			{
				case "Add":			result = Add(args[0]);				return true;
				case "Remove":		result = null; Remove(args[0]);		return true;
				case "RemoveAt":	result = null; RemoveAt(args[0]);	return true;
				case "Clear":		result = null; Clear();				return true;
			}

			result = null;
			return false;
		}

		// IEnumerable
		IEnumerator IEnumerable.GetEnumerator()
		{
			return _values.GetEnumerator();
		}
	}

	internal class ListItem<_ValueType> : ListItem
	{
		// Stays public for now until a solution is found regarding Activator.CreateInstance
		public ListItem(XmlNode node)
			: base(node)
		{ }

		public override object Add(object value)
		{
			return _Add((_ValueType) value);
		}
		public override void Remove(object value)
		{
			_Remove((_ValueType) value);
		}
		public override void RemoveAt(object index)
		{
			_RemoveAt<_ValueType>(index);
		}
		public override void Clear()
		{
			_Clear<_ValueType>();
		}

		internal override void _Load()
		{
			_Load<_ValueType>();
		}
		internal override bool _Save()
		{
			return _Save<_ValueType>();
		}
	}


	static internal class ItemFactory
	{
		internal static Item Create(XmlNode node)
		{
			XmlAttribute attr;
			Type value_type = null;

			// Find type by attribute, if specified
			attr = node.Attributes["type"];
			if (attr != null)
			{
				string s_type = attr.InnerText;
				value_type = Type.GetType(s_type);
				if (value_type == null)
					value_type = Type.GetType("System." + s_type);
			}

			// No type yet, try to find by context
			if (value_type == null)
			{
				string content = node.InnerText;

				bool b_val;
				if (bool.TryParse(content, out b_val))
					value_type = typeof(bool);

				int i_val;
				if (value_type == null && int.TryParse(content, out i_val))
					value_type = typeof(int);

				float f_val;
				if (value_type == null && float.TryParse(content, out f_val))
					value_type = typeof(float);
			}

			// Still no tpe, then default to string
			if (value_type == null)
				value_type = typeof(string);

			Type inst_type;
			attr = node.Attributes["is-list"];
			string is_list = attr != null ? attr.InnerText : null;
			if (is_list == null)
			{
				// Single value
				inst_type = typeof(Item<>).MakeGenericType(value_type);
			}
			else
			{
				// List of values
				inst_type = typeof(ListItem<>).MakeGenericType(value_type);
			}
			if (inst_type == null)
				return null;

			object[] param = { node };
			Item item = Activator.CreateInstance(inst_type, param) as Item;
			//=> Maybe using BindingFlags.NonPublic is worth a try so we can hide those constructors again
			//Item item = Activator.CreateInstance(inst_type, BindingFlags.NonPublic, null, param, null, null) as Item;

			return item;
		}
	}


	// Sections do store a list of either other sections or items
	public class Section : DynamicObject
	{
		internal Section()
		{
			Sections = new Dictionary<string,Section>();
			Items = new Dictionary<string,Item>();
		}
		internal Section(XmlNode node)
			: this()
		{
			_node = node;
			_Load();
		}

		public string Name
		{
			get { return _node.Name != "configuration" ? _node.Name : ""; }
		}
		public Dictionary<string,Section> Sections { get; internal set; }
		public Dictionary<string,Item> Items { get; internal set; }

		public bool HasSection(string name) { return Sections.ContainsKey(name); }
		public bool HasItem(string name) { return Items.ContainsKey(name); }
		public bool Has(string name) { return HasSection(name) || HasItem(name); }

		public Section AddSection(string name)
		{
			if (HasSection(name))
				return null;

			XmlElement node = _node.OwnerDocument.CreateElement(name);
			_node.AppendChild(node);

			Section section = new Section(node);
			Sections.Add(name, section);

			return section;
		}

		public bool RemoveSection(string name)
		{
			if (!HasSection(name))
				return false;

			Section section = Sections[name];
			Sections.Remove(name);

			_node.RemoveChild(section._node);

			return true;
		}

		public Item AddItem(string name)
		{
			return AddItem(name, "");
		}
		public Item AddItem<_ValueType>(string name, _ValueType initial)
		{
			if (HasItem(name))
				return null;

			XmlElement node = _node.OwnerDocument.CreateElement(name);
			node.InnerText = initial.ToString();
			_node.AppendChild(node);

			XmlAttribute attr = _node.OwnerDocument.CreateAttribute("type");
			attr.InnerText = typeof(_ValueType).Name;
			node.Attributes.Append(attr);

			Item item = ItemFactory.Create(node);
			Items.Add(name, item);

			return item;
		}

		public Item AddListItem(string name)
		{
			return AddListItem(name, "");
		}
		public Item AddListItem<_ValueType>(string name, _ValueType initial)
		{
			if (HasItem(name))
				return null;

			XmlElement node = _node.OwnerDocument.CreateElement(name);
			node.InnerText = initial.ToString();
			_node.AppendChild(node);

			XmlAttribute attr = _node.OwnerDocument.CreateAttribute("type");
			attr.InnerText = typeof(_ValueType).Name;
			node.Attributes.Append(attr);

			attr = _node.OwnerDocument.CreateAttribute("is-list");
			attr.InnerText = "True";
			node.Attributes.Append(attr);

			Item item = ItemFactory.Create(node);
			Items.Add(name, item);

			return item;
		}

		public bool RemoveItem(string name)
		{
			if (!HasItem(name))
				return false;

			Item item = Items[name];
			Items.Remove(name);

			_node.RemoveChild(item._node);

			return true;
		}

		internal XmlNode _node;

		internal void _Load()
		{
			var filtered = _node.ChildNodes.Cast<XmlNode>()
				.Where((node) => node is XmlElement);

			foreach (XmlNode node in filtered)
			{
				if (node.Attributes.Count == 0)
				{
					// Section
					Section section = new Section(node);
					if (Sections.ContainsKey(section.Name))
						throw new Exception("DUPLICATE SECTION!");
					Sections.Add(section.Name, section);
				}
				else
				{
					// (List)Item
					Item item = ItemFactory.Create(node);
					if (item == null)
						throw new Exception("UNABLE TO CREATE ITEM!");
					if (Items.ContainsKey(item.Name))
						throw new Exception("DUPLICATE ITEM!");
					Items.Add(item.Name, item);
				}
			}
		}
		internal bool _Save()
		{
			bool changes = false;

			foreach (Section section in Sections.Values)
				changes |= section._Save();

			foreach (Item item in Items.Values)
				changes |= item._Save();

			return changes;
		}

		// DynamicObject
		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			string name = binder.Name;

			if (Sections.ContainsKey(name))
			{
				result = Sections[name];
				return true;
			}

			if (Items.ContainsKey(name))
			{
				result = Items[name].Value;
				return true;
			}

			result = null;
			return false;
		}
		public override bool TrySetMember(SetMemberBinder binder, object value)
		{
			string name = binder.Name;

			// NOT allowed to write into section directly
			if (Sections.ContainsKey(name))
				return false;

			// Writing into existing items is allowed
			if (Items.ContainsKey(name))
			{
				Items[name].Value = value;
				return true;
			}

			// but NO on-the-fly creation for now!
			return false;
		}
		public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
		{
			switch(binder.Name)
			{
				case "HasSection":	result = HasSection(args[0].ToString());	return true;
				case "HasItem":		result = HasItem(args[0].ToString());		return true;
				case "Has":			result = Has(args[0].ToString());			return true;
			}

			result = null;
			return false;
		}
	}


	public class ConfigFile : DynamicObject
	{
		public static ConfigFile CONFIG = null;

		public ConfigFile(string filepath, string appname)
		{
			string filename = System.IO.Path.Combine(filepath, appname + ".cfg");

			_Load(filename);

			SetConfig(this);
		}

		~ConfigFile()
		{
			Shutdown();
		}


		public void Shutdown()
		{
			CONFIG = null;

			_Save();

			_root = null;
			_config = null;
		}

		public void Flush()
		{
			_Save();
		}


		// Override this if you subclass from ConfigFile, or call it at least once
		virtual public void SetConfig(ConfigFile cfg)
		{
			CONFIG = cfg;
		}


		public string Filename { get { return _filename; } }

		public dynamic Root { get { return _root; } }

		public bool HasSection(string name) { return _root.HasSection(name); }
		public bool HasItem(string name) { return _root.HasItem(name); }
		public bool Has(string name) { return _root.Has(name); }


		internal string _filename;
		internal XmlDocument _config;
		internal string _version;
		internal Section _root;


		internal void _Load(string filename)
		{
			_root = null;
			_config = null;

			_filename = filename;

			//TODO: Detect non-existing config file and create basic skeleton structure

			_config = new XmlDocument();
			_config.Load(filename);
			if (_config.ChildNodes.Count != 2)
				throw new Exception("INVALID CONFIG FILE!");

			XmlNode node = _config.ChildNodes[0];
			if (node.Name.ToLower() != "xml")
				throw new Exception("INVALID CONFIG FILE!");

			node = _config.ChildNodes[1];
			if (node.Name.ToLower() != "configuration"
				|| node.Attributes == null
				|| node.Attributes.Count < 1
				|| node.Attributes[0].Name.ToLower() != "version")
				throw new Exception("INVALID CONFIG FILE!");

			_version = node.Attributes[0].Value;
			_root = new Section(node);
		}

		internal void _Save()
		{
			if (_root != null)
			{
				bool changes = _root._Save();
				if (changes)
					_config.Save(_filename);
			}
		}


		// DynamicObject
		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			if (_root != null)
				return _root.TryGetMember(binder, out result);
			result = null;
			return false;
		}

		public override bool TrySetMember(SetMemberBinder binder, object value)
		{
			if (_root != null)
				return _root.TrySetMember(binder, value);
			return false;
		}
	}

}


// To allow for easy access
public class Config : DynamicObject
{
	public static dynamic Root { get { return C.Root; } }

	public static bool HasSection(string name) { return C.HasSection(name); }
	public static bool HasItem(string name) { return C.HasItem(name); }
	public static bool Has(string name) { return C.Has(name); }


	private static CoreLib.ConfigFile C
	{
		get
		{
			if (CoreLib.ConfigFile.CONFIG == null)
				throw new ArgumentNullException("No config available");
			return CoreLib.ConfigFile.CONFIG;
		}
	}

}
