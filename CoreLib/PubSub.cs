using System;
using System.Collections.Generic;
using System.ComponentModel;
//using System.Linq;
//using System.Text;
using System.Threading;
//using System.Windows.Threading;

namespace CoreLib.PubSub
{
    // Subclass from this to provide event parameters
    public abstract class EventData : EventArgs
    {
        public EventData() : base() {  }

        public abstract EventData Set(object[] data);
        public abstract object[] Get();
    }


    // Use this when passing as parameter
    public interface IEvent
    {
        void Subscribe(EventHandler handler);
		void Subscribe(Action<Publisher, EventArgs> handler);

        void Unsubscribe(EventHandler handler);
		void Unsubscribe(Action<Publisher, EventArgs> handler);

        void Raise(Publisher sender, params object[] data);
		void Raise(Publisher sender, EventArgs data);
	}

	// Subclass from this to provide events to be fired
	public class Event<Type>
        : IEvent
        where Type : EventData, new()
    {
        // IEvent, in case someone wants to access it abstracted
        void IEvent.Subscribe(EventHandler handler)
		{
			//throw new NotImplementedException();
			lock (this)
				_OnEvent += (sender, data) => handler((Publisher)sender, data);
		}
		void IEvent.Subscribe(Action<Publisher, EventArgs> handler)
		{
			lock (this)
				_OnEvent += (sender, data) => handler((Publisher)sender, data);
		}

		void IEvent.Unsubscribe(EventHandler handler)
		{
			lock (this)
				_OnEvent -= (sender, data) => handler((Publisher)sender, data);
		}
		void IEvent.Unsubscribe(Action<Publisher, EventArgs> handler)
		{
			lock (this)
				_OnEvent -= (sender, data) => handler((Publisher)sender, data);
		}

		void IEvent.Raise(Publisher sender, params object[] data)
		{
			lock (this)
				Raise(sender, new Type().Set(data));
		}
		public void Raise(Publisher sender, EventArgs data)
		{
			lock (this)
				Raise(sender, (Type)data);
		}

		// Specialized, preffered way of using this event as this has no conversion overhead
		public void Subscribe(EventHandler<Type> handler)
		{
			lock (this)
				_OnEvent += handler;
		}
		public void Subscribe(Action<Publisher, Type> handler)
		{
			lock (this)
				_OnEvent += (sender, data) => handler((Publisher)sender, data);
		}

		public void Unsubscribe(EventHandler<Type> handler)
		{
			lock (this)
				_OnEvent -= handler;
		}
		public void Unsubscribe(Action<Publisher, Type> handler)
		{
			lock (this)
				_OnEvent -= (sender, data) => handler((Publisher)sender, data);
		}

		public void Raise(Publisher sender, Type data)
        {
			//if (sender._context != null)
			//{
			//	sender._context.Send(delegate(object state) { _OnEvent.Invoke(sender,data); }, null);
			//	return;
			//}
			//if (Thread.CurrentThread != _owner)
			//{
			//	IAsyncResult res = _OnEvent.BeginInvoke(sender, data, null, this);
			//	_OnEvent.EndInvoke(res);
			//	return;
			//}

			// List of aggregated exceptions, if any
			List<Exception> exceptions = new List<Exception>();

            // Invoke action by iterating on all subscribers event handlers
            foreach (Delegate handler in _OnEvent.GetInvocationList())
            {
				//if (handler.Target is ISynchronizeInvoke)
				//{
				//	ISynchronizeInvoke si = (ISynchronizeInvoke) handler.Target;
				//	if (si == null || !si.InvokeRequired)
				//	{
				//		handler.DynamicInvoke(sender, data);
				//	}
				//	else
				//	{
				//		si.BeginInvoke(handler, new object[]{ sender, data });
				//	}
				//}
				//else
				{
					try
					{
						// Pass sender object and eventArgs
						handler.DynamicInvoke(sender, data);
					}
					catch (Exception e)
					{
						// Add exception in exception list if occured any
						exceptions.Add(e);
					}
				}
            }

            // Check if any exception occured while invoking the subscribers event handlers
            if (exceptions.Count > 0)
            {
                // Throw aggregate exception of all exceptions occured while invoking subscribers event handlers
                throw new AggregateException(exceptions);
            }
        }

		// The actual event, 
		// only accessible by using (Un)Subscribe above
		private event EventHandler<Type> _OnEvent = delegate { };

		// Used to get a handler suitable for our event above,
		// in case event (un)subscribers were called the abstract way
		//private EventHandler<Type> _ConvertHandler(EventHandler<object[]> h)
		//{
		//	return new EventHandler<Type>((sender, data) => h.Invoke(sender, data.Get()));
		//}

	}


	public class Publisher
    {
		public SynchronizationContext _context;

		public Publisher(SynchronizationContext ctx = null)
		{
			_context = ctx;// ?? SynchronizationContext.Current;
		}

		// Raising the abstract way
        public void Raise(IEvent ev, params object[] data)
        {
			lock (this)
			{
				//if (_context != null)
				//	_context.Send(delegate(object state) { ev.Raise(this, data); }, null);
				//else
					ev.Raise(this, data);
			}
        }

		// Raising the specialized way (preffered way)
		//public void Raise<_EventType, _EventData>(_EventType ev, _EventData data) 
		//	where _EventType : IEvent 
		//	where _EventData : EventData
		//{
		//	ev.Raise(this, data);
		//}

		public void Raise<_EventData>(Event<_EventData> ev, _EventData data)
			where _EventData : EventData, new()
		{
			lock (this)
			{
				//if (_context != null)
				//	_context.Send(delegate(object state) { ev.Raise(this, data); }, null);
				//else
					ev.Raise(this, data);
			}
		}

	}


}