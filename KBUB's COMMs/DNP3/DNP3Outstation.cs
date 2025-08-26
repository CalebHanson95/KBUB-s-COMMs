


using System;
using System.Collections.Generic;

using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using dnp3;
namespace KBUBComm.DNP3 { 
public class DNP3Outstation
{

    Runtime runtime;
    OutstationServer server;
    Outstation mainOutstation;
    AddressFilter filter;

    public delegate void log(string msg);
    public log onLog;

    void doLog(string msg)
    {
        onLog?.Invoke(msg);
    }

    public enum type { TCP, TLS };
    public type TYPE { get; private set; } = type.TCP;
    string PeerCertPath = "C:/Users/caleb.hanson/Desktop/RTAC KEYS/master_cert.pem";
    string LocalCertPath = "C:/Users/caleb.hanson/Desktop/RTAC KEYS/outstation_cert.pem";
    string LocalKeyPath = "C:/Users/caleb.hanson/Desktop/RTAC KEYS/outstation_key.pem";
    string CommonName = "DNP3RTAC";

    public string serverIP { get; private set; }
    public int serverPort { get; private set; }

    public string ipPort { get { return serverIP + ":" + serverPort; } }

    bool running;
    List<double> points = new List<double>();
    public status serverStatus { get; internal set; } = status.Not_Started;
    public enum status { Not_Started, Starting, Running, Stopped, Error }

    public DNP3Outstation(bool type, string peercert, string localcert, string localkey, string commonname)
    {
        TYPE = type ? DNP3Outstation.type.TCP : DNP3Outstation.type.TLS;

        PeerCertPath = peercert;
        LocalCertPath = localcert;
        LocalKeyPath = localkey;
        CommonName = commonname;

        runtime = new Runtime(new RuntimeConfig { NumCoreThreads = 4 });
    }
    public void setPoint(int index, double value)
    {
        if (index >= 0 && index < points.Count)
        {
            points[index] = value;
        }
        else doLog("Invalid DNP3 Index " + index);
    }

    public void defineCertInfo(string PeerCertPath, string LocalCertPath, string LocalKeyPath, string CommonName)
    {
        this.PeerCertPath = PeerCertPath;
        this.LocalCertPath = LocalCertPath;
        this.LocalKeyPath = LocalKeyPath;
        this.CommonName = CommonName;
    }

    private TlsServerConfig GetSelfSignedTlsConfig()
    {

        // ANCHOR: tls_self_signed_config
        doLog("Initializing TLS");
        var config = new TlsServerConfig(
            CommonName,
            PeerCertPath,
            LocalCertPath,
            LocalKeyPath,
            string.Empty
        ).WithCertificateMode(CertificateMode.AuthorityBased);

        // ANCHOR_END: tls_self_signed_config
        return config;
    }
    public void initializeServer(int numberOfPoints, type type, string serverIP = "0.0.0.0", int serverPort = 20000, ushort master = 3, ushort outstation = 4, params string[] whitelistedIPs)
    {

        if (serverStatus == status.Running) return;
        doLog("DNP3 Init started. Filter Status: " + (filter == null ? "NULL" : "Pending"));

        AddressFilter _filter = new AddressFilter("127.0.0.1");

        foreach (string ip in whitelistedIPs) { _filter.Add(ip); }
        filter = _filter;


        doLog("Adding DNP3 Points");
        points = Enumerable.Repeat<double>(0, numberOfPoints).ToList();


        this.serverIP = serverIP;
        this.serverPort = serverPort;

        serverStatus = status.Starting;
        var config = GetSelfSignedTlsConfig();
        TYPE = type;

        doLog("Starting DNP3 Server type of " + type);

        if (type == type.TLS)
            server = OutstationServer.CreateTlsServer(runtime, LinkErrorMode.Close, ipPort, config);
        if (type == type.TCP)
            server = OutstationServer.CreateTcpServer(runtime, LinkErrorMode.Close, ipPort);
        // ANCHOR: tcp_server_add_outstation

        onLog("Building Main Outstation");

        mainOutstation = server.AddOutstation(
            GetOutstationConfig(outstation, master),
            new TestOutstationApplication(),
            new TestOutstationInformation(),
            new TestControlHandler(),
            new TestConnectionStateListener(),
            filter
        );
        // ANCHOR_END: tcp_server_add_outstation

        // ANCHOR: tcp_server_bind
        if (server != null)
            server.Bind();
        else doLog("Null DNP3 Server");

        // ANCHOR_END: tcp_server_bind

        running = true;
        if (mainOutstation != null && points != null)
        {
            Task.Run(() => runOutstationAsync(mainOutstation, numberOfPoints));
            doLog("DNP3 Server is running on port " + ipPort);
        }
        else
        {
            doLog($"Null DNP3 settings: mo={(mainOutstation == null ? "NULL" : "Present")} po={(points == null ? "NULL" : "Present")}");
        }


    }


