using Amazon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TODOListPortableLibrary
{
    public class Constants
    {
        //identity pool id for cognito credentials
        public const string IdentityPoolId = "us-east-1:28bb08da-0dd9-42ab-a5e2-84f9bdaee2fc";
        public const string PROVIDER_NAME = "graph.facebook.com";
        //set your regionendpoints here
        public static RegionEndpoint CognitoIdentityRegion = RegionEndpoint.USEast1;
        public static RegionEndpoint CognitoSyncRegion = RegionEndpoint.USEast1;
    }
}
