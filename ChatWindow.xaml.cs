using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Windows.Threading;

namespace Miless
{
    /// <summary>
    /// ChatWindow.xaml 的交互逻辑
    /// </summary>
    /// 
    public delegate void cwEventHandler();
   
    public partial class ChatWindow : Window
    {
        public event cwEventHandler CloseEvent;

        private string friendID = "";
        private string friendAlias = "";
        private string friendIP = "";
        private string MyID = "";
        private string DisplayfriendID = "";
        

        private P2PCore P2PCore_instance = null;
        private CSCore CSCore_instance = null;

        private InterThreads inter = null;
        private ObservableCollection<ChattingMessage> ChattingMessageList = null;

        private const int FileMaxSize = 15 * 1024 * 1024;
        private byte[] FileBuffer = null;

        private const int SendDataPort = 15120;

        public List<string> GroupIDList = null;

        private byte[] P2PFileBuffer = null;
        private int[] P2PFileIndexPool = null;
        private List<string> P2PFileIDList = null;
        private int ActualFileLength = 0;
        private byte[] P2pMyOriginalPart = null;
        private int MyP2PIndex = 0;
        private int GroupInit = 0;
        private string P2PFileName = null;

        

        public ChatWindow()
        {
            
            InitializeComponent();
            CSCore_instance = CSCore.GetInstance();
            P2PCore_instance = P2PCore.GetInstance();
            //P2PCore_instance.BeginListen();

            FileBuffer = new byte[FileMaxSize];

            inter = InterThreads.GetInstance();
            ChattingMessageList = new ObservableCollection<ChattingMessage>();
        }
        public void SetMyID(string ID)
        {
            MyID = ID;
        }
        public void SetFriendInfo(string info)
        {
            var temp = info.Split('_');
            friendID = temp[0];
            friendAlias = temp[1];
            FriendName.Content = friendAlias;
            DisplayfriendID = friendID;
            FriendID.Content = DisplayfriendID;
            
            
            if(friendID[0]==' ')
            {
                //is group!
                GroupIDList = new List<string>();
                var arr = friendID.Split(' ');
                //save group info 
                foreach(var id in arr)
                {
                    if (id != "")
                    {
                        GroupIDList.Add(id);
                    }
                }
                if(!GroupIDList.Contains(MyID))
                    GroupIDList.Add(MyID);
                //update friendID
                friendID = " ";
                foreach(var id in GroupIDList)
                {
                    friendID += id + " ";
                }
                //greetings message
                string msg = "__init__";
                if (GroupInit == 0)
                {
                    SendGroupMessage(msg);
                }
                GroupInit = 1;

                //change window look
                icHead.Kind = MaterialDesignThemes.Wpf.PackIconKind.AccountMultiple;
                lbAlias.Content = "群组昵称";
                lbAlias.VerticalContentAlignment = VerticalAlignment.Center;
                lbAlias.FontSize = 14;
                FriendName.FontSize = 12;
                lbID.Content = "群组内学号";
                FriendID.FontSize = 12;
                lbID.FontSize = 14;
                lbID.VerticalContentAlignment = VerticalAlignment.Center;
                Sendbtn.Height = 150;
                SendUDPbtn.IsEnabled = false;
                SendUDPbtn.Width = 0;
                SendUDPbtn.Opacity = 0;
            }
        }
        private void SendGroupMessage(string text)
        {
            var msg = new ChattingMessage();
            foreach (var id in GroupIDList)
            {
                if(id == MyID)
                {
                    continue;
                }
                //check online
                var temp = "q" + id;
                var resp = CSCore_instance.Query(temp);
                if (resp == "n")
                {
                    //MessageBox.Show("当前好友不在线!");
                    //this.Close();
                    continue;
                }
                else
                {
                    friendIP = resp;
                }
                msg.CMType = MessageType.Text;
                msg.Content = text;
                msg.DstID = id;
                msg.SrcID = MyID;
                msg.SendTime = DateTime.Now.ToString();

                //P2P发送
                MyDataGram mdg = new MyDataGram();
                mdg.SrcID = MyID;

                mdg.Text = text;
                mdg.DstID = id;
                mdg.GroupID = friendID;
                mdg.Type = MessageType.Text;
                P2PCore_instance.SendData(MyDataGram.EncodeMessage(mdg), friendIP, SendDataPort);

            }
            if (msg.Content == "__init__")
            {
                return;
            }
            //回显
            msg.Content = "我: " + msg.Content;
            msg.hori = HorizontalAlignment.Right;
            ChattingMessageList.Add(msg);

            ChattingWindowListBox.ScrollIntoView(msg);
            //遍历所有的group_id 给所有其他人发一个奇怪的message

        }
        private string ConvertIDs()
        {
            string rst = " ";
            foreach(var id in GroupIDList)
            {
                rst += id + " ";
            }
            return rst;
        }
        public string GetWindowInfo()
        {
            return friendID;
        }
        public string GetFriendIP()
        {
            return friendIP;
        }        

