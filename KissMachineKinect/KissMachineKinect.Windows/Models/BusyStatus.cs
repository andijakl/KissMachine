using System.ComponentModel;
using System.Runtime.CompilerServices;
using KissMachineKinect.Properties;

namespace KissMachineKinect.Models
{
    public class BusyStatus : INotifyPropertyChanged
    {
        private bool _isBusy;

        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                if (value == _isBusy) return;
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        private string _busyText;
        public string BusyText
        {
            get { return _busyText; }
            set
            {
                if (value == _busyText) return;
                _busyText = value;
                OnPropertyChanged();
            }
        }

        public enum BusyEndTypes
        {
            ChangeToTrigger,
            Disappear,
            Fadeout,
            Checkmark,
            CheckmarkFadeoutBg,
            CheckmarkFirstHalf,
            CheckmarkSecondHalf,
            Error
        }

        private BusyEndTypes _busyEnd;
        public BusyEndTypes BusyEnd
        {
            get { return _busyEnd; }
            set
            {
                if (value == _busyEnd) return;
                _busyEnd = value;
                OnPropertyChanged();
            }
        }

        public enum BusyEndedTypes
        {
            MiddleOfAnimation,
            CompletedAnimation
        }


        public void SetBusy(string busyText)
        {
            BusyText = busyText;
            BusyEnd = BusyEndTypes.ChangeToTrigger;
            IsBusy = true;
        }

        public void EndBusy(BusyEndTypes busyEndType)
        {
            if (busyEndType == BusyEndTypes.Disappear)
            {
                IsBusy = false;
            }
            else
            {
                BusyEnd = busyEndType;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
