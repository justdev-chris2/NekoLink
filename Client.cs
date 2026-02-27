using System;
using System.Drawing;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;

class NekoLinkClient
{
    static TcpClient controlClient;
    static NetworkStream controlStream;
    static bool keyboardLocked = false;
    static Form form;
    static Label statusLabel;
    static Button lockBtn;
    static Button unlockBtn;
    static Process ffmpegProcess;
    static StreamWriter log;
    
    [STAThread]
    static void Main(string[] args)
    {
        // Setup logging to file only
        log = new StreamWriter("client_debug.txt", true);
        Log("Client starting...");
        
        string serverIp = args.Length > 0 ? args[0] : "";
        
        if (string.IsNullOrEmpty(serverIp))
        {
            serverIp = Microsoft.VisualBasic.Interaction.InputBox("Enter server IP:", "NekoLink", "192.168.1.", 500, 500);
            if (string.IsNullOrEmpty(serverIp)) return;
        }
        
        Log($"Server IP: {serverIp}");
        
        try
        {
            // Connect control channel
            Log("Connecting to control port 5901...");
            controlClient = new TcpClient();
            controlClient.Connect(serverIp, 5901);
            controlStream = controlClient.GetStream();
            Log("Control connected!");
        }
        catch (Exception ex)
        {
            Log($"Control connection failed: {ex.Message}");
            MessageBox.Show($"Could not connect to control server: {ex.Message}");
            return;
        }
        
        // Start ffmpeg video
        Log("Starting ffmpeg video...");
        StartVideo(serverIp);
        
        // Create simple control window
        CreateWindow();
        
        Application.Run(form);
    }
    
    static void StartVideo(string serverIp)
    {
        try
        {
            string ffmpeg = "ffmpeg.exe";
            
            if (!File.Exists(ffmpeg))
            {
                Log($"ERROR: {ffmpeg} not found!");
                MessageBox.Show("ffmpeg.exe not found! Put it in the same folder.");
                return;
            }
            
            Log($"Launching: {ffmpeg} -i udp://{serverIp}:5900...");
            
            ffmpegProcess = new Process();
            ffmpegProcess.StartInfo.FileName = ffmpeg;
            ffmpegProcess.StartInfo.Arguments = $"-i udp://{serverIp}:5900?pkt_size=1316 -f sdl \"NekoLink - {serverIp}\"";
            ffmpegProcess.StartInfo.UseShellExecute = false;
            ffmpegProcess.StartInfo.CreateNoWindow = true;
            ffmpegProcess.StartInfo.RedirectStandardError = true;
            
            ffmpegProcess.Start();
            Log($"ffmpeg started with PID: {ffmpegProcess.Id}");
            
            // Log ffmpeg errors to file only
            ffmpegProcess.BeginErrorReadLine();
            ffmpegProcess.ErrorDataReceived += (s, e) => {
                if (!string.IsNullOrEmpty(e.Data))
                    Log($"ffmpeg: {e.Data}");
            };
        }
        catch (Exception ex)
        {
            Log($"ffmpeg error: {ex.Message}");
            MessageBox.Show($"Failed to start ffmpeg: {ex.Message}");
        }
    }
    
    static void CreateWindow()
    {
        form = new Form();
        form.Text = "NekoLink Control";
        form.Size = new Size(300, 150);
        form.StartPosition = FormStartPosition.CenterScreen;
        form.TopMost = true;
        form.FormBorderStyle = FormBorderStyle.FixedToolWindow;
        form.KeyPreview = true;
        
        statusLabel = new Label();
        statusLabel.Text = "Status: Unlocked";
        statusLabel.Dock = DockStyle.Top;
        statusLabel.Height = 30;
        statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        statusLabel.Font = new Font("Arial", 10, FontStyle.Bold);
        
        FlowLayoutPanel buttonPanel = new FlowLayoutPanel();
        buttonPanel.Dock = DockStyle.Fill;
        buttonPanel.FlowDirection = FlowDirection.LeftToRight;
        buttonPanel.Padding = new Padding(10);
        
        lockBtn = new Button();
        lockBtn.Text = "Lock";
        lockBtn.Size = new Size(80, 30);
        lockBtn.Click += (s, e) => Lock();
        
        unlockBtn = new Button();
        unlockBtn.Text = "Unlock";
        unlockBtn.Size = new Size(80, 30);
        unlockBtn.Click += (s, e) => Unlock();
        
        Button fullscreenBtn = new Button();
        fullscreenBtn.Text = "Fullscreen";
        fullscreenBtn.Size = new Size(80, 30);
        fullscreenBtn.Click += (s, e) => {
            if (ffmpegProcess != null && !ffmpegProcess.HasExited)
            {
                SetForegroundWindow(ffmpegProcess.MainWindowHandle);
                SendKeys.SendWait("%{ENTER}");
            }
        };
        
        buttonPanel.Controls.Add(lockBtn);
        buttonPanel.Controls.Add(unlockBtn);
        buttonPanel.Controls.Add(fullscreenBtn);
        
        form.Controls.Add(buttonPanel);
        form.Controls.Add(statusLabel);
        
        // Keyboard events
        form.KeyDown += (s, e) => {
            if (e.Control && e.KeyCode == Keys.RControlKey)
            {
                Unlock();
            }
            
            if (keyboardLocked && !e.Control)
            {
                SendKey((byte)e.KeyCode, true);
                Log($"Sent key: {e.KeyCode}");
            }
        };
        
        form.KeyUp += (s, e) => {
            if (keyboardLocked)
            {
                SendKey((byte)e.KeyCode, false);
            }
        };
        
        form.FormClosing += (s, e) => {
            Log("Closing...");
            try { ffmpegProcess?.Kill(); } catch { }
        };
    }
    
    static void Lock()
    {
        keyboardLocked = true;
        statusLabel.Text = "Status: 🔒 LOCKED";
        statusLabel.ForeColor = Color.Red;
        form.Text = "NekoLink [LOCKED]";
        Log("Locked");
    }
    
    static void Unlock()
    {
        keyboardLocked = false;
        statusLabel.Text = "Status: 🔓 Unlocked";
        statusLabel.ForeColor = Color.Green;
        form.Text = "NekoLink Control";
        Log("Unlocked");
    }
    
    static void SendCommand(string cmd)
    {
        try
        {
            byte[] data = System.Text.Encoding.ASCII.GetBytes(cmd);
            controlStream.Write(data, 0, data.Length);
        }
        catch (Exception ex)
        {
            Log($"Send error: {ex.Message}");
        }
    }
    
    static void SendKey(byte key, bool down)
    {
        SendCommand($"KEY,{key},{down}");
    }
    
    static void Log(string message)
    {
        try
        {
            log.WriteLine($"{DateTime.Now:HH:mm:ss} - {message}");
            log.Flush();
        }
        catch { }
    }
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);
}