        private async void SendFile_MouseLeftButtonUpAsync(object sender, MouseButtonEventArgs e)
        {
            //MessageBox.Show("Send File called!");
            //open dialog
            OpenFileDialog filedlg = new OpenFileDialog
            {
                Title = "请选择要发送的文件",
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false
         
            };
            if (filedlg.ShowDialog() == true)
            {

                Array.Clear(FileBuffer, 0, FileMaxSize);
                string filename = filedlg.FileName;
                FileInfo info = new FileInfo(filename);
                if(info.Length > FileMaxSize)
                {
                    MessageBox.Show("文件过大,支持最大文件: 16MB");
                    return;
                }
                //MessageBox.Show(filename);

                if(friendID[0]==' ')
                {
                    //P2P文件传输
                    List<string> OnLineIDList = new List<string>();
                    List<string> OnLineIPList = new List<string>();
                    string GroupFileID = " ";
                    foreach(var id in GroupIDList)
                    {
                        if (id != MyID)
                        {
                            var temp = "q" + id;
                            var resp = CSCore_instance.Query(temp);
                            if (resp != "n")
                            {
                                //danger!
                                OnLineIDList.Add(id);
                                OnLineIPList.Add(resp);
                                GroupFileID += id + " ";
                            }
                        }                        
                    }
                    int TotalOnlineNum = OnLineIDList.Count();
                    if (TotalOnlineNum == 0)
                    {
                        MessageBox.Show("无人在线, 别发了吧...");
                        return;
                    }
                    //load file
                    string ShortName = Path.GetFileName(filename);
                    FileStream fs = File.OpenRead(filename);
                    int FileLength = await fs.ReadAsync(FileBuffer, 0, FileBuffer.Length);
                    var FullFile = new byte[FileLength];
                    Buffer.BlockCopy(FileBuffer, 0, FullFile, 0, FileLength);

                    //cut file
                    int flag = 0;
                    int filecutter =(int) Math.Floor((double)(FileLength/TotalOnlineNum));
                    for (int i = 0;i<TotalOnlineNum; i++)
                    {
                        var mdg = new MyDataGram();
                        mdg.SrcID = MyID;
                        mdg.DstID = OnLineIDList[i];
                        mdg.Text = ShortName;
                        mdg.Type = MessageType.File;
                        mdg.GroupFileIndex = i + 1;
                        mdg.GroupFileID = GroupFileID;
                        mdg.GroupID = friendID;
                        
                        if(i == TotalOnlineNum - 1)
                        {
                            mdg.FileContent = new byte[FileLength - i * filecutter];
                            Buffer.BlockCopy(FullFile, i * filecutter, mdg.FileContent, 0, FileLength - i * filecutter);
                        }
                        else
                        {
                            mdg.FileContent = new byte[filecutter];
                            Buffer.BlockCopy(FullFile, i * filecutter, mdg.FileContent, 0, filecutter);
                        }
                        try
                        {
                            P2PCore_instance.SendData(MyDataGram.EncodeMessage(mdg), OnLineIPList[i], SendDataPort);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("发送失败!接收方: "+OnLineIDList[i]);
                            return;
                        }
                        ChattingMessage cmsg = new ChattingMessage();
                        cmsg.SrcID = MyID;
                        cmsg.DstID = friendID;
                        cmsg.CMType = mdg.Type;
                        cmsg.Content = ShortName;
                        cmsg.SendTime = DateTime.Now.ToString();
                        if (cmsg.CMType == MessageType.Image)
                        {
                            cmsg.BitMapSource = new BitmapImage(new Uri(filename, UriKind.Absolute));
                        }
                        if (flag == 0)
                        {
                            cmsg.hori = HorizontalAlignment.Right;
                            ChattingMessageList.Add(cmsg);
                            ChattingWindowListBox.ScrollIntoView(cmsg);
                        }
                        flag = 1;
                    }
                }
                else
                {
                    var temp = "q" + friendID;
                    var resp = CSCore_instance.Query(temp);
                    if (resp == "n")
                    {
                        MessageBox.Show("当前好友不在线!");
                        this.Close();
                        return;
                    }
                    else
                    {
                        friendIP = resp;
                    }
                    string ShortName = Path.GetFileName(filename);
                    var mdg = new MyDataGram();
                    FileStream fs = File.OpenRead(filename);
                    int FileLength = await fs.ReadAsync(FileBuffer, 0, FileBuffer.Length);

                    mdg.SrcID = MyID;
                    mdg.DstID = friendID;
                    mdg.Text = ShortName;
                    mdg.Type = MessageType.File;
                    if (IsImage(filename))
                    {
                        mdg.Type = MessageType.Image;
                    }
                    mdg.FileContent = new byte[FileLength];
                    Buffer.BlockCopy(FileBuffer, 0, mdg.FileContent, 0, FileLength);

                    try
                    {
                        P2PCore_instance.SendData(MyDataGram.EncodeMessage(mdg), friendIP, SendDataPort);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("发送失败!");
                        return;
                    }
                    ChattingMessage cmsg = new ChattingMessage();
                    cmsg.SrcID = MyID;
                    cmsg.DstID = friendID;
                    cmsg.CMType = mdg.Type;
                    cmsg.Content = ShortName;
                    cmsg.SendTime = DateTime.Now.ToString();
                    if(cmsg.CMType == MessageType.Image)
                    {
                        cmsg.BitMapSource = new BitmapImage(new Uri(filename, UriKind.Absolute));
                    }

                    cmsg.hori = HorizontalAlignment.Right;
                    ChattingMessageList.Add(cmsg);
                    ChattingWindowListBox.ScrollIntoView(cmsg);

                }
                

            }
            MessageBox.Show("文件成功发送!");
            return;

        }
        

