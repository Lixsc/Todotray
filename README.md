# TodoTray 🚀

> A sleek, lightning-fast Windows system tray To-Do app with reminders, global hotkeys, priority levels, and local storage.

![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows)
![Framework](https://img.shields.io/badge/.NET%20Framework-4.7.2%2B-512BD4?logo=dotnet)
![License](https://img.shields.io/badge/License-MIT-blue.svg)

---

## ✨ Features

- **⚡ Global Hotkey (<kbd>Alt</kbd> + <kbd>Shift</kbd> + <kbd>T</kbd>):** Summon or dismiss TodoTray instantly from anywhere on Windows, even while inside full-screen games or applications.
- **🔔 Task Reminders & Alarms:** Set quick reminders (*15m, 30m, 1h, 3h, Tomorrow 9 AM, or Custom*) and get Windows system tray notifications when time is up.
- **📌 Pin to Desktop (Always on Top):** Keep your tasks floating on top of your work while coding, studying, or researching.
- **⭐ Task Priority Badges:** Organize tasks by **🔥 High**, **⚡ Medium**, and **⚪ Normal** priority. High-priority tasks automatically float to the top.
- **📊 Visual Progress Bar:** Live completion percentage tracker that turns green at 100%.
- **🔍 Instant Search (<kbd>Ctrl</kbd> + <kbd>F</kbd>):** Find any task in real-time as you type.
- **💾 100% Offline & Private:** Stored locally in `%LocalAppData%\TodoTray\todos.json`. No cloud tracking, no accounts needed.
- **🚀 Windows Startup Integration:** Optional toggle to boot silently into the system tray when your PC starts.

---

## ⌨️ Shortcuts

| Shortcut | Action |
| :--- | :--- |
| <kbd>Alt</kbd> + <kbd>Shift</kbd> + <kbd>T</kbd> | Global summon / hide toggle |
| <kbd>Ctrl</kbd> + <kbd>N</kbd> | Focus task input box |
| <kbd>Ctrl</kbd> + <kbd>F</kbd> | Focus search box |
| <kbd>Enter</kbd> | Add task |
| <kbd>Space</kbd> | Toggle completed state of selected task |
| <kbd>Delete</kbd> | Delete selected task |
| <kbd>Esc</kbd> | Hide to system tray |

---

## 🛠️ Building from Source

1. Clone this repository:
   ```shell
   git clone https://github.com/Lixsc/Todotray.git
   ```
2. Open `IDK.slnx` or `IDK.csproj` in Visual Studio 2022.
3. Build and run in **Release** or **Debug** mode.

---

## 📄 License
This project is licensed under the MIT License.
