# ⏯️ MediaFlyout

A light system tray flyout that provides an alternate easy access to control media playback.

---

### 🖼️ Gallery

<p align="center">
  <img src="Windows 11.png" width="97%" alt="Windows 11 Desktop Preview" />
  <br>
  <em>Application Integrated into the Windows 11 taskbar</em>
</p>

<p align="center">
  <img src="Windows 10.png" width="97%" alt="Windows 10 Integration" />
  <br>
  <em>Application Integrated into the Windows 10 taskbar</em>
</p>

<p align="center">
  <img src="Application User Interface.png" width="48%" alt="UI Detail" />
  <br>
  <em>MediaFlyout User Interface </em>
</p>

---

### ✨ Key Features
- **Cross-Architecture:** Native builds provided for **x64** (Standard PCs) and **ARM64** (Apple Silicon/Snapdragon).
- **Theme-Aware:** Supports both Light and Dark Modes natively.
- **Transparency Support:** Supports Acryllic.
- **Ultra-Lightweight:** Negligible CPU and Memory footprint.

### 🚀 How to Run
1.  Navigate to the [Releases](https://github.com/Raanans28/MediaFlyout/releases) tab.
2.  Download the binary corresponding to your CPU:
    * `MediaFlyout_x64.exe` (most Intel/AMD desktops/laptops).
    * `MediaFlyout_ARM64.exe` (Macs running Parallels/VMware or Windows ARM devices).
3.  Launch the executable. *(Requires .NET 8.0 Runtime)*.

> **Tip:** To make it start with Windows, press `Win + R`, type `shell:startup`, and place a shortcut to the EXE in that folder.

### 📖 How to Use
- Click the ▶ icon in the system tray to open the flyout  
- Click outside the flyout or press the "Ⅹ" button to close it  
- Scroll on the flyout to adjust volume  
- Right-click the tray icon → **Exit** to fully close the app  


### 🛠️ Tech Stack
- **Framework:** .NET 8 / WPF
- **Language:** C#
- **Styling:** Custom XAML
- **Development:** .NET CLI

## ⚠️ Notes

- This project was built using AI and is classified as "Vibe Coding". 
- The focus was on achieving a clean Windows-style UI rather than strict adherence to best practices.

---
*Developed as a personal project focusing on UI consistency and architecture-aware distribution.*
