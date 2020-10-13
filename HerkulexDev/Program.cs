using System;
using HerkulexControl;
using ExtendedSerialPort;
using System.IO.Ports;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using Microsoft.VisualBasic.CompilerServices;
using System.Diagnostics;
using SharpDX.XInput;

namespace HerkulexDev
{
    class Program
    {
        static HerkulexController herkulexController = new HerkulexController("COM7", 115200, Parity.None, 8, StopBits.One);
        static System.Threading.Timer blinkTimer;
        static Controller controller = new Controller(UserIndex.One);
        static State gamepadState;

        [MTAThread]
        static void Main(string[] args)
        {
            herkulexController.InfosUpdatedEvent += HerkulexController_InfosUpdatedEvent; ;
            herkulexController.HerkulexErrorEvent += HerkulexController_HerkulexErrorEvent; ;

            herkulexController.AutoRecoverMode = true;
            herkulexController.SetAckTimeout(50);




            herkulexController.AddServo(2, HerkulexDescription.JOG_MODE.positionControlJOG, 512);
            herkulexController.AddServo(5, HerkulexDescription.JOG_MODE.positionControlJOG, 512);
            herkulexController.AddServo(4, HerkulexDescription.JOG_MODE.positionControlJOG, 512);

            herkulexController.SetTorqueMode(2, HerkulexDescription.TorqueControl.TorqueOn);
            herkulexController.SetTorqueMode(4, HerkulexDescription.TorqueControl.TorqueOn);
            herkulexController.SetTorqueMode(5, HerkulexDescription.TorqueControl.TorqueOn);

            ScanAndPrint();
            herkulexController.StartPolling();

            Thread.Sleep(1000);


            //herkulexController.SetTorqueMode(1, HerkulexDescription.TorqueControl.TorqueFree);
            //herkulexController.SetTorqueMode(2, HerkulexDescription.TorqueControl.TorqueFree);
            //herkulexController.SetTorqueMode(5, HerkulexDescription.TorqueControl.TorqueFree);
            //herkulexController.SetTorqueMode(4, HerkulexDescription.TorqueControl.TorqueFree);


            //while (true)
            //{
            //    herkulexController.SetPosition(2, 512, 10, true);
            //    herkulexController.SetPosition(5, 512, 10, true);
            //    herkulexController.SetPosition(4, 512, 10, true);
            //    herkulexController.SendSynchronous(10);
            //    Thread.Sleep(300);
            //    herkulexController.SetPosition(2, 800, 10, true);
            //    herkulexController.SetPosition(5, 800, 10, true);
            //    herkulexController.SetPosition(4, 800, 10, true);
            //    herkulexController.SendSynchronous(10);
            //    Thread.Sleep(300);
            //}
            Thread.CurrentThread.Join();
        }

        private static void HerkulexController_HerkulexErrorEvent(object sender, HerkulexErrorArgs e)
        {

        }

        private static void HerkulexController_InfosUpdatedEvent(object sender, InfosUpdatedArgs e)
        {

        }

        private static void ScanAndPrint()
        {
            byte[] ID_Array;
            ID_Array = herkulexController.ScanForServoIDs(timeout: 1000, maxID: 5);

            foreach (byte e in ID_Array)
                Console.Write("[ " + e + " ] ");
        }
    }
}