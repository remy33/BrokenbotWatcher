using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TestStack.White.Factory;
using TestStack.White.UIItems.Finders;
using TestStack.White.UIItems.WindowItems;
using Application = TestStack.White.Application;
using Button = TestStack.White.UIItems.Button;

namespace BrokenBotWatcher
{
    static class Program
    {
        #region Fields

        const string BLUESTACK_PROCESS_NAME = "HD-Frontend";
        const string BB_PROCESS_NAME = "BrokenBot";
        const string BB_START_BUTTON_ID = "BtnStart";
        const string BB_START_BUTTON_START_LABEL = "Start Bot";
        const string BB_START_BUTTON_STOP_LABEL = "Stop Bot";
        const int SECOND = 1000;

        static string BB_PATH;
        static int PORT_NO;
        static int WAIT_INTERVAL;
        static int BB_STARTUP_INTERVAL;
        static ProcessPriorityClass APPS_PRIORITY;
        static private NotifyIcon sNotifyIcon;
        static private Window bbWindow;

        #endregion

        #region Enums

        internal enum Operations
        {
            Start,
            Stop,
            Resume,
            Pause,
        }

        #endregion

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {

            // Setup data
            SetupData();

            SetupNotify(out sNotifyIcon);

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
            var application = Application.Attach(BB_PROCESS_NAME);

            bbWindow = application.GetWindow(SearchCriteria.All, InitializeOption.NoCache);
            
            BbStartStop(Operations.Start);

            // After BB has start it's good idea to start the server
            StartServer();

            // Change bluestacks priority
            // Make sure the bot is work and bluestacks priority as needed
            while (true)
            {
                ChangeBluestack();
                Task.Delay(WAIT_INTERVAL).Wait();
            }
        }

        private static void BbStartStop(Operations operation)
        {
            var button = bbWindow.Get<Button>(BB_START_BUTTON_ID);

            // Start only if not started yet
            if (operation == Operations.Start && button.Text == BB_START_BUTTON_START_LABEL)
            {
                RestoreWindows(bbWindow);
                button.Click();
                Task.Delay(3000).Wait();
            }
            else if (operation == Operations.Stop && button.Text == BB_START_BUTTON_STOP_LABEL)
            {
                RestoreWindows(bbWindow);
                button.Click();
                Task.Delay(1000).Wait();
            }
            else
            {
                // Check hasn't Already has stoped or started
                if (!(button.Text == BB_START_BUTTON_STOP_LABEL ||
                    button.Text == BB_START_BUTTON_START_LABEL))
                {
                    throw new InvalidOperationException("Operation isn't match or button han't found");
                }
            }
        }

        private static void RestoreWindows(Window window)
        {
            if (window.DisplayState == DisplayState.Minimized)
            {
                window.Focus(DisplayState.Restored);
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
                NotifyUser("Problem", "Can't find blustacks", ToolTipIcon.Error);
            }
        }

        public static void ExitProgram()
        {
            var result = MessageBox.Show("Do you want to exit?", "Exit confirmation",
                                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                BbServer.Server.MessageArived -= OnMessageArive;
                BbServer.Server.ServerInitiated -= OnServerInitiated;
                BbServer.Server.Stop();
                Environment.Exit(0);
            }
        }

        private static void SetupData()
        {
            var appSettings = ConfigurationSettings.AppSettings;
            BB_PATH = appSettings["BB_PATH"];
            WAIT_INTERVAL = Int32.Parse(appSettings["WAIT_INTERVAL"])*SECOND;
            BB_STARTUP_INTERVAL = Int32.Parse(appSettings["BB_STARTUP_INTERVAL"])*SECOND;
            PORT_NO = Int32.Parse(appSettings["PORT_NO"]);
            ProcessPriorityClass.TryParse(appSettings["APPS_PRIORITY"], out APPS_PRIORITY);

            // Check if the priority is fine
            if (APPS_PRIORITY == 0 || WAIT_INTERVAL <= 0 || 
                BB_STARTUP_INTERVAL < 5 || PORT_NO <= 1000)
            {
                throw new ArgumentException("WHY THE HECK YOU TOUCHED THE CONFIG FILE???");
            }
        }

        static private void StartServer()
        {
            BbServer.Server.MessageArived += OnMessageArive;
            BbServer.Server.ServerInitiated += OnServerInitiated;
            BbServer.Server.Start(PORT_NO);
        }

        private static void OnServerInitiated(string serverIp)
        {
            NotifyUser("Server is up", "Server is up and runs on:\n" + serverIp, ToolTipIcon.Info);
        }

        private static void OnMessageArive(string message)
        {
            message = message.ToLower();

            // Checks type of the message
            if (message.Equals(Operations.Start.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                BbStartStop(Operations.Start);
            }
            else if (message.Equals(Operations.Stop.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                BbStartStop(Operations.Stop);
            }
        }

        private static void NotifyUser(string title, string text, ToolTipIcon icon)
        {
            sNotifyIcon.ShowBalloonTip(5000, title, text, icon);
        }
    }
}
