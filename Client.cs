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
    static StreamWriter log;
    static bool fullscreen = false;
    static FormWindowState prevWindowState;
    static FormBorderStyle prevBorderStyle;
    static Panel topPanel;
    
    [STAThread]
    static void Main()
    {
        // Setup logging
        log = new StreamWriter("client_debug.txt", true);
        Log("Client starting...");
        
        string ip = Microsoft.VisualBasic.Interaction.InputBox("Enter Server IP:", "NekoLink", "192.168.1.", 500, 500);
        if (string.IsNullOrEmpty(ip)) return;
        
        Log($"Server IP: {ip}");
        
        try
        {
            client = new TcpClient();
            client.Connect(ip, 5900);
            client.NoDelay = true; // Disable Nagle's algorithm for less latency
            client.ReceiveBufferSize = 65536;
            client.SendBufferSize = 65536;
            stream = client.GetStream();
            Log("Connected to server");
        }
        catch (Exception ex)
        {
            Log($"Connection failed: {ex.Message}");
            MessageBox.Show("Could not connect to server");
            return;
        }
        
        form = new Form();
        form.Text = "NekoLink";
        form.WindowState = FormWindowState.Maximized;
        form.KeyPreview = true;
        form.BackColor = Color.Black;
        form.FormClosing += (s, e) => {
            Log("Form closing");
            log?.Close();
        };
        
        // Top status panel
        topPanel = new Panel();
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
        
        // Mouse events - HIGH FREQUENCY
        pb.MouseMove += (s, e) => {
            if (locked && pb.Image != null)
            {
                float ratioX = (float)Screen.PrimaryScreen.Bounds.Width / pb.Width;
                float ratioY = (float)Screen.PrimaryScreen.Bounds.Height / pb.Height;
                int remoteX = (int)(e.X * ratioX);
                int remoteY = (int)(e.Y * ratioY);
                remoteX = Math.Max(0, Math.Min(Screen.PrimaryScreen.Bounds.Width - 1, remoteX));
                remoteY = Math.Max(0, Math.Min(Screen.PrimaryScreen.Bounds.Height - 1, remoteY));
                SendMouse(remoteX, remoteY);
            }
        };
        
        pb.MouseDown += (s, e) => {
            if (locked && pb.Image != null)
            {
                float ratioX = (float)Screen.PrimaryScreen.Bounds.Width / pb.Width;
                float ratioY = (float)Screen.PrimaryScreen.Bounds.Height / pb.Height;
                int remoteX = (int)(e.X * ratioX);
                int remoteY = (int)(e.Y * ratioY);
                remoteX = Math.Max(0, Math.Min(Screen.PrimaryScreen.Bounds.Width - 1, remoteX));
                remoteY = Math.Max(0, Math.Min(Screen.PrimaryScreen.Bounds.Height - 1, remoteY));
                
                if (e.Button == MouseButtons.Left)
                    SendClickDown(remoteX, remoteY, "Left");
                else if (e.Button == MouseButtons.Right)
                    SendClickDown(remoteX, remoteY, "Right");
            }
        };
        
        pb.MouseUp += (s, e) => {
            if (locked && pb.Image != null)
            {
                if (e.Button == MouseButtons.Left)
                    SendClickUp("Left");
                else if (e.Button == MouseButtons.Right)
                    SendClickUp("Right");
            }
        };
        
        // Click to lock
        pb.Click += (s, e) => {
            locked = true;
            statusLabel.Text = "🔒 LOCKED - Press Right Ctrl or F11 to unlock";
            statusLabel.ForeColor = Color.LightGreen;
            form.Text = "NekoLink [LOCKED]";
            Log("Locked by click");
        };
        
        // Double-click to fullscreen
        pb.DoubleClick += (s, e) => ToggleFullscreen();
        
        // Keyboard events
        form.KeyDown += (s, e) => {
            if (e.KeyCode == Keys.F11)
            {
                ToggleFullscreen();
                e.Handled = true;
            }
            
            if (e.Control && e.KeyCode == Keys.RControlKey)
            {
                locked = false;
                statusLabel.Text = "🔓 Unlocked - Click screen to lock";
                statusLabel.ForeColor = Color.White;
                form.Text = "NekoLink";
                Log("Unlocked by Right Ctrl");
            }
            
            if (locked && !e.Control && e.KeyCode != Keys.RControlKey && e.KeyCode != Keys.F11)
            {
                SendKey((byte)e.KeyCode, true);
                Log($"Key down: {e.KeyCode}");
            }
        };
        
        form.KeyUp += (s, e) => {
            if (locked && !e.Control && e.KeyCode != Keys.RControlKey)
            {
                SendKey((byte)e.KeyCode, false);
                Log($"Key up: {e.KeyCode}");
            }
        };
        
        // Start receive thread with higher priority
        Thread receiveThread = new Thread(ReceiveScreen);
        receiveThread.IsBackground = true;
        receiveThread.Priority = ThreadPriority.AboveNormal;
        receiveThread.Start();
        
        // Timeout check thread
        Thread timeoutThread = new Thread(() => {
            while (true)
            {
                Thread.Sleep(1000);
                if ((DateTime.Now - lastFrame).TotalSeconds > 5)
                {
                    pb.Invoke((MethodInvoker)(() => {
                        Log("Server timeout - no frames for 5 seconds");
                        MessageBox.Show("Server stopped responding");
                        Application.Exit();
                    }));
                    break;
                }
            }
        });
        timeoutThread.IsBackground = true;
        timeoutThread.Start();
        
        Log("Application started");
        Application.Run(form);
    }
    
    static void ToggleFullscreen()
    {
        fullscreen = !fullscreen;
        
        if (fullscreen)
        {
            prevWindowState = form.WindowState;
            prevBorderStyle = form.FormBorderStyle;
            
            form.FormBorderStyle = FormBorderStyle.None;
            form.WindowState = FormWindowState.Normal;
            form.Bounds = Screen.PrimaryScreen.Bounds;
            topPanel.Visible = false;
            
            pb.SizeMode = PictureBoxSizeMode.Zoom;
        }
        else
        {
            form.FormBorderStyle = prevBorderStyle;
            form.WindowState = prevWindowState;
            topPanel.Visible = true;
            pb.SizeMode = PictureBoxSizeMode.Zoom;
        }
        
        Log($"Fullscreen: {fullscreen}");
    }
    
    static void ReceiveScreen()
    {
        byte[] lenBytes = new byte[4];
        int failedReads = 0;
        int frameCount = 0;
        DateTime lastLog = DateTime.Now;
        
        while (true)
        {
            try
            {
                // Read length with timeout
                int read = 0;
                int timeout = 0;
                while (read < 4 && timeout < 50)
                {
                    int bytes = stream.Read(lenBytes, read, 4 - read);
                    if (bytes == 0) throw new Exception("Connection closed");
                    read += bytes;
                    timeout++;
                }
                
                int len = BitConverter.ToInt32(lenBytes, 0);
                if (len <= 0 || len > 10 * 1024 * 1024)
                {
                    Log($"Invalid length: {len}");
                    continue;
                }
                
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
                frameCount++;
                
                if ((DateTime.Now - lastLog).TotalSeconds >= 5)
                {
                    Log($"Receiving ~{frameCount/5}fps");
                    frameCount = 0;
                    lastLog = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                Log($"Receive error: {ex.Message}");
                failedReads++;
                if (failedReads > 3)
                {
                    pb.Invoke((MethodInvoker)(() => {
                        MessageBox.Show("Connection to server lost");
                        Application.Exit();
                    }));
                    break;
                }
                Thread.Sleep(50);
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
        catch (Exception ex)
        {
            Log($"Send error: {ex.Message}");
        }
    }
    
    static void SendMouse(int x, int y)
    {
        SendCommand($"MOUSE,{x},{y}");
    }
    
    static void SendClickDown(int x, int y, string button)
    {
        SendCommand($"CLICKDOWN,{x},{y},{button}");
    }
    
    static void SendClickUp(string button)
    {
        SendCommand($"CLICKUP,{button}");
    }
    
    static void SendKey(byte key, bool down)
    {
        SendCommand($"KEY,{key},{down}");
    }
    
    static void Log(string message)
    {
        try
        {
            string logMsg = $"{DateTime.Now:HH:mm:ss} - {message}";
            Console.WriteLine(logMsg);
            log?.WriteLine(logMsg);
            log?.Flush();
        }
        catch { }
    }
}
