using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Windows.Forms;

class NekoLinkClient
{
    static string relayUrl = "https://fantastic-umbrella-jjpgj56jrvgvc7g9-8080.app.github.dev";
    static HttpClient client = new HttpClient();
    static PictureBox pb;
    static Form form;
    static bool locked = false;
    static Label statusLabel;
    
    [STAThread]
    static void Main()
    {
        form = new Form();
        form.Text = "NekoLink";
        form.WindowState = FormWindowState.Maximized;
        form.KeyPreview = true;
        
        // Status panel
        Panel topPanel = new Panel();
        topPanel.Height = 30;
        topPanel.Dock = DockStyle.Top;
        topPanel.BackColor = Color.FromArgb(30, 30, 30);
        
        statusLabel = new Label();
        statusLabel.Text = "🔓 Unlocked - Click to lock";
        statusLabel.ForeColor = Color.White;
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        statusLabel.Font = new Font("Arial", 10, FontStyle.Bold);
        topPanel.Controls.Add(statusLabel);
        
        pb = new PictureBox();
        pb.Dock = DockStyle.Fill;
        pb.SizeMode = PictureBoxSizeMode.Zoom;
        pb.BackColor = Color.Black;
        
        form.Controls.Add(pb);
        form.Controls.Add(topPanel);
        
        // Mouse events
        pb.MouseMove += (s, e) => {
            if (locked && pb.Image != null)
            {
                float ratioX = (float)Screen.PrimaryScreen.Bounds.Width / pb.Width;
                float ratioY = (float)Screen.PrimaryScreen.Bounds.Height / pb.Height;
                int x = (int)(e.X * ratioX);
                int y = (int)(e.Y * ratioY);
                x = Math.Max(0, Math.Min(Screen.PrimaryScreen.Bounds.Width - 1, x));
                y = Math.Max(0, Math.Min(Screen.PrimaryScreen.Bounds.Height - 1, y));
                SendCommand($"MOUSE,{x},{y}");
            }
        };
        
        pb.MouseClick += (s, e) => {
            if (locked && pb.Image != null)
            {
                float ratioX = (float)Screen.PrimaryScreen.Bounds.Width / pb.Width;
                float ratioY = (float)Screen.PrimaryScreen.Bounds.Height / pb.Height;
                int x = (int)(e.X * ratioX);
                int y = (int)(e.Y * ratioY);
                x = Math.Max(0, Math.Min(Screen.PrimaryScreen.Bounds.Width - 1, x));
                y = Math.Max(0, Math.Min(Screen.PrimaryScreen.Bounds.Height - 1, y));
                SendCommand($"CLICK,{x},{y},{e.Button}");
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
                statusLabel.Text = "🔓 Unlocked - Click to lock";
                statusLabel.ForeColor = Color.White;
                form.Text = "NekoLink";
            }
            
            if (locked && !e.Control && e.KeyCode != Keys.RControlKey)
            {
                SendCommand($"KEY,{(byte)e.KeyCode},True");
            }
        };
        
        form.KeyUp += (s, e) => {
            if (locked && !e.Control && e.KeyCode != Keys.RControlKey)
            {
                SendCommand($"KEY,{(byte)e.KeyCode},False");
            }
        };
        
        // Start frame receiver
        Thread frameThread = new Thread(GetFrames);
        frameThread.IsBackground = true;
        frameThread.Start();
        
        Application.Run(form);
    }
    
    static void GetFrames()
    {
        while (true)
        {
            try
            {
                var response = client.GetAsync($"{relayUrl}/frame").Result;
                if (response.IsSuccessStatusCode)
                {
                    byte[] data = response.Content.ReadAsByteArrayAsync().Result;
                    using (MemoryStream ms = new MemoryStream(data))
                    {
                        Image img = Image.FromStream(ms);
                        pb.Invoke((MethodInvoker)(() => {
                            pb.Image?.Dispose();
                            pb.Image = (Image)img.Clone();
                        }));
                    }
                }
            }
            catch { }
            Thread.Sleep(66); // ~15fps
        }
    }
    
    static void SendCommand(string cmd)
    {
        try
        {
            var content = new StringContent(cmd, Encoding.UTF8);
            client.PostAsync($"{relayUrl}/command", content).Wait();
        }
        catch { }
    }
}
