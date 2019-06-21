using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CoreLib;
using CoreLib.PubSub;
//using CoreLib.Logger;


using Savegame;
using Savegame.Properties;


namespace TestBed
{
    class Program
    {
		#region PubSub

		public class TestCallback 
			: Callback<TestCallback.StartData, TestCallback.UpdateData, TestCallback.StopData>
		{
			public class StartData 
				: EventData
			{
				int MaxVal;
				string Status;
				string Info;

				// IEventData
				override public EventData Set(object[] data)
				{
					if (data.Length != 3)
						throw new ArgumentException();

					MaxVal = (int)data[0];
					Status = (string)data[1];
					Info = (string)data[2];

					return this;
				}

				override public object[] Get()
				{
					return new object[] { MaxVal, Status, Info };
				}


				override public string ToString()
				{
					return string.Format("[ MaxVal:{0}, Status:'{1}', Info:'{2}' ]",
						MaxVal, Status, Info);
				}
			}

			public class UpdateData 
				: EventData
			{
				int CurrVal;
				string Status;
				string Info;

				// IEventData
				override public EventData Set(object[] data)
				{
					if (data.Length != 3)
						throw new ArgumentException();

					CurrVal = (int)data[0];
					Status = (string)data[1];
					Info = (string)data[2];

					return this;
				}

				override public object[] Get()
				{
					return new object[] { CurrVal, Status, Info };
				}


				override public string ToString()
				{
					return string.Format("[ CurrVal:{0}, Status:'{1}', Info:'{2}' ]",
						CurrVal, Status, Info);
				}
			}

			public class StopData 
				: EventData
			{
				string Status;
				string Info;

				// IEventData
				override public EventData Set(object[] data)
				{
					if (data.Length != 2)
						throw new ArgumentException();

					Status = (string)data[0];
					Info = (string)data[1];

					return this;
				}

				override public object[] Get()
				{
					return new object[] { Status, Info };
				}


				override public string ToString()
				{
					return string.Format("[ Status:'{0}', Info:'{1}' ]",
						Status, Info);
				}
			}

		}

		public class TestListener
		{
			public void TestOnStart(Publisher sender, TestCallback.StartData data)
			{
				Console.WriteLine("LISTENER: START : Sender:" + sender + " Data:" + data);
			}

			public void TestOnUpdate(Publisher sender, TestCallback.UpdateData data)
			{
				Console.WriteLine("LISTENER: UPDATE: Sender:" + sender + " Data:" + data);
			}

			public void TestOnStop(Publisher sender, TestCallback.StopData data)
			{
				Console.WriteLine("LISTENER: STOP  : Sender:" + sender + " Data:" + data);
			}
		}

		public static void TestOnStart(Publisher sender, TestCallback.StartData data)
		{
		//	/*Console.WriteLine*/Log.Info("GLOBAL  : START : Sender:" + sender + " Data:" + data);
		}

		public static void TestOnUpdate(Publisher sender, TestCallback.UpdateData data)
		{
		//	/*Console.WriteLine*/Log.Info("GLOBAL  : UPDATE: Sender:" + sender + " Data:" + data);
		}

		public static void TestOnStop(Publisher sender, TestCallback.StopData data)
		{
		//	/*Console.WriteLine*/Log.Info("GLOBAL  : STOP  : Sender:" + sender + " Data:" + data);
		}

		static void TestPubSub()
		{
			//Initialize pub class object
			Publisher p = new Publisher();

			TestCallback cb = new TestCallback();
			TestListener l = new TestListener();

			//cb.OnStart.Subscribe(TestOnStart);
			cb.OnStart.Subscribe((sender, data) => l.TestOnStart(sender, data));
			cb.OnUpdate.Subscribe(l.TestOnUpdate);
			cb.OnStop.Subscribe(l.TestOnStop);

			cb.OnStart.Subscribe(TestOnStart);
			cb.OnUpdate.Subscribe(TestOnUpdate);
			cb.OnStop.Subscribe(TestOnStop);

			//p.Raise(cb.Start, new EventData());
			cb.Start(new TestCallback.StartData());

			// Line below should show an type error
			//cb.Update(new TestCallback.StartData());
		}

		#endregion

		#region Logger

		static Logger _l;
		static void StartLogger()
		{
			string path = @"E:\GitHub\satisfactory-save-repairer-ng";
			/*TODO:
			path = Process.GetCurrentProcess().StartInfo.WorkingDirectory;
			if (path == "" || !Directory.Exists(path))
			{
				path = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
			}
			if (path == "" || !Directory.Exists(path))
			{
				path = @"E:\GitHub\satisfactory-save-repairer-ng";
			}*/
			path = Path.Combine(path, "logs");

			_l = new Logger(path, "TestBed", Logger.Level.Debug);
		}

		static void StopLogger()
		{
			_l.Shutdown();
			_l = null;
		}

		static void TestLogger()
		{
			_l.Debug("Testing .Debug");
			_l.Error("Testing .Error");
			_l.Info("Testing .Info");
			_l.Log("Testing .Log");

			Trace.Write("Testing Trace.Write");
			Trace.WriteLine("Testing Trace.WriteLine");

			Debug.Write("Testing Debug.Write");
			Debug.WriteLine("Testing Debug.WriteLine");

			Trace.Fail("Testing Trace.Fail");
			Trace.Fail("Testing Trace.Fail with details", "Some details here");

			Trace.TraceError("Testing Trace.TraceError");
			Trace.TraceInformation("Testing Trace.TraceInformation");
			Trace.TraceWarning("Testing Trace.TraceWarning");

			Debug.Print("Testing Debug.Print");
		}

