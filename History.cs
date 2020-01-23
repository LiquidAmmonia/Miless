using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Miless
{
    class History
    {
        public List<ChattingMessage> ConvertMessageList(ObservableCollection<ChattingMessage> OClist)
        {
            List<ChattingMessage> rst = new List<ChattingMessage>();
            foreach (var msg in OClist)
            {
                rst.Add(msg);
            }
            return rst;
        }
        public void SaveHistory(string MyID, string FriID, ObservableCollection<ChattingMessage> OClist)
        {
            /*
            数据格式: ME_xxx_FRI_xxx.txt
            发送方$信息类型$内容$时间
            ...
            #time
            */
            string Time = DateTime.Now.ToLongDateString().ToString();
            var list = ConvertMessageList(OClist);
            List<string> SaveMessageList = new List<string>();
            foreach(var msg in list)
            {
                string temp = "";
                temp = msg.SrcID + "$"+ msg.CMType.ToString() + "$" + msg.Content + "$"+msg.SendTime;
                SaveMessageList.Add(temp);
                
            }
            string FileName = "ME_" + MyID + "_FRIEND_" + FriID + ".txt";


            File.Delete(@".\data\"+FileName);
            var fs = new FileStream(@".\data\" + FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            StreamWriter sw = new StreamWriter(fs);
            //规定数据结构:FriendID_FriendAlias
            foreach (var smsg in SaveMessageList)
            {                
                sw.WriteLine(smsg);
            }
            sw.Flush();
            sw.Close();
            fs.Close();            
        }

        public void SaveOneHistory(MyDataGram mdg)
        {
            /*
            数据格式: ME_xxx_FRI_xxx.txt
            发送方$信息类型$内容$时间
            ...
            #time
            */
            string Time = DateTime.Now.ToLongDateString().ToString();
            var MyID = mdg.DstID;
            var FriID = mdg.SrcID;
            string temp = "";
            temp = MyID + "$" + mdg.Type.ToString() + "$" + mdg.Text + "$" + Time;
                

            string FileName = "ME_" + MyID + "_FRIEND_" + FriID + ".txt";


            File.Delete(@".\data\" + FileName);
            var fs = new FileStream(@".\data\" + FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            StreamWriter sw = new StreamWriter(fs);
            //规定数据结构:FriendID_FriendAlias
            
            sw.WriteLine(temp);            
            sw.Flush();
            sw.Close();
            fs.Close();
        }
        private int _MessageTypeConverter(string s)
        {
            if (s == "Text")
            {
                return 1;
            }
            else if (s == "File")
            {
                return 2;
            }
            else if (s == "Image")
            {
                return 3;
            }
            else
            {
                return 0;
            }
        }

        public ObservableCollection<ChattingMessage> LoadHistory(string MyID, string FriID, ObservableCollection<ChattingMessage> old)
        {
            /*
            数据格式: ME_xxx_FRI_xxx.txt
            发送方$信息类型$内容$时间
            ...
            #time
            */
            var rst = old;
            string FileName = "ME_" + MyID + "_FRIEND_" + FriID + ".txt";

            var fs = new FileStream(@".\data\" + FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            StreamReader sr = new StreamReader(fs);
            var line = sr.ReadLine();

            List<String> temp = new List<string>();
            while ( line != null)
            {
                temp.Add(line);
                line = sr.ReadLine();
            }
            sr.Close();
            fs.Close();

            foreach (var t in temp)
            {
                ChattingMessage tcm = new ChattingMessage();
                
                var arr = t.Split('$');
                if (arr[0] == MyID)
                {
                    tcm.SrcID = MyID;
                    tcm.DstID = FriID;
                }
                else
                {
                    tcm.SrcID = FriID;
                    tcm.DstID = MyID;
                }
                tcm.CMType = (MessageType)_MessageTypeConverter(arr[1]);
                tcm.Content = arr[2];
                tcm.SendTime = arr[3];
                if(tcm.CMType== MessageType.Image)
                {
                    var fullpath = Path.GetFullPath(tcm.Content);
                    try
                    {
                        tcm.BitMapSource = new BitmapImage(new Uri(fullpath, UriKind.Absolute));

                    }catch(FileNotFoundException e)
                    {
                        tcm.Content = "已经删除的文件" + tcm.Content;
                    }
                }
                if (tcm.SrcID == MyID)
                {
                    tcm.hori = HorizontalAlignment.Right;
                }
                rst.Add(tcm);
            }
            return rst;


        }
    }
}