        private void Sendbtn_Click(object sender, RoutedEventArgs e)
        {
            if (GroupIDList == null)
            {
                SendMessage();
            }
            else
            {
                var text = EditMessage.Text;
                EditMessage.Text = "";
                if (text == "")
                {
                    MessageBox.Show("发送消息不能为空!");
                    return;
                }
                SendGroupMessage(text);
            }
            //if group send group
        }
        public void SendMessage()
        {
            //MessageBox.Show("Send Called!");
            var text = EditMessage.Text;
            EditMessage.Text = "";
            if (text == "")
            {
                MessageBox.Show("发送消息不能为空!");
                return;
            }

            //check online
            var temp = "q" + friendID;
            var resp = CSCore_instance.Query(temp);
            if (resp == "n")
            {
                MessageBox.Show("当前好友不在线!");                
                this.Close();
                return;
            }
            else
            {
                friendIP = resp;
            }
            var msg = new ChattingMessage();
            msg.CMType = MessageType.Text;
            msg.Content = text;
            msg.DstID = friendID;
            msg.SrcID = MyID;
            msg.SendTime = DateTime.Now.ToString();

            //P2P发送
            MyDataGram mdg = new MyDataGram();
            mdg.SrcID = MyID;
            
            mdg.Text = text;
            mdg.DstID = friendID;
            mdg.Type = MessageType.Text;
            P2PCore_instance.SendData(MyDataGram.EncodeMessage(mdg), friendIP, SendDataPort);

            //回显
            msg.Content = "我: " + msg.Content;

            msg.hori = HorizontalAlignment.Right;
            ChattingMessageList.Add(msg);
            ChattingWindowListBox.ScrollIntoView(msg);
        }

