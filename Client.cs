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
            return;
        }
        
        form = new Form();
        form.Text = "NekoLink - Remote Desktop";
        form.WindowState = FormWindowState.Maximized;
        form.KeyPreview = true;
        form.FormClosing += (s, e) => { Log("Form closing"); };
        
        pictureBox = new PictureBox();
        pictureBox.Dock = DockStyle.Fill;
        pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBox.MouseMove += (s, e) => { if(keyboardLocked) SendMouse(e.X, e.Y); };
        pictureBox.MouseClick += (s, e) => { if(keyboardLocked) SendClick(e.X, e.Y, e.Button.ToString()); };
        pictureBox.Click += (s, e) => { 
            keyboardLocked = true; 
            form.Text = "NekoLink - Keyboard LOCKED (Press Right Ctrl to unlock)";
            Log("Keyboard locked");
        };
        
        form.Controls.Add(pictureBox);
        
        form.KeyDown += (s, e) => {
            if (e.Control && e.KeyCode == Keys.RControlKey)
            {
                keyboardLocked = false;
                form.Text = "NekoLink - Remote Desktop (Unlocked)";
                Log("Keyboard unlocked");
            }
            
            if (keyboardLocked)
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
                            form.Text = $"NekoLink - {frameCount} FPS" + (keyboardLocked ? " (LOCKED)" : "");
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
    }
    
    static void SendMouse(int x, int y)
    {
        string cmd = $"MOUSE,{x},{y}";
        stream.Write(System.Text.Encoding.ASCII.GetBytes(cmd), 0, cmd.Length);
    }
    
    static void SendClick(int x, int y, string button)
    {
        string cmd = $"CLICK,{x},{y},{button}";
        stream.Write(System.Text.Encoding.ASCII.GetBytes(cmd), 0, cmd.Length);
    }
    
    static void SendKey(byte key, bool down)
    {
        string cmd = $"KEY,{key},{down}";
        stream.Write(System.Text.Encoding.ASCII.GetBytes(cmd), 0, cmd.Length);
    }
}
