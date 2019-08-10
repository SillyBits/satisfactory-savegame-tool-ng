using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;


namespace SatisfactorySavegameTool.Supplements
{
	public class VersionTable
	{
		public static VersionTable INSTANCE;

		public VersionTable()
		{
			_versions = new List<VersionEntry>();

			_Load();

			INSTANCE = this;
		}


		public VersionEntry Find(int buildno)
		{
			return _versions.Find(v => v._cl == buildno);
		}


		private void _Load()
		{
			string filename = Path.Combine(Settings.RESOURCEPATH, "VersionTable.xml");

			XmlDocument xml = new XmlDocument();
			try
			{
				xml.Load(filename);
			}
			catch (Exception exc)
			{
				string msg = string.Format("Error loading version table {0}:\n", filename)
							+ exc.Message.ToString() + "\n"
							+ exc.Source.ToString() + "\n"
							+ exc.StackTrace.ToString() + "\n"
							;
				Log.Error(msg);
				return;
			}
			if (xml.ChildNodes.Count != 2)
				throw new Exception("INVALID VERSION TABLE FILE!");

			XmlNode node = xml.ChildNodes[0];
			if (node.Name.ToLower() != "xml")
				throw new Exception("INVALID VERSION TABLE FILE!");

			node = xml.ChildNodes[1];
			if (node.Name.ToLower() != "versions")
				throw new Exception("INVALID VERSION TABLE FILE!");


			foreach (XmlNode childnode in node.ChildNodes)
			{
				XmlElement element = childnode as XmlElement;
				if (element == null)
					continue; // Skip comments and such
				if (element.Attributes.Count < 3)
					throw new Exception("INVALID VERSION TABLE FILE!");

				//<version build="95718" type="1" version="0.1" remarks="Update 1" />
				int cl = int.Parse(element.Attributes["build"].InnerText);
				int type = int.Parse(element.Attributes["type"].InnerText);
				string version = element.Attributes["version"].InnerText;
				string remarks = element.HasAttribute("remarks") ? element.Attributes["remarks"].InnerText : null;

				VersionEntry v = new VersionEntry(cl, type, version, remarks);

				_versions.Add(v);				
			}

		}

		private List<VersionEntry> _versions;


		public enum Version {
			Unknown      = -1,
			Experimental = 0,
			EarlyAccess  = 1,
		}

		public class VersionEntry
		{
			public Version Release { get { return _release; } }


			// release: 0=Experimental, 1=Early Access
			internal VersionEntry(int cl, int release, string version_num, string remarks = null)
			{
				_cl = cl;
				_release = (Version) release;
				_version = version_num;
				_remarks = remarks;
			}

			public override string ToString()
			{
				// Early Access Release - v0.1, Update 1 - CL 95718
				string s = (_release == Version.EarlyAccess) ? "Early Access" : "Experimental";
				s += " - v" + _version;
				if (_remarks != null)
					s += ", " + _remarks;
				s += " - CL #" + _cl;
				return s;
			}

			internal int _cl;
			internal Version _release;
			internal string _version, _remarks;
		}

	}

}