using Amazon.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace AWSSDK_DotNet.IntegrationTests.Tests
{
    public class TestBase<T>
        where T : AmazonServiceClient, new()
    {
        private static T client;
        public static T Client
        {
            get
            {
                if (client == null)
                {
                    client = new T();

                }
                return client;
            }
            set
            {
                client = value;
            }
        }

        public static void BaseClean()
        {
            if (client != null)
            {
                client.Dispose();
                client = null;
            }
        }

        public static void SetEndpoint(AmazonServiceClient client, string serviceUrl, string region = null)
        {
            var clientConfig = client
                .GetType()
                .GetProperty("Config", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(client, null) as ClientConfig;
            clientConfig.ServiceURL = serviceUrl;
            if (region != null)
                clientConfig.AuthenticationRegion = region;
        }


    }
}
