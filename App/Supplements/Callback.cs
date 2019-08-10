using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using CoreLib;
using CoreLib.PubSub;


namespace SatisfactorySavegameTool.Supplements
{

	public class DefaultCallbackImpl : Callback<DefaultCallbackImpl.StartData, DefaultCallbackImpl.UpdateData, DefaultCallbackImpl.StopData>
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
			public long MaxVal;
			public string Status;
			public string Info;

			// IEventData
			override public EventData Set(object[] data)
			{
				if (data.Length != 3)
					throw new ArgumentException();

				// Temporary workaround until old code migrated to using long
				if (data[0] is long)
					MaxVal = (long)data[0];
				else
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
			public long CurrVal;
			public string Status;
			public string Info;

			// IEventData
			override public EventData Set(object[] data)
			{
				if (data.Length != 3)
					throw new ArgumentException();

				// Temporary workaround until old code migrated to using long
				if (data[0] is long)
					CurrVal = (long)data[0];
				else
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