		#endregion

		#region Savegame

		static void TestSavegame()
		{
			Property prop = PropertyFactory.Construct("ValueProperty", null);//, null, 0, 0, null);
			Trace.Write("Discovered: " + prop.TypeName + " = " + prop);

			var published = Publish.Retrieve(prop);
			Trace.Write("This property published the values following:");
			foreach (var mi in published)
			{
				Trace.Write("- " + mi.Key + ":" + mi.Value.MemberType + ":" + mi.Value);
			}

			var keys = prop.GetKeys();
			Trace.Write("This property published the properties following:");
			foreach (var k in keys)
			{
				Trace.Write("- " + k);
			}

			var childs = prop.GetChilds();
			Trace.Write("This property published the childs following:");
			foreach (var c in childs)
			{
				Trace.Write("- " + c);
			}
		}

		static void TestSavegameLoad()
		{
			Trace.Write("Trying to load savegame");

			//Initialize pub class object
			Publisher p = new Publisher();
			TestCallback cb = new TestCallback();
			cb.OnStart.Subscribe(TestOnStart);
			cb.OnUpdate.Subscribe(TestOnUpdate);
			cb.OnStop.Subscribe(TestOnStop);

			string filename = @"E:\GitHub\satisfactory-save-repairer-ng\TestBed\NF-Start.sav";
			//string filename = @"C:\Users\SillyBits\AppData\Local\FactoryGame\Saved\SaveGames\testfile2.sav";

			Savegame.Savegame sg = new Savegame.Savegame(filename);

			sg.Load(cb);

			Trace.Write("Finished loading savegame");
		}

		#endregion

		#region ConfigFile

		static void TestConfigFile()
		{
			string path = @"E:\GitHub\satisfactory-save-repairer-ng";
			/*TODO:
			path = Process.GetCurrentProcess().StartInfo.WorkingDirectory;
			if (path == "" || !Directory.Exists(path))
			{
				path = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
			}
			if (path == "" || !Directory.Exists(path))
			{
				path = @"E:\GitHub\satisfactory-save-repairer-ng";
			}*/
			path = Path.Combine(path, "TestBed");

			Trace.Write(string.Format("Opening config file for '{0}' at '{1}'", "TestBed", path));

			dynamic _c = new ConfigFile(path, "TestBed");

			// Read tests
			#if false
			object content;

			Trace.Write("Trying to read: _c.core");
			content = _c.core;
			Trace.Write(content);

			Trace.Write("Trying to read: _c.core.defaultpath");
			content = _c.core.defaultpath;
			Trace.Write(content);

			Trace.Write("Trying to read: _c.core.default_path ... not existing and should throw an error!");
			try
			{
				content = _c.core.default_path;
				Trace.Write(content);
			}
			catch
			{
				Trace.Write("Exception catched, good");
			}
			#endif

			// Write tests
			#if false
			object content_w = "Some test value";

			Trace.Write(string.Format("Trying to write: _c.core = '{0}' ... should fail, sections are read-only", content_w));
			try
			{
				_c.core = content_w;
				Trace.Write("Darn, expected an exception!");
			}
			catch
			{
				Trace.Write("Exception catched, good");
			}

			Trace.Write(string.Format("Trying to write: _c.core.defaultpath = '{0}'", content_w));
			_c.core.defaultpath = content_w;

			Trace.Write(string.Format("Trying to write: _c.core.default_path = '{0}' ... not existing and should throw an error!", content_w));
			try
			{
				_c.core.default_path = content_w;
				Trace.Write("Darn, expected an exception!);
			}
			catch
			{
				Trace.Write("Exception catched, good");
			}
			#endif

			// More r/w tests
			#if true
			string p = Config.Root.core.defaultpath;
			Trace.Write(string.Format("Config.Root.core.defaultpath: {0}", p));

			var content_r = Config.Root.mru;
			p = content_r.ToString();
			Trace.Write(string.Format("Config.Root.mru: {0}", p));

			content_r = Config.Root.mru.files;
			p = content_r.ToString();
			Trace.Write(string.Format("Config.Root.mru.files: {0}", p));

			p = Config.Root.mru.files[0];//.Get(0);
			Trace.Write(string.Format("Config.Root.mru.files[0]: {0}", p));


			Config.Root.mru.files[0] = @"D:\";
			//Config.Root.mru.files.Set(0, @"D:\");

			Config.Root.mru.files.Add(@"C:\");
			#endif

			// Sadfully, below does NOT work :(
			//string p = Config.core.defaultpath;
			//Trace.Write(string.Format("Config.Root.core.defaultpath: {0}", p));

	
			Trace.Write("Shutting down config file");
			_c.Shutdown();

			Trace.Write("Done testing config file");
		}

#endregion



		static void TestBed(string[] args)
		{
			//TestPubSub();
			//TestLogger();
			//TestSavegame();
			//TestSavegameLoader();
			TestConfigFile();
		}


		static void Main(string[] args)
		{
			// Now that logger was tested we can actually use it here :D
			StartLogger();

			TestBed(args);

			StopLogger();

			//Process p = System.Diagnostics.Process.GetCurrentProcess();
			//p.Refresh();

			//Console.WriteLine("Press enter to terminate!");
			//Console.ReadKey();
		}
	}
}
