/* Z21 - C#-Implementierung des Protokolls der Kommunikation mit der digitalen
 * Steuerzentrale Z21 oder z21 von Fleischmann/Roco
 * ---------------------------------------------------------------------------
 * Datei:     z21.cs
 * Version:   16.06.2014
 * Besitzer:  Mathias Rentsch (rentsch@online.de)
 * Lizenz:    GPL
 *
 * Die Anwendung und die Quelltextdateien sind freie Software und stehen unter der
 * GNU General Public License. Der Originaltext dieser Lizenz kann eingesehen werden
 * unter http://www.gnu.org/licenses/gpl.html.
 * 
 */

using Serilog;
using Serilog.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Markup;
using Z21.Enums;
using Z21.Events;
using Z21.Model;

namespace Z21
{

    public sealed class Client
    {
        public const int maxDccStep = 127;
        private bool clientReachable = false;
        public const int port = 21105;

        /// <summary>
        /// True if the z21 client is reachable via icmp ping. False if not reachable.
        /// </summary>
        public bool ClientReachable
        {
            get => clientReachable; private set
            {
                var clientReachabletemp = clientReachable;
                clientReachable = value;

                if (!clientReachabletemp && value)
                {
                    Log.Logger.Information("Z21 Client reachable via ping");
                    LogOn();
                    GetStatus();
                    ClientReachabilityChanged?.Invoke(this, clientReachable);
                    RenewClientSubscription.Enabled = true;
                }
                else if (clientReachabletemp && !value)
                {
                    Log.Logger.Warning("Z21 Client unreachable via ping");
                    ClientReachabilityChanged?.Invoke(this, clientReachable);
                    RenewClientSubscription.Enabled = false;
                }
            }
        }

        public event EventHandler<FirmwareVersionInfoEventArgs> OnGetFirmwareVersion = default!;

        public event EventHandler<HardwareInfoEventArgs> OnGetHardwareInfo = default!;

        public event EventHandler<LanCode> OnGetLanCode = default!;

        public event EventHandler<GetLocoInfoEventArgs> OnGetLocoInfo = default!;

        public event EventHandler<GetSerialNumberEventArgs> OnGetSerialNumber = default!;

        public event EventHandler<VersionInfoEventArgs> OnGetVersion = default!;

        public event EventHandler<DataEventArgs> OnReceive = default!;

        public event EventHandler<StateEventArgs> OnStatusChanged = default!;

        public event EventHandler OnStopped = default!;

        public event EventHandler<SystemStateEventArgs> OnSystemStateDataChanged = default!;

        public event EventHandler<TrackPowerEventArgs> TrackPowerChanged = default!;

        public event EventHandler<bool> ClientReachabilityChanged = default!;

        private IZ21Communicator Z21Communicator { get; } = new Z21Communicator(port);

        public IPAddress Address { get; private set; } = default!;

        public bool IsConnected { get; private set; } = false;

        private Timer RenewClientSubscription { get; } = new Timer() { AutoReset = true, Enabled = false, Interval = new TimeSpan(0, 0, 50).TotalMilliseconds, };

        private Timer PingClient { get; } = new Timer() { AutoReset = true, Enabled = false, Interval = new TimeSpan(0, 0, 5).TotalMilliseconds, };

        public void Connect(IPAddress clientIp)
        {
            Log.Logger.Information($"Initialising the Z21 client.");
            if (clientIp is null)
            {
                throw new NullReferenceException($"Invalid IP for the client. Can not be null!");
            }

            if (IsConnected)
            {
                Log.Logger.Warning("Skipping connecting to Z21 client. Client already connected.");
                return;
            }

            if (OperatingSystem.IsWindows())
            {
                Log.Logger.Information("Z21 Client: Allowing NAT traversal");
                Z21Communicator.AllowNatTraversal(true);
            }
            Address = clientIp;
            Z21Communicator.Connect(Address, port);
            IsConnected = true;
            Log.Logger.Information($"UPD connection to {Address}:{port} established.");

            Z21Communicator.BeginReceive(new AsyncCallback(Receiving), default!);

            RenewClientSubscription.Elapsed += (a, b) => GetStatus();
            PingClient.Elapsed += PingClient_Elapsed;
            PingClient.Enabled = true;
            _ = Task.Run(async () => ClientReachable = await PingAsync());

            Log.Logger.Information($"Z21 client initialised.");
        }