        public void ReceiveMessage(MyDataGram mdg)
        {
            var msg = new ChattingMessage();
            msg.SrcID = mdg.SrcID;
            msg.DstID = mdg.DstID;
            msg.CMType = (MessageType)(uint)mdg.Type;

            //文件处理
            if(msg.CMType== MessageType.File|| msg.CMType == MessageType.Image)
            { 
                //if (mdg.GroupID[0]==' ')
                if(mdg.GroupID!="")
                {
                    if (MatchP2PFile(mdg))
                    {
                        //same file
                    }
                    else
                    {
                        P2PFileBuffer = null;
                        P2PFileIDList = null;
                        P2PFileIndexPool = null;
                        ActualFileLength = 0;
                        P2pMyOriginalPart = null;
                    }
                    if (mdg.GroupFileIndex == MyP2PIndex)
                    {
                        //ask!
                        if (P2PFileIndexPool == null)
                        {
                            MessageBox.Show("Fatal!");
                            return;
                        }
                        //向请求方给予你的部分
                        MyDataGram Askdg = new MyDataGram();
                        Askdg.DstID = mdg.SrcID;
                        Askdg.SrcID = mdg.DstID;
                        Askdg.GroupFileID = mdg.GroupFileID;
                        Askdg.GroupID = mdg.GroupID;
                        Askdg.Text = mdg.Text;
                        Askdg.GroupFileIndex = MyP2PIndex;
                        Askdg.Type = MessageType.File;
                        Askdg.FileContent = P2pMyOriginalPart;
                        P2PCore_instance.SendData(MyDataGram.EncodeMessage(Askdg), friendIP, SendDataPort);
                    }
                    else
                    {
                        //receive!
                        var arr = mdg.GroupFileID.Split(' ');

                        if (P2PFileIDList == null)
                        {
                            P2PFileIDList = new List<string>();
                            MyP2PIndex = mdg.GroupFileIndex;
                        }


                        foreach (var id in arr)
                        {
                            if (id != ""&&id!=mdg.SrcID)
                            {
                                P2PFileIDList.Add(id);
                            }
                        }
                        int Totalnum = P2PFileIDList.Count;
                        //MessageBox.Show("Get P2P file part: " + mdg.GroupFileIndex + "in Total " + P2PFileIDList.Count().ToString() + "Parts");
                        if (P2PFileIndexPool == null)
                        {
                            P2PFileIndexPool = new int[Totalnum];
                        }

                        int filecutter = mdg.FileContent.Length;
                        if (P2PFileBuffer == null)
                        {
                            //larger than actual.
                            P2PFileBuffer = new byte[filecutter * (Totalnum + 1)];
                            ActualFileLength = 0;
                            P2pMyOriginalPart = new byte[filecutter];
                            Buffer.BlockCopy(mdg.FileContent, 0, P2pMyOriginalPart, 0, filecutter);
                        }
                        //put in 
                        P2PFileIndexPool[mdg.GroupFileIndex - 1] = 1;
                        Buffer.BlockCopy(mdg.FileContent, 0, P2PFileBuffer, (mdg.GroupFileIndex - 1) * filecutter, filecutter);
                        ActualFileLength += filecutter;
                        int flag = 0;
                        foreach (int i in P2PFileIndexPool)
                        {
                            if (i != 1)
                            {
                                flag = 1;
                            }
                        }
                        byte[] FinalFile = null;
                        if (flag == 0)
                        {
                            //finish
                            FinalFile = new byte[ActualFileLength];
                            Buffer.BlockCopy(P2PFileBuffer, 0, FinalFile, 0, ActualFileLength);
                        }
                        if (FinalFile != null)
                        {
                            var question = "收到P2P的完整文件: " + mdg.GroupID + ",确定接受嘛?";
                            MessageBoxResult rst = MessageBox.Show(question, "Get File!", MessageBoxButton.YesNo);
                            if (rst == MessageBoxResult.Yes)
                            {
                                //MessageBox.Show(msg.Content);
                                var folder_dir = "./" + msg.SrcID + "/";
                                if (!Directory.Exists(folder_dir))
                                {
                                    Directory.CreateDirectory(folder_dir);
                                }
                                var FileFullName = folder_dir + mdg.Text;
                                FileStream fs = File.OpenWrite(FileFullName);
                                fs.Write(FinalFile, 0, ActualFileLength);
                                fs.Close();
                                MessageBox.Show("接收文件成功,储存在: " + FileFullName);
                            }
                            else
                            {
                                return;
                            }
                        }
                        else
                        {
                            //not enough file: Ask file from others
                            for (int i = 0; i < P2PFileIndexPool.Length; i++)
                            {
                                if (P2PFileIndexPool[i] == 0)
                                {
                                    //没有此部分文件
                                    MyDataGram Askdg = new MyDataGram();
                                    Askdg.DstID = P2PFileIDList[i];
                                    Askdg.SrcID = MyID;
                                    Askdg.GroupFileID = mdg.GroupFileID;
                                    Askdg.GroupID = mdg.GroupID;
                                    Askdg.Text = mdg.Text;
                                    Askdg.GroupFileIndex = i+1;
                                    Askdg.Type = MessageType.File;
                                    Askdg.FileContent = P2pMyOriginalPart;
                                    P2PCore_instance.SendData(MyDataGram.EncodeMessage(Askdg), friendIP, SendDataPort);
                                }
                            }

                        }
                    }
                    
                }
                else
                {
                    var question = "你收到来自: " + msg.SrcID + "的文件,确定接受嘛?";
                    MessageBoxResult rst = MessageBox.Show(question, "Get File!", MessageBoxButton.YesNo);
                    if (rst == MessageBoxResult.Yes)
                    {
                        //MessageBox.Show(msg.Content);
                        var folder_dir = "./" + msg.SrcID + "/";
                        if (!Directory.Exists(folder_dir))
                        {
                            Directory.CreateDirectory(folder_dir);
                        }
                        var FileFullName = folder_dir + mdg.Text;
                        FileStream fs = File.OpenWrite(FileFullName);
                        fs.Write(mdg.FileContent, 0, mdg.FileContent.Length);
                        fs.Close();
                        MessageBox.Show("接收文件成功,储存在: " + FileFullName);
                        if(mdg.Type == MessageType.Image)
                        {
                            mdg.Text = FileFullName;
                        }
                    }
                    else
                    {
                        return;
                    }
                }                
            }
            //询问   
            if (mdg.GroupID != null)
            {
                msg.Content = msg.SrcID + ": " + mdg.Text;
            }
            else
            {
                msg.Content = friendAlias + ": " + mdg.Text;
            }
            if(mdg.Type == MessageType.Image)
            {
                msg.Content = mdg.Text;
                string fullpath = Path.GetFullPath(msg.Content);
                msg.BitMapSource = new BitmapImage(new Uri(fullpath, UriKind.Absolute));
            }
            msg.SendTime = DateTime.Now.ToString();
            ChattingMessageList.Add(msg);
            ChattingWindowListBox.ScrollIntoView(msg);

        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            History hist = new History();
            ChattingWindowListBox.ItemsSource = ChattingMessageList;
            ChattingMessageList = hist.LoadHistory(MyID, friendID, ChattingMessageList);
            if(ChattingMessageList.Count!=0)
                ChattingWindowListBox.ScrollIntoView(ChattingMessageList.Last());
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var hist = new History();            
            hist.SaveHistory(MyID, friendID, ChattingMessageList);
            //P2PCore_instance.EndListen();

            CloseEvent();
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void SendUDPbtn_Click(object sender, RoutedEventArgs e)
        {
            SendUDP();
        }
        private void SendUDP()
        {
            if(friendID[0]==' ')
            {
                MessageBox.Show("暂不支持UDP群组通话");
                return;
            }
            var text = EditMessage.Text;

            EditMessage.Text = "";
            if (text == "")
            {
                MessageBox.Show("发送消息不能为空!");
                return;
            }
            var temp = "q" + friendID;
            var resp = CSCore_instance.Query(temp);
            if (resp == "n")
            {
                MessageBox.Show("当前好友不在线!");
                this.Close();
                return;
            }
            else
            {
                friendIP = resp;
            }
            var msg = new ChattingMessage();
            msg.CMType = MessageType.Text;
            msg.Content = text;
            msg.DstID = friendID;
            msg.SrcID = MyID;
            msg.SendTime = DateTime.Now.ToString();


            //P2P发送
            MyDataGram mdg = new MyDataGram();
            mdg.SrcID = MyID;
            mdg.Text = text;
            mdg.DstID = friendID;
            mdg.Type = MessageType.Text;
            P2PCore_instance.SendUDPData(MyDataGram.EncodeMessage(mdg), friendIP, SendDataPort);
            P2PCore_instance.AddUdpMessage(MyDataGram.EncodeMessage(mdg), friendIP, SendDataPort);
            

            //回显
            msg.Content = "我: " + msg.Content;

            msg.hori = HorizontalAlignment.Right;
            ChattingMessageList.Add(msg);
            ChattingWindowListBox.ScrollIntoView(msg);
        }

        private bool MatchP2PFile(MyDataGram mdg)
        {
            if (P2PFileBuffer == null && P2PFileIDList == null &&
                P2PFileIndexPool == null && ActualFileLength == 0 &&
                P2pMyOriginalPart == null)
            {
                return false;
            }
            else
            {
                var arr = mdg.GroupFileID.Split(' ');
                List<string> tempList = new List<string>();
                int flag = 0;
                foreach (var id in arr)
                {
                    if (id != "")
                    {
                        if (!P2PFileIDList.Contains(id))
                        {
                            flag = 1;
                        }
                    }
                }
                if (flag == 1)
                {
                    return false;
                }
                return true;
            }
        }

        private bool IsImage(string path)
        {
            try
            {
                System.Drawing.Image img = System.Drawing.Image.FromFile(path);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }
    public class MsgVisConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Visibility result = Visibility.Collapsed;
            if((Type)parameter == typeof(TextBlock))
            {
                if ((MessageType)value == MessageType.Text)
                    result = Visibility.Visible;
            }
            if((Type)parameter == typeof(Label))
            {
                if ((MessageType)value == MessageType.File)
                {
                    result = Visibility.Visible;
                }
            }
            if ((Type)parameter == typeof(Image))
            {
                if ((MessageType)value == MessageType.Image)
                {
                    result = Visibility.Visible;
                }
            }

            return result;

        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
