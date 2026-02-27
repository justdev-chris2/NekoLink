using System;
using System.Drawing;
using System.Net.Sockets;
using System.Windows.Forms;

class NekoLinkServer
{
    static void Main()
    {
        TcpListener server = new TcpListener(System.Net.IPAddress.Any, 5900);
        server.Start();
        Console.WriteLine("NekoLink Server started on port 5900");
        Console.WriteLine("Waiting for connection...");
        
        TcpClient client = server.AcceptTcpClient();
        Console.WriteLine("Client connected!");
        NetworkStream stream = client.GetStream();
        
        while (true)
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
            
            // Handle incoming commands
            if (stream.DataAvailable)
            {
                byte[] buffer = new byte[1024];
                int read = stream.Read(buffer, 0, buffer.Length);
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
                            mouse_event(0x02, 0, 0, 0, 0); // down
                            mouse_event(0x04, 0, 0, 0, 0); // up
                        }
                        else if (parts[3].Contains("Right"))
                        {
                            mouse_event(0x08, 0, 0, 0, 0); // down
                            mouse_event(0x10, 0, 0, 0, 0); // up
                        }
                        break;
                        
                    case "KEY":
                        keybd_event(byte.Parse(parts[1]), 0, parts[2] == "True" ? 0 : 2, 0);
                        break;
                }
            }
            
            System.Threading.Thread.Sleep(100); // 10 fps
        }
    }
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
}
