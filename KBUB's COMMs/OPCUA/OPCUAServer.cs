using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;

using Opc.Ua.Security.Certificates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Security.AccessControl;

namespace KBUBComm.OPCUA
{
    public class OPCUAServer : StandardServer
    {
        public bool IsListening { get; private set; }
        private ApplicationConfiguration config;
        private EventLog log;
        internal KBUBNodeManager nodeMgr;
        ApplicationInstance app;
        public Dictionary<string, NodeId> addedNodes = new Dictionary<string, NodeId>();

        string rootPointsFolderName = "KBUB Points";
        string nodeManagerURI = "http://kbubcomm/opcua/";
        List<Tuple<string, string>> subfoldersToCreate = null;

        protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration)
        {
            log.WriteEntry("Creating Master Node");
            nodeMgr = new KBUBNodeManager(server, config, nodeManagerURI, rootPointsFolderName, log, subfoldersToCreate);
            var nodeManagers = new INodeManager[] { nodeMgr };

            return new MasterNodeManager(server, configuration, null, nodeManagers);
        }



        public async Task InitializeServer(int port, string appURI, EventLog log, string name = "KBUBComm OPCUA Server", string rootPointsFolderName= "KBUB Points", string nodeManagerURI = "http://kbubcomm/opcua/",  List<Tuple<string,string>> subfoldersToCreate = null)
        {
            this.log = log;

            var basePkiPath = @"C:\opcua\pki";
            var ownStore = Path.Combine(basePkiPath, "own");
            var trustedStore = Path.Combine(basePkiPath, "trusted");
            var rejectedStore = Path.Combine(basePkiPath, "rejected");
            this.subfoldersToCreate = subfoldersToCreate;
            this.nodeManagerURI = nodeManagerURI;
            this.rootPointsFolderName = rootPointsFolderName;

            Directory.CreateDirectory(ownStore);
            Directory.CreateDirectory(trustedStore);
            Directory.CreateDirectory(rejectedStore);

            log.WriteEntry("Creating App Config");

            config = new ApplicationConfiguration()
            {
                ApplicationName = name,
                ApplicationType = ApplicationType.Server,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = "Directory",
                        StorePath = ownStore,
                        SubjectName = $"CN={name}"
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = "Directory",
                        StorePath = trustedStore
                    },
                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = "Directory",
                        StorePath = rejectedStore
                    },
                    AutoAcceptUntrustedCertificates = true
                },
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                ServerConfiguration = new ServerConfiguration
                {
                    BaseAddresses = new StringCollection { $"opc.tcp://localhost:{port}" },
                    UserTokenPolicies = new UserTokenPolicyCollection
                    {
                        new UserTokenPolicy(UserTokenType.Anonymous)
                    },
                    SecurityPolicies = new ServerSecurityPolicyCollection
                    {
                        new ServerSecurityPolicy
                        {
                            SecurityMode = MessageSecurityMode.None,
                            SecurityPolicyUri = SecurityPolicies.None
                        }
                    }
                },
                CertificateValidator = new CertificateValidator()
            };

            log.WriteEntry("Getting Server Cert");
            var cert = await config.SecurityConfiguration.ApplicationCertificate.Find(true);
            if (cert == null)
            {
                cert = CertificateFactory.CreateCertificate(
                    storeType: "Directory",
                    storePath: ownStore,
                    password: null,
                    applicationUri: appURI,
                    applicationName: name,
                    subjectName: $"CN={name}",
                    domainNames: new List<string> { "localhost" },
                    keySize: 2048,
                    startTime: DateTime.UtcNow,
                    lifetimeInMonths: 25 * 12,
                    hashSizeInBits: 256,
                    isCA: false
                );
                config.SecurityConfiguration.ApplicationCertificate.Certificate = cert;
            }

            log.WriteEntry("App Instancing");
            app = new ApplicationInstance
            {
                ApplicationName = config.ApplicationName,
                ApplicationType = config.ApplicationType,
                ApplicationConfiguration = config
            };

            log.WriteEntry("Starting App");
            await app.Start(this);

            IsListening = true;
            log.WriteEntry("Server is now listening.");
        }

        public void StopListening() => IsListening = false;
        public enum CanWrite { ReadWrite = 0, ReadOnly = 1 }


        public BaseDataVariableState CreatePoint(string pointName, NodeId nodeID = null, NodeId dataType = null, CanWrite canWrite = CanWrite.ReadOnly, object initialValue = null, string subFolder = null)
        {

            var bdvs = nodeMgr.CreatePoint(pointName, nodeID, dataType, canWrite == CanWrite.ReadWrite, initialValue, subFolder);
            addedNodes.Add(pointName, bdvs.NodeId);
            return bdvs;

        }

        public bool TryWritePoint(string pointName, object value)
        {
            try
            {
                if (addedNodes.Count > 0 && addedNodes.ContainsKey(pointName))
                {
                    WritePoint(addedNodes[pointName], value);
                    return true;
                }
                else return false;
            }
            catch (Exception ex)
            {
                log.WriteEntry("Error writing point: " + ex);
                return false;
            }
        }
        public bool TryReadPoint(string pointName, out object value)
        {
            try
            {
                if (addedNodes.Count > 0 && addedNodes.ContainsKey(pointName))
                {
                    var obj = ReadPoint(addedNodes[pointName]);
                    value = obj;
                    return true;
                }
                else
                {
                    value = null;
                    return false;
                }
            }
            catch
            {
                value = null;
                return false;
            }
        }

        public void WritePoint(NodeId nodeId, object value)
        {
            nodeMgr.WritePoint(nodeId, value);
        }

        public object ReadPoint(NodeId nodeId)
        {
            return nodeMgr.ReadPoint(nodeId);
        }



    }

    public class KBUBNodeManager : CustomNodeManager2
    {
        private EventLog log;
        private string _pointsFolderName;
        List<Tuple<string, string>> subfoldersToCreate = null;
        // Keeps track of all created folders
        private Dictionary<string, FolderState> _folders = new Dictionary<string, FolderState>(StringComparer.OrdinalIgnoreCase);

        public FolderState PointsFolder { get; private set; }

        // Dictionary for easy access to points by NodeId
        private readonly Dictionary<NodeId, BaseDataVariableState> _points = new Dictionary<NodeId, BaseDataVariableState>();

        public KBUBNodeManager(IServerInternal server, ApplicationConfiguration config, string namespaceUri, string pointsFolderName, EventLog log, List<Tuple<string, string>> subfoldersToCreate )
            : base(server, config)
        {
            this.log = log;
            _pointsFolderName = pointsFolderName;
            this.subfoldersToCreate = subfoldersToCreate;
            string[] namespaceUris = new string[2];
            namespaceUris[0] = namespaceUri;
            namespaceUris[1] = namespaceUri + "/Instance";
            SetNamespaces(namespaceUris);

            log.WriteEntry($"Node Manager initialized with namespace '{namespaceUri}' and points folder '{pointsFolderName}'.");
        }

        protected override NodeStateCollection LoadPredefinedNodes(ISystemContext context)
        {
            return base.LoadPredefinedNodes(context);
        }

        public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            lock (Lock)
            {
                log.WriteEntry("Trying to create address space");
                base.CreateAddressSpace(externalReferences);

                log.WriteEntry("Getting references");
                IList<IReference> references = null;

                if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out references))
                {
                    externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
                }

                log.WriteEntry("Creating points folder");

                PointsFolder = new FolderState(null)
                {
                    NodeId = new NodeId(_pointsFolderName.Replace(" ", "_"), NamespaceIndex),
                    BrowseName = new QualifiedName(_pointsFolderName.Replace(" ", "_"), NamespaceIndex),
                    SymbolicName = _pointsFolderName,
                    DisplayName = _pointsFolderName,
                    TypeDefinitionId = ObjectTypeIds.FolderType
                };

                PointsFolder.AddReference(ReferenceTypeIds.Organizes, true, ObjectIds.ObjectsFolder);
                references.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, PointsFolder.NodeId));
                PointsFolder.EventNotifier = EventNotifiers.SubscribeToEvents;
                AddRootNotifier(PointsFolder);

                log.WriteEntry("Adding points folder as predefined node");
                AddPredefinedNode(SystemContext, PointsFolder);
                log.WriteEntry("Moving onto subfolders " + (subfoldersToCreate == null ? "(None found)" : "(" + (subfoldersToCreate.Count + " subfolders found!)")));
                if (subfoldersToCreate != null && subfoldersToCreate.Count > 0)
                {

                    _folders = new Dictionary<string, FolderState>(StringComparer.OrdinalIgnoreCase)
            {
                { _pointsFolderName, PointsFolder }
            };

                    foreach (var (parentName, childName) in subfoldersToCreate)
                    {
                     
                        try
                        {
                            var parentKey = string.IsNullOrEmpty(parentName) ? _pointsFolderName : parentName;
                            log.WriteEntry("Creating Subfolder " + childName + " in the " + parentKey + " folder.");
                            if (!_folders.TryGetValue(parentKey, out var parentFolder))
                            {
                                log.WriteEntry($"Parent folder '{parentKey}' not found. Skipping child '{childName}'.");
                                continue;
                            }
                        
                            var newFolder = new FolderState(parentFolder)
                            {
                                NodeId = new NodeId(childName.Replace(" ", "_"), NamespaceIndex),
                                BrowseName = new QualifiedName(childName.Replace(" ", "_"), NamespaceIndex),
                                SymbolicName = childName,
                                DisplayName = childName,
                                TypeDefinitionId = ObjectTypeIds.FolderType
                            };

                            newFolder.AddReference(ReferenceTypeIds.Organizes, true, parentFolder.NodeId);
                            parentFolder.AddReference(ReferenceTypeIds.Organizes, false, newFolder.NodeId);
                            references.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, parentFolder.NodeId));
                            AddRootNotifier(newFolder);

                            newFolder.EventNotifier = EventNotifiers.SubscribeToEvents;
                            AddPredefinedNode(SystemContext, newFolder);

                            _folders[childName] = newFolder;


                            log.WriteEntry($"Created subfolder '{childName}' under '{parentKey}'.");
                        }
                        catch (Exception ex)
                        {
                            log.WriteEntry($"Error creating subfolder '{childName}': {ex}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates a point in the points folder with a specified type.
        /// </summary>
        public BaseDataVariableState CreatePoint(
      string pointName,
      NodeId nodeId = null,
      NodeId dataType = null,
      bool canWrite = false,
      object initialValue = null,
      string subFolder = null)
        {
            lock (Lock)
            {
                if (nodeId == null)
                    nodeId = new NodeId(pointName.Replace(" ", "_"), NamespaceIndex);

                if (dataType == null)
                    dataType = DataTypeIds.Double;

                FolderState parentFolder;

                if (string.IsNullOrEmpty(subFolder))
                {
                    parentFolder = PointsFolder;
                }
                else if (!_folders.TryGetValue(subFolder, out parentFolder))
                {
                    log.WriteEntry($"ERROR: Subfolder '{subFolder}' not found. Skipping point '{pointName}'.");
                    return null;
                }

                var point = new BaseDataVariableState(parentFolder)
                {
                    NodeId = nodeId,
                    BrowseName = new QualifiedName(pointName.Replace(" ", "_"), NamespaceIndex),
                    DisplayName = pointName,
                    DataType = dataType,
                    ValueRank = ValueRanks.Scalar,
                    Value = initialValue ?? Activator.CreateInstance(Type.GetType(dataType.ToString()) ?? typeof(double)),
                    UserAccessLevel = canWrite ? AccessLevels.CurrentReadOrWrite : AccessLevels.CurrentRead,
                    AccessLevel = AccessLevels.CurrentReadOrWrite
                };

                parentFolder.AddChild(point);
                AddPredefinedNode(SystemContext, point);
                _points[nodeId] = point;

                log.WriteEntry(
                    $"Created point '{pointName}' in folder '{parentFolder.DisplayName}' with NodeId '{nodeId}' and DataType '{dataType}'.");

                return point;
            }
        }



        /// <summary>
        /// Write value to an existing point
        /// </summary>
        public void WritePoint(NodeId nodeId, object value)
        {
            lock (Lock)
            {
                if (_points.TryGetValue(nodeId, out var point))
                {
                    point.Value = value;
                   // log.WriteEntry($"Wrote value '{value}' to point '{point.DisplayName}'.");
                }
                else
                {
                    log.WriteEntry($"Attempted to write value to non-existent point '{nodeId}'.");
                }
            }
        }

        /// <summary>
        /// Read value from an existing point
        /// </summary>
        public object ReadPoint(NodeId nodeId)
        {
            lock (Lock)
            {
                if (_points.TryGetValue(nodeId, out var point))
                {
                 //   log.WriteEntry($"Read value '{point.Value}' from point '{point.DisplayName}'.");
                    return point.Value;
                }
                else
                {
                    log.WriteEntry($"Attempted to read value from non-existent point '{nodeId}'.");
                    return null;
                }
            }
        }
    }
}

