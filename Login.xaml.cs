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
    /// Login.xaml 的交互逻辑
    /// </summary>
    /// 

    public partial class Login : Window
    {

        private string G_Account = "";
        //private string G_Password = "";
        private CSCore CSCore_instance = null;
        public Login()
        {
            InitializeComponent();
        }
        public string GetAccount()
        {
            return G_Account;
        }

        private void LoginBtn_Click(object sender, RoutedEventArgs e)
        {
            var account = Account.Text;
            var password = Password.Password;
            G_Account = account;
            var merge = account + "_" + password;
            var receive = CSCore_instance.Query(merge);
            if (receive == "lol")
            {
                //跳转逻辑
                StartChat();                
            }
            else
            {
                MessageBox.Show("登陆不成功,请检查网络.");
            }
            
        }
        private void StartChat()
        {
            MainWindow win = new MainWindow();            
            win.Show();
            win.SetAccount(G_Account);
            win.CloseEvent += new EventHandler(DoneTalking);
            //关闭逻辑
            this.Hide();
            void DoneTalking()
            {
                this.Show();
            }
        }

        private void LogoutBtn_Click(object sender, RoutedEventArgs e)
        {
            //logout process

            var merge = "logout" + G_Account;
            var receive = "";
            if (CSCore_instance != null)
            {
                receive = CSCore_instance.Query(merge);
                //receive = "loo";
                if (receive != "loo")
                {
                    if (G_Account != "")
                    {
                        MessageBox.Show("下线不成功,当前账号:" + G_Account);
                    }
                    this.Close();
                }
                else
                {
                    this.Close();
                }
            }
            else
            {
                this.Close();
            }
            this.Close();
            
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if(e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void Login_Loaded(object sender, RoutedEventArgs e)
        {
            CSCore_instance = CSCore.GetInstance();
        }

        private void Login_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

        }
    }
}
