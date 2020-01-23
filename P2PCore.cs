using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;

namespace Miless
{
    class P2PCore
    {
        private const int bufferSize = 16 * 1024 * 1024;

        private Thread threadReceive = null;
        private InterThreads inter = null;

        private byte[] receiveBuffer = null;

        private TcpListener tcplistener = null;
        private TcpClient tcpsender = null;
        public IPAddress IP = null;

        public const int Port = 15120;
        public const int ttPort = 15120;
        private static P2PCore instance = null;

        private UdpClient Udplistener = null;
        private UdpClient UdpSender = null;
        private IPEndPoint endpoint = null;
        private Thread threadUdpreceive = null;

        private List<UdpData> UdpMessageList = null;
        private Thread threadCount = null;

        private CSCore CSCore_instance = null;

        
        public static P2PCore GetInstance()
        {
            if(instance == null)
            {
                instance = new P2PCore();
            }
            return instance;
        }
        public class UdpData
        {
            public byte[] data = null;
            public Stopwatch timer = null;
            public string targetIp = null;
            public int targetport = 0;
            public UdpData(byte[] data, string tarip, int tarport)
            {
                this.data = data;
                this.timer = new Stopwatch();
                this.timer.Start();
                this.targetIp = tarip;
                this.targetport = tarport;
            }
            public long GetTime()
            {
                timer.Stop();
                var t = timer.ElapsedMilliseconds;
                timer.Start();
                return t;
            }
        }

        private P2PCore()
        {
            receiveBuffer = new byte[bufferSize];
            //内部通信线程
            inter = InterThreads.GetInstance();
            //本机IP
            IP = GetMyIPv4();
            endpoint = new IPEndPoint(IP, Port);
            //tcp和udp监听线程
            tcplistener = new TcpListener(IP, Port);     
            Udplistener = new UdpClient(endpoint);
            //初始化已发送的Udp的消息的List
            UdpMessageList = new List<UdpData>();

            //用于发送Udp ack信号的CSCore
            CSCore_instance = CSCore.GetInstance();

            

        }
        public void SendData(byte[] data, string targetIP, int targetPort)
        {
            //tcp发送
            using (tcpsender = new TcpClient())
            {
                tcpsender.SendTimeout = 2000;
                tcpsender.ReceiveTimeout = 2000;
                tcpsender.SendBufferSize = bufferSize;
                try
                {
                    tcpsender.Connect(targetIP, targetPort);
                }
                catch (Exception e)
                {
                    MessageBox.Show("TCP连接失败, 目标IP: "+targetIP);
                    return;
                }
                //流传输
                using (NetworkStream stream = tcpsender.GetStream())
                {
                    stream.Write(data, 0, data.Length);
                    stream.Close();
                }
                tcpsender.Close();
            }
        }

