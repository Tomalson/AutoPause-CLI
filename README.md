# AutoPause Monitor Guard 🛡️ (v11.0)

A smart, CLI-based system utility designed to prevent "death by AFK" in games when a hardware disconnection occurs (Monitor, USB, COM).

## 🚀 Key Features

* **Smart Monitor Guard:** Uses advanced WMI events (`WmiMonitorConnectionEvent`) to detect signal loss on primary monitors instantly.
* **Contextual Learning Mode `[L]`:** * Don't see your device on the list? Just press `L`.
    * Disconnect the device, and the tool captures its unique **Hardware ID**.
    * Saves it as a "My Device" shortcut for 100% precision (ignoring generic names).
* **Clean UI:** Automatically filters out system "junk" (Hubs, Virtual controllers) to show only real peripherals.
* **Safe Navigation:** * `F1`: Return to Main Menu.
    * `Backspace`: Go back one step.
* **Zero-Latency Action:** Instantly injects an `ESC` key press via WinAPI upon disconnection.

## 🛠️ How to Use

1.  Run `AutoPauseCLI.exe` as **Administrator**.
2.  Select a category (e.g., `[2] USB Peripherals`).
3.  **Option A:** Select a specific device from the list.
4.  **Option B:** Use `[L]` to learn a new device if yours is hidden/generic.
5.  **Option C:** Select `[A]` to monitor ALL devices in that category.
6.  The tool runs in the background. Press `F1` or `Backspace` to stop.

## 📦 Build

```bash
csc Program.cs /r:System.Management.dll /win32manifest:app.manifest
