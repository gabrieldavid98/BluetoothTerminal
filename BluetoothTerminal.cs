using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;

namespace BluetoothTerminal;

public class BluetoothTerminal : IDisposable
{
    private readonly BluetoothClient _client;
    private BluetoothDeviceInfo? _selectedDevice = null;
    private List<BluetoothDeviceInfo> _discoveredDevices;
    private StreamWriter? _streamWriter;

    public BluetoothTerminal()
    {
        _client = new BluetoothClient();
        _discoveredDevices = [];
    }

    private static void PrintHelp()
    {
        Console.WriteLine("'help' or 'h' shows available commands");
        Console.WriteLine("'list' or 'l' lists available devices");
        Console.WriteLine("'refresh' or 'r' refresh available devices");
        Console.WriteLine("'select' or 's' [DEVICE_NAME] selects a device");
        Console.WriteLine("'connect' or 'c' [PIN?] connect to a selected device with an optional Bluetooth pin");
        Console.WriteLine("'msg' [MESSAGE] sends a message to selected device");
        Console.WriteLine("'quit' or 'q' quits the terminal ");
    }

    private async Task DiscoverDevices()
    {
        if (_discoveredDevices.Count > 0)
        {
            return;
        }

        await foreach (var discoveredDevice in _client.DiscoverDevicesAsync())
        {
            if (string.IsNullOrWhiteSpace(discoveredDevice.DeviceName))
            {
                continue;
            }

            _discoveredDevices.Add(discoveredDevice);
        }
    }

    private void PrintDiscoveredDevices()
    {
        if (_discoveredDevices.Count == 0)
        {
            Console.WriteLine("No devices");
            return;
        }

        _discoveredDevices.Select(d => d.DeviceName)
            .ToList()
            .ForEach(Console.WriteLine);
    }

    private async Task RefreshDevices()
    {
        _discoveredDevices = [];
        await DiscoverDevices();
        Console.WriteLine("Devices refreshed!");
    }

    private void SelectDevice(string deviceName)
    {
        var selectedDevice = _discoveredDevices.FirstOrDefault(d => d.DeviceName == deviceName);
        if (selectedDevice is null)
        {
            Console.WriteLine("Selected device not found");
            return;
        }

        _selectedDevice = selectedDevice;
    }

    private void Connect(string? pin = null)
    {
        if (_selectedDevice is null)
        {
            Console.WriteLine("No device selected");
            return;
        }

        if (!_selectedDevice.Authenticated)
        {
            BluetoothSecurity.PairRequest(_selectedDevice.DeviceAddress, pin);
        }

        _selectedDevice.Refresh();
        _client.Connect(_selectedDevice.DeviceAddress, BluetoothService.SerialPort);
        Console.WriteLine("Connected");

        var stream = _client.GetStream();
        _streamWriter = new(stream, System.Text.Encoding.ASCII);
    }

    private void Msg(string message)
    {
        if (_selectedDevice is null)
        {
            Console.WriteLine("No device selected");
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            Console.WriteLine("Message is empty!");
            return;
        }

        _streamWriter?.WriteLine($"{message}\r\n\r\n");
        _streamWriter?.Flush();
    }

    public async Task Run()
    {
        Console.Clear();
        Console.WriteLine("Bluetooth terminal 0.0.1");
        Console.WriteLine("Write 'help' or 'h' to see available commands");
        Console.WriteLine("Discovering devices!");
        await DiscoverDevices();
        Console.WriteLine("Done!");

        var shouldRun = true;
        while (shouldRun)
        {
            if (_selectedDevice is not null)
            {
                var connected = _client.Connected ? "$" : string.Empty;
                Console.Write($"{connected}({_selectedDevice.DeviceName})>> ");
            }
            else
            {
                Console.Write(">> ");
            }

            var input = Console.ReadLine()?.ReplaceLineEndings();
            if (input is null)
            {
                throw new Exception("Invalid input");
            }

            var splittedInput = input.Split(" ");
            var cmd = splittedInput[0];
            var cmdParams = splittedInput.Skip(1).ToArray();

            switch (cmd)
            {
                case "help" or "h":
                    PrintHelp();
                    break;
                case "list" or "l":
                    PrintDiscoveredDevices();
                    break;
                case "refresh" or "r":
                    await RefreshDevices();
                    break;
                case "select" or "s":
                    SelectDevice(cmdParams[0]);
                    break;
                case "connect" or "c":
                    Connect(cmdParams.Length > 1 ? cmdParams[0] : null);
                    break;
                case "msg":
                    Msg(string.Join(' ', cmdParams));
                    break;
                case "quit" or "q":
                    shouldRun = false;
                    break;
            }
        }

        Console.WriteLine("See ya soon, bye :)");
    }

    public void Dispose()
    {
        _client.Dispose();
        _streamWriter?.Dispose();
    }
}