        public void Dispose()
        {
            LogOFF();
            Z21Communicator.Close();
            IsConnected = false;
            Z21Communicator.Dispose();
        }

        public void GetFirmwareVersion()
        {
            byte[] bytes = new byte[7];
            bytes[0] = 0x07;
            bytes[1] = 0;
            bytes[2] = 0x40;
            bytes[3] = 0;
            bytes[4] = 0xF1;
            bytes[5] = 0x0A;
            bytes[6] = 0xFB;
            Log.Logger.ForContext("data", bytes).Information($"GET FIRMWARE VERSION");
            Sending(bytes);
        }

        public void GetHardwareInfo()
        {
            byte[] bytes = new byte[4];
            bytes[0] = 0x04;
            bytes[1] = 0;
            bytes[2] = 0x1A;
            bytes[3] = 0;
            Log.Logger.ForContext("data", bytes).Information($"GET HWINFO");
            Sending(bytes);
        }

        public void GetLanCode()
        {
            byte[] bytes = new byte[4];
            bytes[0] = 0x04;
            bytes[1] = 0x00;
            bytes[2] = 0x18;
            bytes[3] = 0x00;
            Log.Logger.ForContext("data", bytes).Information($"GET LAN CODE");
            Sending(bytes);
        }

        public void GetLocoInfo(LokAdresse adresse)
        {
            if (adresse is null) return;
            byte[] bytes = new byte[9];
            bytes[0] = 0x09;
            bytes[1] = 0;
            bytes[2] = 0x40;
            bytes[3] = 0;
            bytes[4] = 0xE3;
            bytes[5] = 0xF0;
            bytes[6] = adresse.ValueBytes.Adr_MSB;
            bytes[7] = adresse.ValueBytes.Adr_LSB;
            bytes[8] = (byte)(bytes[4] ^ bytes[5] ^ bytes[6] ^ bytes[7]);
            Log.Logger.ForContext("data", bytes).Information($"Get loco info for adresse {adresse.Value}");
            Sending(bytes);
        }

        public void GetSerialNumber()
        {
            byte[] bytes = new byte[4];
            bytes[0] = 0x04;
            bytes[1] = 0;
            bytes[2] = 0x10;
            bytes[3] = 0;
            Log.Logger.ForContext("data", bytes).Information($"GET SERIAL NUMBER");
            Sending(bytes);
        }

        public void GetStatus()
        {
            byte[] bytes = new byte[7];
            bytes[0] = 0x07;
            bytes[1] = 0;
            bytes[2] = 0x40;
            bytes[3] = 0;
            bytes[4] = 0x21;
            bytes[5] = 0x24;
            bytes[6] = 0x05;
            Log.Logger.ForContext("data", bytes).Information($"GET STATUS");
            Sending(bytes);
        }

        public void GetVersion()
        {
            byte[] bytes = new byte[7];
            bytes[0] = 0x07;
            bytes[1] = 0;
            bytes[2] = 0x40;
            bytes[3] = 0;
            bytes[4] = 0x21;
            bytes[5] = 0x21;
            //bytes[6] = 0x47;   // = XOR-Byte  selbst ausgerechnet, in der LAN-Doku steht 0 ?!
            bytes[6] = 0;
            Log.Logger.ForContext("data", bytes).Information($"GET VERSION");
            Sending(bytes);
        }

        public void LogOFF()
        {
            byte[] bytes = new byte[4];
            bytes[0] = 0x04;
            bytes[1] = 0x00;
            bytes[2] = 0x30;
            bytes[3] = 0x00;
            Log.Logger.Information("LOG OFF", bytes);
            Sending(bytes);
        }

