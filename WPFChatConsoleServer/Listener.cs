using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace WPFChatConsoleServer
{
    class ChatServer
    {
        public int Port
        {
            get;
            private set;
        }

        public bool Listening
        {
            get;
            private set;
        }

        private Socket _listener;

        public Dictionary<string, Socket> _clientsList = new Dictionary<string, Socket>();

        private byte[] _recievedFromAllBuffer = new byte[512];

        private byte[] _UsrListBuff = new byte[32];

        private string _LastJoinedUserNickName = string.Empty;

        private bool _nickNameReceived = false;

        Dictionary<string, string> _usersList = new Dictionary<string, string>();

        private Thread _ChatUserListThread;

       // private SqlConnection _sqlHandle = new SqlConnection(@"Data Source=D:\C#C#PROGRAMMING STUFF\Projects\WPFChatConsoleServer\WPFChatConsoleServer\Chat.sdf");

        //private SqlCommand _cmd = new SqlCommand(); 

        public ChatServer(int Port)
        {
            this.Port = Port;
            _listener = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp);

            //_cmd.Connection = _sqlHandle;
            //_sqlHandle.Open();
        }

        public void Start()
        {
            if (Listening) return;

            _listener.Bind(new IPEndPoint(IPAddress.Any, this.Port));
            _listener.Listen(500);
            _listener.BeginAccept(AcceptCallback, _listener);
            //_ChatUserListThread = new Thread(new ParameterizedThreadStart(SendChatUserListUpdate));
            Listening = true;
        }

        public void close()
        {
            if (!Listening) return;

            //_ssocket.Shutdown(SocketShutdown.Both);
            //_listener.Close();
            //_listener.Dispose();
            //_listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //_sqlHandle.Close();
        }

        private void AcceptCallback(IAsyncResult asyncResult)
        {
            Socket cs = this._listener.EndAccept(asyncResult);
            try
            {
                if (SocketConnected(cs))
                {
                    cs.BeginReceive(_UsrListBuff, 0, _UsrListBuff.Length,
                        SocketFlags.None, SendUserListCallback, cs);                     
                 }                
            }
            catch (SocketException sex)
            {   
                Console.WriteLine("ACCEPT ERROR: " + sex.Message);
            }

            this._listener.BeginAccept(AcceptCallback, cs);
        }

        private void SendUserListCallback(IAsyncResult asyncResult)
        {
            Socket cs = (Socket)asyncResult.AsyncState; 
            string _nickName = string.Empty;
            int recieved = cs.EndReceive(asyncResult);

            if (recieved != 0 && recieved < _UsrListBuff.Length)
            {
                byte[] packet = new byte[recieved];
                Array.Copy(_UsrListBuff, packet, packet.Length);
                _nickName = Encoding.UTF8.GetString(packet);

                Console.WriteLine(_nickName + " has connected/" + cs.RemoteEndPoint.ToString());

                _clientsList.Add(cs.RemoteEndPoint.ToString(), cs);
                _usersList.Add(cs.RemoteEndPoint.ToString(), _nickName);

                if (_clientsList.Count() >= 1)
                {
                    //this.BroadCastUsrList(_nickName, recieved);
                }

                this.BeginMsgsReceiveLoop(cs);  
            }                   
        }

        private void BroadCastUsrList(string data, int recieved)
        {
            foreach (var client in _clientsList.Values)
            {
                byte[] packet = new byte[recieved];
                Array.Copy(Encoding.UTF8.GetBytes(data + '\n'), packet, packet.Length);

                client.BeginSend(packet, 0, packet.Length,
                    SocketFlags.None, sendToClient, client);
            }
        }

        private void BeginMsgsReceiveLoop(Socket cs)
        {
            if (SocketConnected(cs))
            {
                cs.BeginReceive(_recievedFromAllBuffer, 0, _recievedFromAllBuffer.Length,
                    SocketFlags.None, OnClientMsgsRecievedCallback, cs);
            }
        }

        private void OnClientMsgsRecievedCallback(IAsyncResult asyncResult)
        {
            Socket cs = (Socket)asyncResult.AsyncState;            
            try
            {
                if (cs != null && SocketConnected(cs))
                {
                    int recieved = cs.EndReceive(asyncResult);

                    if (recieved != 0 && recieved < _recievedFromAllBuffer.Length)
                    {
                        byte[] packet = new byte[recieved];
                        Array.Copy(_recievedFromAllBuffer, packet, packet.Length);
                        Console.WriteLine("Data recieved: " + Encoding.UTF8.GetString(packet));

                        if (_clientsList.Count() >= 1)
                        {
                            this.BroadCastMessage(
                                Encoding.UTF8.GetString(packet),
                            recieved);                            
                        }
                    }                    
                }
                else
                {
                    string value;
                    foreach (var key in _usersList.Keys)
                    {
                        if (cs.RemoteEndPoint.ToString() == key)
                        {
                            _clientsList.Remove(key);
                            if (_usersList.TryGetValue(key, out value))
                            {
                                Console.WriteLine(value + " - has disconnected!");
                                _usersList.Remove(key);
                                return;
                            }
                        }
                    }                   
                }
            }
            catch (SocketException sex)
            {
                Console.WriteLine("RECIEVING DATA ERROR: " + sex.Message);
            }
          
            cs.BeginReceive(_recievedFromAllBuffer, 0, _recievedFromAllBuffer.Length,
                SocketFlags.None, OnClientMsgsRecievedCallback, cs);           
        }

        private void BroadCastMessage(string data, int recieved)
        {
            foreach (var client in _clientsList.Values)
            {
                byte[] packet = new byte[recieved];
                Array.Copy(Encoding.UTF8.GetBytes(data + '\n'), packet, packet.Length);
                //Console.WriteLine("data to be sent: " + Encoding.UTF8.GetString(packet));

                client.BeginSend(packet, 0, packet.Length,
                    SocketFlags.None, sendToClient, client);
            }
        }        

        private void sendToClient(IAsyncResult asyncResult)
        {
            Socket client = (Socket)asyncResult.AsyncState;
            client.EndSend(asyncResult);
        }

        private bool SocketConnected(Socket cs)
        {
            return !((cs.Poll(1000, SelectMode.SelectRead) && (cs.Available == 0)) || !cs.Connected);
        }
    }
}
