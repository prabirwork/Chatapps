using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ChatClient
{
    public partial class Form1 : Form
    {
        private TcpClient? client;
        private NetworkStream? ns;
        private Thread? receiveThread;

        public Form1()
        {
            InitializeComponent();

            // ensure events are wired in code (safer than relying only on Designer)
            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;
            btnSend.Click += btnSend_Click;
            this.AcceptButton = btnSend; // Enter key sends
        }

        private void Form1_Load(object? sender, EventArgs e)
        {
            ConnectToServer();
        }

        private void ConnectToServer()
        {
            try
            {
                client = new TcpClient();
                client.Connect("127.0.0.1", 5000); // change IP if server runs on different machine
                ns = client.GetStream();

                Log("Connected to server.");

                receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
                receiveThread.Start();
            }
            catch (Exception ex)
            {
                Log("Connect failed: " + ex.Message);
            }
        }

        private void ReceiveLoop()
        {
            if (ns == null) return;
            var buffer = new byte[4096];
            try
            {
                while (client?.Connected == true)
                {
                    int read = ns.Read(buffer, 0, buffer.Length);
                    if (read <= 0) break;
                    string msg = Encoding.UTF8.GetString(buffer, 0, read);
                    Log("Server: " + msg.Trim());
                }
            }
            catch (Exception ex)
            {
                Log("Receive error: " + ex.Message);
            }
            finally
            {
                Log("Receive loop ended.");
                Disconnect();
            }
        }

        // THIS WILL RUN when the Send button is clicked
        private void btnSend_Click(object? sender, EventArgs e)
        {
            //Log("Send button clicked");                 // <-- helpful debug: shows whether handler ran
            if (client == null || ns == null || client.Connected == false)
            {
                Log("Not connected to server.");
                return;
            }

            string msg = txtMessage.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(msg))
            {
                Log("Empty message; not sent.");
                return;
            }

            try
            {
                // Add newline if you prefer delimiting messages (optional)
                byte[] data = Encoding.UTF8.GetBytes(msg);
                ns.Write(data, 0, data.Length);
                ns.Flush();

                Log("Me: " + msg);
                // Clear text safely on UI thread:
                if (txtMessage.InvokeRequired)
                    txtMessage.Invoke(new Action(() => txtMessage.Clear()));
                else
                    txtMessage.Clear();
            }
            catch (Exception ex)
            {
                Log("Send failed: " + ex.Message);
            }
        }

        private void Disconnect()
        {
            try { ns?.Close(); } catch { }
            try { client?.Close(); } catch { }
            ns = null;
            client = null;
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            Disconnect();
        }

        private void Log(string msg)
        {
            if (txtLog.InvokeRequired)
                txtLog.Invoke(new Action(() => txtLog.AppendText(msg + Environment.NewLine)));
            else
                txtLog.AppendText(msg + Environment.NewLine);
        }
    }
}
