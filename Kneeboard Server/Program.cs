using System;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace Kneeboard_Server
{
    static class Program
    {
        private static Mutex m_Mutex;
        /// <summary>
        /// Der Haupteinstiegspunkt für die Anwendung.
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool debug = false;
            if (debug == false)
            {
                int milliseconds = 1000;
                Thread.Sleep(milliseconds);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                bool createdNew;
                m_Mutex = new Mutex(true, "KneeboardServerMutex", out createdNew);
                if (createdNew)
                {
                    if (!IsAdministrator())
                    {
                        Console.WriteLine("Restarting as admin");
                        StartAsAdmin(Assembly.GetExecutingAssembly().Location);
                        return;
                    }
                    else
                    {
                        Application.Run(new Kneeboard_Server());
                    }
                }
                else
                {
                    // MessageBox.Show("The application is already running.", Application.ProductName,
                    // MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }
            else
            {
                int milliseconds = 1000;
                Thread.Sleep(milliseconds);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Kneeboard_Server());
            }
        }

        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static void StartAsAdmin(string fileName)
        {
            var proc = new Process
            {
                StartInfo =
        {
            FileName = fileName,
            UseShellExecute = true,
            Verb = "runas"
        }
            };
            proc.Start();
        }
    }
}
