using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;

using CoreLib.PubSub;

namespace SatisfactorySavegameTool.Dialogs
{
	/// <summary>
	/// Interaction logic for ProgressDialog.xaml
	/// </summary>
	public partial class ProgressDialog : Window
	{
		public string CounterFormat { get; set; }
		public int Interval { get; set; }
		public Callback Events { get { return _callback; } }


		public ProgressDialog(Window parent, string title, int interval = 10000)
			: base()
		{
			Owner = parent;
			Title = title;
			Interval = interval;

			InitializeComponent();

			// Bind our events
			_publisher = new Publisher(SynchronizationContext.Current);
			_callback = new Callback();

			_callback.OnStart.Subscribe(OnStart);
			_callback.OnUpdate.Subscribe(OnUpdate);
			_callback.OnStop.Subscribe(OnStop);
			_callback.OnDestroy.Subscribe(OnDestroy);
		}


		protected override void OnClosing(CancelEventArgs e)
		{
			// Unbind events
			_callback.OnStart.Unsubscribe(OnStart);
			_callback.OnUpdate.Unsubscribe(OnUpdate);
			_callback.OnStop.Unsubscribe(OnStop);
			_callback.OnDestroy.Unsubscribe(OnDestroy);

			_callback = null;
			_publisher = null;

			base.OnClosing(e);
		}

		private Publisher _publisher;
		private Callback _callback;
		private int _max_val;
		private int _last_val;

		private void Update(int value, string status, string info)
		{
			Progress.Value = value;
			Counts.Text = string.Format(CounterFormat, value, _max_val);

			if (!string.IsNullOrEmpty(status))
				Status.Text = status;

			if (!string.IsNullOrEmpty(info))
				Info.Text = info;
		}

		private void OnStart(Publisher sender, Callback.StartData data)
		{
			Dispatcher.InvokeAsync(() => {
				_last_val = 0;
				_max_val = data.MaxVal;
				Progress.Value = 0;
				Progress.Maximum = _max_val;
				Update(0, data.Status, data.Info);
				Show();
			});
		}

		private void OnUpdate(Publisher sender, Callback.UpdateData data)
		{
			if (data.CurrVal >= _last_val + Interval)
			{
				_last_val = data.CurrVal;
				Dispatcher.InvokeAsync(() => {
					Update(data.CurrVal, data.Status, data.Info);
				});
			}
		}

		private void OnStop(Publisher sender, Callback.StopData data)
		{
			Dispatcher.InvokeAsync(() => {
				Update((int)Progress.Maximum, data.Status, data.Info);
				Hide();
			});
		}

		private void OnDestroy(Publisher sender, Callback.DestroyData data)
		{
			Dispatcher.InvokeAsync(() => {
				Hide();
				Close();
			});
		}

		public class Callback : CoreLib.Callback<Callback.StartData, Callback.UpdateData, Callback.StopData>
		{
			public Event<DestroyData> OnDestroy = new Event<DestroyData>();
			public void Destroy(params object[] data)
			{
				_publisher.Raise(OnDestroy, data);
			}
			public void Destroy(EventArgs data)
			{
				_publisher.Raise(OnDestroy, data);
			}
			public void Destroy(DestroyData data)
			{
				_publisher.Raise(OnDestroy, data);
			}


			#region Event data classes

			public class StartData 
				: EventData
			{
				public int MaxVal;
				public string Status;
				public string Info;

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
				public int CurrVal;
				public string Status;
				public string Info;

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
				public string Status;
				public string Info;

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

			public class DestroyData 
				: EventData
			{
				// IEventData
				override public EventData Set(object[] data)
				{
					return this;
				}

				override public object[] Get()
				{
					return new object[] {};
				}


				override public string ToString()
				{
					return "[]";
				}
			}

			#endregion

		}

	}
}
