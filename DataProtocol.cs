using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Miless
{
    
    public class InterThreads
    {
        //内部信息传递
        public static InterThreads instance = null;
        public bool listening = false;
        public bool processing = false;
        public Queue<byte[]> messages = null;

        private InterThreads()
        {
            messages = new Queue<byte[]>();
        }

        public static InterThreads GetInstance()
        {
            //Singleton
            if (instance == null)
            {
                instance = new InterThreads();
            }
            return instance;
        }
    }
    class DataProtocol
    {

    }

    public enum MessageType
    {
        Disable = 0,
        Text = 1,
        File = 2, 
        Image = 3
    }
    public class ChattingMessage
    {
        public MessageType CMType { get; set; }
        public string Content { get; set; }
        public string SrcID { get; set; }
        public string DstID { get; set; }
        public string SendTime { get; set; }
        public BitmapImage BitMapSource { get; set; }
        public HorizontalAlignment hori { get; set; }
        public ChattingMessage()
        {
            CMType = MessageType.Disable;
            Content = "";
            SrcID = "";
            DstID = "";
            SendTime = "";
            BitMapSource = null;
            hori = HorizontalAlignment.Left;
        }
    }


    public class MyDataGram
    {
        //校验和
        private const uint BeginMatch = 1213812138;
        private const uint EndMatch = 2341023410;

        public string SrcID { get; set; }
        public string DstID { get; set; }
        public string Text { get; set; }
        public MessageType Type { get; set; }
        public byte[] FileContent { get; set; }
        public string GroupID { get; set; }

        public string GroupFileID { get; set; }
        public int GroupFileIndex { get; set; }

        public MyDataGram()
        {
            Type = MessageType.Disable;
            SrcID = null;
            DstID = null;
            Text = null;
            FileContent = null;
            GroupID = null;
            GroupFileID = null;
            GroupFileIndex = -1;
        }

        public static byte[] EncodeMessage(MyDataGram mdg)
        {
            /* Protocol
             Begin Match: 4B
             SrcID: 4B
             DstID: 4B
             File Name: 4B
             File Part Length: 4B
             GroupID Length: 4B
             GroupID: ???

             GroupFileIDLength: 4B
             GroupFileID: ???
             GroupFileIndex: 4B

             DATA max = 32MB
             End Match: 4B
            */
            List<byte> tempmsg = new List<byte>();
            bool isFile = false;
            int FileLength = -1;
            int FileNameLength = -1;
            int GroupIdLength = -1;
            int GroupFileIdLength = -1;
            if(mdg.GroupID!=null)
                GroupIdLength = Encoding.UTF8.GetByteCount(mdg.GroupID);
            if (mdg.Type == MessageType.File|| mdg.Type == MessageType.Image)
            {
                isFile = true;
                FileLength = mdg.FileContent.Length;
                if (GroupIdLength != -1)
                {
                    GroupFileIdLength = Encoding.UTF8.GetByteCount(mdg.GroupFileID);
                }
            }

            FileNameLength = Encoding.UTF8.GetByteCount(mdg.Text);
            tempmsg.AddRange(BitConverter.GetBytes(BeginMatch));
            tempmsg.AddRange(BitConverter.GetBytes((uint)mdg.Type));            
            tempmsg.AddRange(BitConverter.GetBytes(Convert.ToUInt32(mdg.SrcID)));
            tempmsg.AddRange(BitConverter.GetBytes(Convert.ToUInt32(mdg.DstID)));
            tempmsg.AddRange(BitConverter.GetBytes(FileNameLength));
            tempmsg.AddRange(BitConverter.GetBytes(FileLength));
            tempmsg.AddRange(BitConverter.GetBytes(GroupIdLength));
            if(GroupIdLength!=-1)
                tempmsg.AddRange(Encoding.UTF8.GetBytes(mdg.GroupID));
            tempmsg.AddRange(Encoding.UTF8.GetBytes(mdg.Text));
            tempmsg.AddRange(BitConverter.GetBytes(GroupFileIdLength));

            if (isFile)
            {               
                if(GroupIdLength != -1)
                {
                    //P2PFile
                    tempmsg.AddRange(Encoding.UTF8.GetBytes(mdg.GroupFileID));
                    tempmsg.AddRange(BitConverter.GetBytes(mdg.GroupFileIndex));
                }
                tempmsg.AddRange(mdg.FileContent);
            }
            tempmsg.AddRange(BitConverter.GetBytes(EndMatch));
            return tempmsg.ToArray();
        }
        public static MyDataGram DecodeMessage(byte[] msg)
        {
            /* Protocol
             Begin Match: 4B
             SrcID: 4B
             DstID: 4B
             File Name: 4B
             File Part Length: 4B
             GroupID Length: 4B
             GroupID: ??
             
             GroupFileIDLength: 4B
             GroupFileID: ???
             GroupFileIndex: 4B

             DATA max = 32MB
             End Match: 4B
            */
            int MessageLength = msg.Length;
            MyDataGram result = new MyDataGram();
            uint msgBeginMatch = BitConverter.ToUInt32(msg, 0);
            uint msgEndMatch = BitConverter.ToUInt32(msg, MessageLength - 4);
            if(msgBeginMatch!= BeginMatch|| msgEndMatch!= EndMatch)
            {
                return result;
            }
            uint type = BitConverter.ToUInt32(msg, 4);
            result.Type = (MessageType)type;
            result.SrcID = BitConverter.ToUInt32(msg, 8).ToString();
            result.DstID = BitConverter.ToUInt32(msg, 12).ToString();
            int FileNameLength = BitConverter.ToInt32(msg, 16);
            int FileLength = BitConverter.ToInt32(msg, 20);
            int GroupIDLength = BitConverter.ToInt32(msg, 24);
            if (GroupIDLength != -1)
            {
                byte[] GroupNamePart = new byte[GroupIDLength];
                Array.Copy(msg, 28, GroupNamePart, 0, GroupIDLength);
                result.GroupID = Encoding.UTF8.GetString(GroupNamePart);
            }
            else
            {
                result.GroupID = "";
                GroupIDLength = 0;
            }
            if (type == 1)
            {
                byte[] FileNamePart = new byte[FileNameLength];
                Array.Copy(msg, 28 + GroupIDLength, FileNamePart, 0, FileNameLength);
                result.Text = Encoding.UTF8.GetString(FileNamePart);
                
            }
            else 
            {
                byte[] FileContent = new byte[FileLength];
                byte[] FileNamePart = new byte[FileNameLength];
                Array.Copy(msg, 28 + GroupIDLength, FileNamePart, 0, FileNameLength);
                int GroupFileIDLength = BitConverter.ToInt32(msg, 28 + GroupIDLength + FileNameLength);
                if (GroupFileIDLength >= 0)
                {
                    //P2P
                    byte[] GroupFileNamePart = new byte[GroupFileIDLength];
                    Array.Copy(msg, 32 + GroupIDLength + FileNameLength, GroupFileNamePart, 0, GroupFileIDLength);
                    result.GroupFileID = Encoding.UTF8.GetString(GroupFileNamePart);

                    result.GroupFileIndex = BitConverter.ToInt32(msg, 32 + GroupIDLength + FileNameLength + GroupFileIDLength);
                    
                    Array.Copy(msg, 36 + GroupIDLength + FileNameLength + GroupFileIDLength, FileContent, 0, FileLength);
                }
                else
                {
                    
                    Array.Copy(msg, 32 + GroupIDLength + FileNameLength, FileContent, 0, FileLength);
                }
                result.Text = Encoding.UTF8.GetString(FileNamePart);
                result.FileContent = FileContent;
            }
            return result;


        }

    }
}
