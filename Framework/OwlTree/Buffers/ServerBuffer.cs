
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OwlTree
{
    /// <summary>
    /// Manages sending and receiving messages for a server instance.
    /// </summary>
    public class ServerBuffer : NetworkBuffer
    {
        /// <summary>
        /// Manages sending and receiving messages for a server instance.
        /// </summary>
        /// <param name="args">NetworkBuffer parameters.</param>
        /// <param name="maxClients">The max number of clients that can be connected at once.</param>
        public ServerBuffer(Args args, int maxClients, long requestTimeout) : base (args)
        {
            IPEndPoint tpcEndPoint = new IPEndPoint(IPAddress.Any, TcpPort);
            _tcpServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _tcpServer.Bind(tpcEndPoint);
            _tcpServer.Listen(maxClients);
            _readList.Add(_tcpServer);

            IPEndPoint udpEndPoint = new IPEndPoint(IPAddress.Any, ServerUdpPort);
            _udpServer = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _udpServer.Bind(udpEndPoint);
            _readList.Add(_udpServer);

            MaxClients = maxClients == -1 ? int.MaxValue : maxClients;
            _requests = new(MaxClients, requestTimeout);
            LocalId = ClientId.None;
            Authority = ClientId.None;
            IsReady = true;
            OnReady?.Invoke(LocalId);
        }

        /// <summary>
        /// The maximum number of clients allowed to be connected at once on this connection.
        /// </summary>
        public int MaxClients { get; private set; }

        // server state
        private Socket _tcpServer;
        private Socket _udpServer;
        private List<Socket> _readList = new List<Socket>();
        private ClientDataList _clientData = new();
        private ConnectionRequestList _requests;

        /// <summary>
        /// Reads any data currently on sockets. Putting new messages in the queue, and connecting new clients.
        /// </summary>
        public override void Read()
        {
            _readList.Clear();
            _readList.Add(_tcpServer);
            _readList.Add(_udpServer);
            foreach (var data in _clientData)
                _readList.Add(data.tcpSocket);
            
            Socket.Select(_readList, null, null, 0);

            _requests.ClearTimeouts();

            foreach (var socket in _readList)
            {
                // new client connects
                if (socket == _tcpServer)
                {
                    var tcpClient = socket.Accept();

                    // reject connections that aren't from verified app instances
                    if(!_requests.TryGet((IPEndPoint)tcpClient.RemoteEndPoint, out var udpPort))
                    {
                        tcpClient.Close();
                        continue;
                    }

                    IPEndPoint udpEndPoint = new IPEndPoint(((IPEndPoint)tcpClient.RemoteEndPoint).Address, udpPort);

                    var clientData = new ClientData() {
                        id = ClientId.New(), 
                        tcpPacket = new Packet(BufferSize), 
                        tcpSocket = tcpClient,
                        udpPacket = new Packet(BufferSize, true),
                        udpEndPoint = udpEndPoint
                    };
                    clientData.tcpPacket.header.owlTreeVer = OwlTreeVersion;
                    clientData.tcpPacket.header.appVer = AppVersion;
                    clientData.udpPacket.header.owlTreeVer = OwlTreeVersion;
                    clientData.udpPacket.header.appVer = AppVersion;

                    _clientData.Add(clientData);

                    if (Logger.includes.connectionAttempts)
                    {
                        Logger.Write($"TCP handshake made with {((IPEndPoint)tcpClient.RemoteEndPoint).Address} (tcp port: {((IPEndPoint)tcpClient.RemoteEndPoint).Port}) (udp port: {udpPort}). Assigned: {clientData.id}");
                    }

                    OnClientConnected?.Invoke(clientData.id);

                    // send new client their id
                    var span = clientData.tcpPacket.GetSpan(LocalClientConnectLength);
                    LocalClientConnectEncode(span, new ClientIdAssignment(clientData.id, Authority));

                    foreach (var otherClient in _clientData)
                    {
                        if (otherClient.id == clientData.id) continue;

                        // notify clients of a new client in the next send
                        span = otherClient.tcpPacket.GetSpan(ClientMessageLength);
                        ClientConnectEncode(span, clientData.id);

                        // add existing clients to new client
                        span = clientData.tcpPacket.GetSpan(ClientMessageLength);
                        ClientConnectEncode(span, otherClient.id);
                    }
                    HasClientEvent = true;
                    
                    clientData.tcpPacket.header.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    ApplySendSteps(clientData.tcpPacket);
                    var bytes = clientData.tcpPacket.GetPacket();
                    tcpClient.Send(bytes);
                    clientData.tcpPacket.Reset();
                }
                else if (socket == _udpServer) // receive client udp messages
                {
                    Array.Clear(ReadBuffer, 0, ReadBuffer.Length);
                    ReadPacket.Clear();

                    EndPoint source = new IPEndPoint(IPAddress.Any, 0);
                    int dataLen = -1;
                    try
                    {
                        dataLen = socket.ReceiveFrom(ReadBuffer, ref source);
                        ReadPacket.FromBytes(ReadBuffer, 0);

                        if (ReadPacket.header.appVer < MinAppVersion || ReadPacket.header.owlTreeVer < MinOwlTreeVersion)
                        {
                            throw new InvalidOperationException("Cannot accept packets from outdated OwlTree or app versions.");
                        }
                    }
                    catch { }

                    if (dataLen <= 0)
                    {
                        continue;
                    }

                    var client = _clientData.Find((IPEndPoint)source);

                    // try to verify a new client connection
                    if (client == ClientData.None)
                    {
                        var accepted = false;

                        if (Logger.includes.connectionAttempts)
                        {
                            Logger.Write("Connection attempt from " + ((IPEndPoint)source).Address.ToString() + " (udp port: " + ((IPEndPoint)source).Port + ") received: \n" + PacketToString(ReadPacket));
                        }

                        ReadPacket.StartMessageRead();
                        if (ReadPacket.TryGetNextMessage(out var bytes))
                        {
                            var rpcId = ServerMessageDecode(bytes, out var request);
                            if (
                                rpcId == RpcId.CONNECTION_REQUEST && 
                                request.appId == ApplicationId && !request.isHost &&
                                _clientData.Count < MaxClients && _requests.Count < MaxClients
                            )
                            {

                                // connection request verified, send client confirmation
                                _requests.Add((IPEndPoint)source);
                                accepted = true;
                            }
                            
                            ReadPacket.Clear();
                            ReadPacket.header.owlTreeVer = OwlTreeVersion;
                            ReadPacket.header.appVer = AppVersion;
                            ReadPacket.header.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            ReadPacket.header.sender = 0;
                            ReadPacket.header.target = 0;
                            var response = ReadPacket.GetSpan(4);
                            BitConverter.TryWriteBytes(response, (int)(accepted ? ConnectionResponseCode.Accepted : ConnectionResponseCode.Rejected));
                            var responsePacket = ReadPacket.GetPacket();
                            _udpServer.SendTo(responsePacket.ToArray(), source);
                        }

                        if (Logger.includes.connectionAttempts)
                        {
                            Logger.Write("Connection attempt from " + ((IPEndPoint)source).Address.ToString() + " (udp port: " + ((IPEndPoint)source).Port + ") " + (accepted ? "accepted, awaiting TCP handshake..." : "rejected."));
                        }
                        continue;
                    }

                    if (Logger.includes.udpPreTransform)
                    {
                        var packetStr = new StringBuilder($"RECEIVED: Pre-Transform UDP packet from {client.id} at {DateTime.UtcNow}:\n");
                        PacketToString(ReadPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    ApplyReadSteps(ReadPacket);

                    if (Logger.includes.udpPostTransform)
                    {
                        var packetStr = new StringBuilder($"RECEIVED: Post-Transform UDP packet from {client.id} at {DateTime.UtcNow}:\n");
                        PacketToString(ReadPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    ReadPacket.StartMessageRead();
                    while (ReadPacket.TryGetNextMessage(out var bytes))
                    {
                        if (TryDecode(client.id, bytes, out var message))
                        {
                            message.protocol = Protocol.Udp;
                            if (message.callee != ClientId.None)
                                _outgoing.Enqueue(message);
                            else
                                _incoming.Enqueue(message);
                        }
                    }
                }
                else // receive client tcp messages
                {
                    Array.Clear(ReadBuffer, 0, ReadBuffer.Length);
                    int dataRemaining = -1;
                    int dataLen = -1;
                    ClientData client = ClientData.None;

                    do {
                        ReadPacket.Clear();

                        int iters = 0;
                        do {
                            try
                            {
                                if (dataRemaining <= 0)
                                {
                                    dataLen = socket.Receive(ReadBuffer);
                                    dataRemaining = dataLen;
                                }
                                dataRemaining -= ReadPacket.FromBytes(ReadBuffer, dataLen - dataRemaining);
                                iters++;
                            }
                            catch
                            {
                                dataLen = -1;
                                break;
                            }
                        } while (ReadPacket.Incomplete && iters < 10);

                        if (ReadPacket.header.appVer < MinAppVersion || ReadPacket.header.owlTreeVer < MinOwlTreeVersion)
                        {
                            dataLen = -1;
                        }

                        if (client == ClientData.None)
                            client = _clientData.Find(socket);

                        // disconnect if receive fails
                        if (dataLen <= 0)
                        {
                            Disconnect(client);
                            continue;
                        }

                        if (Logger.includes.tcpPreTransform)
                        {
                            var packetStr = new StringBuilder($"RECEIVED: Pre-Transform TCP packet from {client.id} at {DateTime.UtcNow}:\n");
                            PacketToString(ReadPacket, packetStr);
                            Logger.Write(packetStr.ToString());
                        }

                        ApplyReadSteps(ReadPacket);

                        if (Logger.includes.tcpPostTransform)
                        {
                            var packetStr = new StringBuilder($"RECEIVED: Post-Transform TCP packet from {client.id} at {DateTime.UtcNow}:\n");
                            PacketToString(ReadPacket, packetStr);
                            Logger.Write(packetStr.ToString());
                        }
                        
                        ReadPacket.StartMessageRead();
                        while (ReadPacket.TryGetNextMessage(out var bytes))
                        {
                            if (TryDecode(client.id, bytes, out var message))
                            {
                                message.protocol = Protocol.Tcp;
                                if (message.callee != ClientId.None)
                                    _outgoing.Enqueue(message);
                                else
                                    _incoming.Enqueue(message);
                            }
                        }
                    } while (dataRemaining > 0);
                }
            }
        }

        /// <summary>
        /// Write current buffers to client sockets.
        /// Buffers are cleared after writing.
        /// </summary>
        public override void Send()
        {
            while (_outgoing.TryDequeue(out var message))
            {

                if (message.callee != ClientId.None)
                {
                    var client = _clientData.Find(message.callee);
                    if (client != ClientData.None)
                    {
                        if (message.protocol == Protocol.Tcp)
                            Encode(message, client.tcpPacket);
                        else
                            Encode(message, client.udpPacket);
                    }
                }
                else
                {
                    if (message.protocol == Protocol.Tcp)
                    {
                        foreach (var client in _clientData)
                        {
                            Encode(message, client.tcpPacket);
                        }
                    }
                    else
                    {
                        foreach (var client in _clientData)
                        {
                            Encode(message, client.udpPacket);
                        }
                    }
                }
            }
            foreach (var client in _clientData)
            {
                if (!client.tcpPacket.IsEmpty)
                {
                    client.tcpPacket.header.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    if (Logger.includes.tcpPreTransform)
                    {
                        var packetStr = new StringBuilder($"SENDING: Pre-Transform TCP packet to {client.id} at {DateTime.UtcNow}:\n");
                        PacketToString(client.tcpPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    ApplySendSteps(client.tcpPacket);
                    var bytes = client.tcpPacket.GetPacket();

                    if (Logger.includes.tcpPostTransform)
                    {
                        var packetStr = new StringBuilder($"SENDING: Post-Transform TCP packet to {client.id} at {DateTime.UtcNow}:\n");
                        PacketToString(client.tcpPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    client.tcpSocket.Send(bytes);
                    client.tcpPacket.Reset();
                }

                if (!client.udpPacket.IsEmpty)
                {
                    client.udpPacket.header.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    if (Logger.includes.tcpPreTransform)
                    {
                        var packetStr = new StringBuilder($"SENDING: Pre-Transform UDP packet to {client.id} at {DateTime.UtcNow}:\n");
                        PacketToString(client.udpPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    ApplySendSteps(client.udpPacket);
                    var bytes = client.udpPacket.GetPacket();

                    if (Logger.includes.tcpPostTransform)
                    {
                        var packetStr = new StringBuilder($"SENDING: Post-Transform UDP packet to {client.id} at {DateTime.UtcNow}:\n");
                        PacketToString(client.udpPacket, packetStr);
                        Logger.Write(packetStr.ToString());
                    }

                    _udpServer.SendTo(bytes.ToArray(), client.udpEndPoint);
                    client.udpPacket.Reset();
                }
            }

            HasClientEvent = false;
        }

        /// <summary>
        /// Disconnects all clients, and closes the server.
        /// </summary>
        public override void Disconnect()
        {
            var ids = _clientData.GetIds();
            foreach (var id in ids)
            {
                Disconnect(id);
            }
            _tcpServer.Close();
            _udpServer.Close();
        }


        /// <summary>
        /// Disconnect a client from the server.
        /// Invokes <c>OnClientDisconnected</c>.
        /// </summary>
        public override void Disconnect(ClientId id)
        {
            var client = _clientData.Find(id);
            if (client != ClientData.None)
                Disconnect(client);
        }

        private void Disconnect(ClientData client)
        {
            _clientData.Remove(client);
            client.tcpSocket.Close();
            OnClientDisconnected?.Invoke(client.id);

            foreach (var otherClient in _clientData)
            {
                var span = otherClient.tcpPacket.GetSpan(ClientMessageLength);
                ClientDisconnectEncode(span, client.id);
            }
            HasClientEvent = true;
        }

        public override void MigrateHost(ClientId newHost)
        {
            throw new InvalidOperationException("Servers cannot migrate authority off of themselves.");
        }
    }
}