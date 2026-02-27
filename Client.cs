using System;
using System.Windows.Forms;
using LibVLCSharp.Shared;

class NekoLinkClient
{
    static LibVLC libvlc;
    static MediaPlayer player;
    
    [STAThread]
    static void Main()
    {
        string ip = Microsoft.VisualBasic.Interaction.InputBox("Server IP:", "NekoLink", "192.168.1.", 500, 500);
        if (string.IsNullOrEmpty(ip)) return;
        
        LibVLCSharp.Shared.Core.Initialize();
        libvlc = new LibVLC();
        
        Form form = new Form();
        form.WindowState = FormWindowState.Maximized;
        form.Text = $"NekoLink - {ip}";
        
        var videoView = new WindowsFormsVideoView();
        videoView.Dock = DockStyle.Fill;
        form.Controls.Add(videoView);
        
        player = new MediaPlayer(libvlc);
        videoView.MediaPlayer = player;
        
        using var media = new Media(libvlc, $"rtp://{ip}:5900");
        player.Play(media);
        
        Application.Run(form);
    }
}
