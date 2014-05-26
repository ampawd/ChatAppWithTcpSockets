using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace WPFClientApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public partial class MainWindow : Window
    {
        private SynchronizationContext GUIContext = SynchronizationContext.Current;

        private Socket _serverSocket = new Socket(
            AddressFamily.InterNetwork,
            SocketType.Stream,
            ProtocolType.Tcp
        );

        private byte[] _recievedFromAllBuffer = new byte[512];

        private bool _isJoined = false;

        private bool _usersListUpdated = false;

        private byte[] _chatUsersListBuff = new byte[512];

        private string _nickName = string.Empty;

        Thread tr;

        public MainWindow()
        {
            InitializeComponent();
            //  tr = new Thread(new ParameterizedThreadStart());
        }

        private void JoinChatBtn_Click(object sender, RoutedEventArgs e)
        {
            connectToServer();
        }

        private bool connectToServer()
        {
            if (!SocketConnected(_serverSocket) && nickNameTextBox.Text != string.Empty)
            {                
                try
                {
                    _serverSocket.BeginConnect(IPAddress.Loopback, 1555,
                        OnConnectedCallback, _serverSocket);

                    JoinChatBtn.IsEnabled = false;
                    _nickName = nickNameTextBox.Text;
                    return true;
                }
                catch (SocketException)
                {                    
                    ChatLog.Clear();
                    this.ResetConnection();
                    return false;
                }
            }
            return false;
        }
       
        private void OnConnectedCallback(IAsyncResult asyncResult)
        {
            Socket ss = (Socket)asyncResult.AsyncState;
            try
            {
                ss.EndConnect(asyncResult);
                _isJoined = true;

                this.StartGlobalActionsLoop(ss);           
            }
            catch (SocketException) {}

            this.UpdateJoinStatus();
        }

        private void StartGlobalActionsLoop(Socket ss)
        {
            byte[] _nickBuff = new byte[32];

            _nickBuff = Encoding.UTF8.GetBytes(_nickName);
            ss.BeginSend(_nickBuff, 0, _nickBuff.Length,
                SocketFlags.None, OnNickSendCallback, ss);
        }

        private void OnNickSendCallback(IAsyncResult asyncResult)
        {
            Socket ss = (Socket)asyncResult.AsyncState;
            ss.EndSend(asyncResult);

            //this.UpdateUsrList(ss);
            this.StartMessageRecieveLoop(ss);            
        }

        private void UpdateUsrList(Socket ss)
        {
            if (ss != null && SocketConnected(ss))
            {
                ss.BeginReceive(_chatUsersListBuff, 0, _chatUsersListBuff.Length,
                    SocketFlags.None, OnChatUsrListReceivedCallback, ss);
            }
        }

        private void OnChatUsrListReceivedCallback(IAsyncResult asyncResult)
        {
            Socket ss = (Socket)asyncResult.AsyncState;
            if (SocketConnected(ss))
            {
                int recieved = ss.EndReceive(asyncResult);
                if (recieved != 0)
                {
                    byte[] packet = new byte[recieved];
                    Array.Copy(_chatUsersListBuff, packet, recieved);
                    _nickName = Encoding.UTF8.GetString(packet);

                    GUIContext.Post(delegate
                    {
                         Allusers.Items.Add(_nickName + '\n');
                    }, null);
                }                
            }            
        }

        private void StartMessageRecieveLoop(Socket ss)
        {
            if (ss != null && SocketConnected(ss))
            {
                ss.BeginReceive(_recievedFromAllBuffer, 0, _recievedFromAllBuffer.Length,
                    SocketFlags.None, OnChatMsgAllRecievedCallback, ss);
            } 
        }

        private void SendMessageBtn_Click(object sender, RoutedEventArgs e)
        {
            GUIContext.Post(delegate
            {
                if (!SocketConnected(_serverSocket) || messageInput.Text == string.Empty)
                {
                    return;
                }

                string message = nickNameTextBox.Text + ":  " + messageInput.Text;

                byte[] _chatMsgBuffer = Encoding.UTF8.GetBytes(message);

                _serverSocket.BeginSend(_chatMsgBuffer, 0, _chatMsgBuffer.Length,
                    SocketFlags.None, OnChatMsgSendCallback, _serverSocket);

                messageInput.Clear();
            }, null);            
        }

        private void OnChatMsgSendCallback(IAsyncResult asyncResult)
        {
            Socket ss = (Socket)asyncResult.AsyncState;
            if (ss != null && SocketConnected(ss))
            {
                ss.EndSend(asyncResult);
                ss.BeginReceive(_recievedFromAllBuffer, 0, _recievedFromAllBuffer.Length,
                    SocketFlags.None, OnChatMsgAllRecievedCallback, ss);
            }
        }

        private void OnChatMsgAllRecievedCallback(IAsyncResult asyncResult)
        {
            Socket ss = (Socket)asyncResult.AsyncState;
            if (ss != null && SocketConnected(ss))
            {
                int recieved = ss.EndReceive(asyncResult);

                byte[] packet = new byte[recieved];
                Array.Copy(_recievedFromAllBuffer, packet, recieved);

                GUIContext.Post(delegate
                {
                    ChatLog.AppendText(Encoding.UTF8.GetString(packet) + '\n');
                }, null);
                
                ss.BeginReceive(_recievedFromAllBuffer, 0, _recievedFromAllBuffer.Length,
                    SocketFlags.None, OnChatMsgAllRecievedCallback, ss);
            }
        }

        private void messageInput_KeyDown(object sender, KeyEventArgs e)
        {           
            if (e.Key == Key.Enter)
            {
                SendMessageBtn_Click(this, new RoutedEventArgs());
            }
        }

        private void QuitBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SocketConnected(_serverSocket))
                {
                    SendMessageBtn.IsEnabled = false;
                    _serverSocket.Shutdown(SocketShutdown.Both);
                    _serverSocket.BeginDisconnect(false, OnDisconnectCallback, _serverSocket);
                }
            }
            catch (SocketException)
            { }
        }

        private void OnDisconnectCallback(IAsyncResult asyncResult)
        {
            Socket ActiveSocket = (Socket)asyncResult.AsyncState;
            try
            {
                if (ActiveSocket != null)
                    ActiveSocket.EndDisconnect(asyncResult);

                GUIContext.Post(delegate
                {
                    ChatLog.Clear();
                    Allusers.Items.Clear();
                    ChatLog.AppendText("You left the chat...");
                }, null);

                _serverSocket = new Socket(
                        AddressFamily.InterNetwork,
                        SocketType.Stream,
                        ProtocolType.Tcp
                );
            }
            catch (SocketException sex)
            {
                GUIContext.Post(delegate
                {
                    ChatLog.Clear();
                    ChatLog.AppendText("Disconnection error: " + sex.Message + '\n');
                    SendMessageBtn.IsEnabled = false;
                }, null);
            }
        }

        private void RemoveNickName()
        {
            /*if (_nickName != string.Empty)
            {
                Allusers.Items.Remove(_nickName);
                _nickName = string.Empty;
            }*/
        }

        private bool SocketConnected(Socket ss)
        {
            return !((ss.Poll(1000, SelectMode.SelectRead) && (ss.Available == 0)) || !ss.Connected);
        }

        private void ResetConnection()
        {
            _serverSocket.Shutdown(SocketShutdown.Both);
            _serverSocket.Dispose();
            _serverSocket.Close();
            _serverSocket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
            );
        }

        private void UpdateJoinStatus()
        {
            if (_isJoined && SocketConnected(_serverSocket))
            {
                GUIContext.Post(delegate
                {
                    ChatLog.Clear();
                    ChatLog.AppendText("You have connected to the chat!...\n\n");
                    SendMessageBtn.IsEnabled = true;
                    JoinChatBtn.IsEnabled = true;
                }, null);
            }
            else
            {
                GUIContext.Post(delegate
                {
                    ChatLog.Clear();
                    ChatLog.AppendText("Sorry, but chat server is not available at the moment...\n\n");
                    SendMessageBtn.IsEnabled = false;
                    JoinChatBtn.IsEnabled = true;
                }, null);
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            var response = MessageBox.Show("Do you really want to leave the chat?", "Exiting...",
                                   MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
            if (response == MessageBoxResult.No)
            {
                e.Cancel = true;
            }
            else
            {
                if (SocketConnected(_serverSocket))
                {
                    //_clientSocket.Dispose();
                    _serverSocket.Shutdown(SocketShutdown.Both);
                    _serverSocket.Close();
                }
								
                Application.Current.Shutdown();
            }
        }
    }
}
