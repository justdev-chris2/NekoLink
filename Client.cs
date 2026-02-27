using System;
using System.Drawing;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;

class NekoLinkClient
{
    static TcpClient controlClient;
    static NetworkStream controlStream;
    static bool keyboardLocked = false;
    static Form form;
    static Panel controlPanel;
    static Label statusLabel;
    static Button lockBtn;
    static Button unlockBtn;
    static Process ffmpegProcess;
    
    [STAThread]
    static void Main(string[] args)
    {
        string serverIp = args.Length > 0 ? args[0] : "";
        
        if (string.IsNullOrEmpty(serverIp))
        {
            serverIp = Microsoft.VisualBasic.Interaction.InputBox("Enter server IP:", "NekoLink", "192.168.1.", 500, 500);
            if (string.IsNullOrEmpty(serverIp)) return;
        }
        
        try
        {
            // Connect control channel
            controlClient = new TcpClient();
            controlClient.Connect(serverIp, 5901);
            controlStream = controlClient.GetStream();
        }
        catch
        {
            MessageBox.Show("Could not connect to control server");
            return;
        }
        
        // Start ffmpeg video in a window
        StartVideo(serverIp);
        
        // Create GUI
        CreateWindow();
        
        Application.Run(form);
    }
    
    static void StartVideo(string serverIp)
    {
        string ffmpeg = "ffmpeg.exe";
        ffmpegProcess = new Process();
        ffmpegProcess.StartInfo.FileName = ffmpeg;
        ffmpegProcess.StartInfo.Arguments = $"-i udp://{serverIp}:5900?pkt_size=1316 -f sdl \"NekoLink - {serverIp}\"";
        ffmpegProcess.StartInfo.UseShellExecute = false;
        ffmpegProcess.StartInfo.CreateNoWindow = true;
        ffmpegProcess.Start();
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
        
        controlPanel = new Panel();
        controlPanel.Dock = DockStyle.Fill;
        controlPanel.Padding = new Padding(10);
        
        Label infoLabel = new Label();
        infoLabel.Text = "NekoLink Connected";
        infoLabel.Dock = DockStyle.Top;
        infoLabel.Height = 30;
        infoLabel.TextAlign = ContentAlignment.MiddleCenter;
        
        statusLabel = new Label();
        statusLabel.Text = "Status: Unlocked";
        statusLabel.Dock = DockStyle.Top;
        statusLabel.Height = 25;
        statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        
        FlowLayoutPanel buttonPanel = new FlowLayoutPanel();
        buttonPanel.Dock = DockStyle.Top;
        buttonPanel.Height = 40;
        buttonPanel.FlowDirection = FlowDirection.LeftToRight;
        buttonPanel.Padding = new Padding(5);
        
        lockBtn = new Button();
        lockBtn.Text = "Lock";
        lockBtn.Size = new Size(80, 30);
        lockBtn.Click += (s, e) => Lock();
        
        unlockBtn = new Button();
        unlockBtn.Text = "Unlock";
        unlockBtn.Size = new Size(80, 30);
        unlockBtn.Click += (s, e) => Unlock();
        
        buttonPanel.Controls.Add(lockBtn);
        buttonPanel.Controls.Add(unlockBtn);
        
        controlPanel.Controls.Add(buttonPanel);
        controlPanel.Controls.Add(statusLabel);
        controlPanel.Controls.Add(infoLabel);
        
        form.Controls.Add(controlPanel);
        
        // Keyboard events
        form.KeyDown += (s, e) => {
            if (e.Control && e.KeyCode == Keys.RControlKey)
            {
                Unlock();
            }
            
            if (keyboardLocked && !e.Control)
            {
                SendKey((byte)e.KeyCode, true);
            }
        };
        
        form.KeyUp += (s, e) => {
            if (keyboardLocked)
            {
                SendKey((byte)e.KeyCode, false);
            }
        };
        
        form.FormClosing += (s, e) => {
            try { ffmpegProcess.Kill(); } catch { }
        };
    }
    
    static void Lock()
    {
        keyboardLocked = true;
        statusLabel.Text = "Status: LOCKED";
        statusLabel.ForeColor = Color.Red;
        form.Text = "NekoLink Control [LOCKED]";
    }
    
    static void Unlock()
    {
        keyboardLocked = false;
        statusLabel.Text = "Status: Unlocked";
        statusLabel.ForeColor = Color.Green;
        form.Text = "NekoLink Control";
    }
    
    static void SendCommand(string cmd)
    {
        try
        {
            byte[] data = System.Text.Encoding.ASCII.GetBytes(cmd);
            controlStream.Write(data, 0, data.Length);
        }
        catch { }
    }
    
    static void SendKey(byte key, bool down)
    {
        SendCommand($"KEY,{key},{down}");
    }
}
