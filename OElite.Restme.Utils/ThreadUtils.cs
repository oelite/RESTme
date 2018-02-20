using System;
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
    }
}