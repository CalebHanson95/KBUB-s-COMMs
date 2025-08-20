using Modbus.Device;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Modbus.Data;
using Modbus.Device;
using Modbus.Utility;
namespace KBUBComm
{
    public class ModbusClient
    {
        private ModbusIpMaster _modbusMaster;
        public bool isConnected { private set; get; }


        public ModbusClient()
        {
            _modbusMaster = null;
        }

        public bool Connect(string ipAddress, int port)
        {
            try
            {
                _modbusMaster = ModbusIpMaster.CreateIp(new TcpClient(ipAddress, port));
                _modbusMaster.Transport.ReadTimeout = 1000; // Set a timeout value as needed

                isConnected = true;

                return true;
            }
            catch
            {

                isConnected = false;
                return false;
            }
        }


        public class ams
        {
            public int addressoffset = 0;
            public int samplelength = 2;
            public int bitshift = 16;
            public bool endianness = false; //big = false, little = true
            public int byteoffset = 0;
            public int samplereadstart = 0;
            public static ams get()
            {
                return new ams();
            }
        }
        public void Disconnect()
        {
            _modbusMaster?.Dispose();
            _modbusMaster = null;
            isConnected = false;
        }
        public Dictionary<int, T> ReadRegisters<T>(ushort startAddress, ushort valueCount, bool isInput, out string errmsg) where T : struct
        {
            errmsg = null;
            var result = new Dictionary<int, T>();

            if (_modbusMaster == null)
            {
                errmsg = "Modbus connection not established.";
                return result;
            }

            try
            {
                int registersPerValue = typeof(T) == typeof(float) ? 2 :
                                        typeof(T) == typeof(double) ? 4 :
                                        throw new NotSupportedException($"Type {typeof(T)} not supported");

                ushort numRegisters = (ushort)(valueCount * registersPerValue);
                ushort[] registers = isInput
                    ? _modbusMaster.ReadInputRegisters(startAddress, numRegisters)
                    : _modbusMaster.ReadHoldingRegisters(startAddress, numRegisters);

                for (int i = 0; i < valueCount; i++)
                {
                    ushort[] slice = registers.Skip(i * registersPerValue).Take(registersPerValue).ToArray();
                    T value = default;

                    if (typeof(T) == typeof(float))
                    {
                        byte[] bytes = new byte[4];
                        bytes[0] = (byte)(slice[1] >> 8);   // high byte of high register
                        bytes[1] = (byte)(slice[1] & 0xFF); // low byte of high register
                        bytes[2] = (byte)(slice[0] >> 8);   // high byte of low register
                        bytes[3] = (byte)(slice[0] & 0xFF); // low byte of low register
                        value = (T)(object)BitConverter.ToSingle(bytes, 0);
                    }
                    else if (typeof(T) == typeof(double))
                    {
                        byte[] bytes = new byte[8];
                        for (int j = 0; j < 4; j++)
                        {
                            bytes[j * 2 + 0] = (byte)(slice[j] >> 8);   // high byte
                            bytes[j * 2 + 1] = (byte)(slice[j] & 0xFF); // low byte
                        }
                        value = (T)(object)BitConverter.ToDouble(bytes, 0);
                    }

                    result.Add(startAddress + i * registersPerValue, value);
                }
            }
            catch (Exception ex)
            {
                errmsg = "Error reading registers: " + ex.Message;
            }

            return result;
        }


    }
}

