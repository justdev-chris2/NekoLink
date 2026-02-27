using System;
using System.Drawing;
using System.Net.Sockets;
using System.Windows.Forms;
using System.IO;

class NekoLinkServer
{
    static StreamWriter log;
    
    static void Main()
    {
        // Setup logging
        log = new StreamWriter("server_debug.txt", true);
        Log("Server starting...");
        
        try
        {
            TcpListener server = new TcpListener(System.Net.IPAddress.Any, 5900);
            server.Start();
            Log("Server started on port 5900");
            Console.WriteLine("NekoLink Server started on port 5900");
            Console.WriteLine("Waiting for connection...");
            
            TcpClient client = server.AcceptTcpClient();
            Log("Client connected!");
            Console.WriteLine("Client connected!");
            NetworkStream stream = client.GetStream();
            
            while (true)
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
                    
                    // Handle incoming commands
                    if (stream.DataAvailable)
                    {
                        byte[] buffer = new byte[1024];
                        int read = stream.Read(buffer, 0, buffer.Length);
                        string command = System.Text.Encoding.ASCII.GetString(buffer, 0, read);
                        
                        Log("Received: " + command);
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
                                    mouse_event(0x02, 0, 0, 0, UIntPtr.Zero); // down
                                    mouse_event(0x04, 0, 0, 0, UIntPtr.Zero); // up
                                }
                                else if (parts[3].Contains("Right"))
                                {
                                    mouse_event(0x08, 0, 0, 0, UIntPtr.Zero); // down
                                    mouse_event(0x10, 0, 0, 0, UIntPtr.Zero); // up
                                }
                                break;
                                
                            case "KEY":
                                uint flags = parts[2] == "True" ? 0u : 2u;
                                keybd_event(byte.Parse(parts[1]), 0, flags, UIntPtr.Zero);
                                break;
                        }
                    }
                    
                    System.Threading.Thread.Sleep(100); // 10 fps
                }
                catch (Exception ex)
                {
                    Log("Loop error: " + ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Log("Fatal error: " + ex.Message);
            Console.WriteLine("Error: " + ex.Message);
            Console.ReadLine();
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
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
