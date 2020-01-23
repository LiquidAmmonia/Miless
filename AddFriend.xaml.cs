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
using System.Windows.Shapes;

namespace Miless
{
    /// <summary>
    /// AddFriend.xaml 的交互逻辑
    /// </summary>
    /// 
    public delegate void AFEventHandler();
    public partial class AddFriend : Window
    {
        private CSCore CSCore_instance = null;
        private string myaccount = "";
        private string newaccount = "";
        private string newname = "";
        private string newIP = "";

        public event AFEventHandler CloseEvent;
        public int HaveFindFriend = 0;

        public AddFriend()
        {
            InitializeComponent();
            CSCore_instance = CSCore.GetInstance();
        }
        public void GetMyAccount(string acc)
        {
            myaccount = acc;
            CurrentAccount.Content = acc;
            if (myaccount == "")
            {
                MessageBox.Show("Fatal:Accout is Null");
                Close();
            }
        }
        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void Enter_Click(object sender, RoutedEventArgs e)
        {
            var temp = FriendAccount.Text;
            var merged = "q" + temp;
            var receive = CSCore_instance.Query(merged);
            if (receive == "n" || receive.Contains("1"))
            {
                newaccount = FriendAccount.Text;
                if (receive.Contains("1"))
                {
                    newIP = receive;
                }
            }
            else
            {
                MessageBox.Show("输入有误或账号不存在.");
                newaccount = "";
                FriendAccount.Text = "";
                return;
            }
            newname = FriendName.Text;
            if (newname.Contains("_")||newname=="")
            {
                MessageBox.Show("昵称不合法,请不要包含'_'字符或者为空");
                newname = "";
                FriendName.Text = "";
                return;
            }
            else
            {
                if(FriendAccount.Text == myaccount)
                {
                    MessageBox.Show("请不要添加你自己!");
                    return;
                }
                newname = FriendName.Text;
            }

            //MessageBox.Show(receive);
            HaveFindFriend = 1;

            Close();
            CloseEvent();
        }
        public string GetNewAccount()
        {
            if (newaccount != "" && newname != "")
            {
                return newaccount+"_"+newname;
            }
            else
            {
                return "";
            }
        }
        public string GetNewIP()
        {
            return newIP;
        }
        
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {

            Close();
            CloseEvent();
        }
    }
}
