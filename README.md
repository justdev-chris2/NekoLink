# NekoLink 🐱

A lightweight remote desktop tool for Windows. Control another PC's screen, mouse, and keyboard with zero setup.

## How it works

Two computers connect directly - no cloud, no accounts, no backend (peer-to-peer).

- **Server** = the PC being controlled
- **Client** = the PC doing the controlling

## Usage

### On the target PC (Server)
```
NekoLink-Server.exe
```
Server waits for connection on port 5900

### On your PC (Client)
```
NekoLink-Client.exe 192.168.1.xxx
```
Replace IP with the server's actual IP address
- or double click the exe for available connectable devices.

## Controls

- **Click** on the remote screen → locks keyboard/mouse to remote
- **Right Ctrl** → unlocks, back to controlling your local PC
- Close window to disconnect

## Build from source

```
csc Server.cs /out:NekoLink-Server.exe
csc Client.cs /out:NekoLink-Client.exe /reference:Microsoft.VisualBasic.dll
```

## Notes

- Both PCs need to be on the same network, or port forward 5900 on the server's router
- Default is 15 FPS (change Thread.Sleep in Server.cs for more/less)
- Super simple - does exactly what it says
