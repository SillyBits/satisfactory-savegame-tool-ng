using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

using CoreLib;
using CoreLib.PubSub;

using SatisfactorySavegameTool.Supplements;


namespace SatisfactorySavegameTool.Dialogs
{
	/// <summary>
	/// Interaction logic for ProgressDialog.xaml
	/// </summary>
	public partial class ProgressDialog : Window
	{
		public string CounterFormat { get; set; }
		public int Interval { get; set; }
		public ICallback Events { get { return _callback; } }


		public ProgressDialog(Window parent, string title, int interval = 10000)
			: base()
		{
			InitializeComponent();

			Icon = Application.Current.MainWindow.Icon;

			if (parent == null)
				parent = Application.Current.MainWindow;
			Owner = parent;

			if (!string.IsNullOrEmpty(title))
				Title = title;

			CounterFormat = "{0:#,#0} / {1:#,#0}";

			Interval = interval;

			// Bind our events
			_publisher = new Publisher(SynchronizationContext.Current);
			_callback = new DefaultCallbackImpl();

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
		private DefaultCallbackImpl _callback;
		private long _max_val;
		private long _last_val;
		private static readonly Action EmptyDelegate = delegate { };

		private void Update(long value, string status, string info)
		{
			bool updated = false;

			if ((long)Progress.Value != value)
			{
				Progress.Value = value;
				Counts.Text = string.Format(CounterFormat, value, _max_val);
				updated = true;
			}

			if (!string.IsNullOrEmpty(status))
			{
				Status.Text = status;
				updated = true;
			}

			if (!string.IsNullOrEmpty(info))
			{
				Info.Text = info;
				updated = true;
			}

			if (updated)
				Dispatcher.Invoke(DispatcherPriority.Render, EmptyDelegate);
		}

		private void OnStart(Publisher sender, DefaultCallbackImpl.StartData data)
		{
			Dispatcher.Invoke/*Async*/(() => {
				_last_val = 0;
				if (data.MaxVal == -1)
				{
					Progress.IsIndeterminate = true;
					_max_val = 0;
				}
				else
				{
					Progress.IsIndeterminate = false;
					_max_val = data.MaxVal;
				}
				Progress.Value = 0;
				Progress.Maximum = _max_val;
				Update(0, data.Status, data.Info);
				Show();
			});
		}

		private void OnUpdate(Publisher sender, DefaultCallbackImpl.UpdateData data)
		{
			if (data.CurrVal >= _last_val + Interval)
			{
				_last_val = data.CurrVal;
				Dispatcher.InvokeAsync(() => {
					Update(data.CurrVal, data.Status, data.Info);
				});
			}
		}

		private void OnStop(Publisher sender, DefaultCallbackImpl.StopData data)
		{
			Dispatcher.Invoke/*Async*/(() => {
				Update((long)Progress.Maximum, data.Status, data.Info);
				Hide();
			});
		}

		private void OnDestroy(Publisher sender, DefaultCallbackImpl.DestroyData data)
		{
			Dispatcher.Invoke/*Async*/(() => {
				Hide();
				Close();
			});
		}

	}
}
