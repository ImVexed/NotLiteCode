using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NotLiteCode.Misc
{
  public class TaskQueue
  {
    private SemaphoreSlim TaskLock;
    public TaskQueue(int ConcurrentTasks)
    {
      TaskLock = new SemaphoreSlim(ConcurrentTasks);
    }

    public async Task<T> Enqueue<T>(Func<Task<T>> QueueingTask)
    {
      await TaskLock.WaitAsync();
      try
      {
        return await QueueingTask();
      }
      finally
      {
        TaskLock.Release();
      }
    }
    public async Task Enqueue(Func<Task> QueueingTask)
    {
      await TaskLock.WaitAsync();
      try
      {
        await QueueingTask();
      }
      finally
      {
        TaskLock.Release();
      }
    }

    public async Task Enqueue(Action QueueingTask)
    {
      await TaskLock.WaitAsync();
      try
      {
        await Task.Run(QueueingTask);
      }finally
      {
        TaskLock.Release();
      }
    }
  }
}
