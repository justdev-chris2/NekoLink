using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

class NekoLinkServer
{
    static TcpListener controlServer;
    
    static void Main()
    {
        // Start MJPEG HTTP server
        Thread httpThread = new Thread(StartHttp);
        httpThread.Start();
        
        // Start control server
        controlServer = new TcpListener(IPAddress.Any, 5901);
        controlServer.Start();
        
        while (true)
        {
            var client = controlServer.AcceptTcpClient();
            ThreadPool.QueueUserWorkItem(HandleControl, client);
        }
    }
    
    static void StartHttp()
    {
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://*:5900/");
        listener.Start();
        
        while (true)
        {
            var context = listener.GetContext();
            context.Response.Headers.Add("Content-Type", "multipart/x-mixed-replace; boundary=--boundary");
            
            while (true)
            {
                using (Bitmap bmp = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                        g.CopyFromScreen(0, 0, 0, 0, bmp.Size);
                    
                    using (var ms = new System.IO.MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Jpeg);
                        var data = ms.ToArray();
                        
                        string header = $"--boundary\r\nContent-Type: image/jpeg\r\nContent-Length: {data.Length}\r\n\r\n";
                        context.Response.OutputStream.Write(System.Text.Encoding.ASCII.GetBytes(header), 0, header.Length);
                        context.Response.OutputStream.Write(data, 0, data.Length);
                    }
                }
                Thread.Sleep(33);
            }
        }
    }
    
    static void HandleControl(object obj)
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
                
                string cmd = System.Text.Encoding.ASCII.GetString(buffer, 0, read);
                string[] parts = cmd.Split(',');
                
                switch(parts[0])
                {
                    case "MOUSE":
                        Cursor.Position = new Point(int.Parse(parts[1]), int.Parse(parts[2]));
                        break;
                    case "CLICK":
                        Cursor.Position = new Point(int.Parse(parts[1]), int.Parse(parts[2]));
                        mouse_event(parts[3].Contains("Left") ? 0x02u : 0x08u, 0, 0, 0, UIntPtr.Zero);
                        Thread.Sleep(50);
                        mouse_event(parts[3].Contains("Left") ? 0x04u : 0x10u, 0, 0, 0, UIntPtr.Zero);
                        break;
                    case "KEY":
                        uint flags = parts[2] == "True" ? 0u : 2u;
                        keybd_event(byte.Parse(parts[1]), 0, flags, UIntPtr.Zero);
                        break;
                }
            }
            catch { break; }
        }
    }
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
