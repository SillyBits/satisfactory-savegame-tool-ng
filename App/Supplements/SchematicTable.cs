using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using FileHandler;


namespace SatisfactorySavegameTool.Supplements
{
	public class SchematicTable
	{
		public static SchematicTable INSTANCE;

		public SchematicTable()
		{
			_schematics = new List<Schematic>();

			_Load();

			INSTANCE = this;
		}


		public Schematic Find(str path)
		{
			if (!str.IsNullOrEmpty(path))
				return Find(path.ToString());
			return null;
		}
		public Schematic Find(string path)
		{
			return _schematics.Find(r => r.PathName == path);
		}


		private void _Load()
		{
			string filename = Path.Combine(Settings.RESOURCEPATH, "SchematicTable.xml");

			XmlDocument xml = new XmlDocument();
			try
			{
				xml.Load(filename);
			}
			catch (Exception exc)
			{
				string msg = string.Format("Error loading schematic table {0}:\n", filename)
							+ exc.Message.ToString() + "\n"
							+ exc.Source.ToString() + "\n"
							+ exc.StackTrace.ToString() + "\n"
							;
				Log.Error(msg);
				return;
			}
			if (xml.ChildNodes.Count != 2)
				throw new Exception("INVALID SCHEMATIC TABLE FILE!");

			XmlNode node = xml.ChildNodes[0];
			if (node.Name.ToLower() != "xml")
				throw new Exception("INVALID SCHEMATIC TABLE FILE!");

			node = xml.ChildNodes[1];
			if (node.Name.ToLower() != "schematics")
				throw new Exception("INVALID SCHEMATIC TABLE FILE!");

			foreach (XmlNode childnode in node.ChildNodes)
			{
				XmlElement element = childnode as XmlElement;
				if (element == null)
					continue; // Skip comments and such

				string name = null, path = null, clazz = null, bp = null, native = null, exp = null, blocked = null;
				foreach (XmlAttribute attr in element.Attributes)
				{
					switch (attr.Name)
					{
						case "name":	name    = attr.Value; break;
						case "path":	path    = attr.Value; break;
						case "class":	clazz   = attr.Value; break;
						case "bp":		bp      = attr.Value; break;
						case "native":	native  = attr.Value; break;
						case "exp":		exp     = attr.Value; break;
						case "blocked":	blocked = attr.Value; break;
					}
				}
				if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
					throw new Exception("INVALID SCHEMATIC TABLE FILE!");

				Schematic schematic = new Schematic(name, path, clazz, bp, native, (exp != null), (blocked != null));

				_schematics.Add(schematic);
			}

		}

		private List<Schematic> _schematics;

		public class Schematic
		{
			public string Name;
			public string PathName;
			public string Blueprint;
			public string Native;
			public bool   Experimental;
			public bool   Blocked;

			internal Schematic(string name, string path, string clazz, string blueprint, string native, 
				bool exp_only, bool blocked)
			{
				Name         = name;
				PathName     = path + "." + (clazz == null ? name + "_C" : clazz);
				Blueprint    = blueprint;
				Native       = native;
				Experimental = exp_only;
				Blocked      = blocked;
			}

		}

	}

}