    async Task runOutstationAsync(Outstation outstation, int pointCount)
    {
        await RunOutstation(outstation, pointCount);
    }
    private async Task RunOutstation(Outstation outstation, int pointCount)
    {
        // Setup initial points
        // ANCHOR: database_init
        outstation.Transaction(db =>
        {


            // define device attributes made available to the master
            db.DefineStringAttr(0, false, AttributeVariations.DeviceManufacturersName, "Nor-Cal Controls ES");
            db.DefineStringAttr(0, true, AttributeVariations.UserAssignedLocation, "El Dorado Hills, CA");
            AnalogInputConfig aC = new AnalogInputConfig();
            aC.StaticVariation = StaticAnalogInputVariation.Group30Var6;
            for (ushort i = 0; i < pointCount; i++)
            {
                if (!db.AddAnalogInput(i, EventClass.Class1, aC))
                {
                    doLog("Failed to add Analog Input #" + i);
                }

            }

        }
        );

        // ANCHOR_END: database_init
        outstation.Enable();

        var onlineFlags = new Flags(Flag.Online);

        var detectEvent = UpdateOptions.DetectEvent();
        var syncedNOW = Now();

        while (running)
        {
            try
            {
                serverStatus = status.Running;


                outstation.Transaction(db =>
                {
                    for (ushort i = 0; i < pointCount; i++)
                    {


                        // Update analog input with value and quality flags
                        if (!db.UpdateAnalogInput(new AnalogInput(i, points[i], onlineFlags, syncedNOW), detectEvent))
                        {
                            doLog("Failed to update Analog Input #" + i);
                        }

                    }
                });



                await Task.Delay(100);

            }
            catch (Exception ex)
            {
                serverStatus = status.Error;
            }
        }

        serverStatus = status.Stopped;
    }

    class TestOutstationApplication : IOutstationApplication
    {
        public ushort GetProcessingDelayMs()
        {
            return 0;
        }

        public WriteTimeResult WriteAbsoluteTime(ulong time)
        {
            return WriteTimeResult.NotSupported;
        }

        public ApplicationIin GetApplicationIin()
        {
            return new ApplicationIin();
        }

        public RestartDelay ColdRestart()
        {
            return RestartDelay.Seconds(60);
        }

        public RestartDelay WarmRestart()
        {
            return RestartDelay.NotSupported();
        }

        FreezeResult IOutstationApplication.FreezeCountersAll(FreezeType freezeType, DatabaseHandle database) { return FreezeResult.NotSupported; }

        FreezeResult IOutstationApplication.FreezeCountersRange(ushort start, ushort stop, FreezeType freezeType, DatabaseHandle database) { return FreezeResult.NotSupported; }

        FreezeResult IOutstationApplication.FreezeCountersAllAtTime(DatabaseHandle databaseHandle, ulong time, uint interval)
        {
            return FreezeResult.NotSupported;
        }

        FreezeResult IOutstationApplication.FreezeCountersRangeAtTime(ushort start, ushort stop, DatabaseHandle databaseHandle, ulong time, uint interval)
        {
            return FreezeResult.NotSupported;
        }

        bool IOutstationApplication.SupportWriteAnalogDeadBands()
        {
            return false;
        }

        void IOutstationApplication.BeginWriteAnalogDeadBands() { }

        void IOutstationApplication.WriteAnalogDeadBand(ushort index, double deadBand) { }

        void IOutstationApplication.EndWriteAnalogDeadBands() { }

        bool IOutstationApplication.WriteStringAttr(byte set, byte variation, StringAttr attrType, string value)
        {
            // Allow writing any string attributes that have been defined as writable
            return true;
        }

        bool IOutstationApplication.WriteFloatAttr(byte set, byte variation, FloatAttr attrType, float value)
        {
            return false;
        }

        bool IOutstationApplication.WriteDoubleAttr(byte set, byte variation, FloatAttr attrType, double value)
        {
            return false;
        }

        bool IOutstationApplication.WriteUintAttr(byte set, byte variation, UintAttr attrType, uint value)
        {
            return false;
        }

        bool IOutstationApplication.WriteIntAttr(byte set, byte variation, IntAttr attrType, int value)
        {
            return false;
        }

        bool IOutstationApplication.WriteOctetStringAttr(byte set, byte variation, OctetStringAttr attrType, ICollection<byte> value)
        {
            return false;
        }

        bool IOutstationApplication.WriteBitStringAttr(byte set, byte variation, BitStringAttr attrType, ICollection<byte> value)
        {
            return false;
        }

        bool IOutstationApplication.WriteTimeAttr(byte set, byte variation, TimeAttr attrType, ulong value)
        {
            return false;
        }

        void IOutstationApplication.BeginConfirm()
        {

        }
        void IOutstationApplication.EventCleared(ulong id)
        {

        }

        void IOutstationApplication.EndConfirm(BufferState state)
        {

        }
    }
    class TestOutstationInformation : IOutstationInformation
    {
        public void ProcessRequestFromIdle(RequestHeader header) { }

