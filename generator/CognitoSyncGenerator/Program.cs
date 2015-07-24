using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CognitoSyncGenerator
{
    public class Program
    {
        static int Main(string[] args)
        {
            var commandArguments = CommandArguments.Parse(args);
            if (!string.IsNullOrEmpty(commandArguments.Error))
            {
                Console.WriteLine(commandArguments.Error);
                return -1;
            }

            var returnCode = 0;
            var options = commandArguments.ParsedOptions;

            try
            {
                
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error running generator: " + e.Message);
                Console.Error.WriteLine(e.StackTrace);
                returnCode = -1;
            }

            if (options.WaitOnExit)
            {
                Console.WriteLine();
                Console.WriteLine("Generation complete. Press a key to exit.");
                Console.ReadLine();
            }

            return returnCode;
        }
    }
}
