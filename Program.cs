using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TestStack.White.Factory;
using TestStack.White.UIItems.Finders;

namespace BrokenBotWatcher
{
    static class Program
    {
        const string BLUESTACK_PROCESS_NAME      = "HD-Frontend";
        const string BB_PROCESS_NAME             = "BrokenBot";
        const string BB_START_BUTTON_ID          = "BtnStart";
        const string BB_START_BUTTON_START_LABEL = "Start Bot";
        const int    SECOND                      = 1000;

        static string BB_PATH;
        static int WAIT_INTERVAL;
        static int BB_STARTUP_INTERVAL;
        static NotifyIcon ntIcon;
        static ProcessPriorityClass APPS_PRIORITY;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Setup data
            SetupData();

            SetupNotify(out ntIcon);

            // Check the bot is running
            var botProcesses = Process.GetProcessesByName(BB_PROCESS_NAME);
            Process botProcess;

            // Check if it's already run or need to launch
            if (!botProcesses.Any())
            {
                botProcess = Process.Start(BB_PATH, null);
                Thread.Sleep(BB_STARTUP_INTERVAL);
            }
            else
            {
                botProcess = botProcesses[0];
            }

            // Change bot priority
            botProcess.PriorityClass = APPS_PRIORITY;

            // Start the bot
            var application = TestStack.White.Application.Attach(BB_PROCESS_NAME);

            var window = application.GetWindow(SearchCriteria.All, InitializeOption.NoCache);
            
            var button = window.Get<TestStack.White.UIItems.Button>(BB_START_BUTTON_ID);

            // Start only if not started yet
            if (button.Text == BB_START_BUTTON_START_LABEL)
            {
                button.Click();
                Thread.Sleep(3000);
            }

            // Change bluestacks priority
            // Make sure the bot is work and bluestacks priority as needed
            while (true)
            {
                ChangeBluestack();
                Task.Delay(WAIT_INTERVAL).Wait();
            }
        }

        private static void SetupNotify(out NotifyIcon ntIcon)
        {
            // Notify user everything is startup
            ntIcon = new NotifyIcon();
            ntIcon.Icon = SystemIcons.Application;
            ntIcon.Visible = true;
            ntIcon.ShowBalloonTip(5000, "BrokenBotHelper", "Everyting is starting", ToolTipIcon.Info);
            ntIcon.MouseDoubleClick += (e, t) => ExitProgram();
        }

        private static void ChangeBluestack()
        {
            try
            {
                var botProcesses = Process.GetProcessesByName(BLUESTACK_PROCESS_NAME);
                botProcesses[0].PriorityClass = APPS_PRIORITY;
            }
            catch (Exception)
            {
                ntIcon.ShowBalloonTip(5000, "Problem", "Can't find blustacks", ToolTipIcon.Error);
            }
        }

        public static void ExitProgram()
        {
            var result = MessageBox.Show("Do you want to exit?", "Exit confirmation",
                                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                Environment.Exit(0);
            }
        }


        private static void SetupData()
        {
            var appSettings = System.Configuration.ConfigurationSettings.AppSettings;
            BB_PATH = appSettings["BB_PATH"];
            WAIT_INTERVAL = int.Parse(appSettings["WAIT_INTERVAL"])*SECOND;
            BB_STARTUP_INTERVAL = int.Parse(appSettings["BB_STARTUP_INTERVAL"])*SECOND;
            ProcessPriorityClass.TryParse(appSettings["APPS_PRIORITY"], out APPS_PRIORITY);

            // Check if the priority is fine
            if (APPS_PRIORITY == 0 || WAIT_INTERVAL <= 0 || BB_STARTUP_INTERVAL < 5)
            {
                throw new ArgumentException("WHY THE HECK YOU TOUCHED THE CONFIG FILE???");
            }
        }
    }
}
