using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Threading;
using System.Xml;


namespace CoreLib
{
	/// <summary>
	/// <para>
	/// Downloads a version xml from URL given and allows for checking against version info given,
	/// returning either a download URL or null if no update is avail resp. an error occurred.
	/// </para>
	/// <para>
	/// Format of this xml is expected as follows:
	/// <code>
	/// &lt;?xml version="1.0" encoding="utf-8"?&gt;
	/// &lt;version-info&gt;
	///   &lt;latest title="appname" version="MM.mm{.bbbbb{.rrrrrr}}{{ }label}"&gt;
	///     url
	///   &lt;/latest&gt;
	///   {&lt;changelog&gt;
	///     Changelog text
	///   &lt;/changelog&gt;}
	/// &lt;/version-info&gt;
	/// </code>
	/// with:
	/// <list type="bullet">
	///   <listheader><term>Parameter</term><description>Description</description></listheader>
	///   <item><term>appname</term><description>being the application name (which must match the
	///   one given to <c>Check</c>)</description></item>
	///   <item><term>MM</term><description>The major version</description></item>
	///   <item><term>mm</term><description>The minor version</description></item>
	///   <item><term>bbbbb</term><description>The optional build number</description></item>
	///   <item><term>rrrrrr</term><description>The optional revision number (only valid if build
	///   number is given too)</description></item>
	///   <item><term>label</term><description>An optional label like <c>alpha</c> or <c>beta</c>
	///   to denote special builds</description></item>
	///   <item><term>url</term><description>being the actual download url for your installer or 
	///   update package</description></item>
	/// </list>
	/// (see regular expression <see cref="_version_regex"/> for full details)
	/// 
	/// You can also include a <c>changelog</c> tag which will be returned when using the second 
	/// variant of <c>Check</c> (be sure to use CDATA if your text contains XML markup chars).
	/// As an extension, this tag may contain an attribute <c>lang</c> to allow for a localized
	/// changelog to be returned.
	/// </para>
	/// </summary>
	public class UpdateChecker
	{

		public UpdateChecker(string url, ICallback callback, Logger logger = null)
		{
			_Configure(new Uri(url), callback, logger);
		}

		public UpdateChecker(Uri url, ICallback callback, Logger logger = null)
		{
			_Configure(url, callback, logger);
		}


		~UpdateChecker()
		{
			Close();
		}


		// Close down
		public void Close()
		{
			if (_Client == null)
				return;
			HttpClient client = _Client;
			_Client = null;

			_Log("Closing down update checker instance ...");

			_Debug("Cancelling pending requests");
			client.CancelPendingRequests();
			_Debug("Closing HttpClient");
			client.Dispose();

			_Callback = null;

			_Log("... update checker instance down");
			_Logger = null;
		}


		// Download version info xml and check against parameters passed,
		// returning either dowload url or null in no update avail or error occurred
		public string Check(string app_name, string app_version)
		{
			string changelog;
			return Check(app_name, app_version, out changelog);
		}