        public void BroadcastReceived(FunctionCode functionCode, BroadcastAction action)
        {

        }

        public void EnterSolicitedConfirmWait(byte ecsn) { }

        public void SolicitedConfirmTimeout(byte ecsn) { }

        public void SolicitedConfirmReceived(byte ecsn) { }

        public void SolicitedConfirmWaitNewRequest() { }

        public void WrongSolicitedConfirmSeq(byte ecsn, byte seq) { }

        public void UnexpectedConfirm(bool unsolicited, byte seq) { }

        public void EnterUnsolicitedConfirmWait(byte ecsn) { }

        public void UnsolicitedConfirmTimeout(byte ecsn, bool retry) { }

        public void UnsolicitedConfirmed(byte ecsn) { }

        public void ClearRestartIin() { }
    }

    // ANCHOR: control_handler
    class TestControlHandler : IControlHandler
    {
        public void BeginFragment() { }

        public void EndFragment(DatabaseHandle database) { }

        public CommandStatus SelectG12v1(Group12Var1 control, ushort index, DatabaseHandle database)
        {
            if (index < 10 && (control.Code.OpType == OpType.LatchOn || control.Code.OpType == OpType.LatchOff))
            {
                return CommandStatus.Success;
            }
            else
            {
                return CommandStatus.NotSupported;
            }
        }

        public CommandStatus OperateG12v1(Group12Var1 control, ushort index, OperateType opType, DatabaseHandle database)
        {
            if (index < 10 && (control.Code.OpType == OpType.LatchOn || control.Code.OpType == OpType.LatchOff))
            {
                var status = (control.Code.OpType == OpType.LatchOn);
                database.Transaction(db =>
                    db.UpdateBinaryOutputStatus(new BinaryOutputStatus(index, status, new Flags(Flag.Online), Now()), UpdateOptions.DetectEvent())
                );
                return CommandStatus.Success;
            }
            else
            {
                return CommandStatus.NotSupported;
            }
        }

        public CommandStatus SelectG41v1(int value, ushort index, DatabaseHandle database)
        {
            return SelectAnalogOutput(index);
        }

        public CommandStatus OperateG41v1(int value, ushort index, OperateType opType, DatabaseHandle database)
        {
            return OperateAnalogOutput(value, index, database);
        }

        public CommandStatus SelectG41v2(short value, ushort index, DatabaseHandle database)
        {
            return SelectAnalogOutput(index);
        }

        public CommandStatus OperateG41v2(short value, ushort index, OperateType opType, DatabaseHandle database)
        {
            return OperateAnalogOutput(value, index, database);
        }

        public CommandStatus SelectG41v3(float value, ushort index, DatabaseHandle database)
        {
            return SelectAnalogOutput(index);
        }

        public CommandStatus OperateG41v3(float value, ushort index, OperateType opType, DatabaseHandle database)
        {
            return OperateAnalogOutput(value, index, database);
        }

        public CommandStatus SelectG41v4(double value, ushort index, DatabaseHandle database)
        {
            return SelectAnalogOutput(index);
        }

        public CommandStatus OperateG41v4(double value, ushort index, OperateType opType, DatabaseHandle database)
        {
            return OperateAnalogOutput(value, index, database);
        }

        private CommandStatus SelectAnalogOutput(ushort index)
        {
            return index < 10 ? CommandStatus.Success : CommandStatus.NotSupported;
        }

        private CommandStatus OperateAnalogOutput(double value, ushort index, DatabaseHandle database)
        {
            if (index < 10)
            {
                database.Transaction(db =>
                    db.UpdateAnalogOutputStatus(new AnalogOutputStatus(index, value, new Flags(Flag.Online), Now()), UpdateOptions.DetectEvent())
                );
                return CommandStatus.Success;
            }
            else
            {
                return CommandStatus.NotSupported;
            }
        }
    }
    // ANCHOR_END: control_handler

    class TestConnectionStateListener : IConnectionStateListener
    {
        public void OnChange(ConnectionState state)
        {

        }
    }

    private static OutstationConfig GetOutstationConfig(ushort outstation = 4, ushort master = 3)
    {
        // ANCHOR: outstation_config
        // create an outstation configuration with default values
        var config = new OutstationConfig(
            // outstation address
            outstation,
            // master address
            master,
            // event buffer sizes
            GetEventBufferConfig()
        ).WithDecodeLevel(DecodeLevel.Nothing().WithApplication(AppDecodeLevel.ObjectValues));
        // ANCHOR_END: outstation_config

        return config;
    }
    private static EventBufferConfig GetEventBufferConfig()
    {
        return new EventBufferConfig(
            10, // binary
            10, // double-bit binary
            10, // binary output status
            5,  // counter
            5,  // frozen counter
            5,  // analog
            5,  // analog output status
            3   // octet string
        );
    }

    private static Timestamp Now()
    {
        return Timestamp.SynchronizedTimestamp((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    internal void Shutdown()
    {

        running = false;
        server.Shutdown();

        runtime.Shutdown();
    }


}


}