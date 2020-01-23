using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;

using System.Globalization;
using System.Threading;

namespace Miless
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    /// 
    
    public delegate void EventHandler();
    public partial class MainWindow : Window
    {
        //账号信息
        private string account = "";
        public event EventHandler CloseEvent;

        //新朋友信息
        private string NewFriendAccount = "";
        private ObservableCollection<MilessFriend> FriendList = new ObservableCollection<MilessFriend>();
        private List<ChatWindow> ChatWindowList = new List<ChatWindow>();
        private List<string> ChattingIDList = new List<string>();
        //P2P交互核心和CS交互核心
        private P2PCore P2PCore_instance = null;
        private CSCore CSCore_instance = null;

        private InterThreads inter = null;
        private Thread MsgPump = null;

        private bool listening = false;

        private int GroupChatIter = 0;
        //private List<string> ChattingGroupList

        public MainWindow()
        {
            InitializeComponent();
            //LoginProcess();
            CSCore_instance = CSCore.GetInstance();
            P2PCore_instance = P2PCore.GetInstance();
            P2PCore_instance.BeginListen();
            inter = InterThreads.GetInstance();
            

            //MessageBox.Show("Listen Start!");
            lock (inter) inter.processing = true;
            MsgPump = new Thread(ReceiveListener)
            {
                Name = "MessagePump"
            };

        }

        private void ReceiveListener()
        {
            while (listening)
            {
                Thread.Sleep(500);
                byte[] msg = null;
                if (inter.messages.Count > 0)
                {
                    //have new tcp message!
                    msg = inter.messages.Dequeue();
                }
                if (msg != null && msg.Length > 0)
                {
                    MyDataGram pro = MyDataGram.DecodeMessage(msg);
                    if (pro.Type != MessageType.Disable)
                    {
                        //DistributeMessage(pro);
                        Dispatcher.BeginInvoke(new Dis(DistributeMessage), pro);
                    }
                }
            }
        }
        //使用委托
        private delegate void Dis(MyDataGram mdg);

        private async void DistributeMessage(MyDataGram mdg)
        {
            //群组的greetings消息:直接加入到通讯录
            if (mdg.GroupID != "" && mdg.Text == "__init__" && mdg.DstID == account)
            {
                //Greetings Message
                //Add to Addresbook
                MilessFriend nf = new MilessFriend();
                foreach(var fri in FriendList)
                {
                    if(fri.FriendID == mdg.SrcID)
                    {
                        nf.FriendAlias = fri.FriendAlias;
                        break;
                    }
                }
                nf.FriendAlias = "Group with:" + nf.FriendAlias;

                nf.FriendID = mdg.GroupID;
                int chachong = 0;
                foreach(var fri in FriendList)
                {
                    var a = fri.FriendID;
                    //为分组
                    if(a[0]==' ')
                    {
                        if (GroupIDMatch(fri.FriendID, a))
                        {
                            chachong = 1;
                        }
                    }                    
                }
                if(chachong==0)
                {

                    //本组之前没有出现过
                    FriendList.Add(nf);
                    listFriends.ScrollIntoView(nf);
                }
                return;
            }
            var ChattingWindowNumber = ChatWindowList.Count;
            if (ChattingWindowNumber == 0)
            {
                AlarmNewMessage(mdg);                
            }
            else
            {
                int FindWindow = 0;
                foreach (var cw in ChatWindowList)
                {
                    if (mdg.GroupID != "")
                    {
                        //由组内成员的学号判断是否为同一分组
                        if (GroupIDMatch(mdg.GroupID, cw.GetWindowInfo()))
                        {
                            cw.ReceiveMessage(mdg);
                            FindWindow = 1;
                            break;
                        }
                    }
                    if (mdg.SrcID == cw.GetWindowInfo())
                    {
                        //分给相应的窗口信息
                        cw.ReceiveMessage(mdg);
                        FindWindow = 1;
                        break;
                    }
                    
                }
                if (FindWindow == 0)
                {
                    AlarmNewMessage(mdg);
                }
            }
        }
        private void AlarmNewMessage(MyDataGram mdg)
        {
            //群组消息
            string NewMsgAlias = "";
            foreach (var fri in FriendList)
            {
                if (mdg.SrcID == fri.FriendID)
                {
                    NewMsgAlias = fri.FriendAlias;
                }
                if (mdg.GroupID != "")
                {
                    //群组
                    if(fri.FriendID[0]==' ')
                    {
                        if(GroupIDMatch(mdg.GroupID, fri.FriendID))
                        {
                            NewMsgAlias = fri.FriendAlias;
                        }
                    }   
                }
                
            }
            if (NewMsgAlias != "")
            {
                MessageBox.Show("你收到一个来自: " + NewMsgAlias + "的消息!");
                //打开再放进去这一条消息
                var hist = new History();
                hist.SaveOneHistory(mdg);
            }
            else
            {
                MessageBox.Show("你收到来自陌生人: " + mdg.SrcID + "的消息!");
                var hist = new History();
                hist.SaveOneHistory(mdg);
            }
        }
        
        public void SetAccount(string acc)
        {
            account = acc;
        }
        private void Address_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //todo save things
            CloseEvent();
            P2PCore_instance.EndListen();
            MsgPump.Abort();
            foreach(var win in ChatWindowList)
            {
                win.Close();
            }
            listening = false;
        }

        private void Address_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAddressBook();
            listFriends.ItemsSource = FriendList;
            listening = true;

            MsgPump.Start();
        }

        private void AddFriend_Click(object sender, RoutedEventArgs e)
        {
            //添加新的群组
            var addwin = new AddFriend();
            addwin.Show();
            addwin.CloseEvent += new AFEventHandler(FindAFriend);
            addwin.GetMyAccount(account);
            this.Hide();
            void FindAFriend()
            {
                this.Show();
                if (addwin.HaveFindFriend == 1)
                {
                    NewFriendAccount = addwin.GetNewAccount();
                    string[] arr = NewFriendAccount.Split('_');
                    MilessFriend newFriend = new MilessFriend();
                    foreach (var fri in FriendList)
                    {
                        if (fri.FriendAlias == arr[1] || fri.FriendID == arr[0])
                        {
                            MessageBox.Show("添加失败:ID或昵称重复.");
                            return;
                        }
                    }

                    newFriend.FriendAlias = arr[1];
                    newFriend.FriendID = arr[0];
                    newFriend.FriendIP = addwin.GetNewIP();
                    FriendList.Add(newFriend);
                }
                
            }

        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            SaveAddressBook();
            if (ChatWindowList.Count > 0)
            {
                MessageBox.Show("还有聊天窗口未关闭,请先关闭!");
                return;
            }
            this.Close();
        }
        

        private void SaveAddressBook()
        {

            File.Delete(@".\data\AdressBook.txt");
            var fs = new FileStream(@".\data\AdressBook.txt", FileMode.OpenOrCreate, FileAccess.ReadWrite);
            StreamWriter sw = new StreamWriter(fs);         
            //规定数据结构:FriendID_FriendAlias
            foreach (var fri in FriendList)
            {
                var arr = fri.FriendID+"_"+fri.FriendAlias;
                sw.WriteLine(arr);
            }
            sw.Flush();
            sw.Close();
            fs.Close();
        }
        private void LoadAddressBook()
        {
            
            List<String> temp = new List<string>();
            var fs = new FileStream(@".\data\AdressBook.txt", FileMode.OpenOrCreate, FileAccess.ReadWrite);
            StreamReader sr = new StreamReader(fs);
            var line = sr.ReadLine();
            while (line != null)
            {
                temp.Add(line);
                line = sr.ReadLine();
            }
            //规定数据结构:FriendID_FriendAlias
            foreach (var t in temp)
            {
                if (t != "")
                {
                    var arr = t.Split('_');
                    var newfri = new MilessFriend();
                    newfri.FriendAlias = arr[1];
                    newfri.FriendID = arr[0];
                    FriendList.Add(newfri);
                }                              
            }
            sr.Close();
            fs.Close();
        }

        private void DeleteFriend_Click(object sender, RoutedEventArgs e)
        {
            
            MilessFriend friend = new MilessFriend();
            try
            {
                friend = listFriends.SelectedItem as MilessFriend;
            }
            catch (NullReferenceException)
            {
                
                return;
            };
            if (friend == null)
            {
                MessageBox.Show("请先选择要删除的朋友!");
                return;
            }
            //MessageBox.Show("pass");
            foreach (var fri in FriendList)
            {
                if (fri.FriendID == friend.FriendID)
                {
                    FriendList.Remove(fri);
                    break;
                    //MessageBox.Show("Changed");
                }
            }
        }

        private void StartChat_Click(object sender, RoutedEventArgs e)
        {
            StartChatWindow();
        }
        private void StartChatWindow()
        {

            MilessFriend friend = new MilessFriend();

            friend = listFriends.SelectedItem as MilessFriend;
            
            if (friend == null)
            {
                MessageBox.Show("请先选择聊天的朋友!");
                return;
            }
            if(friend.FriendID[0]==' ')
            {
                foreach (var friID in ChattingIDList)
                {
                    if (GroupIDMatch(friend.FriendID, friID))
                    {
                        MessageBox.Show("已有聊天窗口!");

                        return;
                    }
                }
            }
            else
            {
                string friendIP = "";
                var resp = CSCore_instance.Query("q" + friend.FriendID);
                if (resp == "n")
                {
                    MessageBox.Show("好友不在线!");
                    return;
                }
                else if (friend.FriendID == account)
                {
                    MessageBox.Show("别自言自语,和别人聊天吧!");
                    return;
                }
                else
                {
                    // Danger
                    friendIP = resp;
                }
                friend.FriendIP = friendIP;
                foreach (var friID in ChattingIDList)
                {
                    if (friend.FriendID == friID)
                    {
                        MessageBox.Show("已有聊天窗口!");

                        return;
                    }
                }
            }

            
            ChattingIDList.Add(friend.FriendID);
            ChatWindow cw = new ChatWindow();
            cw.SetMyID(account);
            cw.SetFriendInfo(friend.FriendID + "_" + friend.FriendAlias);
            ChatWindowList.Add(cw);
            cw.Show();
            cw.CloseEvent += new cwEventHandler(cwClose);
            
            void cwClose()
            {
                foreach (var cww in ChatWindowList)
                {
                    if (cww.GetWindowInfo() == cw.GetWindowInfo())
                    {
                        ChatWindowList.Remove(cww);
                        break;
                    }
                }
                foreach(var id in ChattingIDList)
                {
                    if (cw.GetWindowInfo() == id)
                    {
                        ChattingIDList.Remove(id);
                        break;
                    }
                }
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void GroupChat_Click(object sender, RoutedEventArgs e)
        {
            if(GroupChatIter == 0)
            {
                listFriends.SelectionMode = SelectionMode.Multiple;
                GroupChatIter = 1;
                Title.Content = "请选择要聊天的好友,再次点击群聊确认";
                Title.HorizontalContentAlignment = HorizontalAlignment.Center;
                Title.FontSize = 12;
                GroupChat.Content = "确认";
                AddFriend.IsEnabled = false;
                DeleteFriend.IsEnabled = false;
                StartChat.IsEnabled = false;
            }
            else if(GroupChatIter == 1)
            {
                GroupChatIter = 0;
                Title.Content = "Friends";
                Title.FontSize = 32;
                Title.HorizontalContentAlignment = HorizontalAlignment.Left;
                GroupChat.Content = "发起\n群聊";

                AddFriend.IsEnabled = true;
                DeleteFriend.IsEnabled = true;
                StartChat.IsEnabled = true;
                //Start Chat Window 
                //MessageBox.Show("Start Window!");
                var li = listFriends.SelectedItems;
                if (li == null)
                {
                    MessageBox.Show("请先选择聊天的群组!");
                    return;
                }
                List<MilessFriend> GroupChatFriendList = new List<MilessFriend>();
                foreach(var item in li)
                {
                    var friend = item as MilessFriend;
                    if(friend.FriendAlias[0]==' ')
                    {
                        MessageBox.Show("不能添加群组作为群组成员!");
                        return;
                    }
                    GroupChatFriendList.Add(friend);
                }

                //Start Chat Window
                foreach (var gfri in GroupChatFriendList)
                {
                    if (gfri.FriendID == account)
                    {
                        MessageBox.Show("别自言自语,和别人聊天吧!");
                        return;
                    }
                }

                var me = new MilessFriend();
                me.FriendAlias = "me";
                me.FriendID = account;

                if (!GroupChatFriendList.Contains(me))
                {
                    GroupChatFriendList.Add(me);
                }

                listFriends.SelectionMode = SelectionMode.Single;
                

                string group_id = " ";
                string group_name = "";
                foreach (var friend in GroupChatFriendList)
                {
                    group_id += friend.FriendID + " ";
                    group_name += friend.FriendAlias + " ";
                }
                foreach (var friID in ChattingIDList)
                {
                    if (GroupIDMatch(group_id, friID))
                    {
                        MessageBox.Show("已有聊天窗口!");

                        return;
                    }
                }
                ChatWindow cw = new ChatWindow();
                cw.SetMyID(account);
                cw.SetFriendInfo(group_id + "_" + group_name);
                var newcwID = cw.GetWindowInfo();
                ChattingIDList.Add(newcwID);//Super friend
                ChatWindowList.Add(cw);
                cw.Show();
                cw.CloseEvent += new cwEventHandler(cwClose);

                void cwClose()
                {
                    foreach (var cww in ChatWindowList)
                    {
                        if (cww.GetWindowInfo() == cw.GetWindowInfo())
                        {
                            ChatWindowList.Remove(cww);
                            break;
                        }
                    }
                    foreach (var id in ChattingIDList)
                    {
                        if (cw.GetWindowInfo() == id)
                        {
                            ChattingIDList.Remove(id);
                            break;
                        }
                    }
                }
                //Add group to friend_list
                AddGroupToFriendList(GroupChatFriendList);
            }
        }
        private void AddGroupToFriendList(List<MilessFriend> glist)
        {
            string group_name = "";
            string group_id = " ";
            foreach(var friend in glist)
            {
                group_id += friend.FriendID+" ";
                group_name += friend.FriendAlias+" ";
            }
            MilessFriend gFriend = new MilessFriend();
            gFriend.FriendAlias = group_name;
            gFriend.FriendID = group_id;
            FriendList.Add(gFriend);

        }
        private bool GroupIDMatch(string id1, string id2)
        {
            //判断群组ID是否Match的逻辑
            var arr1 = id1.Split(' ');
            var arr2 = id2.Split(' ');

            int flag = 0;
            foreach (var aa in arr1)
            {
                if (aa != "")
                {
                    int flag2 = 0;
                    foreach (var aaa in arr2)
                    {
                        if(aaa != "")
                        {
                            if (aaa == aa)
                            {
                                flag2 = 1;
                            }
                        }
                        
                    }
                    if (flag2 == 0)
                    {
                        flag = 1;
                    }
                }
                
            }
            foreach (var aa in arr2)
            {
                if (aa != "")
                {
                    int flag2 = 0;
                    foreach (var aaa in arr1)
                    {
                        if (aaa != "")
                        {
                            if (aaa == aa)
                            {
                                flag2 = 1;
                            }
                        }
                        
                    }
                    if (flag2 == 0)
                    {
                        flag = 1;
                    }
                }                
            }
            if (flag == 1)
            {
                //buquandeng
                return false;
            }
            return true;
        }
    }
}
