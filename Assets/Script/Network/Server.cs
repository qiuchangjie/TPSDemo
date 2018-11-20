using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Network {
    public class Server : EndPort {
        private Dictionary<string, Connection> connectionMap;

        public Server() : base() {
            this.connectionMap = new Dictionary<string, Connection>();
            this.RegisterHandler(MsgId.Heartbeat, this.Heartbeat);
        }

        public bool Listen(int port) {
            if (base.Init()) {
                this.udp = new UdpClient(port);
                this.Receive();

                return true;
            }

            return false;
        }

        public override void Update(float dt) {
            if (!this.Active) {
                return;
            }

            this.updateTime += dt;
            this.heartbeatTimer.Update(dt);
            var clock = this.ToKCPClock();
            
            foreach (var c in this.connectionMap) {
                c.Value.Update(clock);
            }
        }

        public void Send(IPEndPoint ep, byte id, MessageBase message=null) {
            var key = ep.ToString();

            if (this.connectionMap.ContainsKey(key)) {
                var connection = this.connectionMap[key];
                base.Send(connection, id, message);
            }
        }

        public void SendToAll(byte id, MessageBase message=null) {
            foreach (var c in this.connectionMap) {
                base.Send(c.Value, id, message);
            }
        }

        protected override void ReceiveCallback(IAsyncResult ar) {
            var buffer = this.udp.EndReceive(ar, ref EP);

            if (buffer != null) {
                string key = EP.ToString();
                
                if (!this.connectionMap.ContainsKey(key) && buffer[buffer.Length - 1] == MsgId.Connect) {
                    var ep = new IPEndPoint(EP.Address, EP.Port);
                    this.connectionMap.Add(key, new Connection(ep, this.SendWrap, this.Handle));
                }

                if (this.connectionMap.ContainsKey(key)) {
                    this.connectionMap[key].Input(buffer);
                }
            }

            this.Receive();
        }

        protected override void HeartbeatTick() {
            var removeList = new List<string>();
            
            foreach (var c in this.connectionMap) {
                if (!c.Value.heartbeat) {
                    removeList.Add(c.Key);
                }
                else {
                    c.Value.heartbeat = false;
                }
            }

            foreach (var k in removeList) {
                this.connectionMap.Remove(k);
            }

            this.heartbeatTimer.Enter();
        }

        private void Heartbeat(byte msgId, NetworkReader reader, IPEndPoint ep) {
            this.Send(ep, MsgId.Heartbeat);
            Debug.Log("Server Heartbeat " + ep.ToString());
        }
    }
}