        public void LogOn()
        {
            var flags = BitConverter.GetBytes(0x00000001 | 0x00010000);
            byte[] bytes = new byte[8];
            bytes[0] = 0x08;
            bytes[1] = 0;
            bytes[2] = 0x50;
            bytes[3] = 0;
            bytes[4] = flags[0];
            bytes[5] = flags[1];
            bytes[6] = flags[2];
            bytes[7] = flags[3];
            Log.Logger.ForContext("data", bytes).Information($"SET BROADCASTFLAGS");
            Sending(bytes);
        }

        /// <summary>
        /// Pings the client. 
        /// </summary>
        /// <returns>Returns true if the client is reachable. False if an error occurs. </returns>
        public async Task<bool> PingAsync()
        {
            var ping = new System.Net.NetworkInformation.Ping();
            var result = await ping.SendPingAsync(Address);
            return result.Status == System.Net.NetworkInformation.IPStatus.Success;
        }

        private async void PingClient_Elapsed(object? sender, ElapsedEventArgs e)
        {
            PingClient.Enabled = false;
            try
            {
                ClientReachable = await PingAsync();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while pinging client.");
            }
            finally
            {
                PingClient.Enabled = true;
            }
        }

        public void SetLocoDrive(List<LokInfoData> data)
        {
            var array = new byte[10 * data.Count];
            for (int i = 0, currentIndex = 0; i < data.Count; i++, currentIndex += 10)
                Array.Copy(GetLocoDriveByteArray(data[i]), 0, array, currentIndex, 10);
            Sending(array);
        }

        public void SetLocoDrive(LokInfoData data) => Sending(GetLocoDriveByteArray(data));

        public void SetLocoFunction(FunctionData function) => Sending(GetLocoFunctionByteArray(function));

        public void SetLocoFunction(List<FunctionData> data)
        {
            var array = new byte[10 * data.Count];
            for (int i = 0, currentIndex = 0; i < data.Count; i++, currentIndex += 10)
            {
                var func = data[i];
                Array.Copy(GetLocoFunctionByteArray(func), 0, array, currentIndex, 10);
            }
            Sending(array);
        }

        public void SetStop()
        {
            byte[] bytes = new byte[6];
            bytes[0] = 0x06;
            bytes[1] = 0;
            bytes[2] = 0x40;
            bytes[3] = 0;
            bytes[4] = 0x80;
            bytes[5] = 0x80;
            Log.Logger.ForContext("data", bytes).Information($"SET STOP");
            Sending(bytes);
        }

        public void SetTrackPowerOFF()
        {
            byte[] bytes = new byte[7];
            bytes[0] = 0x07;
            bytes[1] = 0;
            bytes[2] = 0x40;
            bytes[3] = 0;
            bytes[4] = 0x21;
            bytes[5] = 0x80;
            bytes[6] = 0xA1;
            Sending(bytes);
            Log.Logger.ForContext("data", bytes).Information($"SET TRACK POWER OFF");
        }

        public void SetTrackPowerON()
        {
            byte[] bytes = new byte[7];
            bytes[0] = 0x07;
            bytes[1] = 0;
            bytes[2] = 0x40;
            bytes[3] = 0;
            bytes[4] = 0x21;
            bytes[5] = 0x81;
            bytes[6] = 0xA0;
            Log.Logger.ForContext("data", bytes).Information($"SET TRACK POWER ON");
            Sending(bytes);
        }

        public void SystemStateGetData()
        {
            byte[] bytes = new byte[4];
            bytes[0] = 0x04;
            bytes[1] = 0;
            bytes[2] = 0x85;
            bytes[3] = 0;
            Log.Logger.ForContext("data", bytes).Information($"SYSTEMSTATE GETDATA");
            Sending(bytes);
        }

        private TrackPower GetCentralStateData(byte[] received)
        {
            TrackPower state = TrackPower.ON;
            bool isEmergencyStop = (received[6] & 0x01) == 0x01;
            bool isTrackVoltageOff = (received[6] & 0x02) == 0x02;
            bool isShortCircuit = (received[6] & 0x04) == 0x04;
            bool isProgrammingModeActive = (received[6] & 0x20) == 0x20;
            if (isEmergencyStop || isTrackVoltageOff)
                state = TrackPower.OFF;
            else if (isShortCircuit)
                state = TrackPower.Short;
            else if (isProgrammingModeActive)
                state = TrackPower.Programing;
            Log.Logger.ForContext("data", received).Information($"Status changed to {state}");
            return state;
        }

