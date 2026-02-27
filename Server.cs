using System;
using System.Net;
using System.Windows.Forms;
using LibVLCSharp.Shared;

class NekoLinkServer
{
    static LibVLC libvlc;
    static MediaPlayer player;
    static NotifyIcon trayIcon;
    
    [STAThread]
    static void Main()
    {
        // Hide console
        var handle = GetConsoleWindow();
        ShowWindow(handle, 0);
        
        // Show IPs
        string ips = "Server IPs:\n";
        foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                ips += ip + "\n";
        
        LibVLCSharp.Shared.Core.Initialize();
        libvlc = new LibVLC();
        
        // Stream screen to RTP
        string streamParams = $":sout=#rtp{{dst=239.255.12.42,port=5900,mux=ts}} :no-sout-all :sout-keep";
        using var media = new Media(libvlc, "screen://", streamParams);
        player = new MediaPlayer(media);
        player.Play();
        
        // Tray icon
        trayIcon = new NotifyIcon();
        trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        trayIcon.Text = "NekoLink Server";
        trayIcon.Visible = true;
        trayIcon.ShowBalloonTip(1000, "NekoLink", ips, ToolTipIcon.Info);
        
        Application.Run();
    }
    
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
