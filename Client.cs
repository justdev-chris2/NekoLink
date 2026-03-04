static WebSocket ws;
static void ConnectToRelay()
{
    ws = new WebSocket("ws://fantastic-umbrella-jjpgj56jrvgvc7g9-8080.app.github.dev/");
    
    // Register as viewer
    ws.Send(JsonConvert.SerializeObject(new { 
        type = "register", 
        role = "viewer" 
    }));
    
    ws.OnMessage += (s, e) => {
        var msg = JsonConvert.DeserializeObject(e.Data);
        if (msg.type == "frame")
        {
            byte[] imgData = Convert.FromBase64String(msg.data);
            // Display image
        }
    };
}
