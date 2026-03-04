static WebSocket ws;
static void ConnectToRelay()
{
    ws = new WebSocket("ws://fantastic-umbrella-jjpgj56jrvgvc7g9-8080.app.github.dev");
    ws.OnMessage += (s, e) => {
        // Handle commands from relay
    };
    
    // Register as host
    ws.Send(JsonConvert.SerializeObject(new { 
        type = "register", 
        role = "host" 
    }));
    
    // Start sending frames
    while (true)
    {
        byte[] frame = CaptureScreen();
        ws.Send(JsonConvert.SerializeObject(new {
            type = "frame",
            data = Convert.ToBase64String(frame)
        }));
        Thread.Sleep(33);
    }
}
