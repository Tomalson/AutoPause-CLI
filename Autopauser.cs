using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

/*
    === AutoPause CLI Tool v1.0 (Release) ===
    Author: Tomalson
    Description: 
    A robust system utility that automatically pauses active applications/games 
    when a specific hardware disconnection is detected (Monitor, USB, COM).
    Features:
    - Smart Monitor Detection (WMI Connection Event)
    - Contextual Learning Mode (Capture Hardware IDs)
    - Safe Navigation System
*/

namespace AutoPauseCLI
{
    // Navigation actions to control the flow between menus
    enum NavAction { Stay, Back, GoToMain }

    class SavedDevice
    {
        public string FriendlyName { get; set; }
        public string HardwareID { get; set; }
        public string Category { get; set; }
    }

    class Program
    {
        private static bool _isRunning = true;
        private static ManagementEventWatcher _watcher;
        private static CancellationTokenSource _pollingCts;
        private static List<SavedDevice> _myDevices = new List<SavedDevice>();

        private static readonly string[] junkKeywords = {
                    "Root Hub", "Concentrator", "Host Controller", "PCI", "eXtensible",
                    "Policy Controller", "Virtual", "System", "Processor", "Motherboard", "ACPI",
                    "Print", "Volume", "Manager", "Bus", "Direct Memory Access", "Controller",
                    "VHF", "HID Structures",
                    "USB Input Device", "USB Composite Device",
                    "HID-compliant system controller", "HID-compliant vendor-defined device",
                    "HID-compliant device", "HID-compliant mouse", "HID Keyboard Device",
                    "Service", "Gateway", "Protocol", "Transport", "Enumerator",
                    "Avrcp", "A2DP", "Hands-Free", "HFP", "SNK", "Network", "SMS/MMS"
                };

        // --- WinAPI Imports ---
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);
        [DllImport("user32.dll")] private static extern IntPtr GetMessageExtraInfo();

        private const uint INPUT_KEYBOARD = 1;
        private const ushort KEYEVENTF_KEYUP = 0x0002;
        private const ushort VK_ESCAPE = 0x1B;

        [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public InputUnion U; }
        [StructLayout(LayoutKind.Explicit)] public struct InputUnion { [FieldOffset(0)] public MouseInput mi; [FieldOffset(0)] public KeyboardInput ki; [FieldOffset(0)] public HardwareInput hi; }
        [StructLayout(LayoutKind.Sequential)] public struct MouseInput { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] public struct KeyboardInput { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] public struct HardwareInput { public uint uMsg; public ushort wParamL; public ushort wParamH; }

        static void Main(string[] args)
        {
            Console.Title = "AutoPause CLI Tool - Monitor Guard";
            while (_isRunning)
            {
                ShowMainMenu();
            }
        }

        static void ShowMainMenu()
        {
            Console.Clear();
            DrawHeader();
            Console.WriteLine("  [1] Monitors (HDMI / DP)");
            Console.WriteLine("  [2] USB Peripherals / HID");
            Console.WriteLine("  [3] COM Ports");
            Console.WriteLine("  [0] Exit");
            Console.WriteLine("\n  Select option: ");

            var key = Console.ReadKey(true);

            switch (key.KeyChar)
            {
                case '1': HandleCategory("Monitors", "{4d36e96e-e325-11ce-bfc1-08002be10318}", false); break;
                case '2': HandleCategory("USB Peripherals", null, true); break;
                case '3': HandleCategory("COM Ports", "{4d36e978-e325-11ce-bfc1-08002be10318}", false); break;
                case '0': _isRunning = false; break;
            }
        }

