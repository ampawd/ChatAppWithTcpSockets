using System;
using System.Threading;

namespace WPFChatConsoleServer
{
    class Program
    {
        static ChatServer newChatServer;               
        
        static void Main(string[] args)
        {  
            Console.Title = "Chat Server ... ";

            newChatServer = new ChatServer(1555);
            newChatServer.Start();

            Console.Read();
        }
    }
}
