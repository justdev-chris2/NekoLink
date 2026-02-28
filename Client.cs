using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Threading;

class NekoLinkClient
{
    static TcpClient client;
    static NetworkStream stream;
    static PictureBox pb;
    static Form form;
    static bool locked = false;
    static Label statusLabel;
    static DateTime lastFrame = DateTime.Now;
    
    [STAThread]
    static void Main()
    {
        string ip = Microsoft.VisualBasic.Interaction.InputBox("Enter Server IP:", "NekoLink", "192.168.1.", 500, 500);
        if (string.IsNullOrEmpty(ip)) return;
        
        try
        {
            client = new TcpClient();
            client.Connect(ip, 5900);
            client.ReceiveTimeout = 5000;
            stream = client.GetStream();
        }
        catch
        {
            MessageBox.Show("Could not connect to server");
            return;
        }
        
        form = new Form();
        form.Text = "NekoLink";
        form.WindowState = FormWindowState.Maximized;
        form.KeyPreview = true;
        form.BackColor = Color.Black;
        form.FormClosing += (s, e) => Environment.Exit(0);
        
        // Top status panel
        Panel topPanel = new Panel();
        topPanel.Height = 30;
        topPanel.Dock = DockStyle.Top;
        topPanel.BackColor = Color.FromArgb(30, 30, 30);
        
        statusLabel = new Label();
        statusLabel.Text = "🔓 Unlocked - Click screen to lock";
        statusLabel.ForeColor = Color.White;
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        statusLabel.Font = new Font("Arial", 10, FontStyle.Bold);
        topPanel.Controls.Add(statusLabel);
        
        // Picture box for remote screen
        pb = new PictureBox();
        pb.Dock = DockStyle.Fill;
        pb.SizeMode = PictureBoxSizeMode.Zoom;
        pb.BackColor = Color.Black;
        
        form.Controls.Add(pb);
        form.Controls.Add(topPanel);
        
        // Mouse events
        pb.MouseMove += (s, e) => {
            if (locked && pb.Image != null)
            {
                float ratioX = (float)Screen.PrimaryScreen.Bounds.Width / pb.Width;
                float ratioY = (float)Screen.PrimaryScreen.Bounds.Height / pb.Height;
                int remoteX = (int)(e.X * ratioX);
                int remoteY = (int)(e.Y * ratioY);
                SendMouse(remoteX, remoteY);
            }
        };
        
        pb.MouseClick += (s, e) => {
            if (locked && pb.Image != null)
            {
                float ratioX = (float)Screen.PrimaryScreen.Bounds.Width / pb.Width;
                float ratioY = (float)Screen.PrimaryScreen.Bounds.Height / pb.Height;
                int remoteX = (int)(e.X * ratioX);
                int remoteY = (int)(e.Y * ratioY);
                SendClick(remoteX, remoteY, e.Button.ToString());
            }
        };
        
        // Click to lock
        pb.Click += (s, e) => {
            locked = true;
            statusLabel.Text = "🔒 LOCKED - Press Right Ctrl to unlock";
            statusLabel.ForeColor = Color.LightGreen;
            form.Text = "NekoLink [LOCKED]";
        };
        
        // Keyboard events
        form.KeyDown += (s, e) => {
            if (e.Control && e.KeyCode == Keys.RControlKey)
            {
                locked = false;
                statusLabel.Text = "🔓 Unlocked - Click screen to lock";
                statusLabel.ForeColor = Color.White;
                form.Text = "NekoLink";
            }
            
            if (locked && !e.Control && e.KeyCode != Keys.RControlKey)
            {
                SendKey((byte)e.KeyCode, true);
            }
        };
        
        form.KeyUp += (s, e) => {
            if (locked && !e.Control)
            {
                SendKey((byte)e.KeyCode, false);
            }
        };
        
        // Start receive thread
        Thread receiveThread = new Thread(ReceiveScreen);
        receiveThread.IsBackground = true;
        receiveThread.Start();
        
        // Timeout check thread
        Thread timeoutThread = new Thread(() => {
            while (true)
            {
                Thread.Sleep(1000);
                if ((DateTime.Now - lastFrame).TotalSeconds > 5)
                {
                    pb.Invoke((MethodInvoker)(() => {
                        MessageBox.Show("Server stopped responding");
                        Application.Exit();
                    }));
                    break;
                }
            }
        });
        timeoutThread.IsBackground = true;
        timeoutThread.Start();
        
        Application.Run(form);
    }
    
    static void ReceiveScreen()
    {
        byte[] lenBytes = new byte[4];
        int failedReads = 0;
        
        while (true)
        {
            try
            {
                // Check if connection is dead
                if (client.Client.Poll(1000, SelectMode.SelectRead) && client.Client.Available == 0)
                {
                    pb.Invoke((MethodInvoker)(() => {
                        MessageBox.Show("Connection to server lost");
                        Application.Exit();
                    }));
                    break;
                }
                
                int read = stream.Read(lenBytes, 0, 4);
                if (read == 0) break;
                
                int len = BitConverter.ToInt32(lenBytes, 0);
                if (len <= 0 || len > 10 * 1024 * 1024) break; // Sanity check
                
                byte[] imgData = new byte[len];
                int total = 0;
                while (total < len)
                {
                    int bytesRead = stream.Read(imgData, total, len - total);
                    if (bytesRead == 0) throw new Exception("Connection lost");
                    total += bytesRead;
                }
                
                using (MemoryStream ms = new MemoryStream(imgData))
                {
                    Image img = Image.FromStream(ms);
                    pb.Invoke((MethodInvoker)(() => {
                        pb.Image?.Dispose();
                        pb.Image = (Image)img.Clone();
                    }));
                }
                
                lastFrame = DateTime.Now;
                failedReads = 0;
            }
            catch
            {
                failedReads++;
                if (failedReads > 5)
                {
                    pb.Invoke((MethodInvoker)(() => {
                        MessageBox.Show("Connection to server lost");
                        Application.Exit();
                    }));
                    break;
                }
                Thread.Sleep(100);
            }
        }
    }
    
    static void SendCommand(string cmd)
    {
        try
        {
            byte[] data = System.Text.Encoding.ASCII.GetBytes(cmd);
            stream.Write(data, 0, data.Length);
        }
        catch { }
    }
    
    static void SendMouse(int x, int y)
    {
        SendCommand($"MOUSE,{x},{y}");
    }
    
    static void SendClick(int x, int y, string button)
    {
        SendCommand($"CLICK,{x},{y},{button}");
    }
    
    static void SendKey(byte key, bool down)
    {
        SendCommand($"KEY,{key},{down}");
    }
}
