using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace CoreLib
{

	public class Uploader
	{

		public Uploader(string url, string user, string pass, string[] agent, string log = null)
		{
			_Configure(new Uri(url), _ToBase64(user, pass), agent, log);
		}

		public Uploader(Uri url, string user, string pass, string[] agent, string log = null)
		{
			_Configure(url, _ToBase64(user, pass), agent, log);
		}

		public Uploader(string url, string login, string[] agent, string log = null)
		{
			_Configure(new Uri(url), login, agent, log);
		}

		public Uploader(Uri url, string login, string[] agent, string log = null)
		{
			_Configure(url, login, agent, log);
		}


		~Uploader()
		{
			Close();
		}


		// Close down
		public void Close()
		{
			if (_Client == null)
				return;

			_Log("Closing down uploader instance ...");

			Stop();

			if (_Client != null)
			{
				_Debug("Canceling pending requests");
				_Client.CancelPendingRequests();
				_Debug("Closing HttpClient");
				_Client.Dispose();
			}
			_Client = null;

			_Log("... uploader instance down");
			_Logger.Shutdown();
			_Logger = null;
		}


		// Start/stop background uploader
		public void Start()
		{
			if (_Client == null)
				throw new Exception("Uploader not configured");

			if (_WorkerThread != null)
			{
				_Debug("Uploader started already");
				return;
			}

			_Log("Setting up background worker ...");

			_WorkerThread = new Thread(_WorkerThreadFunc);
			_WorkerThread.Name = "Uploader.WorkerThread";
			_WorkerThread.IsBackground = true;

			//_IsWorkerActive = false;
			_WorkerThreadStopped = false;
			_WorkerWakeup = new ManualResetEvent(false);
			_WorkerCancelWork = new ManualResetEvent(false);

			_Log("... done setting up");

			_Log("Starting background worker");
			_WorkerThread.Start();
		}

		public void Stop()
		{
			if (_WorkerThread == null)
			{
				_Debug("Uploader stopped already");
				return;
			}

			_Log("Stopping background worker ...");

			if (_WorkerThread.IsAlive)
			{
				_WorkerThreadStopped = true;
				//if (force)
				//	_WorkerThread.Abort();
				//_WorkerThread.Interrupt();
				_WorkerCancelWork.Set();
				_WorkerWakeup.Set();

				//while (_WorkerThread.ThreadState == System.Threading.ThreadState.Running)
				while (_WorkerThread.IsAlive)
					Thread.Yield();
			}

			_WorkerThread = null;

			if (_WorkerWakeup != null)
				_WorkerWakeup.Dispose();
			_WorkerWakeup = null;

			if (_WorkerCancelWork != null)
				_WorkerCancelWork.Dispose();
			_WorkerCancelWork = null;

			_Log("... background worker stopped");
		}


		// Check state
		public bool HasPendingWork
		{
			get
			{
				lock (_UploadQueue)
					return (_UploadQueue.Count > 0);
			}
		}


		// Send
		public void Send(byte[] data)
		{
			if (_Client == null)
				throw new Exception("Uploader not configured");
			if (data == null || data.Length == 0)
				throw new ArgumentException("Invalid parameters", "data");

			_Debug("Enqueueing a {0} of data", data.Length);
			lock (_UploadQueue)
			{
				// Enqueue item and signal there's pending data to be sent
				_UploadQueue.Enqueue(data);
				_WorkerWakeup.Set();
			}
		}


		// Config
		private void _Configure(Uri url, string login, string[] agent, string log = null)
		{
			if (url == null)
				throw new ArgumentNullException("url");
			if (string.IsNullOrEmpty(login))
				throw new ArgumentException("Invalid parameters", "login");
			if (agent == null || agent.Length != 2 || string.IsNullOrEmpty(agent[0]) || string.IsNullOrEmpty(agent[1]))
				throw new ArgumentException("Invalid parameters", "agent");

			if (!string.IsNullOrEmpty(log))
				_Logger = new Logger(Path.GetDirectoryName(log), Path.GetFileName(log), LOGLEVEL, false);
			else
				_Logger = null;// In case of re-use

			_Log("Configuring ...");

			if (_Client != null)
			{
				_Client.CancelPendingRequests();
				_Client.Dispose();
			}
			_Client = new HttpClient();
			_Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", login);
			_Client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(agent[0], agent[1].Replace(' ','-')));

			_Url = url;

			_UploadQueue = new Queue<byte[]>();

			_Log("... configured");
		}


		private static string _ToBase64(string user, string pass)
		{
			return Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format(user + ":" + pass)));
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
				lock(_Logger)
				{
					string m = string.Format(msg, args);
					_Logger.Log(m, level);
				}
		}


		// Background uploader
		private void _WorkerThreadFunc()
		{
			_Log("[Worker] Up and running");
			//_IsWorkerActive = true;

			while (!_WorkerThreadStopped)
			{
				//if (_WorkerStopWatch != null)
				//	_WorkerStopWatch.Stop();

				// Wait for activation
				_WorkerWakeup.WaitOne();
				_WorkerWakeup.Reset();

				//_WorkerStopWatch = _Stopwatch.StartNew();

				if (_WorkerThreadStopped)
				{
					_Debug("[Worker] Received STOP");
					break;
				}

				_Debug("[Worker] Peeking for data avail ...");
				byte[] data = null;
				lock (_UploadQueue)
				{
					if (_UploadQueue.Count > 0)
						data = _UploadQueue.Peek();
				}
				if (data == null)
				{
					_Debug("[Worker] ... none avail, going back to sleep");
					continue;// Cease work for now
				}
				if (data.Length == 0)
				{
					_Debug("[Worker] ... skipping empty data, going back to sleep");
					continue;// Cease work for now
				}
				_Log("[Worker] Trying to send a {0} of data", data.Length);

				HttpContent content = null;
				Task<HttpResponseMessage> post = null;
				HttpResponseMessage result = null;
				Task<string> response = null;
				try
				{
					while (true)
					{
						content = new ByteArrayContent(data);

						_Debug("[Worker] POSTing ...");
						post = _Client.PostAsync(_Url, content);
						while (!post.IsCompleted)
						{
							post.Wait(100);
							if (_WorkerThreadStopped || _WorkerCancelWork.WaitOne(0))
								break;
						}
						if (_WorkerThreadStopped || _WorkerCancelWork.WaitOne(0))
						{
							_Debug("[Worker] Received STOP in middle of operation (Post)");
							break;
						}

						result = post.Result;
#if DEBUG
						StringBuilder sb = new StringBuilder();
						sb.Append("[Worker] ... result header:");
						foreach (var h in result.Headers)
						{
							string vals;
							if (h.Value != null)
								vals = "'" + string.Join("' | '", h.Value) + "'";
							else
								vals = "n/a";
							sb.AppendFormat("\n- {0}: {1}", h.Key, vals);
						}
						_Debug(sb.ToString());
#endif

						response = result.Content.ReadAsStringAsync();
						while (!response.IsCompleted)
						{
							response.Wait(100);
							if (_WorkerThreadStopped || _WorkerCancelWork.WaitOne(0))
								break;
						}
						if (_WorkerThreadStopped || _WorkerCancelWork.WaitOne(0))
						{
							_Debug("[Worker] Received STOP in middle of operation (Response)");
							break;
						}

						if (!result.IsSuccessStatusCode)
						{
							_Error("[Worker] ... failed -> {0} | {1}", 
								result.StatusCode, response.Result);
							//TODO: What to do in case of failure?
						}
						else
						{
							// Done our job w/o any issues
#if DEBUG
							_Debug("[Worker] ... success -> {0} | {1}", 
								result.StatusCode, response.Result);
#else
							_Log("[Worker] ... success");
#endif
							lock (_UploadQueue)
								_UploadQueue.Dequeue();
						}
						break;
					}

					if (_WorkerThreadStopped || _WorkerCancelWork.WaitOne(0))
					{
						_Debug("[Worker] Received STOP in middle of operation, killing pending requests");
						_Client.CancelPendingRequests();
					}

				}
				catch (Exception exc)
				{
					_Error("[Worker] ... failed with exception!", exc);
					//TODO: What to do in case of failure?
					_Client.CancelPendingRequests();
				}
				finally
				{
					// Cleanup
					if (post != null)
						post.Dispose();
					if (result != null)
						result.Dispose();
					if (response != null)
						response.Dispose();
					if (content != null)
						content.Dispose();
				}

				_Debug("[Worker] Going back to sleep");
			}

			if (_WorkerThreadStopped || _WorkerCancelWork.WaitOne(0))
				_Client.CancelPendingRequests();
			if (_WorkerCancelWork.WaitOne(0))
				_WorkerCancelWork.Reset();


			//if (_WorkerStopWatch != null)
			//	_WorkerStopWatch.Stop();

			// End of thread
			_Log("[Worker] Is down");
			//_IsWorkerActive = false;
		}


		private Logger _Logger;

		private HttpClient _Client;
		
		private Uri _Url;

		private Queue<byte[]> _UploadQueue;

		//private bool _IsWorkerActive;
		private Thread _WorkerThread;
		private volatile bool _WorkerThreadStopped;
		private ManualResetEvent _WorkerWakeup;
		private ManualResetEvent _WorkerCancelWork;
		//private Stopwatch _WorkerStopWatch;

#if DEBUG
		private const Logger.Level LOGLEVEL = Logger.Level.Debug;
#else
		private const Logger.Level LOGLEVEL = Logger.Level.Info;
#endif

	}


}
