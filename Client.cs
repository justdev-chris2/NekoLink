using System;
using System.Drawing;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Net;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

class NekoLinkClient
{
    static TcpClient client;
    static NetworkStream stream;
    static Form form;
    static PictureBox pictureBox;
    static bool keyboardLocked = false;
    static StreamWriter log;
    static int fps = 10;
    static TextBox fpsBox;
    static Label statusLabel;
    
    [STAThread]
    static void Main(string[] args)
    {
        // Setup logging
        log = new StreamWriter("client_debug.txt", true);
        Log("Client starting...");
        
        // Check if running as admin
        bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);
        Log("Admin: " + isAdmin);
        
        // If not admin and no firewall rule, prompt once
        if (!isAdmin && !CheckFirewallRule())
        {
            Log("Requesting admin for firewall...");
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = true;
            startInfo.WorkingDirectory = Environment.CurrentDirectory;
            startInfo.FileName = Application.ExecutablePath;
            startInfo.Arguments = string.Join(" ", args);
            startInfo.Verb = "runas";
        
            try
            {
                Process.Start(startInfo);
                Log("Admin instance started, exiting...");
                return;
            }
            catch
            {
                Log("User declined admin");
            }
        }
        
        string serverIp;
        
        if (args.Length == 0)
        {
            // Show IP selector
            var ips = Dns.GetHostEntry(Dns.GetHostName()).AddressList
                .Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .ToList();
            
            if (ips.Count == 0)
            {
                MessageBox.Show("No network adapters found!");
                Log("No network adapters");
                return;
            }
            
            string ipList = "Available IPs on network:\n\n";
            foreach (var ip in ips)
                ipList += ip.ToString() + "\n";
            
            ipList += "\nEnter the server IP:";
            
            string input = Microsoft.VisualBasic.Interaction.InputBox(ipList, "NekoLink - Connect", "192.168.1.", 500, 500);
            if (string.IsNullOrEmpty(input)) return;
            serverIp = input;
        }
        else
        {
            serverIp = args[0];
        }
        
        Log("Attempting to connect to: " + serverIp);
        
        try
        {
            client = new TcpClient();
            client.Connect(serverIp, 5900);
            stream = client.GetStream();
            Log("Connected successfully!");
        }
        catch (Exception ex)
        {
            Log("Connection failed: " + ex.Message);
            MessageBox.Show($"Could not connect to {serverIp}:5900\n\nMake sure server is running and IP is correct.");
            
            // Try again
            Main(new string[0]);
            return;
        }
        
        // Create the viewer window
        form = new Form();
        form.Text = "NekoLink - Remote Desktop";
        form.WindowState = FormWindowState.Maximized;
        form.KeyPreview = true;
        form.FormClosing += (s, e) => { Log("Form closing"); };
        
        // Create controls panel at top
        Panel controlPanel = new Panel();
        controlPanel.Height = 40;
        controlPanel.Dock = DockStyle.Top;
        controlPanel.BackColor = Color.FromArgb(50, 50, 50);
        
        Label fpsLabel = new Label();
        fpsLabel.Text = "FPS:";
        fpsLabel.ForeColor = Color.White;
        fpsLabel.Location = new Point(10, 10);
        fpsLabel.Size = new Size(40, 20);
        controlPanel.Controls.Add(fpsLabel);
        
        fpsBox = new TextBox();
        fpsBox.Text = fps.ToString();
        fpsBox.Location = new Point(50, 8);
        fpsBox.Size = new Size(40, 20);
        fpsBox.TextChanged += (s, e) => { int.TryParse(fpsBox.Text, out fps); if (fps < 1) fps = 1; };
        controlPanel.Controls.Add(fpsBox);
        
        Button applyFps = new Button();
        applyFps.Text = "Apply";
        applyFps.Location = new Point(100, 7);
        applyFps.Size = new Size(60, 23);
        applyFps.Click += (s, e) => { 
            int.TryParse(fpsBox.Text, out fps); 
            if (fps < 1) fps = 1;
            SendCommand($"SET_FPS,{fps}");
        };
        controlPanel.Controls.Add(applyFps);
        
        Button lockBtn = new Button();
        Button unlockBtn = new Button(); // Declare both buttons first
        
        lockBtn.Text = "Lock";
        lockBtn.Location = new Point(180, 7);
        lockBtn.Size = new Size(60, 23);
        lockBtn.Click += (s, e) => { 
            keyboardLocked = true; 
            lockBtn.BackColor = Color.LightGreen;
            unlockBtn.BackColor = SystemColors.Control;
            form.Text = "NekoLink - LOCKED (Press Right Ctrl to unlock)";
        };
        controlPanel.Controls.Add(lockBtn);
        
