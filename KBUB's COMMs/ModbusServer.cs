using System;
using System.Net;
using System.Net.Sockets;
using Modbus.Device;
using Modbus.Data;
using Modbus.Utility;

namespace KBUBComm
{
    public class ModbusServer
    {
        private TcpListener tcpListener;
        private ModbusTcpSlave modbusSlave;
        internal int slaveId = 1;

        public ModbusServer() { }

        public void CreateSlave(int port)
        {
            tcpListener = new TcpListener(IPAddress.Any, port);
            modbusSlave = ModbusTcpSlave.CreateTcp((byte)slaveId, tcpListener);
            modbusSlave.DataStore = DataStoreFactory.CreateDefaultDataStore();
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

        #region Read/Write Registers

        // Generic read
        public T ReadHoldingRegister<T>(ushort address) where T : struct
        {
            if (typeof(T) == typeof(float)) return (T)(object)ReadHoldingRegister_AsFloat(address);
            if (typeof(T) == typeof(double)) return (T)(object)ReadHoldingRegister_AsDouble(address);
            throw new NotSupportedException($"Type {typeof(T)} not supported.");
        }

        public T ReadInputRegister<T>(ushort address) where T : struct
        {
            if (typeof(T) == typeof(float)) return (T)(object)ReadInputRegister_AsFloat(address);
            if (typeof(T) == typeof(double)) return (T)(object)ReadInputRegister_AsDouble(address);
            throw new NotSupportedException($"Type {typeof(T)} not supported.");
        }

        public void WriteHoldingRegister(ushort address, float value)
        {
            ushort[] regs = FloatToRegisters(value);
            modbusSlave.DataStore.HoldingRegisters[address] = regs[0];
            modbusSlave.DataStore.HoldingRegisters[address + 1] = regs[1];
        }

        public void WriteHoldingRegister(ushort address, double value)
        {
            ushort[] regs = DoubleToRegisters(value);
            for (int i = 0; i < 4; i++)
                modbusSlave.DataStore.HoldingRegisters[address + i] = regs[i];
        }

        public void WriteInputRegister(ushort address, float value)
        {
            ushort[] regs = FloatToRegisters(value);
            modbusSlave.DataStore.InputRegisters[address] = regs[0];
            modbusSlave.DataStore.InputRegisters[address + 1] = regs[1];
        }

        public void WriteInputRegister(ushort address, double value)
        {
            ushort[] regs = DoubleToRegisters(value);
            for (int i = 0; i < 4; i++)
                modbusSlave.DataStore.InputRegisters[address + i] = regs[i];
        }

        #endregion

        #region Float/Double Helpers

        private static ushort[] FloatToRegisters(float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes); // big-endian for Modbus
            return new ushort[]
            {
                (ushort)((bytes[0] << 8) | bytes[1]),
                (ushort)((bytes[2] << 8) | bytes[3])
            };
        }

        private static float RegistersToFloat(ushort high, ushort low)
        {
            byte[] bytes = new byte[4]
            {
                (byte)(high >> 8), (byte)(high & 0xFF),
                (byte)(low >> 8),  (byte)(low & 0xFF)
            };
            Array.Reverse(bytes); // back to little-endian
            return BitConverter.ToSingle(bytes, 0);
        }

        private static ushort[] DoubleToRegisters(double value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes); // big-endian for Modbus
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
            byte[] bytes = new byte[8]
            {
                (byte)(r0 >> 8), (byte)(r0 & 0xFF),
                (byte)(r1 >> 8), (byte)(r1 & 0xFF),
                (byte)(r2 >> 8), (byte)(r2 & 0xFF),
                (byte)(r3 >> 8), (byte)(r3 & 0xFF)
            };
            Array.Reverse(bytes);
            return BitConverter.ToDouble(bytes, 0);
        }

        private float ReadHoldingRegister_AsFloat(ushort address)
        {
            ushort high = modbusSlave.DataStore.HoldingRegisters[address];
            ushort low = modbusSlave.DataStore.HoldingRegisters[address + 1];
            return RegistersToFloat(high, low);
        }

        private float ReadInputRegister_AsFloat(ushort address)
        {
            ushort high = modbusSlave.DataStore.InputRegisters[address];
            ushort low = modbusSlave.DataStore.InputRegisters[address + 1];
            return RegistersToFloat(high, low);
        }

        private double ReadHoldingRegister_AsDouble(ushort address)
        {
            ushort r0 = modbusSlave.DataStore.HoldingRegisters[address];
            ushort r1 = modbusSlave.DataStore.HoldingRegisters[address + 1];
            ushort r2 = modbusSlave.DataStore.HoldingRegisters[address + 2];
            ushort r3 = modbusSlave.DataStore.HoldingRegisters[address + 3];
            return RegistersToDouble(r0, r1, r2, r3);
        }

        private double ReadInputRegister_AsDouble(ushort address)
        {
            ushort r0 = modbusSlave.DataStore.InputRegisters[address];
            ushort r1 = modbusSlave.DataStore.InputRegisters[address + 1];
            ushort r2 = modbusSlave.DataStore.InputRegisters[address + 2];
            ushort r3 = modbusSlave.DataStore.InputRegisters[address + 3];
            return RegistersToDouble(r0, r1, r2, r3);
        }

        #endregion
    }
}
