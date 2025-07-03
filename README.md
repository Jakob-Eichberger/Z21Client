# Z21 [![](https://github.com/Jakob-Eichberger/z21Client/actions/workflows/dotnet.yml/badge.svg?branch=master)](https://github.com/Jakob-Eichberger/z21Client/actions/workflows/dotnet.yml)

The **Z21 client** library is a C# library for interfacing with the Roco and Fleischmann [Z21 Digital Command Center (DCC)](https://www.z21.eu/). It allows you to control trains, functions, and eventually signals/switches using the Z21 protocol. The library is available as a [NuGet package](https://www.nuget.org/packages/Z21/) for easy integration into your project.

## Features

- **Train Control**: Send commands to control locomotives, set speed, direction, and read locomotive status.
- **Function Control**: Activate and deactivate functions (lights, sound, etc.) on locomotives.
- **Signal/Switch Control (Planned)**: Future enhancements will include support for controlling signals and switches on your layout.

## Getting Started

1. **Installation**: Install the `Z21` NuGet package in your project.

    ```bash
    Install-Package Z21
    ```

2. **Initialization**:

    ```csharp
    using Z21;

    // Initialize the Z21 client
    var client = new Client();
    client.Connect(IPAddress.Parse("192.168.1.111")); // Replace with your Z21 IP address
    ```

3. **Train Control**:

    ```csharp
    // Set locomotive speed
    client.SetLocoDrive(new LokInfoData() { Adresse = new(1), Speed = 10, DrivingDirection = true }); // Locomotive address 1, speed 50, direction forward

    // Get locomotive speed
    client.OnGetLocoInfo += Client_OnGetLocoInfo; // Subscribe to the specific event
    void Client_OnGetLocoInfo(object? sender, Z21.Events.GetLocoInfoEventArgs e) // GetLocoInfoEventArgs contains address, speed, direction, and function states
    client.GetLocoInfo(new LokAdresse(2)); // Call GetLocoInfo() to pull the data from the z21 and distribute it on the event bus
    ```

4. **Function Control**:

    ```csharp
    // Activate function F1 (e.g., lights)
    client.SetLocoFunction(new FunctionData(1, 0, Z21.Enums.ToggleType.Off));; // Locomotive address 1, function F1

    // Deactivate function F1
    client.SetLocoFunction(new FunctionData(1, 0, Z21.Enums.ToggleType.On));; // Locomotive address 1, function F1

    // Toggle function F1
    client.SetLocoFunction(new FunctionData(1, 0, Z21.Enums.ToggleType.Toggle));; // Locomotive address 1, function F1
    ```

## Contributing

Contributions are welcome! If you encounter any issues or have suggestions, please open an issue or submit a pull request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
