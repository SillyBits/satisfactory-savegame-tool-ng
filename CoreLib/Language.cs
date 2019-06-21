using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Markup;
using System.Xml;


namespace CoreLib.Language.XAML
{
	// The actual markup extension used to load translations into XAML
	//
	public class Translate : MarkupExtension
	{
		public string Key { get; set; }

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			if (LanguageHandler.LANG == null)
				return "[" + Key + "]";
			return LanguageHandler.LANG.Translate(Key);
		}
	}
}

namespace CoreLib
{

	public class LanguageHandler
	{
		public static LanguageHandler LANG = null;

		public LanguageHandler(string path, string langid = null, string translationfilename = null)
		{
			_path = path;

			if (translationfilename != null)
				_langfile = translationfilename;

			_LoadLanguages();

			if (langid != null)
				SelectLanguage(langid);

			LANG = this;
		}

		~LanguageHandler()
		{
			LANG = null;

			_translations = null;
			_languages = null;
		}

		public IReadOnlyCollection<string> LanguagesAvail { get { return _languages.AsReadOnly(); } }
		public IReadOnlyDictionary<string,string> TranslationsAvail { get { return _translations; } }

		public bool SelectLanguage(string langid)
		{
			return _LoadTranslations(langid);
		}

		public bool HasTranslation(string id)
		{
			if (_translations == null)
				return false;
			return _translations.ContainsKey(id);
		}

		public string Translate(string id)
		{
			if (!HasTranslation(id))
			{
				Log.Warning("Missing translation for id '{0}'", id);
				return "[" + id + "]";
			}
			return _translations[id];
		}


		internal string _path = null;
		internal string _langfile = "lang.dat";
		internal List<string> _languages = null;
		internal string _version = null;
		internal string _langid = null;
		internal Dictionary<string,string> _translations = null;

		internal void _LoadLanguages()
		{
			// Find all language files available
			if (!Directory.Exists(_path))
			{
				Log.Error("Localisation missing! ({0})", _path);
				return;
			}

			Log.Info("Enumerating available languages in '{0}'", _path);
			_languages = new List<string>();
			IEnumerable<string> dirs = Directory.EnumerateDirectories(_path);
			foreach (string dir in dirs)
			{
				if (File.Exists(Path.Combine(dir, _langfile)))
				{
					string langid = Path.GetFileName(dir);
					Log.Info("- adding '{0}'", langid);
					_languages.Add(langid);
				}
			}
			Log.Info("... found a total of {0} languages", _languages.Count);
		}

		internal bool _LoadTranslations(string langid)
		{
			if (_languages == null || !_languages.Contains(langid))
				return false;

			string filename = Path.Combine(_path, langid, _langfile);
			Log.Info("Loading translations from '{0}'", filename);

			XmlDocument xml = new XmlDocument();
			try
			{
				xml.Load(filename);
			}
			catch (Exception exc)
			{
				string msg = "Error loading translation file:\n"
							+ exc.Message.ToString() + "\n"
							+ exc.Source.ToString() + "\n"
							+ exc.StackTrace.ToString() + "\n"
							;
				Log.Error(msg);
				return false;
			}
			if (xml.ChildNodes.Count != 2)
				throw new Exception("INVALID LANGUAGE FILE!");

			XmlNode node = xml.ChildNodes[0];
			if (node.Name.ToLower() != "xml")
				throw new Exception("INVALID LANGUAGE FILE!");

			node = xml.ChildNodes[1];
			if (node.Name.ToLower() != "translation"
				|| node.Attributes == null
				|| node.Attributes.Count < 2)
				throw new Exception("INVALID LANGUAGE FILE!");

			_version = null;
			_langid = null;
			foreach (XmlAttribute attr in node.Attributes)
			{
				switch (attr.Name)
				{
					case "version": _version = attr.InnerText; break;
					case "langid": _langid = attr.InnerText; break;
				}
			}
			if (_version == null || _langid != langid)
				throw new Exception("INVALID LANGUAGE FILE!");

			_translations = new Dictionary<string, string>();
			foreach (XmlNode childnode in node.ChildNodes)
			{
				XmlElement element = childnode as XmlElement;
				if (element == null)
					continue; // Skip comments and such
				_translations.Add(element.Name, element.InnerText);
			}

			Log.Info("... loaded a total of {0} translations", _translations.Count);

			return true;
		}

	}

}


// To allow for easy access
public class Translate
{
	public static string _(string id) { return L.Translate(id); }

	private static CoreLib.LanguageHandler L
	{
		get
		{
			if (CoreLib.LanguageHandler.LANG == null)
				throw new ArgumentNullException("No translations available");
			return CoreLib.LanguageHandler.LANG;
		}
	}

}

