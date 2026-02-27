using System;
using System.Drawing;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Net;
using System.Linq;
using System.Threading;

class NekoLinkServer : Form
{
    static TcpListener server;
    static NetworkStream stream;
    static NotifyIcon trayIcon;
    static ContextMenuStrip trayMenu;
    static bool running = true;
    static int fps = 10;
    static TextBox fpsBox;
    static ListBox logBox;
    static Thread serverThread;
    static Label statusLabel;
    
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new NekoLinkServer());
    }
    
    public NekoLinkServer()
    {
        // Build the window
        this.Text = "NekoLink Server";
        this.Size = new Size(500, 400);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormClosing += Form_FormClosing;
        
        // Show local IPs
        Label ipLabel = new Label();
        ipLabel.Text = "Server IPs:\n" + GetLocalIPs();
        ipLabel.Location = new Point(10, 10);
        ipLabel.Size = new Size(300, 60);
        this.Controls.Add(ipLabel);
        
        // FPS control
        Label fpsLabel = new Label();
        fpsLabel.Text = "FPS:";
        fpsLabel.Location = new Point(10, 80);
        fpsLabel.Size = new Size(40, 20);
        this.Controls.Add(fpsLabel);
        
        fpsBox = new TextBox();
        fpsBox.Text = "10";
        fpsBox.Location = new Point(50, 78);
        fpsBox.Size = new Size(40, 20);
        fpsBox.TextChanged += (s, e) => { int.TryParse(fpsBox.Text, out fps); if (fps < 1) fps = 1; };
        this.Controls.Add(fpsBox);
        
        Button applyBtn = new Button();
        applyBtn.Text = "Apply";
        applyBtn.Location = new Point(100, 77);
        applyBtn.Size = new Size(60, 23);
        applyBtn.Click += (s, e) => { int.TryParse(fpsBox.Text, out fps); if (fps < 1) fps = 1; };
        this.Controls.Add(applyBtn);
        
        // Log box
        logBox = new ListBox();
        logBox.Location = new Point(10, 110);
        logBox.Size = new Size(460, 200);
        this.Controls.Add(logBox);
        
        // Status
        statusLabel = new Label();
        statusLabel.Text = "Status: Starting...";
        statusLabel.Location = new Point(10, 320);
        statusLabel.Size = new Size(200, 20);
        this.Controls.Add(statusLabel);
        
        // Setup tray
        SetupTray();
        
        // Start server
        serverThread = new Thread(() => RunServer());
        serverThread.Start();
    }
    
    void SetupTray()
    {
        trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Show", null, (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; });
        trayMenu.Items.Add("Exit", null, (s, e) => { running = false; Application.Exit(); });
        
        trayIcon = new NotifyIcon();
        trayIcon.Text = "NekoLink Server";
        trayIcon.Icon = SystemIcons.Application;
        trayIcon.ContextMenuStrip = trayMenu;
        trayIcon.Visible = true;
        trayIcon.DoubleClick += (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; };
    }
    
    void Form_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            this.Hide();
            trayIcon.ShowBalloonTip(1000, "NekoLink", "Server still running in background", ToolTipIcon.Info);
        }
    }
    
    string GetLocalIPs()
    {
        string ips = "";
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                ips += ip.ToString() + "\n";
        }
        return ips;
    }
    
    void Log(string msg)
    {
        try
        {
            this.Invoke((MethodInvoker)delegate {
                logBox.Items.Add(DateTime.Now.ToString("HH:mm:ss") + " - " + msg);
                logBox.TopIndex = logBox.Items.Count - 1;
            });
        }
        catch { }
    }
    
    void RunServer()
    {
        try
        {
            server = new TcpListener(IPAddress.Any, 5900);
            server.Start();
            Log($"Server started on port 5900");
            
            this.Invoke((MethodInvoker)delegate {
                statusLabel.Text = "Status: Waiting for connection...";
                statusLabel.ForeColor = Color.Orange;
            });
            
            TcpClient client = server.AcceptTcpClient();
            stream = client.GetStream();
            
            Log("Client connected!");
            this.Invoke((MethodInvoker)delegate {
                statusLabel.Text = "Status: Client connected";
                statusLabel.ForeColor = Color.Green;
            });
            
            while (running)
            {
                try
                {
                    // Capture screen
                    Bitmap screen = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
                    using (Graphics g = Graphics.FromImage(screen))
                    {
                        g.CopyFromScreen(0, 0, 0, 0, screen.Size);
                    }
                    
                    // Convert to bytes and send
                    byte[] imgData = (byte[])new ImageConverter().ConvertTo(screen, typeof(byte[]));
                    stream.Write(BitConverter.GetBytes(imgData.Length), 0, 4);
                    stream.Write(imgData, 0, imgData.Length);
                    
                    // Handle commands
                    if (stream.DataAvailable)
                    {
                        try
                        {
                            byte[] buffer = new byte[1024];
                            int read = stream.Read(buffer, 0, buffer.Length);
                            string command = System.Text.Encoding.ASCII.GetString(buffer, 0, read);
                            
                            Log("CMD: " + command);
                            string[] parts = command.Split(',');
                            
                            if (parts.Length > 0)
                            {
                                switch(parts[0])
                                {
                                    case "MOUSE":
                                        if (parts.Length >= 3 && int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int y))
                                            Cursor.Position = new Point(x, y);
                                        break;
                                        
                                    case "CLICK":
                                        if (parts.Length >= 4 && int.TryParse(parts[1], out int cx) && int.TryParse(parts[2], out int cy))
                                        {
                                            Cursor.Position = new Point(cx, cy);
                                            if (parts[3].Contains("Left"))
                                            {
                                                mouse_event(0x02, 0, 0, 0, UIntPtr.Zero);
                                                mouse_event(0x04, 0, 0, 0, UIntPtr.Zero);
                                            }
                                            else if (parts[3].Contains("Right"))
                                            {
                                                mouse_event(0x08, 0, 0, 0, UIntPtr.Zero);
                                                mouse_event(0x10, 0, 0, 0, UIntPtr.Zero);
                                            }
                                        }
                                        break;
                                        
                                    case "KEY":
                                        if (parts.Length >= 3 && byte.TryParse(parts[1], out byte key))
                                        {
                                            uint flags = parts[2] == "True" ? 0u : 2u;
                                            keybd_event(key, 0, flags, UIntPtr.Zero);
                                        }
                                        break;
                                        
                                    case "SET_FPS":
                                        if (parts.Length >= 2 && int.TryParse(parts[1], out int newFps) && newFps > 0)
                                        {
                                            fps = newFps;
                                            this.Invoke((MethodInvoker)delegate { fpsBox.Text = fps.ToString(); });
                                        }
                                        break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log("Command error: " + ex.Message);
                        }
                    }
                    
                    Thread.Sleep(1000 / fps);
                }
                catch (Exception ex)
                {
                    Log("Loop error: " + ex.Message);
                    
                    // Check if client disconnected
                    if (!client.Connected)
                    {
                        this.Invoke((MethodInvoker)delegate {
                            statusLabel.Text = "Status: Client disconnected";
                            statusLabel.ForeColor = Color.Red;
                        });
                        Log("Client disconnected, waiting for new connection...");
                        
                        // Wait for new client
                        client = server.AcceptTcpClient();
                        stream = client.GetStream();
                        
                        this.Invoke((MethodInvoker)delegate {
                            statusLabel.Text = "Status: Client connected";
                            statusLabel.ForeColor = Color.Green;
                        });
                        Log("New client connected!");
                    }
                    
                    Thread.Sleep(1000);
                }
            }
        }
        catch (Exception ex)
        {
            Log("Fatal: " + ex.Message);
        }
    }
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
