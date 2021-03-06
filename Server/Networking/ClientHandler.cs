﻿using Server.Entities;
using Server.Misc;
using Server.Networking;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Timers;

namespace Server
{
    class ClientHandler
    {
        Server server;
        TcpClient client;
        public string uid;
        public string clientName;
        public bool initialized = false;

        Thread clientListen;
        Thread clientSend;
        Thread heartbeat;
        System.Timers.Timer heartbeatTimer;
        bool threadKillSignal = false;
        NetworkStream stream;

        public BlockingCollection<Packet> outgoingPackets = new BlockingCollection<Packet>();

        public ClientHandler(Server server, TcpClient client, string uid)
        {
            this.server = server;
            this.client = client;
            this.uid = uid;

            stream = client.GetStream();
            heartbeat = new Thread(new ThreadStart(Heartbeat));
            heartbeat.Start();
            clientSend = new Thread(new ThreadStart(Send));
            clientSend.Start();
            clientListen = new Thread(new ThreadStart(Listen));
            clientListen.Start();
        }

        void Heartbeat()
        {
            heartbeatTimer = new System.Timers.Timer(1000);
            heartbeatTimer.Elapsed += CheckConnecton;
            heartbeatTimer.AutoReset = true;
            heartbeatTimer.Enabled = true;
        }

        void CheckConnecton(Object source, ElapsedEventArgs e)
        {
            if (!client.Connected)
            {
                heartbeatTimer.Enabled = false;
                threadKillSignal = true;
                server.ClientDisconnected(uid);
            }
        }

        void Send()
        {
            while (true)
            {
                if (threadKillSignal)
                {
                    return;
                }
                Packet packet = outgoingPackets.Take();
                byte[] rawData = Encoding.UTF8.GetBytes(packet.json.PadRight(Constants.Networking.MAX_PACKET_SIZE, ' '));
                try
                {
                    stream.Write(rawData, 0, rawData.Length);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error sending packet data");
                    server.ClientDisconnected(uid);
                    return;
                }

            }
        }

        void Listen()
        {
            while (true)
            {
                if (threadKillSignal)
                {
                    return;
                }

                Byte[] rawData = new Byte[Constants.Networking.MAX_PACKET_SIZE];
                Int32 bytes = 0;
                try
                {
                    bytes = stream.Read(rawData, 0, rawData.Length);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                }

                //Check if bytes == 0, if so, connection was dropped
                if (bytes == 0)
                {
                    server.ClientDisconnected(uid);
                    return;
                }

                String data = Encoding.UTF8.GetString(rawData, 0, bytes);
                HandlePacket(Packet.Parse(data));
            }
        }

        void HandlePacket(Packet packet)
        {
            if (packet == null)
            {
                return;
            }
            if (!initialized)
            {
                if (packet.type.Equals(Constants.Networking.PacketTypes.WORLD_INIT))
                {
                    initialized = true;
                    foreach (KeyValuePair<Location, Building> bkv in server.worldDataManager.buildings)
                    {
                        bkv.Value.tileTimestamp = server.worldDataManager.GetTileTimestamp(bkv.Value.location);
                        Packet p = new Packet(Constants.Networking.PacketTypes.ENTITY_CREATE, bkv.Value);
                        outgoingPackets.Add(p);
                    }
                    foreach (KeyValuePair<Location, RoadTile> rkv in server.worldDataManager.roadTiles)
                    {
                        rkv.Value.tileTimestamp = server.worldDataManager.GetTileTimestamp(rkv.Value.location);
                        Packet p = new Packet(Constants.Networking.PacketTypes.ENTITY_CREATE, rkv.Value);
                        outgoingPackets.Add(p);
                    }
                }
                return;
            }

            KeyValuePair<string, Packet> kv = new KeyValuePair<string, Packet>(this.uid, packet);
            if (packet.type.Equals(Constants.Networking.PacketTypes.ENTITY_CREATE))
            {
                server.worldDataManager.entityUpdateQueue.Add(kv);
            }

            if (packet.type.Equals(Constants.Networking.PacketTypes.ENTITY_DELETE))
            {
                server.worldDataManager.entityUpdateQueue.Add(kv);
            }
        }
    }
}
