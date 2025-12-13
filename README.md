
# AutoPause Monitor Guard — Smart Pause for Disconnections 🛡️🎮

AutoPause Monitor Guard is a lightweight, CLI utility that automatically sends an `ESC` keypress when a tracked device disconnects (monitor, USB peripheral, or COM device). It’s designed to help pause games or applications instantly when a hardware failure or disconnect occurs.

This release supports both fast WMI-based detection (when available) and a non-privileged polling fallback so the tool can run without Administrator rights in most environments.

## Quick Start 🚀

1. Open PowerShell and change to the project folder:

```powershell
cd "C:\Users\poz-6\Desktop\AutoPause-CLI"
```

2. Build and run (pick one of the options below).

## Build & Run 🧰

Option A — .NET SDK (recommended; works as non-admin):

```powershell
dotnet new console --framework net6.0 --force
del Program.cs
dotnet add package System.Management
# Make sure Autopauser.cs is in this folder
dotnet run
```

Option B — csc (older .NET Framework compiler):

```powershell
"C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" Autopauser.cs /r:System.Management.dll /out:AutoPauseCLI.exe
.\AutoPauseCLI.exe
```

If `csc` is not in your PATH, use the full path shown above. If you don’t have the .NET SDK, download it from https://dotnet.microsoft.com.

## Usage — what to press 🎛️

- Main menu:
	- `1` — Monitors (HDMI / DP)
	- `2` — USB Peripherals / HID
	- `3` — COM Ports
	- `0` — Exit
- In category view:
	- `A` — Listen to ALL devices (Auto Mode)
	- `L` — Learning Mode (press ENTER, then disconnect device)
	- Choose a numbered device to monitor
	- `Backspace` — Back
	- `F1` — Stop and return to Main Menu

The program listens for disconnection events and sends an `ESC` to help pause or exit the active application.

## Permissions & Behavior ⚙️

- WMI events (e.g. `WmiMonitorConnectionEvent`) provide the fastest detection but may require Administrator privileges on some systems.
- If WMI subscription fails (no privileges), the program automatically falls back to polling the device list every ~1s; this works as a normal user but may be slightly slower.

## Troubleshooting 🩺

- `csc` not recognized: use the full path to `csc.exe` (see examples) or install the .NET SDK and use `dotnet run`.
- Build errors: copy the full compiler output and paste it here — I’ll help fix it.

## Want me to do more? 🤝

- I can generate a proper `dotnet` project (`.csproj`) and include `Autopauser.cs` so `dotnet run` works out of the box.
- I can also add optional logging, a small config file, or remove/adjust the manifest if it causes build issues.

If you want any of the above, tell me which and I’ll set it up.

---

Original author: Tomalson