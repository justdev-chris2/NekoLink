using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

class NekoLinkServer
{
    static ClientWebSocket ws;
    static NotifyIcon trayIcon;
    static ContextMenuStrip trayMenu;
    static bool running = true;
    static StreamWriter log;
    static bool hostRegistered = false;
    
    [STAThread]
    static void Main()
    {
        // Force create log file immediately
        try
        {
            log = new StreamWriter("server_debug.txt", true);
            log.WriteLine($"{DateTime.Now:HH:mm:ss} - SERVER INITIALIZING");
            log.Flush();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create log: {ex.Message}");
            return;
        }
        
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        
        // Hide console
        var handle = GetConsoleWindow();
        ShowWindow(handle, 0);
        
        Log("Server starting...");
        
        // Connect to relay
        string relayUrl = "ws://fantastic-umbrella-jjpgj56jrvgvc7g9-8080.app.github.dev";
        ConnectToRelay(relayUrl).Wait();
        
        // Start screen capture thread
        Thread captureThread = new Thread(CaptureAndSend);
        captureThread.Start();
        
        // Setup tray
        trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Show Debug", null, (s, e) => ShowDebug());
        trayMenu.Items.Add("Exit", null, (s, e) => { 
            Log("Exit clicked");
            running = false; 
            trayIcon.Visible = false;
            Application.Exit(); 
            Environment.Exit(0);
        });
        
        trayIcon = new NotifyIcon();
        trayIcon.Text = "NekoLink Server (Relay)";
        trayIcon.Icon = SystemIcons.Application;
        trayIcon.ContextMenuStrip = trayMenu;
        trayIcon.Visible = true;
        trayIcon.ShowBalloonTip(1000, "NekoLink", "Connected to relay", ToolTipIcon.Info);
        
        Application.ApplicationExit += (s, e) => {
            Log("Application exiting");
            running = false;
            trayIcon?.Dispose();
            log?.Close();
            if (ws != null && ws.State == WebSocketState.Open)
            {
                try { ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).Wait(); } catch { }
            }
        };
        
