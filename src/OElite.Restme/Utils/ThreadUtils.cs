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
    }
}