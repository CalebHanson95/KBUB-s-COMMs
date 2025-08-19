using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using Modbus.Device;

namespace KBUBComm
{
    public class ModbusClient
    {
        private ModbusIpMaster _modbusMaster;
        public bool IsConnected { get; private set; }

        public ModbusClient() { }

        public bool Connect(string ipAddress, int port)
        {
            try
            {
                _modbusMaster = ModbusIpMaster.CreateIp(new TcpClient(ipAddress, port));
                _modbusMaster.Transport.ReadTimeout = 1000;
                IsConnected = true;
                return true;
            }
            catch
            {
                IsConnected = false;
                return false;
            }
        }

        public void Disconnect()
        {
            _modbusMaster?.Dispose();
            _modbusMaster = null;
            IsConnected = false;
        }

        public Dictionary<ushort, T> ReadRegisters<T>(ushort startAddress, ushort valueCount, bool isInput, out string errmsg)
            where T : struct
        {
            errmsg = null;
            var result = new Dictionary<ushort, T>();

            if (_modbusMaster == null)
            {
                errmsg = "Modbus connection not established.";
                return result;
            }

            try
            {
                int registersPerValue = typeof(T) == typeof(float) ? 2 :
                                        typeof(T) == typeof(double) ? 4 :
                                        throw new NotSupportedException($"Type {typeof(T)} not supported.");

                ushort numRegisters = (ushort)(valueCount * registersPerValue);

                ushort[] registers = isInput
                    ? _modbusMaster.ReadInputRegisters(startAddress, numRegisters)
                    : _modbusMaster.ReadHoldingRegisters(startAddress, numRegisters);

                for (int i = 0; i < valueCount; i++)
                {
                    ushort[] slice = registers.Skip(i * registersPerValue).Take(registersPerValue).ToArray();
                    T value;

                    if (typeof(T) == typeof(float))
                    {
                        // Convert 2 registers (big-endian) to float
                        byte[] bytes = new byte[4];
                        bytes[0] = (byte)(slice[0] >> 8);
                        bytes[1] = (byte)(slice[0] & 0xFF);
                        bytes[2] = (byte)(slice[1] >> 8);
                        bytes[3] = (byte)(slice[1] & 0xFF);
                        Array.Reverse(bytes); // back to little-endian
                        value = (T)(object)BitConverter.ToSingle(bytes, 0);
                    }
                    else
                    {
                        // Convert 4 registers (big-endian) to double
                        byte[] bytes = new byte[8];
                        for (int j = 0; j < 4; j++)
                        {
                            bytes[j * 2] = (byte)(slice[j] >> 8);
                            bytes[j * 2 + 1] = (byte)(slice[j] & 0xFF);
                        }
                        Array.Reverse(bytes);
                        value = (T)(object)BitConverter.ToDouble(bytes, 0);
                    }

                    result.Add((ushort)(startAddress + i * registersPerValue), value);
                }
            }
            catch (Exception ex)
            {
                errmsg = "Error reading registers: " + ex.Message;
            }

            return result;
        }

        public void WriteRegister<T>(ushort address, T value)
            where T : struct
        {
            if (_modbusMaster == null) throw new InvalidOperationException("Modbus connection not established.");

            if (typeof(T) == typeof(float))
            {
                byte[] bytes = BitConverter.GetBytes((float)(object)value);
                Array.Reverse(bytes); // big-endian
                ushort[] regs =
                {
                    (ushort)((bytes[0] << 8) | bytes[1]),
                    (ushort)((bytes[2] << 8) | bytes[3])
                };
                _modbusMaster.WriteMultipleRegisters(address, regs);
            }
            else if (typeof(T) == typeof(double))
            {
                byte[] bytes = BitConverter.GetBytes((double)(object)value);
                Array.Reverse(bytes); // big-endian
                ushort[] regs =
                {
                    (ushort)((bytes[0] << 8) | bytes[1]),
                    (ushort)((bytes[2] << 8) | bytes[3]),
                    (ushort)((bytes[4] << 8) | bytes[5]),
                    (ushort)((bytes[6] << 8) | bytes[7])
                };
                _modbusMaster.WriteMultipleRegisters(address, regs);
            }
            else
            {
                throw new NotSupportedException($"Type {typeof(T)} not supported.");
            }
        }
    }
}
