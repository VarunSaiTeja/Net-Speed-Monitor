using Microsoft.Win32;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MenuItem = System.Windows.Controls.MenuItem;

namespace Net_Speed_Monitor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public string LocalAdapter { get; set; }

        readonly PerformanceCounterCategory PCC = new PerformanceCounterCategory("Network Interface");
        CancellationTokenSource tokenSource;
        CancellationToken token;
        Task Worker;
        public MainWindow()
        {
            InitializeComponent();
            ToolLoad();
        }
        public void ToolLoad()
        {
            if (Properties.Settings.Default.FirstRun)
            {
                Properties.Settings.Default.FirstRun = false;
                Properties.Settings.Default.Adapter = PCC.GetInstanceNames()[0];
                Properties.Settings.Default.Left = Top;
                Properties.Settings.Default.Top = Left;
                Properties.Settings.Default.Save();
            }
            else
            {
                Top = Properties.Settings.Default.Top;
                Left = Properties.Settings.Default.Left;
            }

            LocalAdapter = Properties.Settings.Default.Adapter;
            Topmost = true;

            foreach (var item in PCC.GetInstanceNames())
            {
                MenuItem menuItem = new MenuItem
                {
                    Header = item
                };

                menuItem.Click += Set_Instance;

                if (LocalAdapter == item)
                    menuItem.IsChecked = true;

                Adapter.Items.Add(menuItem);
            }

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                if (key.GetValueNames().Contains("Net Speed"))
                    AutoStart.IsChecked = true;
                else
                    AutoStart.IsChecked = false;
            }

            tokenSource = new CancellationTokenSource();
            token = tokenSource.Token;
            Worker = new Task(() => Get_Speed(), token);
            Worker.RunSynchronously();
        }
        private async void Get_Speed()
        {
            //Realtek PCIe GBE Family Controller
            //Qualcomm Atheros QCA9377 Wireless Network Adapter
            PerformanceCounter PCS = new PerformanceCounter("Network Interface", "Bytes Sent/sec", LocalAdapter);
            PerformanceCounter PCR = new PerformanceCounter("Network Interface", "Bytes Received/sec", LocalAdapter);

            while (!token.IsCancellationRequested)
            {
                Dispatcher.Invoke(() =>
                {
                    DownLabel.Content = FormatBytes(PCR.NextValue());
                    UpLabel.Content = FormatBytes(PCS.NextValue());
                });
                await Task.Delay(1000);
            }
        }
        private void Set_Instance(object sender, RoutedEventArgs e)
        {
            tokenSource.Cancel();
            var item = sender as MenuItem;
            foreach (MenuItem AdapterItem in Adapter.Items)
            {
                AdapterItem.IsChecked = false;
            }
            item.IsChecked = true;
            Properties.Settings.Default.Adapter = item.Header.ToString();
            Properties.Settings.Default.Save();
            LocalAdapter = Properties.Settings.Default.Adapter;

            new Thread(() =>
            {
                Thread.Sleep(1500);
                tokenSource = new CancellationTokenSource();
                token = tokenSource.Token;
                Worker = new Task(() => Get_Speed(), token);
                Worker.RunSynchronously();
            }).Start();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
            Properties.Settings.Default.Left = Left;
            Properties.Settings.Default.Top = Top;
            Properties.Settings.Default.Save();
        }
        public static string FormatBytes(float bytes)
        {
            string speed = null, format = null;

            if (bytes > 1000)
            {
                bytes /= 1000;
                if (bytes > 1000)
                {
                    bytes /= 1000;
                    speed = bytes.ToString();
                    format = " MB/s";
                    ;
                }
                else
                {
                    speed = bytes.ToString();
                    format = " KB/s";
                }
            }

            if (speed == null)
                return "0 B/s";
            else
            {
                speed = speed.Substring(0, speed.IndexOf(".")) + speed.Substring(speed.IndexOf("."), 2) + format;
                return speed;
            }
        }

        private void ExitClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void AutoStartClick(object sender, RoutedEventArgs e)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                if (AutoStart.IsChecked)
                {
                    key.SetValue("Net Speed Monitor", System.Reflection.Assembly.GetExecutingAssembly().Location);
                }
                else
                {
                    key.DeleteValue("Net Speed Monitor", false);
                }
            }
        }

        private async void DonateClick(object sender, RoutedEventArgs e)
        {
            using (WebClient wc = new WebClient())
            {
                string page = await wc.DownloadStringTaskAsync("https://raw.githubusercontent.com/VarunSaiTeja/Hosting/master/Donation.txt");
                Process.Start(page);
            }
        }
    }
}
