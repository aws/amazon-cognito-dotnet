using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CognitoSyncGenerator
{
    /// <summary>
    /// Command line options for the AWS SDK code generator.
    /// </summary>
    public class GeneratorOptions
    {
        /// <summary>
        /// If set, causes the generator to emit additional diagnostic messages 
        /// whilst running.
        /// </summary>
        public bool Verbose { get; set; }

        /// <summary>
        /// If set, waits for keypress before exiting generation.
        /// </summary>
        public bool WaitOnExit { get; set; }

        /// <summary>
        /// The name and location of the json manifest file listing all supported services
        /// for generation. By default this is located in the ServiceModels folder and is
        /// named '_manifests.json'.
        /// </summary>
        public string Manifest { get; set; }

        /// <summary>
        /// The root folder beneath which the code for the SDK is arranged. Source code exists under
        /// a .\src folder, which is further partitioned into core runtime (.\Core) and service code 
        /// (.\Services). Tests are placed under a .\test folder, which is further partitioned into 
        /// unit tests (.\UnitTests) and integration tests (.\IntegrationTests).
        /// </summary>
        public string SdkRootFolder { get; set; }

        /// <summary>
        /// The name and location of the samples solution file
        /// </summary>
        public string Sample { get; set; }

        public GeneratorOptions()
        {
            Verbose = false;
            WaitOnExit = false;
            SdkRootFolder = @"..\..\..\..\sdk";

            Manifest = @"..\..\Manifest\_manifest.json";
        }
    }
}
