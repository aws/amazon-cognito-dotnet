using CommonTests.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsConsoleApp
{
    public class ConsoleRunner : TestRunner
    {
        public ConsoleRunner()
            : base()
        {
        }

        protected override void WriteLine(string message)
        {
            System.Console.WriteLine(message);
        }

        protected override string TestTypeNamePrefix
        {
            get { return "WindowsConsoleApp"; }
        }
    }
}
