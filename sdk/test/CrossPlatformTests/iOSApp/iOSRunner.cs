using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Foundation;
using UIKit;
using CommonTests.Framework;

namespace iOSApp
{
    public class iOSRunner : TestRunner
    {
        private UITextView _textView = null;
        private Action<Action> _uiRunner = null;

        public iOSRunner( UITextView txtView, Action<Action> uiRunner)
            : base()
        {
            _textView = txtView;
            _uiRunner = uiRunner;
        }

        protected override void WriteLine(string message)
        {
            Write(message);
        }

        protected override void TestCompleted(string testMethodName, bool succeeded)
        {
            Write("{0}: {1}", testMethodName, succeeded ? "PASSED" : "FAILED");
            
        }

        private void Write(string message, params object[] args)
        {
            var text = string.Format(message, args);
            _uiRunner(() => _textView.Text += Environment.NewLine + text);
        }
    }
}