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

        public static void WaitTillAvailableToProcess<TA>(List<TA> processingObjects, TA newObject)
        {
            if (processingObjects?.Count >= 0)
            {
                #region This code can be better improved using queue mechanism

                var executionWaitRequired = true;

                while (executionWaitRequired)
                {
                    lock (processingObjects)
                    {
                        executionWaitRequired = processingObjects.FirstOrDefault() != null;
                        if (executionWaitRequired)
                            Thread.Sleep(1);
                        else
                        {
                            processingObjects.Add(newObject);
                        }
                    }

                    if (executionWaitRequired)
                        Thread.Sleep(100);
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