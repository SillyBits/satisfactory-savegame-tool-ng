using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Xml;


namespace CoreLib
{
	/// <summary>
	/// Downloads a file from URL given into stream passed.
	/// </summary>
	public class Downloader
	{

		public Downloader(string url, ICallback callback, Logger logger = null)
		{
			_Configure(new Uri(url), callback, logger);
		}

		public Downloader(Uri url, ICallback callback, Logger logger = null)
		{
			_Configure(url, callback, logger);
		}


		~Downloader()
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

			_Log("Closing down downloader instance ...");

			_Debug("Cancelling pending requests");
			client.CancelPendingRequests();
			_Debug("Closing HttpClient");
			client.Dispose();

			_Callback = null;

			_Log("... downloader instance down");
			_Logger = null;
		}


		// Download into stream passed
		public bool Download(Stream out_stream)
		{
			if (_Client == null)
				throw new Exception("Downloader not configured");

			if (out_stream == null || !out_stream.CanWrite)
				throw new ArgumentException("Invalid output stream passed", "out_stream");

			Task<HttpResponseMessage> post = null;
			HttpResponseMessage result = null;
			Task response = null;
			long counter = 0;

			try
			{
				_Debug("Sending request ...");
				post = _Client.GetAsync(_Url, HttpCompletionOption.ResponseContentRead);
				while (!post.IsCompleted)
				{
					if (_Callback != null)
						_Callback.Update(++counter, null, null);
					if (Dispatcher.CurrentDispatcher != null)
						Helpers.TriggerDispatcher(Dispatcher.CurrentDispatcher);
					post.Wait(10);
				}

				result = post.Result;
				if (!result.IsSuccessStatusCode)
				{
					_Error("... failed -> {0} | {1}", 
						result.StatusCode, result.ReasonPhrase);
					return false;
				}
				_Log("... success");

				response = result.Content.CopyToAsync(out_stream);
				while (!response.IsCompleted)
				{
					if (_Callback != null)
						_Callback.Update(out_stream.Length, null, null);
					if (Dispatcher.CurrentDispatcher != null)
						Helpers.TriggerDispatcher(Dispatcher.CurrentDispatcher);
					response.Wait(50);
				}
				out_stream.Flush();
			}
			catch (Exception exc)
			{
				_Error("Error downloading", exc);
				return false;
			}
			finally
			{
				if (post != null)
					post.Dispose();
				if (result != null)
					result.Dispose();
				if (response != null)
					response.Dispose();
			}

			return true;
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

			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

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
					string m = string.Format("[Downloader] " + msg, args);
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

	}


}
