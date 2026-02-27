using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;

class NekoLinkServer
{
    static TcpListener controlServer;
    static NotifyIcon trayIcon;
    
    static void Main()
    {
        // Hide console
        var handle = GetConsoleWindow();
        ShowWindow(handle, 0);
        
        // Setup tray
        trayIcon = new NotifyIcon();
        trayIcon.Icon = SystemIcons.Application;
        trayIcon.Text = "NekoLink Server";
        trayIcon.Visible = true;
        trayIcon.ShowBalloonTip(1000, "NekoLink", "Server running", ToolTipIcon.Info);
        
        // Start video
        Thread videoThread = new Thread(StartVideo);
        videoThread.Start();
        
        // Start control
        StartControlServer();
        
        Application.Run();
    }
    
    static void StartVideo()
    {
        string ffmpeg = "ffmpeg.exe";
        Process process = new Process();
        process.StartInfo.FileName = ffmpeg;
        process.StartInfo.Arguments = "-f gdigrab -framerate 30 -i desktop -vf format=yuv420p -f mpegts udp://0.0.0.0:5900?pkt_size=1316";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.Start();
        process.WaitForExit();
    }
    
    static void StartControlServer()
    {
        controlServer = new TcpListener(IPAddress.Any, 5901);
        controlServer.Start();
        
        while (true)
        {
            var client = controlServer.AcceptTcpClient();
            ThreadPool.QueueUserWorkItem(HandleClient, client);
        }
    }
    
    static void HandleClient(object obj)
    {
        var client = (TcpClient)obj;
        var stream = client.GetStream();
        byte[] buffer = new byte[1024];
        
        while (client.Connected)
        {
            try
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read == 0) break;
                
                string command = System.Text.Encoding.ASCII.GetString(buffer, 0, read);
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
            catch { }
        }
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
