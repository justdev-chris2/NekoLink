import socket
import threading
import tkinter as tk
from tkinter import simpledialog
import requests
from PIL import Image, ImageTk
from io import BytesIO
import pyautogui
import keyboard
import sys

class NekoLinkClient:
    def __init__(self, server_ip):
        self.server_ip = server_ip
        self.locked = False
        self.control_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.control_socket.connect((server_ip, 5903))
        
        self.window = tk.Tk()
        self.window.title(f"NekoLink - {server_ip}")
        self.window.attributes('-fullscreen', True)
        
        # Video label
        self.video_label = tk.Label(self.window)
        self.video_label.pack(fill=tk.BOTH, expand=True)
        
        # Control panel
        panel = tk.Frame(self.window, height=30, bg='gray')
        panel.pack(fill=tk.X, side=tk.BOTTOM)
        
        lock_btn = tk.Button(panel, text="Lock", command=self.lock)
        lock_btn.pack(side=tk.LEFT, padx=5)
        
        unlock_btn = tk.Button(panel, text="Unlock", command=self.unlock)
        unlock_btn.pack(side=tk.LEFT, padx=5)
        
        # Bind keys
        self.window.bind('<Control-R>', lambda e: self.unlock())
        self.window.bind('<Key>', self.on_key)
        
        # Start video thread
        threading.Thread(target=self.video_stream, daemon=True).start()
        
    def lock(self):
        self.locked = True
        self.window.title(f"NekoLink - {self.server_ip} [LOCKED]")
        
    def unlock(self):
        self.locked = False
        self.window.title(f"NekoLink - {self.server_ip}")
        
    def on_key(self, event):
        if self.locked and event.keysym != 'Control_R':
            self.send_command(f"KEY,{event.keysym},{1}")
            
    def send_command(self, cmd):
        try:
            self.control_socket.send(cmd.encode())
        except:
            pass
            
    def video_stream(self):
        stream = requests.get(f'http://{self.server_ip}:5902', stream=True)
        bytes_data = b''
        for chunk in stream.iter_content(chunk_size=1024):
            bytes_data += chunk
            a = bytes_data.find(b'--frame\r\n')
            b = bytes_data.find(b'\r\n\r\n', a)
            if a != -1 and b != -1:
                headers = bytes_data[a:b].decode()
                content_length = int(headers.split('Content-Length: ')[1].split('\r\n')[0])
                data_start = b + 4
                data_end = data_start + content_length
                
                if len(bytes_data) >= data_end:
                    jpg_data = bytes_data[data_start:data_end]
                    bytes_data = bytes_data[data_end:]
                    
                    img = Image.open(BytesIO(jpg_data))
                    photo = ImageTk.PhotoImage(img)
                    self.video_label.config(image=photo)
                    self.video_label.image = photo

if __name__ == '__main__':
    ip = simpledialog.askstring("NekoLink", "Server IP:", initialvalue="192.168.1.")
    if ip:
        client = NekoLinkClient(ip)
        client.window.mainloop()
