# Project deliverable: first iteration - Multiplayer Racing Game (UDP)
**Subject:** Xarxes i Jocs Online  
**Unity Version:** 6000.3.16f1  
**GitHub Repository:** [https://github.com/Jowy02/Xarxes-i-jocs-online](https://github.com/Jowy02/Xarxes-i-jocs-online)  
**VideoDemo:** [https://youtu.be/Z4KymsTZbOk](https://youtu.be/Z4KymsTZbOk)

---

## 🚀 1. How to Play & Test (Execution Guide)

### Setup & Requirements
* Download the executable `.exe` from the latest GitHub Release or build it directly from Unity.
* The game must be tested on two separate PCs over a local network (LAN / Wi-Fi) or locally on one PC by launching two instances.
* Port used: `9050` (UDP).

> ⚠️ **CRITICAL NOTES FOR UNITY EDITOR SETUP:**  
> 1. Copy and replace the `EditorBuildSettings.asset` file inside your project's `ProjectSettings` folder, and then open the `LobbyUDP` scene.  
> 2. Change player input settings to **Both**: navigate to `Edit` → `Project Settings` → `Player` → `Other Settings` → `Active Input Handling` and set it to **Both**.

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
5. **Race Keyboard Controls:**
   * **W / S**: Throttle / Reverse
   * **A / D**: Steer Left / Steer Right
   * **Space Bar**: Handbrake & Drift (triggers tire smoke particles and skid marks)
   * **C**: Toggle in-game race chat
   * **M**: Toggle in-game menu
5. **Race Controller Controls:**
   * **RT / LT**: Throttle / Reverse
   * **Joystick**: Steer Left / Steer Right
   * **Circle button**: Handbrake & Drift (triggers tire smoke particles and skid marks)
   * **Chat Button On Sceen**: Toggle in-game race chat
---

## 🗺️ 2. Main Scene Structure
* **Initial Scene:** `LobbyUDP`  
Handles the initial lobby setup, player connections, and core multiplayer functionality. Players can choose to **Host** a match, becoming the session leader and managing the game session, or **Join** a match to search for and connect to hosted sessions before the race begins. Essential multiplayer session data is preserved during the transition to `RaceTrack` to maintain session continuity.
* **Game Scene:** `RaceTrack`  
Contains the race environment, track layout, car spawning system, and in-game gameplay elements. It supports multiplayer racing as well as single-player testing using host-assigned player configurations. Gameplay features include car synchronization, in-game chat, and local turn indicators.

---

## 🌐 3. Network Architecture: State vs Input
We send state, not input.
* **Input Synchronization:**
Not used. Keyboard inputs (W/A/S/D/Space) are only processed locally by PrometeoCarController and are never sent. Only discrete events (START_RACE:, RESTART_RACE:, CHAT:, LEAVE:) travel as text messages.
* **State Synchronization:**
Each client simulates its own car locally and sends its resulting state (ID, position, rotation and drift flag) in a 30-byte binary packet at 20 Hz. The server relays it to the other clients, where the remote car is kinematic and smoothly interpolated towards the received state.

### **Why State?**
- No physics divergence: Rigidbody physics is not deterministic across machines, so simulating other players’ inputs locally would make cars drift apart.
- Tolerant to packet loss: a lost packet is harmless because the next one (50 ms later) contains the full up-to-date state.
- Simple and cheap: no lockstep or rollback is needed, and 30 bytes at 20 Hz is affordable for up to 12 players.
---

## ⚡ 4. Latency Mitigation & Reliable Messaging

### A. How We Keep Lag Low:
1. **Compact Binary Serialization:**  
   We eliminated JSON and text for gameplay. Using our custom `NetworkSerializer` (`MemoryStream`, `BinaryWriter`, and `BinaryReader`), the vehicle's transform and drifting state are packed into **exactly 30 bytes**:
   **Type (1B) + ID (4B) + Position (12B) + Euler Rotation (12B) + Drift (1B) = 30 Bytes** 

   Compared to standard JSON (~95 bytes), this is a **~68% bandwidth reduction**, we lower the socket buffer overhead and serialization latency.
3. **Fixed Rate Update (20 Hz):** Updates are with a delta timer (`0.05s`) rather than on every graphic frame (`Update()`), preventing socket saturation and packet queuing on high refresh rate displays (144 Hz or more).
4. **Client-Side Interpolation (Jitter Removal):** Remote vehicles do not snap abruptly to raw network coordinates, they smoothly interpolate position and rotation using `Vector3.Lerp` and `Quaternion.Slerp`, masking network jitter and frame discrepancies.

### B. Ensuring Important Messages Arrive (Reliability over UDP):
As not all data in game has the same importance, we treat it differently:

* **Car Movement:** Positions are sent 20 times per second. If a packet gets lost, it doesn't matter, we don't resend it. 50 ms later a newer positions arrives and fixes it. Resending old positions would only create more lag.
* **Important Messages (Start, Lap, Restart, Lobby):**  
Events like `START_RACE:`, `RESTART_RACE:`, `LAP:`, `YOUR_ID:` only happen once. So to make sure they don't get lost:
  *  We keep a **PING heartbet every 1 second** so the router's NAT table never closes the connection.
  * The server always replies to the exact address provided by `ReceiveFrom`, avoiding NAT routing errors.
  * We tried to replicate the **ACK ackowledgment system**, where the sender repeats the important message until the receiver sends back confirmation.

---

## 🏆 5. EXTRAS IMPLEMENTED

We wanted to deliver a polished and engaging multiplayer experience with greater depth and complexity, so we implemented several extra features:

1. **Hereditary Host:** Previously, whenever the host disconnected during a game, all players were disconnected, forcing the session to end. In this new version, we have implemented a host migration system that automatically assigns a new host when the current one disconnects. This allows the game to continue without interrupting the remaining players' experience.
   
<p align="center">
<img src="Gifs/Image_InGame/Full movement.png" width="50%" alt="TCP Chat">
</p>

2. **Unique Car:** Each player's car is now assigned a unique color based on their starting position. This makes it easier to identify and distinguish between players cars during a race, improving visibility and the overall multiplayer experience.
   
<p align="center">
<img src="Gifs/Image_InGame/Drifting.png" width="50%" alt="TCP Chat">
</p>

3. **Ingame UI:** We have redesigned the in-game interface, introducing new features and a cleaner layout. The buttons previously displayed in the middle of the screen have been replaced with drop-down menus, making the interface less intrusive and easier to navigate.
The new interface includes:

    * Chat: Press the C key to show or hide the chat window.
    * Menu: Press the M key to show or hide the menu. From here, players can leave the game, while the host also has access to the Restart Race button.
<p align="center">
<img src="Gifs/Image_InGame/Drifting.png" width="50%" alt="TCP Chat">
</p>

4. **Speedometer:** We have added a speedometer to the in-game interface, allowing players to monitor their vehicle's current speed in km/h. This provides useful real-time feedback while driving and makes the racing experience more immersive.
<p align="center">
<img src="Gifs/Image_InGame/Drifting.png" width="50%" alt="TCP Chat">
</p>

5. **Visual Standings:** Although a standings system was already implemented in the previous version, players could only see the final results once the race was over. We have improved this system by introducing real-time standings, allowing players to monitor everyone's progress throughout the race, including their current position and completed laps. 
<p align="center">
<img src="Gifs/Image_InGame/Drifting.png" width="50%" alt="TCP Chat">
</p>

6. **New lobby visuals:** We have maintained all the functionality of the previous lobby versions while giving it a completely new visual design. The updated layout makes it easier for players to understand where everything is located and interact with the available options. Additionally, the new visuals give the lobby a more distinctive identity and add a personal touch to the game.
<p align="center">
<img src="Gifs/Image_InGame/Drifting.png" width="50%" alt="TCP Chat">
</p>

7. **Controller Inputs:** We have added controller support, allowing players to choose between keyboard and controller inputs whenever they want. To prevent input conflicts, the game automatically switches between input devices rather than allowing both to control the car simultaneously. The system handles the following situations automatically:

    * Controller connected: The game switches to controller input, disabling keyboard controls for driving.
    * Controller disconnected during a race: The game automatically switches back to keyboard controls.
    * Controller reconnected: The game switches back to controller input.

---

## 🔄 6. Detailed Changes Since The Last Delivery

Compared with the previous delivery, this version introduces a multiplayer car race.

- **:** .

---

## 🐛 7. Known Bugs / Limitations
* 
---

## 👥 8. Team Members & Contributions

* **Ana Alcaraz ([Audra0000](https://github.com/Audra0000)):**  
  Worked on:

* **Arthur Cordoba ([000Arthur](https://github.com/000Arthur)):**  
  Worked on:

* **Haosheng Li ([HaosLii](https://github.com/HaosLii)):**  
  Worked on: 

* **Pablo Sanjose ([XXPabloS](https://github.com/XXPabloS)):**  
  Worked on:

* **Joel Vicente ([Jowy02](https://github.com/Jowy02)):**  
  Worked on:
