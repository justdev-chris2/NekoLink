using System;
using System.Drawing;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Threading;

class NekoLinkClient
{
    static TcpClient controlClient;
    static NetworkStream controlStream;
    static bool locked = false;
    static Form form;
    static WebBrowser browser;
    
    [STAThread]
    static void Main()
    {
        string ip = Microsoft.VisualBasic.Interaction.InputBox("Server IP:", "NekoLink", "192.168.1.");
        
        // Connect control channel
        controlClient = new TcpClient();
        controlClient.Connect(ip, 5901);
        controlStream = controlClient.GetStream();
        
        form = new Form();
        form.Text = "NekoLink Control";
        form.Size = new Size(400, 300);
        form.KeyPreview = true;
        
        // WebBrowser for video
        browser = new WebBrowser();
        browser.Dock = DockStyle.Fill;
        browser.Url = new Uri($"http://{ip}:5900");
        
        // Control panel
        Panel panel = new Panel();
        panel.Dock = DockStyle.Top;
        panel.Height = 30;
        
        Button lockBtn = new Button();
        lockBtn.Text = "Lock";
        lockBtn.Location = new Point(10, 3);
        lockBtn.Click += (s, e) => { locked = true; form.Text = "LOCKED"; };
        
        Button unlockBtn = new Button();
        unlockBtn.Text = "Unlock";
        unlockBtn.Location = new Point(100, 3);
        unlockBtn.Click += (s, e) => { locked = false; form.Text = "NekoLink"; };
        
        panel.Controls.Add(lockBtn);
        panel.Controls.Add(unlockBtn);
        
        form.Controls.Add(browser);
        form.Controls.Add(panel);
        
        // Keyboard control
        form.KeyDown += (s, e) => {
            if (locked && !e.Control)
                SendKey((byte)e.KeyCode, true);
            if (e.Control && e.KeyCode == Keys.RControlKey)
                locked = false;
        };
        
        form.KeyUp += (s, e) => {
            if (locked)
                SendKey((byte)e.KeyCode, false);
        };
        
        Application.Run(form);
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
