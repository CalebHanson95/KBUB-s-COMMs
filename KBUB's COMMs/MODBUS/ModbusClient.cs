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
namespace KBUBComm.MODBUS
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


     
        public void Disconnect()
        {
            _modbusMaster?.Dispose();
            _modbusMaster = null;
            isConnected = false;
        }
        public Dictionary<int, T> ReadRegisters<T>(ushort startAddress, ushort valueCount, bool isInput, out string errmsg) where T : struct
        {
            errmsg = null;
            Dictionary<int, T> dictionary = new Dictionary<int, T>();
            if (_modbusMaster == null)
            {
                errmsg = "Modbus connection not established.";
                return dictionary;
            }

            try
            {
                int num;
                if (!(typeof(T) == typeof(float)))
                {
                    if (!(typeof(T) == typeof(double)))
                    {
                        throw new NotSupportedException($"Type {typeof(T)} not supported");
                    }

                    num = 4;
                }
                else
                {
                    num = 2;
                }

                int num2 = num;
                ushort numberOfPoints = (ushort)(valueCount * num2);
                ushort[] source = (isInput ? _modbusMaster.ReadInputRegisters(startAddress, numberOfPoints) : _modbusMaster.ReadHoldingRegisters(startAddress, numberOfPoints));
                for (int i = 0; i < valueCount; i++)
                {
                    ushort[] array = source.Skip(i * num2).Take(num2).ToArray();
                    T value = default(T);
                    if (typeof(T) == typeof(float))
                    {
                        ushort num3 = array[0];
                        value = (T)(object)BitConverter.ToSingle(BitConverter.GetBytes((array[1] << 16) | num3), 0);
                    }
                    else if (typeof(T) == typeof(double))
                    {
                        byte[] array2 = new byte[8];
                        for (int j = 0; j < 4; j++)
                        {
                            array2[j * 2] = (byte)(array[j] & 0xFFu);
                            array2[j * 2 + 1] = (byte)(array[j] >> 8);
                        }

                        value = (T)(object)BitConverter.ToDouble(array2, 0);
                    }

                    dictionary.Add(startAddress + i * num2, value);
                }
            }
            catch (Exception ex)
            {
                errmsg = "Error reading registers: " + ex.Message;
            }

            return dictionary;
        }


    }
}

