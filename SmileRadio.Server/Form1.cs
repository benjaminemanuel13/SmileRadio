using System;
using System.Windows.Forms;

namespace SmileRadio.Server
{
    public partial class Form1 : Form
    {
        private AudioServer _audioServer;
        private Button btnStart;
        private Button btnStop;
        private TextBox txtLog;
        private Label lblStatus;
        private Label lblClients;

        public Form1()
        {
            InitializeComponent();
            SetupCustomUI();
            _audioServer = new AudioServer();
            _audioServer.Log += LogMessage;
            _audioServer.ClientCountChanged += UpdateClientCount;
        }

        private void SetupCustomUI()
        {
            this.Text = "SmileRadio Server";
            this.Size = new System.Drawing.Size(500, 400);

            var lblPort = new Label { Text = "Port:", Location = new System.Drawing.Point(12, 17), AutoSize = true };
            this.Controls.Add(lblPort);

            var txtPort = new TextBox { Text = "5555", Location = new System.Drawing.Point(50, 14), Width = 50 };
            this.Controls.Add(txtPort);

            btnStart = new Button { Text = "Start", Location = new System.Drawing.Point(110, 12) };
            btnStart.Click += (s, e) => 
            {
                if (int.TryParse(txtPort.Text, out int port))
                {
                     _audioServer.Start(port);
                }
                else
                {
                     MessageBox.Show("Invalid Port");
                }
            };
            this.Controls.Add(btnStart);

            btnStop = new Button { Text = "Stop", Location = new System.Drawing.Point(200, 12) };
            btnStop.Click += (s, e) => _audioServer.Stop();
            this.Controls.Add(btnStop);

            lblStatus = new Label { Text = "Clients: 0", Location = new System.Drawing.Point(300, 17), AutoSize = true };
            this.Controls.Add(lblStatus);

            txtLog = new TextBox { Multiline = true, Location = new System.Drawing.Point(12, 50), Size = new System.Drawing.Size(460, 300), ScrollBars = ScrollBars.Vertical };
            this.Controls.Add(txtLog);
        }

        private void LogMessage(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(LogMessage), message);
                return;
            }
            txtLog.AppendText($"{DateTime.Now}: {message}{Environment.NewLine}");
        }

        private void UpdateClientCount(int count)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<int>(UpdateClientCount), count);
                return;
            }
            lblStatus.Text = $"Clients: {count}";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _audioServer?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
