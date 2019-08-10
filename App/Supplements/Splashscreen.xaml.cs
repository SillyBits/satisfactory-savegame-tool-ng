using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;


using CoreLib;
using CoreLib.PubSub;


namespace SatisfactorySavegameTool.Supplements
{
	/// <summary>
	/// Interaction logic for Splashscreen.xaml
	/// </summary>
	public partial class Splashscreen : Window
	{
		public static void ShowSplash(string message = null)
		{
			_Instance = new Splashscreen();
			_Instance.Show();

			if (message != null)
				SetMessage(message);
		}

		public static void HideSplash()
		{
			_Instance.Close();
			_Instance = null;
		}

		public static void SetMessage(string message)
		{
			_Instance.Update(message, "", "");
			_Instance.Refresh();
		}

		public static ICallback Callback { get { return _Instance._callback; } }


		private Splashscreen()
		{
			InitializeComponent();

			background.Source = new BitmapImage(Helpers.GetResourceUri("Splashscreen.png"));

			title.Content = Settings.APPTITLE + " " + Settings.APPVERSION;

			var cr = Assembly.GetCallingAssembly().GetCustomAttribute<AssemblyCopyrightAttribute>();
			copyright.Content = cr.Copyright;

			_message = "";
			message.Content = _message;


			// Bind our events
			_publisher = new Publisher(SynchronizationContext.Current);
			//_callback = new Callback();
			_callback = new DefaultCallbackImpl();

			_callback.OnStart.Subscribe(OnStart);
			_callback.OnUpdate.Subscribe(OnUpdate);
			_callback.OnStop.Subscribe(OnStop);
			_callback.OnDestroy.Subscribe(OnDestroy);
		}


		private void Update(string message, string status, string info)
		{
			string msg;
			if (!string.IsNullOrEmpty(message))
				_message = message;
			msg = _message;

			if (!string.IsNullOrEmpty(status))
				_status = status;
			if (_status != null)
				msg += " : " + _status;

			if (!string.IsNullOrEmpty(info))
				msg += " - " + info;

			if (!msg.Equals(this.message.Content))
			{
				this.message.Content = msg;
				//_Instance.Refresh();
			}
		}

		private void OnStart(Publisher sender, DefaultCallbackImpl.StartData data)
		{
			Dispatcher.Invoke/*Async*/(() => {
				Update(null, data.Status, data.Info);
			});
		}

		private void OnUpdate(Publisher sender, DefaultCallbackImpl.UpdateData data)
		{
			Dispatcher.Invoke/*Async*/(() => {
				Update(null, data.Status, data.Info);
			});
		}

		private void OnStop(Publisher sender, DefaultCallbackImpl.StopData data)
		{
			Dispatcher.Invoke/*Async*/(() => {
				Update(null, data.Status, data.Info);
			});
		}

		private void OnDestroy(Publisher sender, DefaultCallbackImpl.DestroyData data)
		{
			//Dispatcher.Invoke/*Async*/(() => {
			//	Hide();
			//	Close();
			//});
		}


		private string _message;
		private string _status;
		private Publisher _publisher;
		private DefaultCallbackImpl _callback;
	
		private static Splashscreen _Instance;

	}
}