        Log("Server initialized");
        Application.Run();
    }
    
    static async Task ConnectToRelay(string relayUrl)
    {
        try
        {
            ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(relayUrl), CancellationToken.None);
            Log($"Connected to relay: {relayUrl}");
            
            // Register as host
            string registerMsg = "{\"type\":\"register\",\"role\":\"host\"}";
            byte[] regBytes = Encoding.UTF8.GetBytes(registerMsg);
            await ws.SendAsync(new ArraySegment<byte>(regBytes), WebSocketMessageType.Text, true, CancellationToken.None);
            hostRegistered = true;
            
            // Start receiving messages (for control commands)
            _ = Task.Run(ReceiveMessages);
        }
        catch (Exception ex)
        {
            Log($"Relay connection error: {ex.Message}");
            MessageBox.Show($"Failed to connect to relay: {ex.Message}");
            Environment.Exit(1);
        }
    }
    
    static async Task ReceiveMessages()
    {
        byte[] buffer = new byte[4096];
        
        while (ws != null && ws.State == WebSocketState.Open && running)
        {
            try
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Log($"Received: {message}");
                    
                    // Parse control commands
                    if (message.Contains("\"type\":\"control\""))
                    {
                        int cmdStart = message.IndexOf("\"command\":\"") + 11;
                        int cmdEnd = message.IndexOf("\"", cmdStart);
                        if (cmdStart > 10 && cmdEnd > cmdStart)
                        {
                            string command = message.Substring(cmdStart, cmdEnd - cmdStart);
                            ProcessCommand(command);
                        }
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
    
    static void ProcessCommand(string command)
    {
        try
        {
            Log($"Processing command: {command}");
            string[] parts = command.Split(',');
            
            if (parts[0] == "MOUSE" && parts.Length >= 3)
            {
                if (int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int y))
                {
                    Cursor.Position = new Point(x, y);
                }
            }
            else if (parts[0] == "CLICKDOWN" && parts.Length >= 4)
            {
                if (int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int y))
                {
                    Cursor.Position = new Point(x, y);
                    if (parts[3].Contains("Left"))
                        mouse_event(0x02, 0, 0, 0, UIntPtr.Zero);
                    else if (parts[3].Contains("Right"))
                        mouse_event(0x08, 0, 0, 0, UIntPtr.Zero);
                }
            }
            else if (parts[0] == "CLICKUP" && parts.Length >= 2)
            {
                if (parts[1].Contains("Left"))
                    mouse_event(0x04, 0, 0, 0, UIntPtr.Zero);
                else if (parts[1].Contains("Right"))
                    mouse_event(0x10, 0, 0, 0, UIntPtr.Zero);
            }
            else if (parts[0] == "KEY" && parts.Length >= 3)
            {
                if (byte.TryParse(parts[1], out byte key))
                {
                    uint flags = parts[2] == "True" ? 0u : 2u;
                    keybd_event(key, 0, flags, UIntPtr.Zero);
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Command error: {ex.Message}");
        }
    }
    
    static void CaptureAndSend()
    {
        // Get encoder with fully qualified name to avoid ambiguity
        System.Drawing.Imaging.Encoder jpegEncoder = System.Drawing.Imaging.Encoder.Quality;
        var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(jpegEncoder, 70L);
        
        ImageCodecInfo jpegCodec = null;
        foreach (ImageCodecInfo codec in ImageCodecInfo.GetImageEncoders())
        {
            if (codec.FormatID == ImageFormat.Jpeg.Guid)
            {
                jpegCodec = codec;
                break;
            }
        }
        
        int frameCount = 0;
        DateTime lastLog = DateTime.Now;
        
        while (running)
        {
            try
            {
                if (ws == null || ws.State != WebSocketState.Open || !hostRegistered)
                {
                    Thread.Sleep(1000);
                    continue;
                }
                
                using (Bitmap bmp = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                        g.CopyFromScreen(0, 0, 0, 0, bmp.Size);
                    
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bmp.Save(ms, jpegCodec, encoderParams);
                        byte[] imageData = ms.ToArray();
                        
                        // Convert to base64 for JSON
                        string base64 = Convert.ToBase64String(imageData);
                        string frameJson = $"{{\"type\":\"frame\",\"data\":\"{base64}\"}}";
                        
                        byte[] sendData = Encoding.UTF8.GetBytes(frameJson);
                        ws.SendAsync(new ArraySegment<byte>(sendData), WebSocketMessageType.Text, true, CancellationToken.None).Wait();
                        
                        frameCount++;
                        if ((DateTime.Now - lastLog).TotalSeconds >= 10)
                        {
                            Log($"Sending ~{frameCount/10}fps");
                            frameCount = 0;
                            lastLog = DateTime.Now;
                        }
                    }
                }
                
                Thread.Sleep(33); // ~30fps
            }
            catch (Exception ex)
            {
                Log($"Capture error: {ex.Message}");
                Thread.Sleep(1000);
            }
        }
    }
    
    static void ShowDebug()
    {
        try
        {
            if (File.Exists("server_debug.txt"))
            {
                string content = File.ReadAllText("server_debug.txt");
                if (content.Length > 5000)
                    content = content.Substring(content.Length - 5000);
                MessageBox.Show(content, "Server Debug");
            }
            else
            {
                MessageBox.Show("No debug file yet", "Server Debug");
            }
        }
        catch { }
    }
    
    static void Log(string message)
    {
        try
        {
            string logMsg = $"{DateTime.Now:HH:mm:ss} - {message}";
            Console.WriteLine(logMsg);
            if (log != null)
            {
                log.WriteLine(logMsg);
                log.Flush();
            }
        }
        catch { }
    }
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
