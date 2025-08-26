using dnp3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KBUBComm.DNP3
{
    public class DNP3Client
    {
        // ANCHOR: logging_interface
        // callback interface used to receive log messages


        private Runtime _runtime;
        private MasterChannel _masterChannel;
        private string _endpoint;

        string PeerCertPath = "C:/Users/caleb.hanson/Desktop/RTAC KEYS/master_cert.pem";
        string LocalCertPath = "C:/Users/caleb.hanson/Desktop/RTAC KEYS/outstation_cert.pem";
        string LocalKeyPath = "C:/Users/caleb.hanson/Desktop/RTAC KEYS/outstation_key.pem";
        string CommonName = "DNP3RTAC";

        enum type { TCP, TLS }
        type TYPE = type.TLS;

        // Constructor to initialize the necessary components
        public DNP3Client(string endpoint, bool type, string peercert, string localcert, string localkey, string commonname)
        {
            // Initialize runtime
            _runtime = new Runtime(new RuntimeConfig() { NumCoreThreads = 4 });

            TYPE = type ? DNP3Client.type.TLS : DNP3Client.type.TCP;
            PeerCertPath = peercert;
            LocalCertPath = localcert;
            LocalKeyPath = localkey;
            CommonName = commonname;
            // Store the endpoint address
            _endpoint = endpoint;

            // Set up the necessary listeners and configurations
            InitializeChannel();
        }

        // Initialize MasterChannel with TCP configuration
        private void InitializeChannel()
        {
            var channelConfig = GetMasterChannelConfig();

            if (TYPE == type.TCP)
            {
                _masterChannel = MasterChannel.CreateTcpChannel(
                    _runtime,
                    LinkErrorMode.Close,
                    channelConfig,
                    new EndpointList(_endpoint),
                    new ConnectStrategy(),
                    new TestClientStateListener()
                );
            }
            if (TYPE == type.TLS)
            {
                TlsClientConfig TLSConfig = new TlsClientConfig(CommonName, PeerCertPath, LocalCertPath, LocalKeyPath, string.Empty);

                _masterChannel = MasterChannel.CreateTlsChannel(
                    _runtime,
                    LinkErrorMode.Close,
                    channelConfig,
                    new EndpointList(_endpoint),
                    new ConnectStrategy(),
                    new TestClientStateListener(),
                    TLSConfig
                    );
            }
        }

        // 1. Method to start the TCP connection
        TestReadHandler trh = new TestReadHandler();
        public void StartTcpConnection()
        {
            Trace.WriteLine("Starting TCP Connection");

            // Create a master request for Class 1, Class 2, and Class 3 analog inputs
            Request requestClass1 = new Request();
            requestClass1.AddAllObjectsHeader(Variation.Group30Var6);


            // Set up the association for the communication (master -> outstation)
            var association = _masterChannel.AddAssociation(
                4,
                GetAssociationConfig(),
                trh,
                new TestAssociationHandler(),
                new TestAssociationInformation()
            );

            // Add polls for each request (polling Class 1, Class 2, and Class 3 analog inputs)
            var pollClass1 = _masterChannel.AddPoll(association, requestClass1, TimeSpan.FromSeconds(1));

            // Enable the master channel for communication
            _masterChannel.Enable();

        }


        // 2. Method to configure and start the TLS connection
        public void StartTlsConnection()
        {
            Trace.WriteLine("Starting TLS Connection");

            // Create a master request for Class 1, Class 2, and Class 3 analog inputs
            Request requestClass1 = new Request();
            requestClass1.AddAllObjectsHeader(Variation.Group30Var6);


            // Set up the association for the communication (master -> outstation)
            var association = _masterChannel.AddAssociation(
                4,
                GetAssociationConfig(),
                trh,
                new TestAssociationHandler(),
                new TestAssociationInformation()
            );

            // Add polls for each request (polling Class 1, Class 2, and Class 3 analog inputs)
            var pollClass1 = _masterChannel.AddPoll(association, requestClass1, TimeSpan.FromSeconds(1));

            // Enable the master channel for communication
            _masterChannel.Enable();
        }




        // ANCHOR: Logging Interface for example purposes
        class ConsoleLogger : ILogger
        {
            public void OnMessage(LogLevel level, string message)
            {
                Trace.WriteLine($"[{level}] {message}");
            }
        }

        // ANCHOR: Client State Listener for example purposes
        class TestClientStateListener : IClientStateListener
        {
            public void OnChange(ClientState state)
            {
                Trace.WriteLine($"Client state changed: {state}");
            }
        }

        public List<double> getAnalogInputs()
        {

            return trh.analogInputs;


        }



        class TestPortStateListener : IPortStateListener
        {
            public void OnChange(PortState state)
            {
                Trace.WriteLine(state);
            }
        }

        // ANCHOR: read_handler
        class TestReadHandler : IReadHandler
        {
            public void BeginFragment(ReadType readType, ResponseHeader header)
            {
                Trace.WriteLine($"Beginning fragment (broadcast: {header.Iin.Iin1.Broadcast})");
            }

            public void EndFragment(ReadType readType, ResponseHeader header)
            {
                Trace.WriteLine("End fragment");
            }

            public void HandleBinaryInput(HeaderInfo info, ICollection<BinaryInput> values)
            {
                Trace.WriteLine("Binary Inputs:");
                Trace.WriteLine("Qualifier: " + info.Qualifier);
                Trace.WriteLine("Variation: " + info.Variation);

                foreach (var val in values)
                {
                    Trace.WriteLine($"BI {val.Index}: Value={val.Value} Flags={val.Flags.Value} Time={val.Time.Value} ({val.Time.Quality})");
                }
            }

            public void HandleDoubleBitBinaryInput(HeaderInfo info, ICollection<DoubleBitBinaryInput> values)
            {
                Trace.WriteLine("Double Bit Binary Inputs:");
                Trace.WriteLine("Qualifier: " + info.Qualifier);
                Trace.WriteLine("Variation: " + info.Variation);

                foreach (var val in values)
                {
                    Trace.WriteLine($"DBBI {val.Index}: Value={val.Value} Flags={val.Flags.Value} Time={val.Time.Value} ({val.Time.Quality})");
                }
            }

            public void HandleBinaryOutputStatus(HeaderInfo info, ICollection<BinaryOutputStatus> values)
            {
                Trace.WriteLine("Binary Output Statuses:");
                Trace.WriteLine("Qualifier: " + info.Qualifier);
                Trace.WriteLine("Variation: " + info.Variation);

                foreach (var val in values)
                {
                    Trace.WriteLine($"BOS {val.Index}: Value={val.Value} Flags={val.Flags.Value} Time={val.Time.Value} ({val.Time.Quality})");
                }
            }

            public void HandleCounter(HeaderInfo info, ICollection<Counter> values)
            {
                Trace.WriteLine("Counters:");
                Trace.WriteLine("Qualifier: " + info.Qualifier);
                Trace.WriteLine("Variation: " + info.Variation);

                foreach (var val in values)
                {
                    Trace.WriteLine($"Counter {val.Index}: Value={val.Value} Flags={val.Flags.Value} Time={val.Time.Value} ({val.Time.Quality})");
                }
            }

            public void HandleFrozenCounter(HeaderInfo info, ICollection<FrozenCounter> values)
            {
                Trace.WriteLine("Frozen Counters:");
                Trace.WriteLine("Qualifier: " + info.Qualifier);
                Trace.WriteLine("Variation: " + info.Variation);

                foreach (var val in values)
                {
                    Trace.WriteLine($"Frozen Counter {val.Index}: Value={val.Value} Flags={val.Flags.Value} Time={val.Time.Value} ({val.Time.Quality})");
                }
            }
            internal List<double> analogInputs = new List<double>();
            public void HandleAnalogInput(HeaderInfo info, ICollection<AnalogInput> values)
            {
                List<double> updatedInputs = new List<double>();
                Trace.WriteLine("Analog Inputs:");
                Trace.WriteLine("Qualifier: " + info.Qualifier);
                Trace.WriteLine("Variation: " + info.Variation);

                foreach (var val in values)
                {
                    updatedInputs.Add(val.Value);
                }
                if (analogInputs == null || analogInputs.Count == 0 || analogInputs.Count <= updatedInputs.Count)
                    analogInputs = updatedInputs;
            }

            public void HandleFrozenAnalogInput(HeaderInfo info, ICollection<FrozenAnalogInput> values)
            {
                Trace.WriteLine("Frozen Analog Inputs:");
                Trace.WriteLine("Qualifier: " + info.Qualifier);
                Trace.WriteLine("Variation: " + info.Variation);

                foreach (var val in values)
                {
                    Trace.WriteLine($"AI {val.Index}: Value={val.Value} Flags={val.Flags.Value} Time={val.Time.Value} ({val.Time.Quality})");
                }
            }

            public void HandleAnalogOutputStatus(HeaderInfo info, ICollection<AnalogOutputStatus> values)
            {
                Trace.WriteLine("Analog Output Statuses:");
                Trace.WriteLine("Qualifier: " + info.Qualifier);
                Trace.WriteLine("Variation: " + info.Variation);

                foreach (var val in values)
                {
                    Trace.WriteLine($"AOS {val.Index}: Value={val.Value} Flags={val.Flags.Value} Time={val.Time.Value} ({val.Time.Quality})");
                }
            }

            void IReadHandler.HandleBinaryOutputCommandEvent(HeaderInfo info, ICollection<BinaryOutputCommandEvent> values)
            {
                Trace.WriteLine("Binary Output Command Events:");
                Trace.WriteLine("Qualifier: " + info.Qualifier);
                Trace.WriteLine("Variation: " + info.Variation);

                foreach (var val in values)
                {
                    Trace.WriteLine($"BOCE {val.Index}: Value={val.CommandedState} Status={val.Status} Time={val.Time.Value} ({val.Time.Quality})");
                }
            }

            void IReadHandler.HandleAnalogOutputCommandEvent(HeaderInfo info, ICollection<AnalogOutputCommandEvent> values)
            {
                Trace.WriteLine("Analog Output Command Events:");
                Trace.WriteLine("Qualifier: " + info.Qualifier);
                Trace.WriteLine("Variation: " + info.Variation);

                foreach (var val in values)
                {
                    Trace.WriteLine($"AOCE {val.Index}: Value={val.CommandedValue} Type={val.CommandType} Status={val.Status} Time={val.Time.Value} ({val.Time.Quality})");
                }
            }

            void IReadHandler.HandleUnsignedInteger(HeaderInfo info, ICollection<UnsignedInteger> values)
            {
                Trace.WriteLine("Unsigned Integers:");
                Trace.WriteLine("Qualifier: " + info.Qualifier);
                Trace.WriteLine("Variation: " + info.Variation);

                foreach (var val in values)
                {
                    Trace.WriteLine($"{val.Index}: Value={val.Value}");
                }
            }

            public void HandleOctetString(HeaderInfo info, ICollection<OctetString> values)
            {
                Trace.WriteLine("Octet Strings:");
                Trace.WriteLine("Qualifier: " + info.Qualifier);
                Trace.WriteLine("Variation: " + info.Variation);

                foreach (var val in values)
                {
                    Trace.Write($"Octet String {val.Index}: Value=");
                    foreach (var b in val.Value)
                    {
                        Trace.Write($"{b:X2} ");
                    }

                }
            }

            void IReadHandler.HandleStringAttr(HeaderInfo info, StringAttr attr, byte set, byte var, string value)
            {
                Trace.WriteLine($"Visible string attribute: {attr} set: {set} variation: {var} value: {value}");
            }

            void IReadHandler.HandleUintAttr(HeaderInfo info, UintAttr attr, byte set, byte var, uint value)
            {
                Trace.WriteLine($"Unsigned integer attribute: {attr} set: {set} variation: {var} value: {value}");
            }

            void IReadHandler.HandleBoolAttr(HeaderInfo info, BoolAttr attr, byte set, byte var, bool value)
            {
                Trace.WriteLine($"Boolean attribute: {attr} set: {set} variation: {var} value: {value}");
            }

            void IReadHandler.HandleIntAttr(HeaderInfo info, IntAttr attr, byte set, byte var, int value)
            {
                Trace.WriteLine($"Int attribute: {attr} set: {set} variation: {var} value: {value}");
            }

            void IReadHandler.HandleTimeAttr(HeaderInfo info, TimeAttr attr, byte set, byte var, ulong value)
            {
                Trace.WriteLine($"Time attribute: {attr} set: {set} variation: {var} value: {value}");
            }

            void IReadHandler.HandleFloatAttr(HeaderInfo info, FloatAttr attr, byte set, byte var, double value)
            {
                Trace.WriteLine($"Float attribute: {attr} set: {set} variation: {var} value: {value}");
            }

            void IReadHandler.HandleVariationListAttr(HeaderInfo info, VariationListAttr attr, byte set, byte var, ICollection<AttrItem> value)
            {
                Trace.WriteLine($"Attribute variation list: {attr} set: {set} variation: {var}");
                foreach (var item in value)
                {
                    Trace.WriteLine($"variation: {item.Variation} writable: {item.Properties.IsWritable}");
                }
            }

            void IReadHandler.HandleOctetStringAttr(HeaderInfo info, OctetStringAttr attr, byte set, byte var, ICollection<byte> value)
            {
                Trace.WriteLine($"Octet-string attribute: {attr} set: {set} variation: {var} length: {value.Count}");
            }

            void IReadHandler.HandleBitStringAttr(HeaderInfo info, BitStringAttr attr, byte set, byte var, ICollection<byte> value)
            {
                Trace.WriteLine($"Bit-string attribute: {attr} set: {set} variation: {var} length: {value.Count}");
            }


        }
        // ANCHOR_END: read_handler

        // ANCHOR: association_handler
        class TestAssociationHandler : IAssociationHandler
        {
            public UtcTimestamp GetCurrentTime()
            {
                return UtcTimestamp.Valid((ulong)DateTime.UtcNow.Millisecond);
            }
        }
        // ANCHOR_END: association_handler

        // ANCHOR: association_information
        class TestAssociationInformation : IAssociationInformation
        {
            public void TaskStart(TaskType taskType, FunctionCode fc, byte seq) { }
            public void TaskSuccess(TaskType taskType, FunctionCode fc, byte seq) { }
            public void TaskFail(TaskType taskType, TaskError error) { }
            public void UnsolicitedResponse(bool isDuplicate, byte seq) { }
        }
        // ANCHOR_END: association_information

        // ANCHOR: file_logger
        class FileReader : IFileReader
        {
            void IFileReader.Aborted(FileError error)
            {
                Trace.WriteLine($"File transfer aborted: {error}");
            }

            bool IFileReader.BlockReceived(uint blockNum, ICollection<byte> data)
            {
                Trace.WriteLine($"Received file block {blockNum} with size {data.Count}");
                return true;
            }

            void IFileReader.Completed()
            {
                Trace.WriteLine($"File transfer completed");
            }

            bool IFileReader.Opened(uint size)
            {
                Trace.WriteLine($"Outstation open file with size: ${size}");
                return true;
            }
        }
        // ANCHOR_END: file_logger

        // ANCHOR: master_channel_config
        private static MasterChannelConfig GetMasterChannelConfig()
        {
            var conf = new MasterChannelConfig(3)
                 .WithDecodeLevel(DecodeLevel.Nothing().WithApplication(AppDecodeLevel.ObjectValues));

            return conf;
        }
        // ANCHOR_END: master_channel_config

        // ANCHOR: association_config
        private static AssociationConfig GetAssociationConfig()
        {
            var config = new AssociationConfig(
                 // disable unsolicited first (Class 1/2/3)
                 EventClasses.All(),
                 // after the integrity poll, enable unsolicited (Class 1/2/3)
                 EventClasses.All(),
                 // perform startup integrity poll with Class 1/2/3/0
                 Classes.All(),
                 // don't automatically scan Class 1/2/3 when the corresponding IIN bit is asserted
                 EventClasses.All()

             )
             .WithAutoTimeSync(AutoTimeSync.Lan)
             .WithKeepAliveTimeout(TimeSpan.FromSeconds(60));



            return config;
        }
        // ANCHOR_END: association_config

        private static void RunTcp(Runtime runtime)
        {
            // ANCHOR: create_tcp_channel
            var channel = MasterChannel.CreateTcpChannel(
                runtime,
                LinkErrorMode.Close,
                GetMasterChannelConfig(),
                new EndpointList("127.0.0.1:20000"),
                new ConnectStrategy(),
                new TestClientStateListener()
            );
            // ANCHOR_END: create_tcp_channel

            try
            {
                RunChannel(channel);
            }
            finally
            {
                channel.Shutdown();
            }
        }

        private static void RunTls(Runtime runtime, TlsClientConfig tlsConfig)
        {
            // ANCHOR: create_tls_channel
            var channel = MasterChannel.CreateTlsChannel(
                runtime,
                LinkErrorMode.Close,
                GetMasterChannelConfig(),
                new EndpointList("127.0.0.1:20001"),
                new ConnectStrategy(),
                new TestClientStateListener(),
                tlsConfig
            );
            // ANCHOR_END: create_tls_channel

            try
            {
                RunChannel(channel);
            }
            finally
            {
                channel.Shutdown();
            }
        }

        private static TlsClientConfig GetCaTlsConfig()
        {
            // ANCHOR: tls_ca_chain_config
            // defaults to CA mode
            var config = new TlsClientConfig(
                "test.com",
                "./certs/ca_chain/ca_cert.pem",
                "./certs/ca_chain/entity1_cert.pem",
                "./certs/ca_chain/entity1_key.pem",
                "" // no password
            );
            // ANCHOR_END: tls_ca_chain_config
            return config;
        }

        private static TlsClientConfig GetSelfSignedTlsConfig()
        {
            // ANCHOR: tls_self_signed_config
            var config = new TlsClientConfig(
                "test.com",
                "./certs/self_signed/entity2_cert.pem",
                "./certs/self_signed/entity1_cert.pem",
                "./certs/self_signed/entity1_key.pem",
                "" // no password
            ).WithCertificateMode(CertificateMode.SelfSigned);
            // ANCHOR_END: tls_self_signed_config
            return config;
        }




        private static void RunChannel(MasterChannel channel)
        {
            // ANCHOR: association_createA
            var association = channel.AddAssociation(
                1024,
                GetAssociationConfig(),
                new TestReadHandler(),
                new TestAssociationHandler(),
                new TestAssociationInformation()
            );
            // ANCHOR_END: association_create

        }

        internal void Dispose()
        {
            try
            {
                _masterChannel.Shutdown();
                _runtime.Shutdown();

            }
            catch
            {

            }
        }
    }
}
