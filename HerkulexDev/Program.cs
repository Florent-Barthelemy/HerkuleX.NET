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

namespace HerkulexDev
{
    class Program
    {
        static HerkulexController herkulexController = new HerkulexController("COM3", 115200, Parity.None, 8, StopBits.One);
        static System.Threading.Timer blinkTimer;

        [MTAThread]
        static void Main(string[] args)
        {
            herkulexController.InfosUpdatedEvent += HerkulexController_InfosUpdatedEvent;
            herkulexController.HerkulexErrorEvent += HerkulexController_HerkulexErrorEvent;

            herkulexController.AutoRecoverMode = true;
            herkulexController.SetAckTimeout(50);

            

            herkulexController.AddServo(1, HerkulexDescription.JOG_MODE.positionControlJOG, 512);
            herkulexController.AddServo(2, HerkulexDescription.JOG_MODE.positionControlJOG, 512);


            herkulexController.SetTorqueMode(1, HerkulexDescription.TorqueControl.TorqueOn);
            herkulexController.SetTorqueMode(2, HerkulexDescription.TorqueControl.TorqueOn);

            herkulexController.ClearErrors(1);
            herkulexController.ClearErrors(2);

            //beautiful colors
            herkulexController.SetLedColor(1, HerkulexDescription.LedColor.Cyan);
            herkulexController.SetLedColor(2, HerkulexDescription.LedColor.Megenta);

            //10Hz polling frequency
            herkulexController.SetPollingFreq(20);

            Thread.Sleep(1000);

            //paused by default
            herkulexController.StartPolling();

           

          
            while(true)
            {
                herkulexController.SetPosition(1, 512, 10, true);
                herkulexController.SetPosition(2, 512, 10, true);
                herkulexController.SendSynchronous(10);
                Thread.Sleep(200);

                herkulexController.SetPosition(1, 700, 10, true);
                herkulexController.SetPosition(2, 700, 10, true);

                herkulexController.SendSynchronous(10);
                Thread.Sleep(200);
            }

            Thread.CurrentThread.Join();
        }

        private static void ScanAndPrint()
        {
            byte[] ID_Array;
            ID_Array = herkulexController.ScanForServoIDs(maxID : 10);

            foreach (byte e in ID_Array)
                Console.Write("[ " + e + " ] ");
        }

        

        private static void HerkulexController_HerkulexErrorEvent(object sender, HerkulexErrorArgs e)
        {
            //Console.WriteLine("error occured");
        }

        private static void HerkulexController_InfosUpdatedEvent(object sender, InfosUpdatedArgs e)
        {
            //Console.WriteLine("ID : " + e.Servo.GetID() + " Position : " + e.Servo.ActualAbsolutePosition);
        }
    }
}
