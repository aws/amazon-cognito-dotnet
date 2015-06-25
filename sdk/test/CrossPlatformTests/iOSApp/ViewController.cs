using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Foundation;
using UIKit;
using System.Threading.Tasks;
using System.Diagnostics;
using Amazon.Runtime.Internal.Util;
using CoreGraphics;

namespace iOSApp
{
    public class ViewController : UIViewController
    {
        UITextView resultText;

        #region View lifecycle
        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            SQLitePCL.CurrentPlatform.Init();

            //setup the view
            View.BackgroundColor = UIColor.White;
            var label = new UILabel(new CGRect(10, 20, View.Bounds.Size.Width - 20, 40));
            label.Text = "Executing Tests Now";
            label.TextAlignment = UITextAlignment.Center;
            label.AdjustsFontSizeToFitWidth = true;
            View.AddSubview(label);

            resultText = new UITextView(new CGRect(10, label.Frame.Y + label.Frame.Height, View.Bounds.Size.Width - 20, View.Bounds.Size.Height - (30 + label.Frame.Height + label.Frame.X)));
            resultText.TextColor = UIColor.Black;
            resultText.Editable = false;
            View.AddSubview(resultText);

            //execute the tests
            RunTestsAsync();
        }

        #endregion

        private async Task RunTestsAsync()
        {
            try
            {
                resultText.Text = string.Empty;
                var runner = new iOSRunner(resultText, InvokeOnMainThread);
                await runner.ExecuteAllTestsAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                resultText.Text = e.ToString();
            }
        }

    }
}