		public string Check(string app_name, string app_version, out string changelog)
		{
			changelog = null;

			if (_Client == null)
				throw new Exception("Uploader not configured");

			if (string.IsNullOrEmpty(app_name))
				throw new ArgumentException("Invalid name passed", "app_name");

			string label_given;
			long? vers_given = _VersionAsLong(app_version, out label_given);
			if (vers_given == null)
				throw new ArgumentException("Invalid version passed", "app_version");

			_Log("Downloading version info ...");
			string response = null;
			using (MemoryStream strm = new MemoryStream())
			{
				Downloader downloader = new Downloader(_Url, _Callback, _Logger);
				if (!downloader.Download(strm))
				{
					_Error("Downloading version info xml failed!");
					return null;
				}
				_Log("... success");

				byte[] content = strm.GetBuffer();
				// Skip UTF8-BOM if present
				int ofs = (content.Length > 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF) ? 3 : 0;
				int len = content.Length - ofs;
				response = Encoding.UTF8.GetString(content, ofs, len);
			}

			XmlDocument xml = new XmlDocument();
			try
			{
				xml.LoadXml(response);
			}
			catch (Exception exc)
			{
				_Error("Error parsing response!", exc);
				return null;
			}

			if (xml.ChildNodes.Count < 1)
			{
				_Error("Invalid response received! (not enough nodes)");
				return null;
			}

			XmlNode root = xml.FirstChild;
			if (root is XmlDeclaration)// Skip declaration
				root = root.NextSibling;
			if (root.Name != "version-info" || root.ChildNodes.Count < 1)
			{
				_Error("Invalid response received! (missing tag 'version-info')");
				return null;
			}

			XmlNode node = root.FirstChild;
			if (node == null || node.Name != "latest")
			{
				_Error("Invalid response received! (missing tag 'latest')");
				return null;
			}

			string found_name, found_vers;
			try
			{
				found_name = node.Attributes["title"].InnerText;
				found_vers = node.Attributes["version"].InnerText;
			}
			catch
			{
				_Error("Invalid response received! (missing attributes)");
				return null;
			}

			if (found_name != app_name)
			{
				_Error("Version info does not match application name given!");
				return null;
			}

			string label_found;
			long? vers_found = _VersionAsLong(found_vers, out label_found);
			if (vers_found == null)
			{
				_Error("Invalid response received! (unable to parse 'version')");
				return null;
			}

			if (vers_found <= vers_given && label_found == label_given)
			{
				_Log("No new version found");
				return null;
			}

			// Remove any control chars from url, incl. whitespaces
			string dl_url = node.InnerText.Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ').Replace(" ", "");

			node = node.NextSibling;
			if (node != null)
			{
				if (node.Name == "changelog")
				{
					if (node.Attributes["lang"] != null)
					{
						// Localized change logs avail, try to pick suitable one
						string langid = null;
						if (LanguageHandler.LANG != null)
							langid = LanguageHandler.LANG._langid;
						if (langid == null)
							langid = Thread.CurrentThread.CurrentUICulture.Name;
						changelog = _GetLocalizedChangeLog(node, langid);
					}
					if (changelog == null)
					{
						// Fallback to old behaviour with just picking first avail
						if (node.HasChildNodes && node.FirstChild is XmlCDataSection)
							node = node.FirstChild;
						changelog = node.Value;
					}
				}
			}

			return dl_url;
		}


		// Convert version string to a comparable long using pattern MMmmbbbbbrrrrrr
		private long? _VersionAsLong(string version, out string label)
		{
			label = null;

			if (string.IsNullOrEmpty(version))
				return null;

			Match m = _version_regex.Match(version);
			if (!m.Success)
				return null;

			Func<string,long> num = (s) => {
				long l = 0;
				if (m.Groups[s].Success)
					if (!long.TryParse(m.Groups[s].Value, out l))
						l = 0;
				return l;
			};

			label = m.Groups["label"].Value;
			return (((((num("major") * 100) + num("minor")) * 100000) + num("build")) * 1000000) + num("rev");
		}

		// Try to get localized changelog for language id given, returning null if not avail
		private string _GetLocalizedChangeLog(XmlNode node, string langid)
		{
			while (node != null)
			{
				if (node.Attributes["lang"].Value == langid)
				{
					if (node.HasChildNodes && node.FirstChild is XmlCDataSection)
						node = node.FirstChild;
					return node.Value;
				}
				node = node.NextSibling;
			}

			return null;
		}


		// Config
		private void _Configure(Uri url, ICallback callback, Logger logger = null)
		{
			if (url == null)
				throw new ArgumentNullException("url");

			_Callback = callback;
			_Logger = logger;

			_Log("Configuring ...");

			if (_Client != null)
			{
				_Client.CancelPendingRequests();
				_Client.Dispose();
			}
			_Client = new HttpClient();

			_Url = url;

			_Log("... configured");
		}


		private void _Debug(string msg, params object[] args)
		{
			_Log(Logger.Level.Debug, msg, args);
		}

		private void _Error(string msg, params object[] args)
		{
			_Log(Logger.Level.Error, msg, args);
		}

		private void _Log(string msg, params object[] args)
		{
			_Log(Logger.Level.Info, msg, args);
		}

		private void _Log(Logger.Level level, string msg, params object[] args)
		{
			if (_Logger != null)
			{
				// Just in case logger instance was cleaned up already
				try
				{
					string m = string.Format("[UpdateChecker] " + msg, args);
					_Logger.Log(m, level);
				}
				catch { }
			}
		}


		private HttpClient _Client;
		private Uri        _Url;
		private ICallback  _Callback;
		private Logger     _Logger;

#if DEBUG
		private const Logger.Level LOGLEVEL = Logger.Level.Debug;
#else
		private const Logger.Level LOGLEVEL = Logger.Level.Info;
#endif

		private static Regex _version_regex = new Regex(
			@"(?<major>\d{1,2})\.(?<minor>\d{1,2})(\.(?<build>\d{1,5})(\.(?<rev>\d{1,6}))?)?(\s*(?<label>\w+))?", 
			RegexOptions.Compiled|RegexOptions.CultureInvariant|RegexOptions.ExplicitCapture|RegexOptions.Singleline);

	}


}