        private byte[] GetLocoDriveByteArray(LokInfoData data)
        {
            if (data.DrivingDirection) data.Speed |= 0x080;
            byte[] bytes = new byte[10];
            bytes[0] = 0x0A;
            bytes[1] = 0;
            bytes[2] = 0x40;
            bytes[3] = 0;
            bytes[4] = 0xE4;
            bytes[5] = 0x13; //  = 128 Fahrstufen
            bytes[6] = data.Adresse.ValueBytes.Adr_MSB;
            bytes[7] = data.Adresse.ValueBytes.Adr_LSB;
            bytes[8] = (byte)data.Speed;
            bytes[9] = (byte)(bytes[4] ^ bytes[5] ^ bytes[6] ^ bytes[7] ^ bytes[8]);
            Log.Logger.ForContext("data", bytes).Information($"Set loco {data.Adresse.Value} drive to speed {data.Speed} with direction {data.DrivingDirection}");
            return bytes;
        }

        private byte[] GetLocoFunctionByteArray(FunctionData function)
        {
            byte[] bytes = new byte[10];
            bytes[0] = 0x0A;
            bytes[1] = 0;
            bytes[2] = 0x40;
            bytes[3] = 0;
            bytes[4] = 0xE4;
            bytes[5] = 0xF8;
            bytes[6] = function.LokAdresse.ValueBytes.Adr_MSB;
            bytes[7] = function.LokAdresse.ValueBytes.Adr_LSB;
            bytes[8] = (byte)function.FunctionAdress;

            var bitarray = new BitArray(new byte[] { bytes[8] });
            switch (function.ToggleType)
            {
                case ToggleType.Off:
                    break;
                case ToggleType.On:
                    bitarray.Set(6, true);
                    break;
                case ToggleType.Toggle:
                    bitarray.Set(7, true);
                    break;
            }
            bitarray.CopyTo(bytes, 8);
            bytes[9] = (byte)(bytes[4] ^ bytes[5] ^ bytes[6] ^ bytes[7] ^ bytes[8]);
            Log.Logger.ForContext("data", bytes).Information($"Set loco {function.LokAdresse.Value} function {function.FunctionAdress} to {function.ToggleType}");
            return bytes;
        }

        private SystemStateData GetSystemStateData(byte[] received)
        {
            SystemStateData statedata = new SystemStateData();
            statedata.MainCurrent = (received[4]) + received[5];
            statedata.ProgCurrent = (received[6]) + received[7];
            statedata.FilteredMainCurrent = (received[8]) + received[9];
            statedata.Temperature = (received[10]) + received[11];
            //VCCVoltage and SupplyVoltage are not read since their values do not make any sense! They jump from ~100 to nearly 300 at random.
            //statedata.SupplyVoltage = (received[12]) + received[13];
            //statedata.VCCVoltage =(received[14]) + received[15];

            statedata.ClientData.EmergencyStop = (received[16] & 0x01) == 0x01;
            statedata.ClientData.TrackVoltageOff = (received[16] & 0x02) == 0x02;
            statedata.ClientData.ShortCircuit = (received[16] & 0x04) == 0x04;
            statedata.ClientData.ProgrammingModeActive = (received[16] & 0x20) == 0x20;
            statedata.ClientData.HighTemperature = (received[17] & 0x01) == 0x01;
            statedata.ClientData.PowerLost = (received[17] & 0x02) == 0x02;
            statedata.ClientData.ShortCircuitExternal = (received[17] & 0x04) == 0x04;
            statedata.ClientData.ShortCircuitInternal = (received[17] & 0x08) == 0x08;
            return statedata;
        }

        private void CutTelegramm(byte[] bytes)
        {
            if (bytes == null) return;
            int z = 0;
            int max = bytes.GetLength(0);
            while (z < max)
            {
                int length = bytes[z];
                if (length > 3 & z + length <= max)
                {
                    byte[] einzelbytes = new byte[length];
                    Array.Copy(bytes, z, einzelbytes, 0, length);
                    Evaluation(einzelbytes);
                    z += length;
                }
                else
                {
                    z = max;
                    Log.Logger.Error($"Bad telegram", bytes);
                }
            }

        }

