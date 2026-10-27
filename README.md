# Project deliverable: first iteration - Multiplayer Racing Game (UDP)
**Subject:** Xarxes i Jocs Online  
**Unity Version:** 6000.3.16f1  
**GitHub Repository:** [https://github.com/Jowy02/Xarxes-i-jocs-online](https://github.com/Jowy02/Xarxes-i-jocs-online)  

---

## 🚀 1. How to Play & Test (Execution Guide)

### Setup & Requirements
* Download the executable `.exe` from the latest GitHub Release or build it directly from Unity.
* The game must be tested on two separate PCs over a local network (LAN / Wi-Fi) or locally on one PC by launching two instances.
* Port used: `9050` (UDP).

### Step-by-Step Instructions:
1. **Host:**
   * Launch the build.
   * Enter your player name in the input field.
   * Click **"Host Game"**. The UDP server starts listening on port `9050`, and a local client automatically connects. Your role will display as **Host**.
2. **Client:**
   * Launch the build on the second machine.
   * Enter your player name and the **Local IP of the Host** (e.g., `192.168.1.XX` on LAN, or `127.0.0.1` if testing on the same PC).
   * Click **"Join Game"**.
3. **Waiting Room (Lobby):**
   * Both players will see each other in the live player list and can exchange text messages using the integrated UDP chat.
4. **Starting the Race:**
   * The **Host** clicks the **"Start Race"** button.
   * A synchronized network command triggers all connected clients to load the circuit scene (`RaceTrack`) simultaneously.
5. **Race Controls:**
   * **W / S**: Throttle / Reverse
   * **A / D**: Steer Left / Steer Right
   * **Space Bar**: Handbrake & Drift (triggers tire smoke particles and skid marks)
   * **Chat Button On Sceen**: Toggle in-game race chat

---

## 🗺️ 2. Main Scene Structure
* **Initial Scene:** `LobbyUDP`  
  Gagegigogu.
* **Game Scene:** `RaceTrack`  
  Gagegigogu.

---

## 🌐 3. Network Architecture: State vs Input

* **Input Synchronization:**
  Gagegigogu.
* **State Synchronization:**
  Gagegigogu.
  
---

## ⚡ 4. Latency Mitigation & Reliable Messaging

### A. How We Keep Lag Low:
  Gagegigogu.

### B. Ensuring Important Messages Arrive (Reliability over UDP):
  Gagegigogu.

---

## 🏆 5. EXTRAS IMPLEMENTED

We wanted to deliver a polished multiplayer experience, so we made some extras:

1. **Full 3D Elevation & Banked Movement (Pitch, Yaw, Roll):**  
   Gagegigogu.
2. **Synchronized Car Action: Drifting:**  
   Gagegigogu.
3. **Multiplayer Scalability (>2 Players):**  
  Gagegigogu.
4. **Environment & Car-to-Car Solid Collisions:**  
   Gagegigogu.
5. **Synchronized 5-Second Countdown:**  
   Gagegigogu.
6. **Full Game Loop (Lap Detection & Win Condition):**  
   Gagegigogu.
7. **Host Race Restart System:**  
   Gagegigogu.
8. **Gagegigogu:**  
   Gagegigogu.

---

## 🔄 6. Detailed Changes Since The Last Delivery


---

## 🐛 7. Known Bugs / Limitations
* If a player disconnects abruptly by force-closing the window (Alt+F4) without pressing disconnect, their car will remain stationary on the track until the server's 5-second timeout cleans up.
* If a extreme packet loss over an unstable Wi-Fi occurs, it may cause interpolation lag drift corners before the next packet arrives.

---

## 👥 8. Team Members & Contributions

* **Ana Alcaraz ([Audra0000](https://github.com/Audra0000)):**  
  Worked on: ...

* **Arthur Cordoba ([000Arthur](https://github.com/000Arthur)):**  
  Worked on: ...

* **Haosheng Li ([HaosLii](https://github.com/HaosLii)):**  
  Worked on: ...

* **Pablo Sanjose ([XXPabloS](https://github.com/XXPabloS)):**  
  Worked on:
  * RaceTrack Scene and built the circuit.
  * Start Race Button for host and the synchronized scene loading trigger.
  * Starting grid spawn points car system.
  * Capsule Minigame removed.
  * Fix on serialization and Capsule Minigame removed, replacing JSON text with native binary serialization.
  * Jitter Removal with Vector3.Lerp and Quaternion.Slerp interpolation.
  * Restart Race functionality.
  * Drift Marks.

* **Joel Vicente ([Jowy02](https://github.com/Jowy02)):**  
  Worked on: ...

