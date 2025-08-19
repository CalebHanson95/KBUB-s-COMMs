using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Modbus.Device;
using Modbus.Data;
using Modbus.Utility;
namespace KBUBComm
{
    public class ModbusServer
    {
        private TcpListener tcpListener;
        private ModbusTcpSlave modbusSlave;




        public event Action<ushort, float> OnFloatWritten;

        public ModbusServer()
        {


        }
        internal int t = 1;
        public void newSlave(int port)
        {
            tcpListener = new TcpListener(IPAddress.Any, port);
            modbusSlave = ModbusTcpSlave.CreateTcp(((byte)t), tcpListener);

            modbusSlave.DataStore = DataStoreFactory.CreateDefaultDataStore();

            modbusSlave.DataStore.DataStoreWrittenTo += OnDataWritten;
        }

        public void Listen()
        {

            tcpListener.Start();


            modbusSlave.Listen();


        }

        public void StopListening()
        {

            tcpListener.Stop();



        }

        public T ReadFromHoldingRegister<T>(ushort address)
        {
            if (typeof(T) == typeof(float))
            {
                // call your float version
                object value = ReadFromHoldingRegister_AsFloat(address);
                return (T)value;
            }
            else if (typeof(T) == typeof(double))
            {
                object value = ReadFromHoldingRegister_AsDouble(address);
                return (T)value;
            }
            else
            {
                throw new NotSupportedException($"Type {typeof(T)} not supported.");
            }
        }

        public T ReadFromInputRegister<T>(ushort address)
        {
            if (typeof(T) == typeof(float))
            {
                // call your float version
                object value = ReadFromHoldingRegister_AsFloat(address);
                return (T)value;
            }
            else if (typeof(T) == typeof(double))
            {
                object value = ReadFromHoldingRegister_AsDouble(address);
                return (T)value;
            }
            else
            {
                throw new NotSupportedException($"Type {typeof(T)} not supported.");
            }
        }
        // Float-only Modbus server helpers
        public void WriteToHoldingRegister(ushort address, float value)
        {
            ushort[] regs = FloatToRegisters(value);
            modbusSlave.DataStore.HoldingRegisters[address] = regs[0];
            modbusSlave.DataStore.HoldingRegisters[address + 1] = regs[1];

        }

        public void WriteToInputRegister(ushort address, float value)
        {
            ushort[] regs = FloatToRegisters(value);
            modbusSlave.DataStore.InputRegisters[address] = regs[0];
            modbusSlave.DataStore.InputRegisters[address + 1] = regs[1];
        }

        public float ReadFromHoldingRegister_AsFloat(ushort address)
        {
            ushort high = modbusSlave.DataStore.HoldingRegisters[address];
            ushort low = modbusSlave.DataStore.HoldingRegisters[address + 1];
            return RegistersToFloat(high, low);
        }

        public float ReadFromInputRegister_AsFloat(ushort address)
        {
            ushort high = modbusSlave.DataStore.InputRegisters[address];
            ushort low = modbusSlave.DataStore.InputRegisters[address + 1];
            return RegistersToFloat(high, low);
        }

        private static ushort[] FloatToRegisters(float value)
        {
            byte[] bytes = BitConverter.GetBytes(value); // little-endian on Windows
            Array.Reverse(bytes); // big-endian for Modbus
            return new ushort[]
            {
        (ushort)((bytes[0] << 8) | bytes[1]),
        (ushort)((bytes[2] << 8) | bytes[3])
            };
        }

        private static float RegistersToFloat(ushort high, ushort low)
        {
            byte[] bytes = new byte[4];
            bytes[0] = (byte)(high >> 8);
            bytes[1] = (byte)(high & 0xFF);
            bytes[2] = (byte)(low >> 8);
            bytes[3] = (byte)(low & 0xFF);
            Array.Reverse(bytes); // back to little-endian for BitConverter
            return BitConverter.ToSingle(bytes, 0);
        }