        private void Receiving(IAsyncResult res)
        {
            try
            {
                IPEndPoint RemoteIpEndPoint = null!;
                byte[] received = Z21Communicator.EndReceive(res, ref RemoteIpEndPoint!);
                Z21Communicator.BeginReceive(new AsyncCallback(Receiving), null);
                OnReceive?.Invoke(this, new DataEventArgs(received));
                Log.Logger.ForContext("data", received).Debug($"Received data from the z21: {BitConverter.ToString(received)}");
                CutTelegramm(received);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, $"Error while recieving data");
            }
        }

        private void Evaluation(byte[] received)
        {
            int i, j;

            switch (received[2])
            {
                case 0x1A:
                    Log.Logger.Information($"GET HWINFO ", received);
                    i = (received[7] << 24) + (received[6] << 16) + (received[5] << 8) + received[4];
                    j = (received[11] << 24) + (received[10] << 16) + (received[9] << 8) + received[8];
                    var hardwareTyp = i switch
                    {
                        0x00000200 => HardwareTyp.Z21Old,
                        0x00000201 => HardwareTyp.Z21New,
                        0x00000202 => HardwareTyp.SmartRail,
                        0x00000203 => HardwareTyp.z21Small,
                        _ => HardwareTyp.None,
                    };
                    OnGetHardwareInfo?.Invoke(this, new HardwareInfoEventArgs(new HardwareInfo(hardwareTyp, j)));
                    break;
                case 0x10:
                    Log.Logger.Information($"GET SERIAL NUMBER ", received);
                    i = (received[7] << 24) + (received[6] << 16) + (received[5] << 8) + received[4];
                    OnGetSerialNumber?.Invoke(this, new GetSerialNumberEventArgs(i));
                    break;
                case 0x18:
                    if (Enum.IsDefined(typeof(LanCode), received[4]))
                    {
                        var code = (LanCode)received[4];
                        if (code is LanCode.z21StartLocked)
                        {
                            Log.Logger.Warning($"GET LAN CODE '{code}'", received);
                        }
                        else
                        {
                            Log.Logger.Information($"GET LAN CODE '{code}'", received);
                        }
                        OnGetLanCode?.Invoke(this, code);
                    }
                    else
                    {
                        Log.Logger.Warning($"GET LAN CODE RECEIVED INVALID LAN CODE", received);
                    }
                    break;
                case 0x40:
                    switch (received[4])
                    {
                        case 0x61:
                            switch (received[5])
                            {
                                case 0x00:
                                    Log.Logger.Information($"TRACK POWER OFF ", received);
                                    TrackPowerChanged?.Invoke(this, new(TrackPower.OFF));
                                    break;
                                case 0x01:
                                    Log.Logger.Information($"TRACK POWER ON ", received);
                                    TrackPowerChanged?.Invoke(this, new(TrackPower.ON));
                                    break;
                                case 0x02:
                                    Log.Logger.Information($"PROGRAMMING MODE ", received);
                                    TrackPowerChanged?.Invoke(this, new(TrackPower.Programing));
                                    break;
                                case 0x08:
                                    Log.Logger.Information($"TRACK SHORT CIRCUIT ", received);
                                    TrackPowerChanged?.Invoke(this, new(TrackPower.Short));
                                    break;
                                default:
                                    Log.Logger.Warning($"Unbekanntes X-Bus-Telegramm Header 61", received);
                                    break;
                            }
                            break;
                        case 0x62:           //  LAN X STATUS CHANGED  2.12 (13)
                            OnStatusChanged?.Invoke(this, new StateEventArgs(GetCentralStateData(received)));
                            break;
                        case 0x63:
                            switch (received[5])
                            {
                                case 0x21:           //  LAN X GET VERSION  2.3 (10)
                                    Log.Logger.Information($"GET VERSION ", received);
                                    var versionTyp = received[7] switch
                                    {
                                        0x00 => VersionTyp.None,
                                        0x12 => VersionTyp.Z21,
                                        0x13 => VersionTyp.z21,// 0x13 ist keine gesicherte Erkenntnis aus dem LAN-Protokoll, wird aber von meiner z21 so praktiziert
                                        _ => VersionTyp.Other,
                                    };
                                    OnGetVersion?.Invoke(this, new VersionInfoEventArgs(new VersionInfo(received[6], versionTyp)));
                                    break;
                                default:
                                    Log.Logger.Warning($"Unbekanntes X-Bus-Telegramm Header 63", received);
                                    break;
                            }
                            break;

                        case 0x81:           //  LAN X BC STOPPED  2.14 (14)
                            Log.Logger.Information($"BC STOPPED ", received);
                            OnStopped?.Invoke(this, new EventArgs());
                            break;
                        case 0xEF:           //  LAN X LOCO INFO  4.4 (22)

                            ValueBytesStruct vbs = new()
                            {
                                Adr_MSB = received[5],
                                Adr_LSB = received[6]
                            };
                            LokInfoData infodata = new()
                            {
                                Adresse = new LokAdresse(vbs),
                                InUse = (received[7] & 8) == 8,
                                Speed = (byte)(received[8] & 0x7F),
                                DrivingDirection = (received[8] & 0x80) == 0x80
                            };
                            int functionAddressCount = 5;
                            for (int index = 9; index < received.Length && index <= 12; index++)
                            {
                                BitArray functionBits = new(new byte[] { received[index] });
                                if (index == 9)
                                {
                                    infodata.Functions.Add(new(0, Convert.ToBoolean(functionBits.Get(4))));
                                    infodata.Functions.Add(new(1, Convert.ToBoolean(functionBits.Get(0))));
                                    infodata.Functions.Add(new(2, Convert.ToBoolean(functionBits.Get(1))));
                                    infodata.Functions.Add(new(3, Convert.ToBoolean(functionBits.Get(2))));
                                    infodata.Functions.Add(new(4, Convert.ToBoolean(functionBits.Get(3))));
                                }
                                else
                                {
                                    for (int temp = 0; temp < 8; temp++)
                                    {
                                        infodata.Functions.Add(new(functionAddressCount, Convert.ToBoolean(functionBits.Get(temp))));
                                        functionAddressCount++;
                                    }
                                }
                            }
                            OnGetLocoInfo?.Invoke(this, new GetLocoInfoEventArgs(infodata));
                            Log.Logger.ForContext("data", received).Information($"Get loco {infodata.Adresse.Value} drive with speed {infodata.Speed} with direction {infodata.DrivingDirection}");
                            break;
                        case 0xF3:
                            switch (received[5])
                            {
                                case 0x0A:
                                    Log.Logger.Information($"GET FIRMWARE VERSION ", received);
                                    OnGetFirmwareVersion?.Invoke(this, new FirmwareVersionInfoEventArgs(new FirmwareVersionInfo(received[6], received[7])));
                                    // Achtung: die z21 bringt die Minor-Angabe hexadezimal !!!!!!!!    z.B. Firmware 1.23 = Minor 34
                                    break;
                                default:
                                    Log.Logger.Warning($"Unbekanntes X-Bus-Telegramm Header F3", received);
                                    break;
                            }
                            break;
                        default:
                            Log.Logger.Warning($"Unbekanntes X-Bus-Telegramm ", received);
                            break;
                    }
                    break;
                case 0x84:            // LAN SYSTEMSTATE DATACHANGED    2.18 (16)
                    Log.Logger.Debug($"LAN SYSTEMSTATE DATACHANGED ", received);
                    SystemStateData systemStateData = GetSystemStateData(received);
                    OnSystemStateDataChanged?.Invoke(this, new SystemStateEventArgs(systemStateData));
                    break;
                default:
                    Log.Logger.Warning($"Unknown telegram", received);
                    break;
            }
        }

        private async void Sending(byte[] bytes)
        {
            await Z21Communicator.SendAsync(bytes ?? [], bytes?.GetLength(0) ?? 0);
            Log.Logger.ForContext("data", bytes).Debug($"Sending data to the z21: {BitConverter.ToString(bytes ?? [])}");
        }
    }
}
