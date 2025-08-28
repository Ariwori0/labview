using CoreLinkSys1;
using CoreLinkSys1.Utilities;
using System;
using System.Windows.Forms;

namespace CoreLinkSys1
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            DebugTool.ConfigureLogging(false, false, DebugTool.LogLevel.Debug);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}