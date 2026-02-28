import socket
import threading
import pyautogui
from io import BytesIO
from PIL import Image
import http.server
import socketserver
import sys

# MJPEG HTTP Server
class StreamingHandler(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        self.send_response(200)
        self.send_header('Content-Type', 'multipart/x-mixed-replace; boundary=frame')
        self.end_headers()
        try:
            while True:
                # Take screenshot
                screenshot = pyautogui.screenshot()
                img_buffer = BytesIO()
                screenshot.save(img_buffer, format='JPEG', quality=70)
                frame = img_buffer.getvalue()
                
                # Send frame
                self.wfile.write(b'--frame\r\n')
                self.wfile.write(b'Content-Type: image/jpeg\r\n\r\n')
                self.wfile.write(frame)
                self.wfile.write(b'\r\n')
                
        except:
            pass

# Control Server
def handle_control(conn):
    while True:
        data = conn.recv(1024).decode()
        if not data:
            break
        parts = data.split(',')
        if parts[0] == 'MOUSE':
            pyautogui.moveTo(int(parts[1]), int(parts[2]))
        elif parts[0] == 'CLICK':
            pyautogui.click(int(parts[1]), int(parts[2]))
        elif parts[0] == 'KEY':
            pyautogui.press(parts[1])
    conn.close()

# Start HTTP server
httpd = socketserver.ThreadingTCPServer(('', 5902), StreamingHandler)
threading.Thread(target=httpd.serve_forever, daemon=True).start()

# Start control server
control_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
control_socket.bind(('', 5903))
control_socket.listen(5)

print(f"Server IPs: {socket.gethostbyname(socket.gethostname())}")
print("HTTP stream on port 5900")
print("Control on port 5901")

while True:
    conn, addr = control_socket.accept()
    threading.Thread(target=handle_control, args=(conn,), daemon=True).start()
