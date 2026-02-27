using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using Timer = System.Windows.Forms.Timer;

class NekoLinkServer
{
    static TcpListener controlServer;
    static NotifyIcon trayIcon;
    static StreamWriter log;
    static Process ffmpegProcess;
    static ListBox debugBox;
    static Form window;
    
    [STAThread]
    static void Main()
    {
        // Setup logging
        log = new StreamWriter("server_debug.txt", true);
        Log("Server starting...");
        
        // Hide console
        var handle = GetConsoleWindow();
        ShowWindow(handle, 0);
        
        // Create hidden window for debugging
        Thread formThread = new Thread(CreateWindow);
        formThread.SetApartmentState(ApartmentState.STA);
        formThread.Start();
        
        // Setup tray
        trayIcon = new NotifyIcon();
        trayIcon.Icon = SystemIcons.Application;
        trayIcon.Text = "NekoLink Server";
        trayIcon.Visible = true;
        
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add("Show Debug", null, (s, e) => { window?.Show(); window.WindowState = FormWindowState.Normal; });
        menu.Items.Add("Restart Video", null, (s, e) => RestartVideo());
        menu.Items.Add("Exit", null, (s, e) => { Log("Shutting down..."); Application.Exit(); Environment.Exit(0); });
        trayIcon.ContextMenuStrip = menu;
        
        trayIcon.ShowBalloonTip(1000, "NekoLink", "Server running", ToolTipIcon.Info);
        
        // Show local IPs in debug
        Log("Your IPs:");
        foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                Log($"  {ip}");
        
        // Start video
        Thread videoThread = new Thread(StartVideo);
        videoThread.Start();
        
        // Start control
        Thread controlThread = new Thread(StartControlServer);
        controlThread.Start();
        
        Application.Run();
    }
    
    static void CreateWindow()
    {
        window = new Form();
        window.Text = "NekoLink Server Debug";
        window.Size = new Size(500, 300);
        window.StartPosition = FormStartPosition.CenterScreen;
        window.FormClosing += (s, e) => { e.Cancel = true; window.Hide(); };
        
        debugBox = new ListBox();
        debugBox.Dock = DockStyle.Fill;
        debugBox.Font = new Font("Consolas", 9);
        debugBox.HorizontalScrollbar = true;
        
        Button restartBtn = new Button();
        restartBtn.Text = "Restart Video";
        restartBtn.Dock = DockStyle.Bottom;
        restartBtn.Height = 30;
        restartBtn.Click += (s, e) => RestartVideo();
        
        window.Controls.Add(debugBox);
        window.Controls.Add(restartBtn);
        
        // Update debug from log
        Timer timer = new Timer();
        timer.Interval = 1000;
        timer.Tick += (s, e) => {
            try
            {
                if (File.Exists("server_debug.txt"))
                {
                    var lines = File.ReadAllLines("server_debug.txt");
                    debugBox.Items.Clear();
                    foreach (var line in lines)
                        debugBox.Items.Add(line);
                    debugBox.TopIndex = debugBox.Items.Count - 1;
                }
            }
            catch { }
        };
        timer.Start();
    }
    
    static void StartVideo()
    {
        try
        {
            string ffmpeg = "ffmpeg.exe";
            
            if (!File.Exists(ffmpeg))
            {
                Log($"ERROR: {ffmpeg} not found!");
                return;
            }
            
            Log("Starting video stream...");
            
            ffmpegProcess = new Process();
            ffmpegProcess.StartInfo.FileName = ffmpeg;
            ffmpegProcess.StartInfo.Arguments = "-f gdigrab -framerate 30 -i desktop -vf format=yuv420p -f mpegts udp://0.0.0.0:5900?pkt_size=1316";
            ffmpegProcess.StartInfo.UseShellExecute = false;
            ffmpegProcess.StartInfo.CreateNoWindow = true;
            ffmpegProcess.StartInfo.RedirectStandardError = true;
            ffmpegProcess.StartInfo.RedirectStandardOutput = true;
            
            ffmpegProcess.Start();
            Log($"ffmpeg started with PID: {ffmpegProcess.Id}");
            
            ffmpegProcess.BeginErrorReadLine();
            ffmpegProcess.ErrorDataReceived += (s, e) => {
                if (!string.IsNullOrEmpty(e.Data))
                    Log($"ffmpeg: {e.Data}");
            };
            
            ffmpegProcess.WaitForExit();
            Log("ffmpeg exited");
        }
        catch (Exception ex)
        {
            Log($"Video error: {ex.Message}");
        }
    }
    
    static void RestartVideo()
    {
        Log("Restarting video...");
        try { ffmpegProcess?.Kill(); } catch { }
        Thread.Sleep(1000);
        Thread videoThread = new Thread(StartVideo);
        videoThread.Start();
    }
    
    static void StartControlServer()
    {
        try
        {
            controlServer = new TcpListener(IPAddress.Any, 5901);
            controlServer.Start();
            Log("Control server started on port 5901");
            
            while (true)
            {
                var client = controlServer.AcceptTcpClient();
                Log("Client connected to control channel");
                ThreadPool.QueueUserWorkItem(HandleClient, client);
            }
        }
        catch (Exception ex)
        {
            Log($"Control server error: {ex.Message}");
        }
    }
    
    static void HandleClient(object obj)
    {
        var client = (TcpClient)obj;
        var stream = client.GetStream();
        byte[] buffer = new byte[1024];
        
        Log("Control handler started");
        
        while (client.Connected)
        {
            try
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read == 0) break;
                
                string command = System.Text.Encoding.ASCII.GetString(buffer, 0, read);
                Log($"CMD: {command}");
                
                string[] parts = command.Split(',');
                
                switch(parts[0])
                {
                    case "MOUSE":
                        Cursor.Position = new Point(int.Parse(parts[1]), int.Parse(parts[2]));
                        break;
                    case "CLICK":
                        Cursor.Position = new Point(int.Parse(parts[1]), int.Parse(parts[2]));
                        if (parts[3].Contains("Left"))
                        {
                            mouse_event(0x02, 0, 0, 0, UIntPtr.Zero);
                            mouse_event(0x04, 0, 0, 0, UIntPtr.Zero);
                        }
                        break;
                    case "KEY":
                        uint flags = parts[2] == "True" ? 0u : 2u;
                        keybd_event(byte.Parse(parts[1]), 0, flags, UIntPtr.Zero);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log($"Handler error: {ex.Message}");
                break;
            }
        }
        
        Log("Client disconnected");
    }
    
    static void Log(string message)
    {
        try
        {
            string logMsg = $"{DateTime.Now:HH:mm:ss} - {message}";
            Console.WriteLine(logMsg);
            if (log != null) log.WriteLine(logMsg);
            if (log != null) log.Flush();
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
