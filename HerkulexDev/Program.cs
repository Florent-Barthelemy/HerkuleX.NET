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

namespace HerkulexDev
{
    class Program
    {
        static HerkulexController herkulexController = new HerkulexController("COM3", 115200, Parity.None, 8, StopBits.One);

        static System.Threading.Timer timer;

        static ushort pos = 500;

        static void Main(string[] args)
        {
            herkulexController.InfosUpdatedEvent += HerkulexController_InfosUpdatedEvent;

            herkulexController.AddServo(1, HerkulexDescription.JOG_MODE.positionControlJOG);
            herkulexController.AddServo(2, HerkulexDescription.JOG_MODE.positionControlJOG);

            herkulexController.SetTorqueMode(1, HerkulexDescription.TorqueControl.TorqueFree);
            herkulexController.SetTorqueMode(2, HerkulexDescription.TorqueControl.TorqueFree);

            herkulexController.ClearErrors(1);
            herkulexController.ClearErrors(2);

            //init positions pos = 0 otherwise
            herkulexController.SetPosition(1, pos, 70);
            herkulexController.SetPosition(2, pos, 70);

            //beautiful colors
            herkulexController.SetLedColor(1, HerkulexDescription.LedColor.Cyan);
            herkulexController.SetLedColor(2, HerkulexDescription.LedColor.Megenta);

            //starts the polling (polling is paused by default)
            herkulexController.ResumePolling();
            herkulexController.SetPollingFreq(100);

            timer = new Timer((c) =>
            {
                herkulexController.SetPosition(1, pos, 10);
                herkulexController.SetPosition(2, pos, 10);
                if (pos < 800)
                    pos += 100;
                else
                    pos = 500;
            }, null, 0, 1500);

            Thread.CurrentThread.Join();
        }

        private static void HerkulexController_InfosUpdatedEvent(object sender, InfosUpdatedArgs e)
        {
            if(e.Servo.GetID() == 2)
            {
                /*
               Console.WriteLine("Servo " + e.Servo.GetID() + " IsInPosition : " + e.Servo.IsInposition.ToString());
               Console.WriteLine("Servo " + e.Servo.GetID() + " IsMotorOn : " + e.Servo.IsMotorOn.ToString());
               Console.WriteLine("Servo " + e.Servo.GetID() + " CheckSumError : " + e.Servo.CheckSumError.ToString());
               Console.WriteLine("Servo " + e.Servo.GetID() + " UnknownCommandError : " + e.Servo.UnknownCommandError.ToString());
               Console.WriteLine("Servo " + e.Servo.GetID() + " ExceedRegRangeError : " + e.Servo.ExceedRegRangeError.ToString());
               Console.WriteLine("Servo " + e.Servo.GetID() + " GarbageDetectedError : " + e.Servo.GarbageDetectedError.ToString());
               Console.WriteLine();
               */
                Console.WriteLine("Servo " + e.Servo.GetID() + " Actual absolute position : " + e.Servo.ActualAbsolutePosition.ToString());
            }
        }
    }
}
