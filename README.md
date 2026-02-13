# DualSense Haptic Ctrl

**DualSense Haptic Ctrl** is a bridge application designed to reroute system audio into PS5 DualSense haptic feedback. It features a real-time 3D control panel built with Three.js, allowing you to visualize and fine-tune your tactile experience.

---

## 🛠 Prerequisites

To ensure the haptic actuators receive the correct signals, follow these hardware and software configurations:

* **Controller:** Sony DualSense (PS5) connected via **USB** (Bluetooth does not support the necessary multi-channel audio for haptics).
* **Audio Configuration:**
1. Right-click the **Volume icon** in your Taskbar > **Sound Settings** (or Sounds).
2. Select **Wireless Controller** and click **Configure**.
3. Set the speaker configuration to **Quadraphonic**.


> **Note:** This is required to separate standard audio from the haptic vibration data.



---

## 🚀 Quick Start

### 1. Start the Bridge Server

The backend handles the signal processing and communication with the controller.

```bash
cd bridge-server
dotnet run

```

### 2. Launch the UI

The frontend provides the 3D dashboard and manual controls.

* Navigate to the `frontend-ui/` directory.
* Open `index.html` in your browser.
* *Tip: Using the **VS Code Live Server** extension is highly recommended for the best experience.*

---

## 🕹 Features

* **Audio-to-Haptic:** Real-time frequency filtering optimized for DualSense actuators ().
* **Manual Generator:** Manually trigger actuators with specific frequencies to test haptic intensity.
* **Input Monitor:** Real-time visual feedback for every button press and trigger pull.
* **3D Interface:** An interactive, high-fidelity dashboard powered by **Three.js**.

---

## 📁 Project Structure

| Directory | Technology | Description |
| --- | --- | --- |
| `/bridge-server` | **.NET Core (C#)** | The API layer managing audio routing and hardware communication. |
| `/frontend-ui` | **Three.js / JS** | The web-based 3D dashboard for user interaction. |

---

Would you like me to generate a **License** section or a **Troubleshooting** guide to add to this README?
