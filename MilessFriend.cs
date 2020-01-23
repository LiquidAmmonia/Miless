using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miless
{
    
    class MilessFriend : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private string _FriendID;
        public string FriendID
        {
            get { return _FriendID; }
            set
            {
                _FriendID = value;
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("FriendID"));
                }
            }
        }
        private string _FriendAlias;
        public string FriendAlias
        {
            get { return _FriendAlias; }
            set
            {
                _FriendAlias = value;
                
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("FriendAlias"));
                }
                
                //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FriendAlias"));
            }
        }
        private string _FriendIP;
        public string FriendIP
        {
            get { return _FriendIP; }
            set
            {
                _FriendIP = value;
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("FriendIP"));
                }
            }
        }

    }
}
