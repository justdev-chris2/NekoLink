using System;
using System.Drawing;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

class NekoLinkClient
{
    static ClientWebSocket ws;
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
    
    // Mouse throttling
    static DateTime lastMouseSend = DateTime.Now;
    static int lastX = -1, lastY = -1;
    
    [STAThread]
    static void Main()
    {
        // Setup logging
        log = new StreamWriter("client_debug.txt", true);
        Log("Client starting...");
        
        // Relay server URL (your Codespaces URL)
        string relayUrl = "ws://fantastic-umbrella-jjpgj56jrvgvc7g9-8080.app.github.dev";
        
        ConnectToRelay(relayUrl).Wait();
        
        form = new Form();
        form.Text = "NekoLink - Relay Mode";
        form.WindowState = FormWindowState.Maximized;
        form.KeyPreview = true;
        form.BackColor = Color.Black;
        form.FormClosing += (s, e) => {
            Log("Form closing");
            log?.Close();
            ws?.CloseAsync();
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
        
        // Mouse events - THROTTLED
        pb.MouseMove += (s, e) => {
            if (!locked || pb.Image == null) return;
            
            // Throttle to 30 updates per second
            if ((DateTime.Now - lastMouseSend).TotalMilliseconds < 33)
                return;
            
            float ratioX = (float)Screen.PrimaryScreen.Bounds.Width / pb.Width;
            float ratioY = (float)Screen.PrimaryScreen.Bounds.Height / pb.Height;
            int remoteX = (int)(e.X * ratioX);
            int remoteY = (int)(e.Y * ratioY);
            
            // Skip tiny movements
            if (Math.Abs(remoteX - lastX) < 5 && Math.Abs(remoteY - lastY) < 5)
                return;
            
            remoteX = Math.Max(0, Math.Min(Screen.PrimaryScreen.Bounds.Width - 1, remoteX));
            remoteY = Math.Max(0, Math.Min(Screen.PrimaryScreen.Bounds.Height - 1, remoteY));
            
            SendCommand($"MOUSE,{remoteX},{remoteY}");
            lastMouseSend = DateTime.Now;
            lastX = remoteX;
            lastY = remoteY;
        };
        
        pb.MouseDown += (s, e) => {
            if (!locked || pb.Image == null) return;
            
            float ratioX = (float)Screen.PrimaryScreen.Bounds.Width / pb.Width;
            float ratioY = (float)Screen.PrimaryScreen.Bounds.Height / pb.Height;
            int remoteX = (int)(e.X * ratioX);
            int remoteY = (int)(e.Y * ratioY);
            
            if (e.Button == MouseButtons.Left)
                SendCommand($"CLICKDOWN,{remoteX},{remoteY},Left");
            else if (e.Button == MouseButtons.Right)
                SendCommand($"CLICKDOWN,{remoteX},{remoteY},Right");
        };
        
        pb.MouseUp += (s, e) => {
            if (!locked) return;
            
            if (e.Button == MouseButtons.Left)
                SendCommand("CLICKUP,Left");
            else if (e.Button == MouseButtons.Right)
                SendCommand("CLICKUP,Right");
        };
        
        // Click to lock
        pb.Click += (s, e) => {
            locked = true;
            statusLabel.Text = "🔒 LOCKED - Press Right Ctrl to unlock";
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
                SendCommand($"KEY,{(byte)e.KeyCode},True");
                Log($"Key down: {e.KeyCode}");
            }
        };
        
        form.KeyUp += (s, e) => {
            if (locked && !e.Control && e.KeyCode != Keys.RControlKey)
            {
                SendCommand($"KEY,{(byte)e.KeyCode},False");
                Log($"Key up: {e.KeyCode}");
            }
        };
        
        Log("Application started");
        Application.Run(form);
    }
    
    static async Task ConnectToRelay(string relayUrl)
    {
        try
        {
            ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(relayUrl), CancellationToken.None);
            Log($"Connected to relay: {relayUrl}");
            
            // Register as viewer
            string registerMsg = "{\"type\":\"register\",\"role\":\"viewer\"}";
            byte[] regBytes = Encoding.UTF8.GetBytes(registerMsg);
            await ws.SendAsync(new ArraySegment<byte>(regBytes), WebSocketMessageType.Text, true, CancellationToken.None);
            
            // Start receiving messages
            _ = Task.Run(ReceiveMessages);
        }
        catch (Exception ex)
        {
            Log($"Connection error: {ex.Message}");
            MessageBox.Show($"Failed to connect to relay: {ex.Message}");
            Environment.Exit(1);
        }
    }
    
    static async Task ReceiveMessages()
    {
        byte[] buffer = new byte[1024 * 1024]; // 1MB buffer
        
        while (ws.State == WebSocketState.Open)
        {
            try
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    
                    // Parse JSON (simplified - for production use Newtonsoft.Json)
                    if (message.Contains("\"type\":\"frame\""))
                    {
                        // Extract base64 data (quick and dirty)
                        int dataStart = message.IndexOf("\"data\":\"") + 8;
                        int dataEnd = message.LastIndexOf("\"");
                        string base64Data = message.Substring(dataStart, dataEnd - dataStart);
                        
                        byte[] imgData = Convert.FromBase64String(base64Data);
                        
                        using (MemoryStream ms = new MemoryStream(imgData))
                        {
                            Image img = Image.FromStream(ms);
                            pb.Invoke((MethodInvoker)(() => {
                                pb.Image?.Dispose();
                                pb.Image = (Image)img.Clone();
                            }));
                        }
                        
                        lastFrame = DateTime.Now;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Receive error: {ex.Message}");
                break;
            }
        }
        
        Log("Disconnected from relay");
    }
    
    static void SendCommand(string cmd)
    {
        try
        {
            if (ws?.State == WebSocketState.Open)
            {
                string jsonCmd = $"{{\"type\":\"control\",\"command\":\"{cmd}\"}}";
                byte[] data = Encoding.UTF8.GetBytes(jsonCmd);
                ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None).Wait();
            }
        }
        catch (Exception ex)
        {
            Log($"Send error: {ex.Message}");
        }
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
        }
        else
        {
            form.FormBorderStyle = prevBorderStyle;
            form.WindowState = prevWindowState;
            topPanel.Visible = true;
        }
        
        Log($"Fullscreen: {fullscreen}");
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
