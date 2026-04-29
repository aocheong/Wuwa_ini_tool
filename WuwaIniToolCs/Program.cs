using System;
using System.Windows.Forms;

namespace WuwaIniToolCs;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (ElevatedMode.TryHandle(args))
        {
            return;
        }

        Application.Run(new MainForm());
    }
}