        static void HandleCategory(string categoryName, string classGuid, bool useUsbFilter)
        {
            while (true)
            {
                List<string> detectedDevices = ScanForDevices(categoryName, classGuid, useUsbFilter);
                List<SavedDevice> categorySavedDevices = _myDevices.Where(d => d.Category == categoryName).ToList();

                Console.Clear();
                DrawHeader();
                Console.WriteLine($"--- Category: {categoryName} ---");
                Console.WriteLine("  [A] Listen to ALL Devices (Auto Mode)");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  [L] Not listed? Add Manually (Learning Mode)");
                Console.ResetColor();
                Console.WriteLine("---------------------------------");

                int optionIndex = 1;

                // Section: My Saved Devices
                if (categorySavedDevices.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("  -- MY SAVED DEVICES --");
                    foreach (var dev in categorySavedDevices)
                    {
                        Console.WriteLine($"  [{optionIndex}] {dev.FriendlyName}");
                        optionIndex++;
                    }
                    Console.ResetColor();
                }

                // Section: Detected System Devices
                Console.WriteLine("  -- DETECTED SYSTEM DEVICES --");
                if (detectedDevices.Count == 0) Console.WriteLine("  (No matching devices found)");

                foreach (var devName in detectedDevices)
                {
                    Console.WriteLine($"  [{optionIndex}] {devName}");
                    optionIndex++;
                }

                Console.WriteLine("---------------------------------");
                Console.WriteLine("  [Backspace] Back (Main Menu)");

                Console.Write("\nSelect option: ");
                string input = SmartReadLine();

                if (input == "BACK") return;

                if (input.ToUpper() == "L")
                {
                    LearnNewDevice(categoryName);
                    continue;
                }

                if (input.ToUpper() == "A")
                {
                    bool trySmartMonitor = (categoryName == "Monitors");
                    StartListening($"{categoryName} (Auto)", classGuid, null, useUsbFilter, trySmartMonitor, false);
                    // Returns here after stopping listening
                }
                else { int selection; if (int.TryParse(input, out selection) && selection > 0 && selection < optionIndex)
                    {
                    if (selection <= categorySavedDevices.Count)
                    {
                        // Saved Device -> ID Mode
                        var device = categorySavedDevices[selection - 1];
                        StartListening(device.FriendlyName, null, device.HardwareID, false, false, true);
                    }
                    else
                    {
                        // Detected Device -> Name Mode
                        int detectedIndex = selection - categorySavedDevices.Count - 1;
                        string selectedName = detectedDevices[detectedIndex];
                        StartListening(selectedName, classGuid, selectedName, useUsbFilter, false, false);
                    }
                }
                }
            }
        }

