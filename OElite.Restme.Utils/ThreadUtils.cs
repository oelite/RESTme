using System;
using System.Collections.Generic;
using System.Linq;
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

        public static T WaitAndGetResult<T>(this Task<T> task, int timeoutMiliseconds = -1)
        {
            if (task == null) return default(T);
            if (timeoutMiliseconds > 0)
            {
                task.Wait(timeoutMiliseconds);
            }
            else
                task.Wait();

            return task.Result;
        }

        public static void WaitTillAvailableToProcess<TA>(this SimpleRestmeQueue<TA> queue, TA newObject)
        {
            if (queue != null)
            {
                #region This code can be better improved using message queue mechanism

                while (queue.ExecutionWaitRequired)
                {
                    lock (queue)
                    {
                        queue.ExecutionWaitRequired = queue.QueueItems.FirstOrDefault() != null;
                        if (queue.ExecutionWaitRequired)
                            Thread.Sleep(1);
                        else
                        {
                            if (queue.QueueItems == null)
                            {
                                queue.QueueItems = new List<TA>();
                            }

                            queue.QueueItems.Add(newObject);
                            break;
                        }

                        if (queue.ExecutionWaitRequired)
                            Thread.Sleep(100);
                    }
                }

                #endregion
            }
        }

        public static void ClearProcessingObject<TA>(List<TA> processingObjects, TA singleObjectToRemove = default,
            bool throwExceptionIfObjectNotFound = true)
        {
            if (processingObjects?.Count > 0)
            {
                lock (processingObjects)
                {
                    if (singleObjectToRemove != null)
                    {
                        var indexOfObject = processingObjects.IndexOf(singleObjectToRemove);
                        if (indexOfObject >= 0)
                        {
                            processingObjects.Remove(singleObjectToRemove);
                        }
                        else if (throwExceptionIfObjectNotFound)
                        {
                            throw new OEliteException("Object to remove is no longer in the processing queue");
                        }
                    }
                    else
                    {
                        processingObjects.Clear();
                    }
                }
            }
        }
    }
}