using System;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SMT
{
    /// <summary>
    /// Interaction logic for LogonWindow.xaml
    /// </summary>
    public partial class LogonWindow : Window
    {
        private HttpListener listener;

        public LogonWindow()
        {
            InitializeComponent();
            OpenLogoutURL();
        }

        private bool serverDone = false; 
        
        private void Window_Closed(object sender, EventArgs e)
        {
            try
            {
                serverDone = true;

                if (listener != null && listener.IsListening)
                {
                    listener.Stop();
                }
            }
            catch
            {
            }
        }
        
        private void OpenLogoutURL()
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://login.evepc.163.com/account/logoff") { UseShellExecute = true });
            // 等待 1s
            Task.Delay(1000).ContinueWith(t =>
            {
                OpenLogonURL();
            });
        }
        private void OpenLogonURL()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            string esiLogonURL = EVEData.EveManager.Instance.GetESILogonURL();

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(esiLogonURL) { UseShellExecute = true });
        }
        
        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            string url = urlTextBox.Text;
            if (!string.IsNullOrWhiteSpace(url))
            {
                // 在这里处理输入的URL
                HandleURL(url);
            }
            else
            {
                MessageBox.Show("请输入一个有效的URL。");
            }
        }

        private void HandleURL(string url)
        {
            // 在这里处理URL
            EVEData.EveManager.Instance.HandleEveAuthSMTUri(new Uri(url));
            Close();
        }
    }
}