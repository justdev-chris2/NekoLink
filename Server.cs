using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Threading;

class NekoLinkServer : Form
{
    static TcpListener server;
    static NotifyIcon trayIcon;
    static ContextMenuStrip trayMenu;
    static bool running = true;
    
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        
        // Hide console
        var handle = GetConsoleWindow();
        ShowWindow(handle, 0);
        
        // Start server in background
        Thread serverThread = new Thread(RunServer);
        serverThread.Start();
        
        // Setup tray
        trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Show IPs", null, (s, e) => ShowIPs());
        trayMenu.Items.Add("Exit", null, (s, e) => { 
            running = false; 
            trayIcon.Visible = false;
            Application.Exit(); 
            Environment.Exit(0);
        });
        
        trayIcon = new NotifyIcon();
        trayIcon.Text = "NekoLink Server";
        trayIcon.Icon = SystemIcons.Application;
        trayIcon.ContextMenuStrip = trayMenu;
        trayIcon.Visible = true;
        trayIcon.ShowBalloonTip(1000, "NekoLink", "Server running in background", ToolTipIcon.Info);
        
        Application.ApplicationExit += (s, e) => {
            running = false;
            trayIcon?.Dispose();
        };
        
        Application.Run();
    }
    
    static void RunServer()
    {
        try
        {
            server = new TcpListener(System.Net.IPAddress.Any, 5900);
            server.Start();
            
            while (running)
            {
                var client = server.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(HandleClient, client);
            }
        }
        catch { }
    }
    
    static void HandleClient(object obj)
    {
        var client = (TcpClient)obj;
        var stream = client.GetStream();
        
        var jpegCodec = GetEncoder(ImageFormat.Jpeg);
        var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 70L);
        
        while (client.Connected && running)
        {
            try
            {
                using (Bitmap bmp = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                        g.CopyFromScreen(0, 0, 0, 0, bmp.Size);
                    
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bmp.Save(ms, jpegCodec, encoderParams);
                        byte[] data = ms.ToArray();
                        
                        stream.Write(BitConverter.GetBytes(data.Length), 0, 4);
                        stream.Write(data, 0, data.Length);
                    }
                }
                
                // Handle commands
                if (stream.DataAvailable)
                {
                    byte[] cmdBuffer = new byte[1024];
                    int read = stream.Read(cmdBuffer, 0, cmdBuffer.Length);
                    if (read > 0)
                    {
                        string cmd = System.Text.Encoding.ASCII.GetString(cmdBuffer, 0, read);
                        string[] parts = cmd.Split(',');
                        
                        if (parts[0] == "MOUSE" && parts.Length >= 3)
                        {
                            Cursor.Position = new Point(int.Parse(parts[1]), int.Parse(parts[2]));
                        }
                        else if (parts[0] == "CLICK" && parts.Length >= 4)
                        {
                            Cursor.Position = new Point(int.Parse(parts[1]), int.Parse(parts[2]));
                            if (parts[3].Contains("Left"))
                            {
                                mouse_event(0x02, 0, 0, 0, UIntPtr.Zero);
                                Thread.Sleep(50);
                                mouse_event(0x04, 0, 0, 0, UIntPtr.Zero);
                            }
                            else if (parts[3].Contains("Right"))
                            {
                                mouse_event(0x08, 0, 0, 0, UIntPtr.Zero);
                                Thread.Sleep(50);
                                mouse_event(0x10, 0, 0, 0, UIntPtr.Zero);
                            }
                        }
                        else if (parts[0] == "KEY" && parts.Length >= 3)
                        {
                            uint flags = parts[2] == "True" ? 0u : 2u;
                            keybd_event(byte.Parse(parts[1]), 0, flags, UIntPtr.Zero);
                        }
                    }
                }
                
                Thread.Sleep(33); // ~30fps
            }
            catch
            {
                break;
            }
        }
        
        client.Close();
    }
    
    static void ShowIPs()
    {
        string ips = "Server IPs:\n";
        foreach (var ip in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                ips += ip + "\n";
        MessageBox.Show(ips, "NekoLink");
    }
    
    static ImageCodecInfo GetEncoder(ImageFormat format)
    {
        foreach (ImageCodecInfo codec in ImageCodecInfo.GetImageEncoders())
            if (codec.FormatID == format.Guid)
                return codec;
        return null;
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
