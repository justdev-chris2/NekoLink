using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Threading;

class NekoLinkClient
{
    static PictureBox pb;
    static Form form;
    static bool locked = false;
    
    [STAThread]
    static void Main(string[] args)
    {
        string ip = Microsoft.VisualBasic.Interaction.InputBox("Server IP:", "NekoLink", "192.168.1.");
        
        TcpClient client = new TcpClient();
        client.Connect(ip, 5900);
        NetworkStream stream = client.GetStream();
        
        form = new Form();
        form.WindowState = FormWindowState.Maximized;
        form.KeyPreview = true;
        form.Text = "NekoLink";
        
        pb = new PictureBox();
        pb.Dock = DockStyle.Fill;
        pb.SizeMode = PictureBoxSizeMode.Zoom;
        form.Controls.Add(pb);
        
        // Lock controls
        form.KeyDown += (s, e) => {
            if (e.Control && e.KeyCode == Keys.RControlKey)
                locked = false;
            if (locked)
                SendKey(stream, (byte)e.KeyCode, true);
        };
        
        form.KeyUp += (s, e) => {
            if (locked)
                SendKey(stream, (byte)e.KeyCode, false);
        };
        
        pb.Click += (s, e) => locked = true;
        
        new Thread(() => {
            byte[] lenBytes = new byte[4];
            while (true)
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
        }).Start();
        
        Application.Run(form);
    }
    
    static void SendKey(NetworkStream stream, byte key, bool down)
    {
        string cmd = $"KEY,{key},{down}";
        byte[] data = System.Text.Encoding.ASCII.GetBytes(cmd);
        stream.Write(data, 0, data.Length);
    }
}
