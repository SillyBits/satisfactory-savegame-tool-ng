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
			: this(path, langid, new string[] { translationfilename })
		{
		}

		public LanguageHandler(string path, string langid = null, string[] translationfilenames = null)
		{
			_path = path;

			if (translationfilenames != null)
				_langfiles = translationfilenames;

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

		public void SelectLanguage(string langid)
		{
			_LoadTranslations(langid);
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
			string t = _translations[id];
			if (t.Contains("\\t"))
				t = t.Replace("\\t", "\t");
			if (t.Contains("\\n"))
				t = t.Replace("\\n", "\n");
			return t;
		}

		internal string _path = null;
		internal string[] _langfiles = { "lang.dat" };
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
				foreach (string filename in _langfiles)
				{
					if (File.Exists(Path.Combine(dir, filename)))
					{
						// At least on translation file found, so language is valid
						string langid = Path.GetFileName(dir);
						Log.Info("- adding '{0}'", langid);
						_languages.Add(langid);
						break;
					}
				}
			}
			Log.Info("... found a total of {0} languages", _languages.Count);
		}

		internal void _LoadTranslations(string langid)
		{
			Log.Info("Loading translations for language '{0}' ...", langid);

			if (_languages == null || !_languages.Contains(langid))
				return;

			_langid = langid;
			_translations = new Dictionary<string, string>();

			foreach (string filename in _langfiles)
			{
				string filepath = Path.Combine(_path, _langid, filename);
				if (File.Exists(filepath))
					_LoadTranslationFile(filepath);
				else
					Log.Warning("Translation missing: '{0}\\{1}'", _langid, filename);
			}

			Log.Info("... loaded a total of {0} translations", _translations.Count);
		}

		internal void _LoadTranslationFile(string filename)
		{
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
				return;
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

			string version = null;
			string langid = null;
			foreach (XmlAttribute attr in node.Attributes)
			{
				switch (attr.Name)
				{
					case "version": version = attr.InnerText; break;
					case "langid": langid = attr.InnerText; break;
				}
			}
			if (version == null || langid != _langid)
				throw new Exception("INVALID LANGUAGE FILE!");

			int added = 0;
			foreach (XmlNode childnode in node.ChildNodes)
			{
				XmlElement element = childnode as XmlElement;
				if (element == null)
					continue; // Skip comments and such
				_translations.Add(element.Name, element.InnerText);
				++added;
			}

			Log.Info("... loaded an additional {0} translations", added);
		}

	}

}


// To allow for easy access
public class Translate
{
	public static string _(string id)
	{
		return L.Translate(id);
	}

	public static bool Has(string id)
	{
		return L.HasTranslation(id);
	}


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

