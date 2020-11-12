/*
 * Chat Server
 * 
 * WPF form that waits for clients to connect and when
 * a client connects, starts up a thread for each.
 * 
 * A seperate thread looks to see if anyone has disconnected
 * and will clean up after if it finds one.
 * 
 * History
 *  11/11/2020
 *      First take.  Took one day to convert from the console
 *      application and make it much more powerful and capable.
 * 
 */
using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace WPFChatServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ChatServer cs;
        int DebugLevel=9;
        private readonly string LocalPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\";

        /*
         * Constructor for the window
         */
        public MainWindow()
        {
            InitializeComponent();

            WriteDebug("Starting Server");
            ClearOldLogs();

            cs = new ChatServer(this);
            tbxMain.Text = string.Empty;
        }

        /*
         * Close even for the window defined in the xaml
         */
        public void Window_Closing(object sender, CancelEventArgs e)
        {
            cs.StopServer();
            cs = null;
        }

        /*
         * Send text out to the main display
         */
        public void Display(string textout)
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
            {
                tbxMain.AppendText(textout + "\r\n");
                tbxMain.ScrollToEnd();

            });

            WriteDebug("* "+textout);
        }



        /*
         * 
         * Output debug to a log file... each class has a different file based on the InstanceID
         * 
         */
        public void WriteDebug(string info)
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
            {
                // 0 = no output at all
                if (DebugLevel != 0)
                {
                    DateTime date = DateTime.Now;
                    string now = date.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string logFileName = LocalPath + date.ToString("MMdd") + "- WPFChatServer.log";
                    string infoOut = now + " - ";

                    info = info.Replace("\n", String.Empty);

                    // if there are leading EOLs then transfer them to before
                    // the date and time stamp before saving
                    while (info.Length > 0 && info[0] == '\r')
                    {
                        infoOut = "\n\r" + infoOut;
                        info = info.Substring(1);
                    }

                    // why no replace("\r","\n\r")?
                    infoOut += info;

                    using (StreamWriter file = new StreamWriter(logFileName, true))
                    {
                        file.WriteLine(infoOut);
                    }
                }
            });
        }



        /*
         * 
         * Delete log files over a certain number of days in age
         * 
         */
        public void ClearOldLogs()
        {
            DateTime date = DateTime.Now;
            // grab all matching files
            string[] files = Directory.GetFiles(LocalPath, "*.log");
            string file;
            int j;

            // is it too old?
            for (int i = 0; i < files.Length; i++)
            {
                j = files[i].IndexOf('\\');

                if (j >= 0)
                {
                    file = files[i].Substring(j + 1);
                }
                else
                {
                    file = files[i];
                }

                if (file.Length > 4 && file.Substring(4, 1) == "-")
                {
                    if (file.Substring(0, 4) != date.ToString("MMdd"))
                    {
                        try
                        {
                            System.IO.File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            if (DebugLevel > 0)
                                WriteDebug("Can't delete file " + file + "\r\n" + "Exception - " + ex.Message);
                        }
                    }
                }
            }
        }


        /*
         * Start Menu click
         */
        public void BtnStart_Server_Click(object sender, RoutedEventArgs e)
        {
            cs.startServer();
            btnStart_Server.IsChecked = true;
            btnStop_Server.IsChecked = false;
        }

        /*
         * Stop button click
         */
        public void BtnStop_Server_Click(object sender, RoutedEventArgs e)
        {
            cs.StopServer();
            btnStart_Server.IsChecked = false;
            btnStop_Server.IsChecked = true;
        }
    } // End class MainWindow
}
