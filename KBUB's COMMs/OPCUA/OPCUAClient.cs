using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using static System.Collections.Specialized.BitVector32;

namespace KBUBComm.OPCUA
{
    public class OPCUAClient
    {
        private Session session;
        private readonly Dictionary<string, NodeId> pointCache = new Dictionary<string, NodeId>();
        public bool IsConnected => session?.Connected ?? false;

        public async Task<bool> ConnectAsync(string address, int port, string name = "KBUBComm OPCUA Client")
        {
            try
            {
                var config = new ApplicationConfiguration
                {
                    ApplicationName = name,
                    ApplicationType = ApplicationType.Client,
                    SecurityConfiguration = new SecurityConfiguration
                    {
                        ApplicationCertificate = new CertificateIdentifier(),
                        AutoAcceptUntrustedCertificates = true
                    },
                    TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                    ClientConfiguration = new ClientConfiguration(),
                    CertificateValidator = new CertificateValidator()
                };

                var app = new ApplicationInstance
                {
                    ApplicationName = config.ApplicationName,
                    ApplicationType = config.ApplicationType,
                    ApplicationConfiguration = config
                };

                var endpointUrl = $"opc.tcp://{address}:{port}";

                // Use the new overload (with config) to avoid obsolete API
                var selectedEndpoint = CoreClientUtils.SelectEndpoint(config, endpointUrl, useSecurity: false, 15000);

                session = await Session.Create(
                    config,
                    new ConfiguredEndpoint(null, selectedEndpoint, EndpointConfiguration.Create(config)),
                    updateBeforeConnect: false,
                    sessionName: "KBUBCommSession",
                    sessionTimeout: 60000,
                    identity: null,          // anonymous
                    preferredLocales: null);

                pointCache.Clear();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ConnectAsync failed: {ex.Message}");
                return false;
            }
        }


        public void Disconnect()
        {
            session?.Close();
            session?.Dispose();
            session = null;
            pointCache.Clear();
        }

        /// <summary>
        /// Browses for the NodeId of a given point name (BrowseName with spaces replaced by "_").
        /// Caches the result for faster repeated reads/writes.
        /// </summary>
        private NodeId ResolveNodeId(string pointName)
        {
            if (pointCache.TryGetValue(pointName, out var nodeId))
                return nodeId;

            var browseName = pointName.Replace(" ", "_");

            nodeId = RecursiveBrowse(ObjectIds.ObjectsFolder, browseName);
            if (nodeId != null)
            {
                pointCache[pointName] = nodeId;
                return nodeId;
            }

            throw new Exception($"Point '{pointName}' not found on server.");
        }

        private NodeId RecursiveBrowse(NodeId startNode, string browseName)
        {
            var browser = new Browser(session)
            {
                BrowseDirection = BrowseDirection.Forward,
                NodeClassMask = (int)NodeClass.Variable | (int)NodeClass.Object,
                ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
                IncludeSubtypes = true
            };

            foreach (var r in browser.Browse(startNode))
            {
                // Match by BrowseName
                if (r.BrowseName.Name.Equals(browseName, StringComparison.OrdinalIgnoreCase))
                    return (NodeId)r.NodeId;

                // Recurse into objects/folders
                if (r.NodeClass == NodeClass.Object || r.NodeClass == NodeClass.Variable)
                {
                    var child = RecursiveBrowse((NodeId)r.NodeId, browseName);
                    if (child != null)
                        return child;
                }
            }

            return null;
        }

        public bool TryReadPoint(string pointName, out object value)
        {
            try
            {
                value = ReadPoint(pointName);
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }
        public bool TryWritePoint(string pointName, object value)
        {
            try
            {
                WritePoint(pointName, value);
                return true;
            }
            catch
            {

                return false;
            }
        }
        public object ReadPoint(string pointName)
        {
            if (!IsConnected) throw new InvalidOperationException("Not connected.");
            var nodeId = ResolveNodeId(pointName);
            var value = session.ReadValue(nodeId);
            return value.Value;
        }

        public void WritePoint(string pointName, object value)
        {
            if (!IsConnected) throw new InvalidOperationException("Not connected.");
            var nodeId = ResolveNodeId(pointName);

            var v = new WriteValue
            {
                NodeId = nodeId,
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(value))
            };

            var collection = new WriteValueCollection { v };
            session.Write(null, collection, out StatusCodeCollection results, out DiagnosticInfoCollection diag);

            if (StatusCode.IsBad(results[0]))
                throw new Exception($"Write to '{pointName}' failed: {results[0]}");
        }
    }
}
