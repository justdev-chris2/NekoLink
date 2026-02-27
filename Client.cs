using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class NekoLinkClient
{
    static TcpClient client;
    static NetworkStream stream;
    static bool connected = false;
    static int fps = 10;
    
    static void Main()
    {
        Console.WriteLine("=== NekoLink Client ===");
        Console.WriteLine();
        
        while (true)
        {
            Console.Write("Enter server IP: ");
            string serverIp = Console.ReadLine();
            
            if (string.IsNullOrEmpty(serverIp))
                continue;
            
            try
            {
                Console.Write($"Connecting to {serverIp}:5900... ");
                client = new TcpClient();
                client.Connect(serverIp, 5900);
                stream = client.GetStream();
                connected = true;
                Console.WriteLine("OK!");
                Console.WriteLine();
                break;
            }
            catch
            {
                Console.WriteLine("FAILED!");
                Console.WriteLine("Could not connect. Make sure server is running.");
                Console.WriteLine();
            }
        }
        
        // Start receive thread
        Thread receiveThread = new Thread(ReceiveLoop);
        receiveThread.Start();
        
        // Command loop
        Console.WriteLine("Connected! Commands:");
        Console.WriteLine("  /fps [number] - Change FPS");
        Console.WriteLine("  /lock         - Lock remote control");
        Console.WriteLine("  /unlock       - Unlock remote control");
        Console.WriteLine("  /quit         - Exit");
        Console.WriteLine();
        Console.WriteLine("Click on this window to send mouse/keyboard");
        Console.WriteLine("Press Right Ctrl to unlock if locked");
        Console.WriteLine();
        
        bool locked = false;
        
        while (connected)
        {
            string input = Console.ReadLine();
            
            if (input.StartsWith("/fps"))
            {
                string[] parts = input.Split(' ');
                if (parts.Length > 1 && int.TryParse(parts[1], out int newFps))
                {
                    fps = newFps;
                    SendCommand($"SET_FPS,{fps}");
                    Console.WriteLine($"FPS set to {fps}");
                }
            }
            else if (input == "/lock")
            {
                locked = true;
                Console.WriteLine("Remote control LOCKED");
            }
            else if (input == "/unlock")
            {
                locked = false;
                Console.WriteLine("Remote control UNLOCKED");
            }
            else if (input == "/quit")
            {
                break;
            }
            else if (!string.IsNullOrEmpty(input) && locked)
            {
                // Send as text to remote
                foreach (char c in input)
                {
                    SendKey((byte)c, true);
                    Thread.Sleep(10);
                    SendKey((byte)c, false);
                }
                // Send enter
                SendKey(13, true);
                SendKey(13, false);
            }
        }
        
        client?.Close();
    }
    
    static void ReceiveLoop()
    {
        int frameCount = 0;
        DateTime lastTime = DateTime.Now;
        
        while (connected)
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
                
                frameCount++;
                if ((DateTime.Now - lastTime).TotalSeconds >= 1)
                {
                    Console.Title = $"NekoLink - {frameCount} FPS";
                    frameCount = 0;
                    lastTime = DateTime.Now;
                }
            }
            catch
            {
                break;
            }
        }
        
        connected = false;
        Console.WriteLine("Disconnected from server");
    }
    
    static void SendCommand(string cmd)
    {
        try
        {
            byte[] data = Encoding.ASCII.GetBytes(cmd);
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
