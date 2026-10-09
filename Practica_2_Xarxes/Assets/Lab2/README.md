# Lab Sockets (TCP / UDP) - Lobby, Chat & UDP Minigame

## 📝 Disclaimer Note UDP Deviation
As discussed and agreed upon with the professor in class, our UDP implementation deviates slightly from the strict PDF requirements, duplicate the chat in UDP. Instead, **we implemented a real-time positional synchronization minigame**. 

We chose this approach to demonstrate a more realistic use case for UDP in video games, sending fast, non-reliable positional data while still strictly fulfilling all the technical requirements: **Broadcast**, **PING heartbeats**, and **Server Timeout (5s) for disconnected players**.

---

## 📖 Description
This project is a low-level networking implementation in Unity using **C# Sockets**. It features two distinct network architectures:
1. **TCP Implementation**: A connection-based Lobby system with real-time chat, player list synchronization, and safe disconnect handling.
2. **UDP Implementation**: A real-time minigame where players click to move a 3D capsule, synchronizing coordinates across all clients.

## ✨ Features & Architecture

**TCP (Lobby & Chat)**
* **Command Protocol**: Structured messages (`JOIN:`, `CHAT:`, `PLAYERS:`, `LEAVE:`).
* **State Synchronization**: Late joiners receive the active player list and full message history.

**UDP (Minigame)**
* **Broadcasting**: Server relays `POS:x,y,z` , also used the coordinates to all known endpoints using `InvariantCulture` for cross-region decimal safety.
* **Heartbeat System**: Clients send a `PING:` every 1 second.
* **Timeout Disconnect**: The server actively checks the `m_lastSeen` dictionary every frame and kicks/destroys players who stop sending packets for 5 seconds (`DISC:`).

**⭐ Bonus Implemented**
* **The Host also plays**: In the Create Game scene, clicking "Host" successfully starts the Server thread in the background and automatically connects a local Client to `127.0.0.1`, allowing the host to chat, appear in the list, and play the minigame alongside everyone else.

## ⚙️ Installation
**Pendiente**

## 🎮 Controls

* **TCP Chat**: Keyboard (Type in the InputField and press Send).
* **UDP Minigame**: **Left Mouse Click** on the 3D ground to move your capsule to that position.

## 🚀 How to Play / Test

<table>
  <tr>
        <td width="30%">
      <img src="LINK" width="100%" alt="TCP Chat">
    </td>
    <td>
      <b>1. TCP Lobby & Chat</b><br>
      Once connected in the TCP scene, you will see the updated player list. Type a message and click send to broadcast it to all connected players.
    </td>
  </tr>

  <tr>
    <td width="30%">
      <img src="LINK" width="100%" alt="Lobby UDP">
    </td>
    <td>
      <b>2. UDP Host or Join a Game</b><br>
      Open the Create/Join Lobby. Enter your username. If you want to host, click "Host". If you want to join a friend, enter their IP address and click "Join".
    </td>
  </tr>

  <tr>
    <td width="30%">
      <img src="LINK" width="100%" alt="UDP Minigame">
    </td>
    <td>
      <b>3. UDP Minigame</b><br>
      Click anywhere on the ground. A red capsule will appear and move to your cursor's position instantly on all clients' screens.
    </td>
  </tr>

  <tr>
    <td width="30%">
      <img src="LINK" width="100%" alt="Timeout System">
    </td>
    <td>
      <b>4. Test the Timeout (PING)</b><br>
      Force-close a client window. Wait 5 seconds. The server will detect the missing PINGs, print a Timeout warning, and order the remaining clients to destroy the disconnected player's capsule.
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
