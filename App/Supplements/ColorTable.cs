using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using M = System.Windows.Media;

using FileHandler;


namespace SatisfactorySavegameTool.Supplements
{
	public class ColorTable
	{
		public static ColorTable INSTANCE;

		public ColorTable()
		{
			_colors = new List<Color>();

			_Load();

			INSTANCE = this;
		}


		public Color Find(int index)
		{
			return _colors[index];
		}


		private void _Load()
		{
			string filename = Path.Combine(Settings.RESOURCEPATH, "ColorTable.xml");

			XmlDocument xml = new XmlDocument();
			try
			{
				xml.Load(filename);
			}
			catch (Exception exc)
			{
				string msg = string.Format("Error loading color table {0}:\n", filename)
							+ exc.Message.ToString() + "\n"
							+ exc.Source.ToString() + "\n"
							+ exc.StackTrace.ToString() + "\n"
							;
				Log.Error(msg);
				return;
			}
			if (xml.ChildNodes.Count != 2)
				throw new Exception("INVALID COLOR TABLE FILE!");

			XmlNode node = xml.ChildNodes[0];
			if (node.Name.ToLower() != "xml")
				throw new Exception("INVALID COLOR TABLE FILE!");

			node = xml.ChildNodes[1];
			if (node.Name.ToLower() != "colors")
				throw new Exception("INVALID COLOR TABLE FILE!");

			foreach (XmlNode childnode in node.ChildNodes)
			{
				XmlElement element = childnode as XmlElement;
				if (element == null)
					continue; // Skip comments and such

				if (element.Name != "color" || element.Attributes.Count != 1)
					throw new Exception("INVALID COLOR TABLE FILE!");

				XmlAttribute index = element.Attributes["index"];
				if (index == null || string.IsNullOrEmpty(index.Value))
					throw new Exception("INVALID COLOR TABLE FILE!");
				int idx;
				if (!int.TryParse(index.Value, out idx) || idx != _colors.Count)
					throw new Exception("INVALID COLOR TABLE FILE!");

				Color colors = Color.FromXml(element);

				_colors.Add(colors);
			}

		}

		private List<Color> _colors;

		public class Color
		{
			public M.Color Primary;
			public M.Color Secondary;

			internal Color(M.Color primary, M.Color secondary)
			{
				Primary   = primary;
				Secondary = secondary;
			}

			internal static Color FromXml(XmlElement node)
			{
				Color color = new Color();
				foreach (XmlElement child in node.ChildNodes)
				{
					switch (child.Name)
					{
						case "primary":		color.Primary   = _ColorFromXml(child); break;
						case "secondary":	color.Secondary = _ColorFromXml(child); break;
						default:
							throw new Exception("INVALID COLOR TABLE FILE!");
					}
				}
				return color;
			}

			private Color()
			{ }

			private static M.Color _ColorFromXml(XmlElement node)
			{
				if (node.Attributes.Count != 4)
					throw new Exception("INVALID COLOR TABLE FILE!");

				M.Color color = new M.Color();
				foreach (XmlAttribute attr in node.Attributes)
				{
					byte val;
					if (!byte.TryParse(attr.Value, out val))
						throw new Exception("INVALID COLOR TABLE FILE!");

					switch (attr.Name)
					{
						case "r": color.R = val; break;
						case "g": color.G = val; break;
						case "b": color.B = val; break;
						case "a": color.A = val; break;
						default:
							throw new Exception("INVALID COLOR TABLE FILE!");
					}
				}

				return color;
			}
		}

	}

}