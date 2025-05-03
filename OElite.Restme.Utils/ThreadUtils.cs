using System;
using System.Threading;
using System.Threading.Tasks;

namespace OElite
{
    public static class ThreadUtils
    {
        public static void RunInBackgroundAndForget(this Task task)
        {
        }

        public static void RunInBackgroundAndForget(Action action)
        {
            Task.Run(action).RunInBackgroundAndForget();
        }

        public static T? WaitAndGetResult<T>(this Task<T?>? task, int timeoutMilliseconds = -1,
            CancellationToken token = default)
        {
            if (task == null) return default;


            if (timeoutMilliseconds > 0)
            {
                return Task.Run(async () =>
                {
                    using var timeoutCancellationTokenSource = new CancellationTokenSource();
                    var completedTask = await Task
                        .WhenAny(task,
                            Task.Delay(timeoutMilliseconds,
                                token == CancellationToken.None ? timeoutCancellationTokenSource.Token : token));
                    if (completedTask != task) return task.Result;
                    // ReSharper disable once MethodHasAsyncOverload
                    timeoutCancellationTokenSource.Cancel();
                    return await task;
                }, token).Result;
            }

            task.Wait(token);

            return task.Result;
        }

        public static void WaitTillAvailableToProcess<TA>(this SimpleRestmeQueue<TA>? queue, TA newObject)
        {
            if (queue == null) return;

            #region This code can be better improved using message queue mechanism

            if (queue.ExecutionWaitRequired)
            {
                while (queue.ExecutionWaitRequired)
                {
                    lock (queue)
                    {
                        if (queue.ExecutionWaitRequired)
                            Thread.Sleep(1);
                        else
                        {
                            queue.QueueItems.Add(newObject);
                            break;
                        }

                        if (queue.ExecutionWaitRequired)
                            Thread.Sleep(100);
                    }
                }
            }
            else
            {
                queue.QueueItems.Add(newObject);
            }

            #endregion
        }

        public static void ClearProcessingObject<TA>(this SimpleRestmeQueue<TA?>? queue,
            TA? singleObjectToRemove = default,
            bool throwExceptionIfObjectNotFound = true, bool updateWaitRequired = true)
        {
            if (queue == null) return;
            lock (queue)
            {
                if (singleObjectToRemove != null)
                {
                    var indexOfObject = queue.QueueItems.IndexOf(singleObjectToRemove);
                    if (indexOfObject >= 0)
                    {
                        queue.QueueItems.Remove(singleObjectToRemove);
                        if (updateWaitRequired)
                        {
                            queue.ExecutionWaitRequired = false;
                        }
                    }
                    else if (throwExceptionIfObjectNotFound)
                    {
                        throw new OEliteException("Object to remove is no longer in the processing queue");
                    }
                }
                else
                {
                    queue.QueueItems.Clear();
                    if (updateWaitRequired)
                    {
                        queue.ExecutionWaitRequired = false;
                    }
                }
            }
        }
    }
}