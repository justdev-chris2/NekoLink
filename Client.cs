using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Threading;

class NekoLinkClient
{
    static TcpClient client;
    static NetworkStream stream;
    static PictureBox pb;
    static Form form;
    static bool locked = false;
    static Label statusLabel;
    
    [STAThread]
    static void Main()
    {
        string ip = Microsoft.VisualBasic.Interaction.InputBox("Enter Server IP:", "NekoLink", "192.168.1.", 500, 500);
        if (string.IsNullOrEmpty(ip)) return;
        
        try
        {
            client = new TcpClient();
            client.Connect(ip, 5900);
            stream = client.GetStream();
        }
        catch
        {
            MessageBox.Show("Could not connect to server");
            return;
        }
        
        form = new Form();
        form.Text = "NekoLink";
        form.WindowState = FormWindowState.Maximized;
        form.KeyPreview = true;
        form.BackColor = Color.Black;
        
        // Top status panel
        Panel topPanel = new Panel();
        topPanel.Height = 30;
        topPanel.Dock = DockStyle.Top;
        topPanel.BackColor = Color.FromArgb(30, 30, 30);
        
        statusLabel = new Label();
        statusLabel.Text = "🔓 Unlocked - Click screen to lock";
        statusLabel.ForeColor = Color.White;
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        topPanel.Controls.Add(statusLabel);
        
        // Picture box for remote screen
        pb = new PictureBox();
        pb.Dock = DockStyle.Fill;
        pb.SizeMode = PictureBoxSizeMode.Zoom;
        pb.BackColor = Color.Black;
        
        form.Controls.Add(pb);
        form.Controls.Add(topPanel);
        
        // Mouse events
        pb.MouseMove += (s, e) => {
            if (locked)
            {
                float ratioX = (float)Screen.PrimaryScreen.Bounds.Width / pb.Width;
                float ratioY = (float)Screen.PrimaryScreen.Bounds.Height / pb.Height;
                int remoteX = (int)(e.X * ratioX);
                int remoteY = (int)(e.Y * ratioY);
                SendMouse(remoteX, remoteY);
            }
        };
        
        pb.MouseClick += (s, e) => {
            if (locked)
            {
                float ratioX = (float)Screen.PrimaryScreen.Bounds.Width / pb.Width;
                float ratioY = (float)Screen.PrimaryScreen.Bounds.Height / pb.Height;
                int remoteX = (int)(e.X * ratioX);
                int remoteY = (int)(e.Y * ratioY);
                SendClick(remoteX, remoteY, e.Button.ToString());
            }
        };
        
        // Click to lock
        pb.Click += (s, e) => {
            locked = true;
            statusLabel.Text = "🔒 LOCKED - Press Right Ctrl to unlock";
            statusLabel.ForeColor = Color.LightGreen;
            form.Text = "NekoLink [LOCKED]";
        };
        
        // Keyboard events
        form.KeyDown += (s, e) => {
            if (e.Control && e.KeyCode == Keys.RControlKey)
            {
                locked = false;
                statusLabel.Text = "🔓 Unlocked - Click screen to lock";
                statusLabel.ForeColor = Color.White;
                form.Text = "NekoLink";
            }
            
            if (locked && !e.Control)
            {
                SendKey((byte)e.KeyCode, true);
            }
        };
        
        form.KeyUp += (s, e) => {
            if (locked)
            {
                SendKey((byte)e.KeyCode, false);
            }
        };
        
        // Start receive thread
        Thread receiveThread = new Thread(ReceiveScreen);
        receiveThread.Start();
        
        Application.Run(form);
    }
    
    static void ReceiveScreen()
    {
        byte[] lenBytes = new byte[4];
        
        while (true)
        {
            try
            {
                stream.Read(lenBytes, 0, 4);
                int len = BitConverter.ToInt32(lenBytes, 0);
                
                byte[] imgData = new byte[len];
                int read = 0;
                while (read < len)
                    read += stream.Read(imgData, read, len - read);
                
                using (MemoryStream ms = new MemoryStream(imgData))
                {
                    Image img = Image.FromStream(ms);
                    pb.Invoke((MethodInvoker)(() => {
                        pb.Image?.Dispose();
                        pb.Image = (Image)img.Clone();
                    }));
                }
            }
            catch
            {
                break;
            }
        }
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
