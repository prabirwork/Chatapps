using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ChatServer
{
    public partial class Form1 : Form
    {
        private TcpListener? server;
        private readonly List<TcpClient> clients = new();
        private readonly object _lock = new();
        private volatile bool running;

        public Form1()
        {
            InitializeComponent();
            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;
        }

        private void Form1_Load(object? sender, EventArgs e)
        {
            StartServer();
        }

        private void StartServer()
        {
            server = new TcpListener(IPAddress.Any, 5000);
            server.Start();
            running = true;

            Thread t = new Thread(AcceptClients) { IsBackground = true };
            t.Start();

            Log("Server started on port 5000...");
        }

        private void AcceptClients()
        {
            try
            {
                while (running)
                {
                    TcpClient tcp = server!.AcceptTcpClient();
                    lock (_lock) clients.Add(tcp);
                    Log("Client connected: " + tcp.Client.RemoteEndPoint);

                    Thread t = new Thread(HandleClient) { IsBackground = true };
                    t.Start(tcp); // pass client as parameter
                }
            }
            catch (SocketException) { /* server stopped */ }
            catch (Exception ex) { Log("Accept error: " + ex.Message); }
        }

        private void HandleClient(object? obj)
        {
            if (obj is not TcpClient tcp) return;

            try
            {
                using NetworkStream stream = tcp.GetStream();
                byte[] buffer = new byte[4096];
                int bytesRead;
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    string msg = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Log($"[{tcp.Client.RemoteEndPoint}] {msg.Trim()}");
                    Broadcast(msg, tcp);
                }
            }
            catch (Exception ex)
            {
                Log("Client error: " + ex.Message);
            }
            finally
            {
                lock (_lock) { clients.Remove(tcp); }
                try { tcp.Close(); } catch { }
                Log("Client disconnected.");
            }
        }

        private void Broadcast(string msg, TcpClient sender)
        {
            byte[] data = Encoding.UTF8.GetBytes(msg);
            lock (_lock)
            {
                foreach (var c in clients)
                {
                    if (c == sender) continue;
                    try
                    {
                        NetworkStream s = c.GetStream();
                        s.Write(data, 0, data.Length);
                    }
                    catch { /* ignore per-client write errors */ }
                }
            }
        }

        private void Log(string msg)
        {
            if (txtLog.InvokeRequired)
                txtLog.Invoke(new Action(() => txtLog.AppendText(msg + Environment.NewLine)));
            else
                txtLog.AppendText(msg + Environment.NewLine);
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            running = false;
            try { server?.Stop(); } catch { }
            lock (_lock)
            {
                foreach (var c in clients) try { c.Close(); } catch { }
                clients.Clear();
            }
        }
    }
}
