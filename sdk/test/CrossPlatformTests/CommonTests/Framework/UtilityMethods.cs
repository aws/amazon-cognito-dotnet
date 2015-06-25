using Amazon.Runtime;
using System;
using System.Threading.Tasks;

namespace CommonTests.Framework
{
    internal class UtilityMethods
    {
        public const string SDK_TEST_PREFIX = "aws-net-sdk";


        public static string GenerateName()
        {
            return GenerateName(SDK_TEST_PREFIX + "-");
        }

        public static string GenerateName(string name)
        {
            return name + new Random().Next();
        }

        public static T WaitUntilSuccess<T>(Func<T> loadFunction, int sleepSeconds = 5, int maxWaitSeconds = 300)
        {
            T result = default(T);
            WaitUntil(() =>
            {
                try
                {
                    result = loadFunction();
                    return true;
                }
                catch
                {
                    return false;
                }
            }, sleepSeconds, maxWaitSeconds);
            return result;
        }

        public static void WaitUntilSuccess(Action action, int sleepSeconds = 5, int maxWaitSeconds = 300)
        {
            WaitUntil(() =>
            {
                try
                {
                    action();
                    return true;
                }
                catch
                {
                    return false;
                }
            }, sleepSeconds, maxWaitSeconds);
        }

        public static void WaitUntil(Func<bool> matchFunction, int sleepSeconds = 5, int maxWaitSeconds = 300)
        {
            if (sleepSeconds < 0) throw new ArgumentOutOfRangeException("sleepSeconds");
            if (maxWaitSeconds < 0) throw new ArgumentOutOfRangeException("maxWaitSeconds");

            var sleepTime = TimeSpan.FromSeconds(sleepSeconds);
            var maxTime = TimeSpan.FromSeconds(maxWaitSeconds);
            var endTime = DateTime.Now + maxTime;

            while (DateTime.Now < endTime)
            {
                if (matchFunction())
                    return;
                Sleep(sleepTime);
            }

            throw new TimeoutException(string.Format("Wait condition was not satisfied for {0} seconds", maxWaitSeconds));
        }

        public static void Sleep(TimeSpan ts)
        {
            Task.Delay(ts).Wait();
        }
        public static async Task SleepAsync(TimeSpan ts)
        {
            await Task.Delay(ts);
        }

        public static void RunAsSync(Func<Task> asyncFunc)
        {
            Task.Run(asyncFunc).Wait();
        }

        public static T CreateClient<T>()
            where T : AmazonServiceClient
        {
            var client = (T)Activator.CreateInstance(typeof(T),
                new object[] { TestRunner.Credentials, TestRunner.RegionEndpoint });
            return client;
        }
    }
}