        static List<string> ScanForDevices(string categoryName, string classGuid, bool useUsbFilter)
        {
            HashSet<string> uniqueNames = new HashSet<string>();
            try
            {
                string query;
                if (useUsbFilter)
                    query = "SELECT Caption, DeviceID FROM Win32_PnPEntity WHERE ConfigManagerErrorCode = 0";
                else
                    query = $"SELECT Caption, DeviceID FROM Win32_PnPEntity WHERE ClassGuid = '{classGuid}' AND ConfigManagerErrorCode = 0";

                ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);


                foreach (ManagementObject obj in searcher.Get())
                {
                    string name = obj["Caption"]?.ToString();
                    string id = obj["DeviceID"]?.ToString();

                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(id))
                    {
                        bool isRelevant = !useUsbFilter;
                        if (useUsbFilter)
                        {
                            if (id.Contains("USB") || id.Contains("HID")) isRelevant = true;
                            if (id.StartsWith("BTH", StringComparison.OrdinalIgnoreCase)) isRelevant = false;
                            if (id.StartsWith("SW", StringComparison.OrdinalIgnoreCase)) isRelevant = false;
                        }

                        if (isRelevant)
                        {
                            bool isJunk = false;
                            foreach (string junk in junkKeywords)
                            {
                                if (name.IndexOf(junk, StringComparison.OrdinalIgnoreCase) >= 0) { isJunk = true; break; }
                            }
                            if (!isJunk) uniqueNames.Add(name);
                        }
                    }
                }
            }
            catch { }
            return uniqueNames.OrderBy(x => x).ToList();
        }

        static void LearnNewDevice(string currentCategory)
        {
            Console.Clear();
            DrawHeader();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($">>> ADD TO: {currentCategory.ToUpper()} <<<");
            Console.ResetColor();
            Console.WriteLine("\n1. Ensure the device is CONNECTED.");
            Console.WriteLine("2. When ready, press ENTER.");
            Console.WriteLine("3. Then DISCONNECT the device.");
            Console.WriteLine("\n[Backspace] Cancel");

            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Backspace) return;
            if (key.Key != ConsoleKey.Enter) return;

            Console.WriteLine("\nListening... PLEASE DISCONNECT YOUR DEVICE NOW!");
            Console.WriteLine("[Backspace] Cancel");

            bool found = false;
            SavedDevice tempDevice = null;

            var watcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_PnPEntity'"));

            EventArrivedEventHandler handler = (s, e) => {
                try
                {
                    var target = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                    tempDevice = new SavedDevice
                    {
                        FriendlyName = target["Caption"]?.ToString() ?? "Unknown",
                        HardwareID = target["DeviceID"]?.ToString() ?? ""
                    };
                    found = true;
                }
                catch { }
            };

            watcher.EventArrived += handler;
            watcher.Start();

            while (!found)
            {
                if (Console.KeyAvailable)
                {
                    if (Console.ReadKey(true).Key == ConsoleKey.Backspace)
                    {
                        watcher.Stop();
                        return;
                    }
                }
                Thread.Sleep(100);
            }

            watcher.Stop();

            if (tempDevice != null)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\nDETECTED: {tempDevice.FriendlyName}");
                Console.ResetColor();

                Console.WriteLine("\nEnter a friendly name (e.g., My Gaming Mouse): ");
                Console.Write("> ");
                string friendlyName = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(friendlyName)) friendlyName = tempDevice.FriendlyName;

                _myDevices.Add(new SavedDevice
                {
                    FriendlyName = friendlyName,
                    HardwareID = tempDevice.HardwareID,
                    Category = currentCategory
                });

                Console.WriteLine("\nSaved! Press any key...");
                Console.ReadKey();
            }
        }

        static string SmartReadLine()
        {
            string buffer = "";
            while (true)
            {
                ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                if (keyInfo.Key == ConsoleKey.Enter) { if (buffer.Length > 0) return buffer; }
                else if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (buffer.Length > 0) { buffer = buffer.Substring(0, buffer.Length - 1); Console.Write("\b \b"); }
                    else return "BACK";
                }
                else if (char.IsLetterOrDigit(keyInfo.KeyChar)) { buffer += keyInfo.KeyChar; Console.Write(keyInfo.KeyChar); }
            }
        }

        static NavAction StartListening(string title, string classGuid, string filterValue, bool isUnifiedUsbMode, bool trySmartMonitor, bool useIdFilter)
        {
            if (_watcher != null) { _watcher.Stop(); _watcher.Dispose(); _watcher = null; }

            Console.Clear();
            DrawHeader();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($">>> LISTENING ACTIVE <<<");
            Console.ResetColor();
            Console.WriteLine($"Target: {title}");
            if (useIdFilter) Console.WriteLine("Method: PRECISE (Hardware ID)");
            else if (!string.IsNullOrEmpty(filterValue)) Console.WriteLine($"Method: Standard (Name Match)");

            bool smartModeActive = false;

            if (trySmartMonitor)
            {
                try
                {
                    ManagementScope scope = new ManagementScope("root\\wmi");
                    WqlEventQuery query = new WqlEventQuery("SELECT * FROM WmiMonitorConnectionEvent");
                    _watcher = new ManagementEventWatcher(scope, query);
                    _watcher.EventArrived += HandleMonitorConnectionEvent;
                    _watcher.Start();
                    smartModeActive = true;
                    Console.WriteLine("Method: Smart WMI (Main Display Guard)");
                }
                catch { smartModeActive = false; }
            }

            if (!smartModeActive)
            {
                try
                {
                    string query = "SELECT * FROM __InstanceDeletionEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_PnPEntity'";
                    if (!isUnifiedUsbMode && !useIdFilter && classGuid != null)
                        query += $" AND TargetInstance.ClassGuid = '{classGuid}'";

                    _watcher = new ManagementEventWatcher(new WqlEventQuery(query));
                    _watcher.EventArrived += (s, e) => HandlePnPEvent(s, e, filterValue, isUnifiedUsbMode, useIdFilter);
                    _watcher.Start();
                }
                catch (Exception)
                {
                    // If WMI event subscription fails (often due to permissions), fall back to polling
                    Console.WriteLine("WMI event subscription unavailable — falling back to polling (non-admin mode).");
                    _pollingCts = new CancellationTokenSource();
                    var token = _pollingCts.Token;
                    Task.Run(() => PollForDisconnections(classGuid, filterValue, isUnifiedUsbMode, useIdFilter, token), token);
                }
            }

            Console.WriteLine("\n[Backspace] Stop & Return to List");
            Console.WriteLine("[F1] Stop & Return to Main Menu");

            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;

                    if (key == ConsoleKey.Backspace)
                    {
                        if (_watcher != null) { _watcher.Stop(); _watcher.Dispose(); _watcher = null; }
                        if (_pollingCts != null) { _pollingCts.Cancel(); _pollingCts.Dispose(); _pollingCts = null; }
                        return NavAction.Back;
                    }
                    if (key == ConsoleKey.F1)
                    {
                        if (_watcher != null) { _watcher.Stop(); _watcher.Dispose(); _watcher = null; }
                        if (_pollingCts != null) { _pollingCts.Cancel(); _pollingCts.Dispose(); _pollingCts = null; }
                        return NavAction.GoToMain;
                    }
                }
                Thread.Sleep(100);
            }
        }

        static void PollForDisconnections(string classGuid, string filterValue, bool isUnifiedMode, bool useIdFilter, CancellationToken token)
        {
            try
            {
                var previous = GetPnPDeviceMap(classGuid, isUnifiedMode);
                while (!token.IsCancellationRequested)
                {
                    var current = GetPnPDeviceMap(classGuid, isUnifiedMode);

                    // find removed devices
                    foreach (var kvp in previous)
                    {
                        if (token.IsCancellationRequested) break;
                        if (!current.ContainsKey(kvp.Key))
                        {
                            string name = kvp.Value ?? "Unknown";

                            if (useIdFilter)
                            {
                                if (kvp.Key.Equals(filterValue, StringComparison.OrdinalIgnoreCase)) TriggerAction($"{name} (My Device)");
                            }
                            else if (isUnifiedMode)
                            {
                                if (kvp.Key.IndexOf("USB", StringComparison.OrdinalIgnoreCase) >= 0 || kvp.Key.IndexOf("HID", StringComparison.OrdinalIgnoreCase) >= 0)
                                    TriggerAction(name);
                            }
                            else if (!string.IsNullOrEmpty(filterValue))
                            {
                                if (name.Trim().Equals(filterValue.Trim(), StringComparison.OrdinalIgnoreCase)) TriggerAction(name);
                            }
                            else
                            {
                                TriggerAction(name);
                            }
                        }
                    }

                    previous = current;
                    try { Task.Delay(1000, token).Wait(); } catch { break; }
                }
            }
            catch { }
        }

        static Dictionary<string, string> GetPnPDeviceMap(string classGuid, bool useUsbFilter)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string query;
                if (useUsbFilter)
                    query = "SELECT Caption, DeviceID FROM Win32_PnPEntity WHERE ConfigManagerErrorCode = 0";
                else
                    query = $"SELECT Caption, DeviceID FROM Win32_PnPEntity WHERE ClassGuid = '{classGuid}' AND ConfigManagerErrorCode = 0";

                ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);

                foreach (ManagementObject obj in searcher.Get())
                {
                    string name = obj["Caption"]?.ToString();
                    string id = obj["DeviceID"]?.ToString();

                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(id))
                    {
                        bool isRelevant = !useUsbFilter;
                        if (useUsbFilter)
                        {
                            if (id.IndexOf("USB", StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("HID", StringComparison.OrdinalIgnoreCase) >= 0) isRelevant = true;
                            if (id.StartsWith("BTH", StringComparison.OrdinalIgnoreCase)) isRelevant = false;
                            if (id.StartsWith("SW", StringComparison.OrdinalIgnoreCase)) isRelevant = false;
                        }

                        if (isRelevant)
                        {
                            bool isJunk = false;
                            foreach (string junk in junkKeywords)
                            {
                                if (name.IndexOf(junk, StringComparison.OrdinalIgnoreCase) >= 0) { isJunk = true; break; }
                            }
                            if (!isJunk) map[id] = name;
                        }
                    }
                }
            }
            catch { }
            return map;
        }

        private static DateTime _lastTrigger = DateTime.MinValue;

        static void HandleMonitorConnectionEvent(object sender, EventArrivedEventArgs e)
        {
            try { if ((bool)e.NewEvent["Active"] == false) TriggerAction("MONITOR (Signal Lost)"); } catch { }
        }

        static void HandlePnPEvent(object sender, EventArrivedEventArgs e, string filterValue, bool isUnifiedMode, bool useIdFilter)
        {
            try
            {
                var target = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                string name = target["Caption"]?.ToString() ?? "Unknown";
                string id = target["DeviceID"]?.ToString() ?? "";

                if (useIdFilter)
                {
                    if (id.Equals(filterValue, StringComparison.OrdinalIgnoreCase)) TriggerAction($"{name} (My Device)");
                    return;
                }

                if (isUnifiedMode)
                {
                    if (!id.Contains("USB") && !id.Contains("HID")) return;
                    if (id.StartsWith("BTH", StringComparison.OrdinalIgnoreCase)) return;
                    if (id.StartsWith("SW", StringComparison.OrdinalIgnoreCase)) return;
                }

                if (!string.IsNullOrEmpty(filterValue))
                {
                    string cleanName = name.Trim();
                    string cleanFilter = filterValue.Trim();
                    if (!cleanName.Equals(cleanFilter, StringComparison.OrdinalIgnoreCase)) return;
                }

                TriggerAction(name);
            }
            catch { }
        }

        static void TriggerAction(string deviceName)
        {
            if ((DateTime.Now - _lastTrigger).TotalSeconds < 3) return;
            _lastTrigger = DateTime.Now;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] DISCONNECTION DETECTED: {deviceName}");
            Console.ResetColor();
            Console.WriteLine("   -> Sending ESC...");
            SimulateEsc();
        }

        static void SimulateEsc()
        {
            INPUT down = new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KeyboardInput { wVk = VK_ESCAPE, dwFlags = 0 } } };
            INPUT up = new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KeyboardInput { wVk = VK_ESCAPE, dwFlags = KEYEVENTF_KEYUP } } };
            INPUT[] inputs = { down, up };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        static void DrawHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("========================================");
            Console.WriteLine("   AUTO-PAUSE CLI TOOL (v1.0)           ");
            Console.WriteLine("========================================");
            Console.ResetColor();
        }
    }
}