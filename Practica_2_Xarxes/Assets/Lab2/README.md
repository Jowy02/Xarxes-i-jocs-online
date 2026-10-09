# Lab Sockets (TCP / UDP) - Lobby, Chat & UDP Minigame

## 📝 Disclaimer Note UDP Deviation
As discussed and agreed upon with the professor in class, our UDP implementation deviates slightly from the strict PDF requirements, duplicate the chat in UDP. Instead, **we implemented a real-time positional synchronization minigame**. 

We chose this approach to demonstrate a more realistic use case for UDP in video games, sending fast, non-reliable positional data while still strictly fulfilling all the technical requirements: **Broadcast**, **PING heartbeats**, and **Server Timeout (5s) for disconnected players**.

---

## 📖 Description
This project is a low-level networking implementation in Unity using **C# Sockets**. It features two distinct network architectures:
1. **TCP Implementation**: A connection-based Lobby system with a real-time chat, player list synchronization, and safe disconnect handling.
2. **UDP Implementation**: A real-time minigame where players click to move a 3D capsule, synchronizing coordinates across all clients.

## ✨ Features & Architecture

**TCP (Lobby & Chat)**
* **Command Protocol**: Structured messages (`JOIN:`, `CHAT:`, `PLAYERS:`, `LEAVE:`).
* **State Synchronization**: Late joiners receive the active player list and the full message history when connecting.

**UDP (Minigame)**
* **Broadcasting**: Server relays `POS:x,y,z` , also used the coordinates to all known endpoints using `InvariantCulture` for cross-region decimal safety.
* **Heartbeat System**: Clients send a `PING:` every 1 second.
* **Timeout Disconnect**: The server actively checks the `m_lastSeen` dictionary every frame and kicks/destroys players who stop sending packets for 5 seconds (`DISC:`).

**⭐ Bonus Implemented**
* **The Host also plays**: In the Create Game scene, clicking "Host" successfully starts the Server thread in the background and automatically connects a local Client to `127.0.0.1`, allowing the host to chat, appear in the list, and play the minigame alongside everyone else.

## ⚙️ Installation
Download the latest release from [here](https://github.com/Jowy02/Xarxes-i-jocs-online/releases), extract the zip and run the builds.

You can also open the project in Unity (6000.3.16f1) by importing the `.unitypackage` that comes with the release.

To test on the same PC leave the Server IP as `127.0.0.1`. On a LAN use the host's IP.

## 🎮 Controls

* **TCP Chat**: Keyboard (Type in the InputField and press Send).
* **UDP Minigame**: **Left Mouse Click** on the 3D ground to move your capsule to that position.

## 🚀 How to Play / Test

<table>
  <tr>
    <td width="50%">
      <img src="/Gifs/TCP_Chat.gif" width="100%" alt="TCP Chat">
    </td>
    <td>
      <b>1. TCP Lobby & Chat</b><br>
      Once connected in the TCP scene, you will see the updated player list. Type a message and click send to broadcast it to all connected players.
    </td>
  </tr>

  <tr>
    <td width="50%">
      <img src="/Gifs/TCP_StopServer.gif" width="100%" alt="TCP Stop Server">
    </td>
    <td>
      <b>2. TCP Stop Server</b><br>
      When the Host clicks "Stop Server", the server safely closes all socket connections. Because TCP is stream-oriented, clients instantly detect the end of the stream, receiving 0 bytes, and safely disconnect on their end, returning to the main menu.
    </td>
  </tr>
  
  <tr>
    <td width="50%">
      <img src="/Gifs/TCP_ClientDisconnection.gif" width="100%" alt="TCP Client Disconnection">
    </td>
    <td>
      <b>3. TCP Client Disconnection</b><br>
      When a Client clicks "Disconnect", it sends a `LEAVE:` command to the server before closing its socket. The server then safely removes the player from the active players list and broadcasts the updated player list to all remaining clients.
    </td>
  </tr>
  
  <tr>
    <td width="50%">
      <img src="/Gifs/TCP_ForceClose.gif" width="100%" alt="TCP Force Close">
    </td>
    <td>
      <b>4. TCP Force Close</b><br>
      If a client crashes or the window is force-closed (Alt+F4), the socket connection breaks abruptly. The server catches this SocketException, identifies the disconnected client, removes them from the active players list, and broadcasts the updated list to the remaining players so no "ghost clients" are left behind. Also, if the Host (Server) is force-closed, the connected clients will detect the broken connection, and handle the exception.
    </td>
  </tr>
  
  <tr>
    <td width="50%">
      <img src="/Gifs/UDP_Lobby.gif" width="100%" alt="Lobby UDP">
    </td>
    <td>
      <b>5. UDP Host or Join a Game</b><br>
      Open the Create/Join Lobby. Enter your username. If you want to host, click "Host". If you want to join a friend, enter their IP address and click "Join".
    </td>
  </tr>

  <tr>
    <td width="50%">
      <img src="/Gifs/UDP_Minigame.gif" width="100%" alt="UDP Minigame">
    </td>
    <td>
      <b>6. UDP Minigame</b><br>
      Click anywhere on the ground. A red capsule will appear and move to your cursor's position instantly on all clients' screens.
    </td>
  </tr>

  <tr>
    <td width="50%">
      <img src="/Gifs/UDP_Disconnection.gif" width="100%" alt="UDP Minigame">
    </td>
    <td>
      <b>7. UDP Server Disconnection</b><br>
      If the Host (Server) disconnects or the server window is closed, it stops broadcasting and acknowledging packets. The remaining clients will eventually fail to receive updates or hit their own internal timeout, 5s, for the server's heartbeat, automatically disconnecting and safely returning to the main menu.
    </td>
  </tr>
  
  <tr>
    <td width="50%">
      <img src="/Gifs/UDP_Timeout.gif" width="100%" alt="Timeout System">
    </td>
    <td>
      <b>8. Test the Timeout (PING)</b><br>
      To test a forced connection drop, playing with someone, the player has to switch off Wi-fi. Wait 5 seconds. The server will detect the missing PINGs, and order the remaining clients to destroy the disconnected player's capsule.
    </td>
  </tr>
</table>

<p align="center">
  <br>
  <strong>HAVE FUN!</strong>
</p>
     
## 👥 Credits
**All contributors working on this project**:

_Pablo Sanjose_ « **Github**: [XXPabloS](https://github.com/XXPabloS)

_Ana Alcaraz_ « **Github**: [Audra0000](https://github.com/Audra0000)

_Haosheng Li_ « **Github**: [HaosLii](https://github.com/HaosLii)

_Joel Vicente_ « **Github**: [Jowy02](https://github.com/Jowy02)

_Arthur Cordoba_ « **Github**: [000Arthur](https://github.com/000Arthur)

## ⚠️ Known Issues / Limitations
* In the UDP Minigame, overlapping capsules do not have collision physics implemented yet, as the focus of the lab is purely on network packet delivery and synchronization.
* Make sure both the Host and Client are running on the same network or have the correct port (`9050`) on their router.

## ⚖️ Disclaimer
This project is an **academic and non-commercial project** created for educational purposes only.