        public void SendUDPData(byte[] data, string targetIP, int targetPort)
        {
            //UDP传输
            var targetendpoint = new IPEndPoint(IPAddress.Parse(targetIP), targetPort);
            using (UdpSender = new UdpClient())
            {
                UdpSender.Send(data, data.Length, targetendpoint);
                UdpSender.Close();
            }
        }
        public void BeginListen()
        {
            //begin Tcp listen            
            threadReceive = new Thread(AcceptReceive)
            {
                Name = "MyNetMessage"
            };
            lock (inter)
            {
                inter.listening = true;
            }            
            tcplistener.Start();            
            threadReceive.Start();

            //begin Udp listen            
            threadUdpreceive = new Thread(AccecptUdp)
            {
                Name = "MyUdpMessage"
            };
            threadUdpreceive.Start();

            threadCount = new Thread(CountUdp)
            {
                Name = "MyUdpCount"
            };
            threadCount.Start();
            
        }
        public void EndListen()
        {
            lock (inter)
            {
                inter.listening = false;
            }
            //关闭线程
            threadReceive.Join();
            threadUdpreceive.Abort();
            threadCount.Abort();
            //关闭监听
            tcplistener.Stop();
            Udplistener.Close();


        }
        public void AddUdpMessage(byte[] data, string targetIP, int targetPort)
        {
            UdpData udp = new UdpData(data, targetIP, targetPort);
            UdpMessageList.Add(udp);
        }
        private void CountUdp()
        {
            while (UdpMessageList != null)
            {
                Thread.Sleep(1000);
                for (int i = 0;i<UdpMessageList.Count;i++)
                {
                    var udp = UdpMessageList[i];
                    if (udp.GetTime() > 50000)
                    {
                        //超时重传.
                        MessageBox.Show("Udp消息未收到Ack.超时.进行重传"); 
                        udp.timer.Restart();
                        SendUDPData(udp.data, udp.targetIp, udp.targetport);
                    }
                }
            }
        }
        private void AccecptUdp()
        {
            bool OnGoing = true;
            while (OnGoing)
            {
                //todo:加功能,发送端的计时器
                //收到udp
                Thread.Sleep(50);
                try
                {
                    var buff = Udplistener.Receive(ref endpoint);
                    int flag = 0;
                    lock (inter)
                    {
                        MyDataGram pro = MyDataGram.DecodeMessage(buff); 
                        if(pro.Text.Length > 7)
                        {
                            if (pro.Text.Substring(0, 7) == "__ack__")
                            {
                                for (int i = 0; i < UdpMessageList.Count; i++)
                                {
                                    var tempudp = MyDataGram.DecodeMessage(UdpMessageList[i].data);
                                    if (tempudp.SrcID == pro.DstID && tempudp.DstID == pro.SrcID)
                                    {
                                        if (tempudp.Text == pro.Text.Substring(7))
                                        {
                                            //成功收到了对应的ack
                                            MessageBox.Show("收到了ACK报文.");
                                            UdpMessageList.RemoveAt(i);
                                            flag = 1;
                                        }
                                    }
                                }
                            }
                        }
                        
                        if (pro.Type != MessageType.Disable)
                        {
                            if (pro.Text.Length < 7)
                            {
                                SendAck(buff);
                            }
                            else if(pro.Text.Substring(0, 7) != "__ack__")
                            {
                                SendAck(buff);
                            }

                        }
                        if (flag == 0)
                        {
                            inter.messages.Enqueue(buff);
                        }
                    }
                }
                catch
                {
                    //do nothing
                }
                lock (inter)
                {
                    OnGoing = inter.listening;
                }
            }
        }
        private void SendAck(byte[] data)
        {
            //发送ACK报文
            MyDataGram pro = MyDataGram.DecodeMessage(data);
            MyDataGram ack = new MyDataGram();
            ack.DstID = pro.SrcID;
            ack.SrcID = pro.DstID;
            ack.Text = "__ack__"+pro.Text;
            ack.Type = MessageType.Text;
            string tempIP = "";

            var resp = CSCore_instance.Query("q"+pro.SrcID);
            if (resp == "n")
            {
                MessageBox.Show("当前好友不在线!");
                return;
            }
            else
            {
                tempIP = resp;
            }
            SendUDPData(MyDataGram.EncodeMessage(ack), tempIP, ttPort);//danger!
        }

        private void AcceptReceive()
        {
            bool OnGoing = true;
            while (OnGoing)
            {
                if (tcplistener.Pending())
                {
                    //有数据
                    TcpClient client = null;                    
                    client = tcplistener.AcceptTcpClient();   
                    client.ReceiveBufferSize = bufferSize;
                    using (NetworkStream stream = client.GetStream())
                    {
                        int bufferUnit = 1024 * 1024;
                        int len = 0;
                        byte[] buff = new byte[bufferUnit];
                        if (stream.CanRead)
                        {
                            do
                            {
                                //连续读入数据
                                int unitLen = 0;                                
                                unitLen = stream.Read(buff, 0, bufferUnit);                                
                                Buffer.BlockCopy(buff, 0, receiveBuffer, len, unitLen);
                                len += unitLen;
                                Thread.Sleep(50);
                            } while (stream.DataAvailable);
                        }
                        stream.Close();
                        byte[] msg = new byte[len];
                        Buffer.BlockCopy(receiveBuffer, 0, msg, 0, len);
                        lock (inter)
                        {
                            //放入缓存队列中
                            inter.messages.Enqueue(msg);
                        }
                    }
                    client.Close();
                }
                lock (inter)
                {
                    OnGoing = inter.listening;
                }
            }
        }


        private IPAddress GetMyIPv4()
        {
            //获得本机IP地址            
            string HostName = Dns.GetHostName();
            IPHostEntry IpEntry = Dns.GetHostEntry(HostName);
            for (int i = 0; i< IpEntry.AddressList.Length; i++)
            {
                if(IpEntry.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
                {
                    return IpEntry.AddressList[i];
                }
            }
            return null;            
        }
    }
}
