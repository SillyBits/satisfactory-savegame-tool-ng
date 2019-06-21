using System;
using CoreLib.PubSub;

namespace CoreLib
{

    public interface ICallback
    {
        void Start(params object[] data);
		void Start(EventArgs data);
        void Update(params object[] data);
		void Update(EventArgs data);
		void Stop(params object[] data);
		void Stop(EventArgs data);
	}

	public class Callback<_StartData, _UpdateData, _StopData>
        : ICallback
		where _StartData : EventData, new()
        where _UpdateData : EventData, new()
        where _StopData : EventData, new()
    {
        protected Publisher _publisher;

        public Callback()
        {
            _publisher = new Publisher();
        }


		public Event<_StartData> OnStart = new Event<_StartData>();
        public Event<_UpdateData> OnUpdate = new Event<_UpdateData>();
        public Event<_StopData> OnStop = new Event<_StopData>();


		// ICallback
        void ICallback.Start(params object[] data)
        {
            _publisher.Raise(OnStart, data);
        }
		void ICallback.Start(EventArgs data)
		{
			_publisher.Raise(OnStart, data);
		}

		void ICallback.Update(params object[] data)
        {
            _publisher.Raise(OnUpdate, data);
        }
		void ICallback.Update(EventArgs data)
		{
			_publisher.Raise(OnUpdate, data);
		}

		void ICallback.Stop(params object[] data)
        {
            _publisher.Raise(OnStop, data);
        }
		void ICallback.Stop(EventArgs data)
		{
			_publisher.Raise(OnStop, data);
		}

		// Specialized triggers (preffered way of triggering)
		public void Start(_StartData data)
		{
			_publisher.Raise(OnStart, data);
		}

		public void Update(_UpdateData data)
		{
			_publisher.Raise(OnUpdate, data);
		}

		public void Stop(_StopData data)
		{
			_publisher.Raise(OnStop, data);
		}

	}

}