using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using FileHandler;


namespace SatisfactorySavegameTool.Supplements
{
	public class RecipeTable
	{
		public static RecipeTable INSTANCE;

		public RecipeTable()
		{
			_recipes = new List<Recipe>();

			_Load();

			INSTANCE = this;
		}


		public Recipe Find(str path)
		{
			if (!str.IsNullOrEmpty(path))
				return Find(path.ToString());
			return null;
		}
		public Recipe Find(string path)
		{
			return _recipes.Find(r => r.PathName == path);
		}


		private void _Load()
		{
			string filename = Path.Combine(Settings.RESOURCEPATH, "RecipeTable.xml");

			XmlDocument xml = new XmlDocument();
			try
			{
				xml.Load(filename);
			}
			catch (Exception exc)
			{
				string msg = string.Format("Error loading recipe table {0}:\n", filename)
							+ exc.Message.ToString() + "\n"
							+ exc.Source.ToString() + "\n"
							+ exc.StackTrace.ToString() + "\n"
							;
				Log.Error(msg);
				return;
			}
			if (xml.ChildNodes.Count != 2)
				throw new Exception("INVALID RECIPE TABLE FILE!");

			XmlNode node = xml.ChildNodes[0];
			if (node.Name.ToLower() != "xml")
				throw new Exception("INVALID RECIPE TABLE FILE!");

			node = xml.ChildNodes[1];
			if (node.Name.ToLower() != "recipes")
				throw new Exception("INVALID RECIPE TABLE FILE!");

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
					throw new Exception("INVALID RECIPE TABLE FILE!");

				Recipe recipe = new Recipe(name, path, clazz, bp, native, (exp != null), (blocked != null));

				_recipes.Add(recipe);
			}

		}

		private List<Recipe> _recipes;

		public class Recipe
		{
			public string Name;
			public string PathName;
			public string Blueprint;
			public string Native;
			public bool   Experimental;
			public bool   Blocked;

			internal Recipe(string name, string path, string clazz, string blueprint, string native, 
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