        // Double-only Modbus server helpers
        public void WriteToHoldingRegister(ushort address, double value)
        {
            ushort[] regs = DoubleToRegisters(value);
            modbusSlave.DataStore.HoldingRegisters[address] = regs[0];
            modbusSlave.DataStore.HoldingRegisters[address + 1] = regs[1];
            modbusSlave.DataStore.HoldingRegisters[address + 2] = regs[2];
            modbusSlave.DataStore.HoldingRegisters[address + 3] = regs[3];
        }

        public void WriteToInputRegister(ushort address, double value)
        {
            ushort[] regs = DoubleToRegisters(value);
            modbusSlave.DataStore.InputRegisters[address] = regs[0];
            modbusSlave.DataStore.InputRegisters[address + 1] = regs[1];
            modbusSlave.DataStore.InputRegisters[address + 2] = regs[2];
            modbusSlave.DataStore.InputRegisters[address + 3] = regs[3];
        }

        public double ReadFromHoldingRegister_AsDouble(ushort address)
        {
            ushort r0 = modbusSlave.DataStore.HoldingRegisters[address];
            ushort r1 = modbusSlave.DataStore.HoldingRegisters[address + 1];
            ushort r2 = modbusSlave.DataStore.HoldingRegisters[address + 2];
            ushort r3 = modbusSlave.DataStore.HoldingRegisters[address + 3];
            return RegistersToDouble(r0, r1, r2, r3);
        }

        public double ReadFromInputRegister_AsDouble(ushort address)
        {
            ushort r0 = modbusSlave.DataStore.InputRegisters[address];
            ushort r1 = modbusSlave.DataStore.InputRegisters[address + 1];
            ushort r2 = modbusSlave.DataStore.InputRegisters[address + 2];
            ushort r3 = modbusSlave.DataStore.InputRegisters[address + 3];
            return RegistersToDouble(r0, r1, r2, r3);
        }

        private static ushort[] DoubleToRegisters(double value)
        {
            byte[] bytes = BitConverter.GetBytes(value); // little-endian on Windows
            Array.Reverse(bytes); // convert to big-endian for Modbus
            return new ushort[]
            {
        (ushort)((bytes[0] << 8) | bytes[1]),
        (ushort)((bytes[2] << 8) | bytes[3]),
        (ushort)((bytes[4] << 8) | bytes[5]),
        (ushort)((bytes[6] << 8) | bytes[7])
            };
        }

        private static double RegistersToDouble(ushort r0, ushort r1, ushort r2, ushort r3)
        {
            byte[] bytes = new byte[8];
            bytes[0] = (byte)(r0 >> 8);
            bytes[1] = (byte)(r0 & 0xFF);
            bytes[2] = (byte)(r1 >> 8);
            bytes[3] = (byte)(r1 & 0xFF);
            bytes[4] = (byte)(r2 >> 8);
            bytes[5] = (byte)(r2 & 0xFF);
            bytes[6] = (byte)(r3 >> 8);
            bytes[7] = (byte)(r3 & 0xFF);
            Array.Reverse(bytes); // back to little-endian for BitConverter
            return BitConverter.ToDouble(bytes, 0);
        }


        private void OnDataWritten(object sender, DataStoreEventArgs e)
        {
            // Only handle holding register writes
            if (e.ModbusDataType != ModbusDataType.HoldingRegister)
                return;

            for (int i = 0; i < e.Data.B.Count; i++)
            {
                ushort address = (ushort)(e.StartAddress + i);
                ushort value = e.Data.B[i];


                var store = modbusSlave.DataStore.HoldingRegisters;
                if (address + 1 < store.Count)
                {
                    try
                    {
                        ushort low = store[address];
                        ushort high = store[(ushort)(address + 1)];
                        float floatValue = ModbusUtility.GetSingle(high, low);

                        OnFloatWritten?.Invoke(address, floatValue);
                    }
                    catch
                    {

                    }
                }

            }
        }
    }
}