        unlockBtn.Text = "Unlock";
        unlockBtn.Location = new Point(250, 7);
        unlockBtn.Size = new Size(60, 23);
        unlockBtn.Click += (s, e) => { 
            keyboardLocked = false; 
            unlockBtn.BackColor = Color.LightGreen;
            lockBtn.BackColor = SystemColors.Control;
            form.Text = "NekoLink - Remote Desktop";
        };
        controlPanel.Controls.Add(unlockBtn);
        
        statusLabel = new Label();
        statusLabel.Text = "Connected";
        statusLabel.ForeColor = Color.LightGreen;
        statusLabel.Location = new Point(350, 10);
        statusLabel.Size = new Size(200, 20);
        controlPanel.Controls.Add(statusLabel);
        
        form.Controls.Add(controlPanel);
        
        // Picture box for remote screen
        pictureBox = new PictureBox();
        pictureBox.Dock = DockStyle.Fill;
        pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBox.BackColor = Color.Black;
        
        // Mouse events
        pictureBox.MouseMove += (s, e) => { 
            if(keyboardLocked) 
            {
                // Convert to screen coordinates
                float ratioX = (float)Screen.PrimaryScreen.Bounds.Width / pictureBox.Width;
                float ratioY = (float)Screen.PrimaryScreen.Bounds.Height / pictureBox.Height;
                int remoteX = (int)(e.X * ratioX);
                int remoteY = (int)(e.Y * ratioY);
                SendMouse(remoteX, remoteY);
            }
        };
        
        pictureBox.MouseClick += (s, e) => { 
            if(keyboardLocked) 
            {
                float ratioX = (float)Screen.PrimaryScreen.Bounds.Width / pictureBox.Width;
                float ratioY = (float)Screen.PrimaryScreen.Bounds.Height / pictureBox.Height;
                int remoteX = (int)(e.X * ratioX);
                int remoteY = (int)(e.Y * ratioY);
                SendClick(remoteX, remoteY, e.Button.ToString());
            }
        };
        
        // Click on picture box locks
        pictureBox.Click += (s, e) => { 
            keyboardLocked = true; 
            lockBtn.BackColor = Color.LightGreen;
            unlockBtn.BackColor = SystemColors.Control;
            form.Text = "NekoLink - LOCKED (Press Right Ctrl to unlock)";
            Log("Keyboard locked");
        };
        
        form.Controls.Add(pictureBox);
        
        // Keyboard events
        form.KeyDown += (s, e) => {
            if (e.Control && e.KeyCode == Keys.RControlKey)
            {
                keyboardLocked = false;
                lockBtn.BackColor = SystemColors.Control;
                unlockBtn.BackColor = Color.LightGreen;
                form.Text = "NekoLink - Remote Desktop";
                Log("Keyboard unlocked");
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
        
        // Start receive thread
        System.Threading.Thread pool = new System.Threading.Thread(ReceiveScreen);
        pool.Start();
        Log("Receive thread started");
        
        Application.Run(form);
        Log("Application exiting");
    }
    
    static bool CheckFirewallRule()
    {
        try
        {
            Process process = new Process();
            process.StartInfo.FileName = "netsh";
            process.StartInfo.Arguments = "advfirewall firewall show rule name=\"NekoLink Client\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            bool exists = output.Contains("NekoLink Client");
            Log("Firewall rule exists: " + exists);
            return exists;
        }
        catch (Exception ex)
        {
            Log("Firewall check error: " + ex.Message);
            return false;
        }
    }
    
    static void Log(string message)
    {
        try
        {
            log.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " - " + message);
            log.Flush();
        }
        catch { }
    }
    
    static void ReceiveScreen()
    {
        int frameCount = 0;
        DateTime lastTime = DateTime.Now;
        
        while (true)
        {
            try
            {
                byte[] lenBytes = new byte[4];
                int read = stream.Read(lenBytes, 0, 4);
                if (read == 0) break;
                
                int len = BitConverter.ToInt32(lenBytes, 0);
                
                byte[] imgData = new byte[len];
                int total = 0;
                while (total < len)
                    total += stream.Read(imgData, total, len - total);
                
                using (MemoryStream ms = new MemoryStream(imgData))
                {
                    Image img = Image.FromStream(ms);
                    pictureBox.Invoke((MethodInvoker)delegate { 
                        if (pictureBox.Image != null)
                            pictureBox.Image.Dispose();
                        pictureBox.Image = (Image)img.Clone(); 
                        
                        frameCount++;
                        if ((DateTime.Now - lastTime).TotalSeconds >= 1)
                        {
                            statusLabel.Text = $"FPS: {frameCount}";
                            frameCount = 0;
                            lastTime = DateTime.Now;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Log("Receive error: " + ex.Message);
                break;
            }
        }
        
        // Connection lost
        pictureBox.Invoke((MethodInvoker)delegate { 
            MessageBox.Show("Connection to server lost");
            Application.Exit();
        });
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
