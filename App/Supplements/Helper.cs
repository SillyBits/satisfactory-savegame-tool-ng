using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;


namespace SatisfactorySavegameTool.Supplements
{
	public static class Helper
	{

		/// <summary>
		/// Allows to run and wait on async action given
		/// </summary>
		/// <param name="action">Action to run</param>
		public static void WaitAsync(Func<Task> action)
		{
			Task task = Task.Run(async() => await action());
			while (!task.IsCompleted)
			{
				Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
				System.Threading.Thread.Yield();
				Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
			}
		}

		/// <summary>
		/// Allows to run and wait on async action given and returning its outcome
		/// </summary>
		/// <param name="action">Action to run</param>
		/// <returns>Action result</returns>
		public static _ReturnType WaitAsync<_ReturnType>(Func<Task<_ReturnType>> action)
		{
			Task<_ReturnType> task = Task.Run(async() => await action());
			while (!task.IsCompleted)
			{
				Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
				System.Threading.Thread.Yield();
				Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
			}
			return task.Result;
		}


	}
}
