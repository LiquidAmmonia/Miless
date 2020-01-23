using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

using System.Windows;

namespace Miless
{
    class CSCore
    {
        private IPEndPoint ServerEndPoint = null;
        private Socket SocketToServer = null;

        //服务器信息
        public const string IPServer = "166.111.140.57";
        public const int PortServer = 8000;
        private const int bufferSize = 64;


        private static CSCore instance = null;
        //单例模式"懒汉式"
        public static CSCore GetInstance()
        {
            if (instance == null)
            {
                instance = new CSCore();
            }
            return instance;
        }
        private CSCore()
        {
            //创造IPEndPoint
            ServerEndPoint = new IPEndPoint(IPAddress.Parse(IPServer), PortServer);
        }
        
        public string Query(string message)
        {
            string receive = "NRP";
            SocketToServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                SendTimeout = 2000,
                ReceiveTimeout = 2000
            };
            
            SocketToServer.Connect(ServerEndPoint);
            
            //打包询问
            byte[] toSend = Encoding.UTF8.GetBytes(message);
            SocketToServer.Send(toSend);
            
            //直接接收解包
            byte[] buffer = new byte[bufferSize];
            int receivevLength = SocketToServer.Receive(buffer);
            receive = Encoding.UTF8.GetString(buffer, 0, receivevLength);            
            SocketToServer.Disconnect(true);            
            SocketToServer.Close();
            return receive;
        }
    }
}
