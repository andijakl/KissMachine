using System;
using System.Diagnostics;
using System.Windows.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using KissMachineKinect.Models;

namespace KissMachineKinect.Controls
{
    public partial class BusyControl : UserControl
    {
        #region Dependency Property Definitions
        public bool IsBusy
        {
            get { return (bool)GetValue(IsBusyProperty); }
            set
            {
                SetValue(IsBusyProperty, value);
            }
        }

        // Using a DependencyProperty as the backing store for IsBusy.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsBusyProperty =
            DependencyProperty.Register("IsBusy", typeof(bool), typeof(BusyControl), new PropertyMetadata(false, IsBusyPropertyChanged));



        public string BusyText
        {
            get { return (string)GetValue(BusyTextProperty); }
            set { SetValue(BusyTextProperty, value); }
        }

        // Using a DependencyProperty as the backing store for BusyText.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty BusyTextProperty =
            DependencyProperty.Register("BusyText", typeof(string), typeof(BusyControl), new PropertyMetadata("Loading"));


        public BusyStatus.BusyEndTypes BusyEnd
        {
            get { return (BusyStatus.BusyEndTypes)GetValue(BusyEndProperty); }
            set { SetValue(BusyEndProperty, value); }
        }

        // Using a DependencyProperty as the backing store for BusyText.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty BusyEndProperty =
            DependencyProperty.Register("BusyEnd", typeof(BusyStatus.BusyEndTypes), typeof(BusyControl), new PropertyMetadata(BusyStatus.BusyEndTypes.ChangeToTrigger, BusyEndPropertyChanged));

        private BusyStatus.BusyEndTypes _busyEnd;
        #endregion


        #region BusyEndedCommand Definitions
        public ICommand BusyEndedCommand
        {
            get { return (ICommand)GetValue(BusyEndedCommandProperty); }
            set { SetValue(BusyEndedCommandProperty, value); }
        }

        public readonly DependencyProperty BusyEndedCommandProperty =
            DependencyProperty.Register(
                "BusyEndedCommand",
                typeof(ICommand),
                typeof(BusyControl),
                new PropertyMetadata(null));
        
        #endregion

        public BusyControl()
        {
            InitializeComponent();
            LayoutRoot.DataContext = this;
            Debug.WriteLine("Busy control constructor");
        }



        private static void IsBusyPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            Debug.WriteLine("IsBusyPropertyChanged: " + e.OldValue + " -> " + e.NewValue);
            ((BusyControl)obj).BusyChanged(e);
        }

        private void BusyChanged(DependencyPropertyChangedEventArgs args)
        {
            if (!(args.NewValue is bool)) return;
            var newBusy = (bool)args.NewValue;
            if (newBusy)
            {
                BackgroundFill.Opacity = 1.0;
                BusyIndicator.Opacity = 1.0;
                BusyIndicatorText.Opacity = 1.0;
            }

        }

        public static void BusyEndPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            // TODO: gets called multiple times when navigating from choose task to select event and then back again
            ((BusyControl)obj).EndBusy(e);
        }


        private void EndBusy(DependencyPropertyChangedEventArgs args)
        {
            if (!(args.NewValue is BusyStatus.BusyEndTypes)) return;
            _busyEnd = (BusyStatus.BusyEndTypes)args.NewValue;
            switch (_busyEnd)
            {
                case BusyStatus.BusyEndTypes.ChangeToTrigger:
                    return;
                case BusyStatus.BusyEndTypes.Fadeout:
                    {
                        FadeoutStoryboard.Completed += FadeoutStoryboard_Completed;
                        FadeoutStoryboard.Begin();
                    }
                    break;
                case BusyStatus.BusyEndTypes.Checkmark:
                case BusyStatus.BusyEndTypes.CheckmarkFadeoutBg:
                case BusyStatus.BusyEndTypes.CheckmarkFirstHalf:
                    {
                        BusyIndicator.Opacity = 0.0;
                        BusyIndicatorText.Opacity = 0.0;
                        CheckmarkSymbol.Visibility = Visibility.Visible;
                        if (_busyEnd == BusyStatus.BusyEndTypes.CheckmarkFirstHalf)
                        {
                            CheckmarkStoryboardFirstHalf.Completed += CheckmarkStoryboardFirstHalf_Completed;
                            CheckmarkStoryboardFirstHalf.Begin();
                        }
                        else
                        {
                            if (_busyEnd == BusyStatus.BusyEndTypes.CheckmarkFadeoutBg)
                            {
                                SlowFadeoutStoryboard.Begin();
                            }
                            CheckmarkStoryboard.Completed += CheckmarkStoryboard_Completed;
                            CheckmarkStoryboard.Begin();
                        }
                    }
                    break;
                case BusyStatus.BusyEndTypes.CheckmarkSecondHalf:
                    {
                        CheckmarkStoryboardSecondHalf.Completed += CheckmarkStoryboard_Completed;
                        CheckmarkStoryboardSecondHalf.Begin();
                    }
                    break;
                case BusyStatus.BusyEndTypes.Error:
                    {
                        BusyIndicator.Opacity = 0.0;
                        BusyIndicatorText.Opacity = 0.0;
                        ErrorSymbol.Visibility = Visibility.Visible;
                        ErrorStoryboard.Completed += ErrorStoryboard_Completed;
                        SlowFadeoutStoryboard.Begin();
                        ErrorStoryboard.Begin();
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void FadeoutStoryboard_Completed(object sender, object e)
        {
            FadeoutStoryboard.Completed -= FadeoutStoryboard_Completed;
            TriggerBusyEndedCommand(BusyStatus.BusyEndedTypes.CompletedAnimation);
        }

        private void CheckmarkStoryboard_Completed(object sender, object e)
        {
            CheckmarkStoryboard.Completed -= CheckmarkStoryboard_Completed;
            CheckmarkStoryboardSecondHalf.Completed -= CheckmarkStoryboard_Completed;
            CheckmarkSymbol.Visibility = Visibility.Collapsed;
            TriggerBusyEndedCommand(BusyStatus.BusyEndedTypes.CompletedAnimation);
        }

        private void CheckmarkStoryboardFirstHalf_Completed(object sender, object e)
        {
            CheckmarkStoryboardFirstHalf.Completed -= CheckmarkStoryboardFirstHalf_Completed;
            TriggerBusyEndedCommand(BusyStatus.BusyEndedTypes.MiddleOfAnimation);
        }

        private void ErrorStoryboard_Completed(object sender, object e)
        {
            ErrorStoryboard.Completed -= ErrorStoryboard_Completed;
            ErrorSymbol.Visibility = Visibility.Collapsed;
            TriggerBusyEndedCommand(BusyStatus.BusyEndedTypes.CompletedAnimation);
        }

        private void TriggerBusyEndedCommand(BusyStatus.BusyEndedTypes busyEndedType)
        {
            if (BusyEndedCommand != null)
            {
                BusyEndedCommand.Execute(busyEndedType);
            }
        }
    }
}
