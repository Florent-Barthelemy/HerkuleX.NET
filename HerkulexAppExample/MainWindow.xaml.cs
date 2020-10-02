using HerkulexControl;
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
using System.IO.Ports;
using System.Timers;
using System.Windows.Threading;
using System.Threading;

namespace HerkulexAppExample
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private HerkulexController herkulexController = new HerkulexController("COM3", 115200, Parity.None, 8, StopBits.One);
        private DispatcherTimer UI_updater = new DispatcherTimer();

        private ushort absolutePositionServo1 = 0;
        private ushort absolutePositionServo2 = 0;
        private ushort aPosBuffer2 = 512;
        private ushort aPosBuffer1 = 512;

        private byte[] ServoOnBusID;


        public MainWindow()
        {
            InitializeComponent();
            UI_updater.Tick += UI_updater_Tick;
            UI_updater.Interval = new TimeSpan(0, 0, 0, 0, 100);
            UI_updater.Start();

            herkulexController.InfosUpdatedEvent += HerkulexController_InfosUpdatedEvent;
            herkulexController.HerkulexErrorEvent += HerkulexController_HerkulexErrorEvent;

            herkulexController.AutoRecoverMode = true;

            herkulexController.AddServo(1, HerkulexDescription.JOG_MODE.positionControlJOG, 512);
            herkulexController.AddServo(2, HerkulexDescription.JOG_MODE.positionControlJOG, 512);

            herkulexController.SetPollingFreq(10);
            herkulexController.StartPolling();
        }

        private void UI_updater_Tick(object sender, EventArgs e)
        {
            TextPos1.Text = absolutePositionServo1.ToString();
            TextPos2.Text = absolutePositionServo2.ToString();
        }

        private void HerkulexController_HerkulexErrorEvent(object sender, HerkulexErrorArgs e)
        {
            
        }

        private void HerkulexController_InfosUpdatedEvent(object sender, InfosUpdatedArgs e)
        {
            if (e.Servo.GetID() == 1)
                absolutePositionServo1 = e.Servo.ActualAbsolutePosition;
            if (e.Servo.GetID() == 2)
                absolutePositionServo2 = e.Servo.ActualAbsolutePosition;
        }

        private void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            ScannedServosListBox.Items.Clear();
            ServoOnBusID = herkulexController.ScanForServoIDs(minID : 0, maxID : 0xFE);
            foreach (byte ID in ServoOnBusID)
                ScannedServosListBox.Items.Add(ID.ToString());
        }

        private bool tglrl1 = true;
        private void ReleasTrq1_Click(object sender, RoutedEventArgs e)
        {
            if(tglrl1)
            {
                herkulexController.SetTorqueMode(1, HerkulexDescription.TorqueControl.TorqueFree);
                tglrl1 = !tglrl1;
            }
            else
            {
                herkulexController.SetTorqueMode(1, HerkulexDescription.TorqueControl.TorqueOn);
                tglrl1 = !tglrl1;
            }
            
        }

        private bool tglrl2 = true;
        private void ReleasTrq2_Click(object sender, RoutedEventArgs e)
        {
            if (tglrl2)
            {
                herkulexController.SetTorqueMode(2, HerkulexDescription.TorqueControl.TorqueFree);
                tglrl2 = !tglrl2;
            }
            else
            {
                herkulexController.SetTorqueMode(2, HerkulexDescription.TorqueControl.TorqueOn);
                tglrl2 = !tglrl2;
            }
        }

        private void Save1_Click(object sender, RoutedEventArgs e)
        {
            aPosBuffer1 = absolutePositionServo1;
        }

        private void Save2_Click(object sender, RoutedEventArgs e)
        {
            aPosBuffer2 = absolutePositionServo2;
        }

        private void GotoSync_Click(object sender, RoutedEventArgs e)
        {
            herkulexController.SetPosition(1, aPosBuffer1, 10, true);
            herkulexController.SetPosition(2, aPosBuffer2, 10, true);
            herkulexController.SendSynchronous(1);
        }
    }
}
