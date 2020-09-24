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

            herkulexController.SetPollingFreq(10);
            //herkulexController.StartPolling();

            herkulexController.SetMaximumPosition(2, 1000, true);
            herkulexController.SetMinimumPosition(2, 0, true);

            herkulexController.SetPosition(1, 511, 10);

            Thread.Sleep(1000);

            herkulexController.SetID(4, 1);

            ScanAndPrint();

            herkulexController.StartPolling();

            while(true)
            {
                herkulexController.SetPosition(1, 800, 100);
                Thread.Sleep(2000);
                herkulexController.SetPosition(1, 400, 100);
                Thread.Sleep(2000);
            }

            Thread.CurrentThread.Join();
        }

        private static void ScanAndPrint()
        {
            byte[] ID_Array;
            ID_Array = herkulexController.ScanForServoIDs();

            foreach (byte e in ID_Array)
                Console.Write("[ " + e + " ] ");

            Console.WriteLine("Done");
        }

        private static void HerkulexController_HerkulexErrorEvent(object sender, HerkulexErrorArgs e)
        {
            //Console.WriteLine("error occured");
        }

        private static void HerkulexController_InfosUpdatedEvent(object sender, InfosUpdatedArgs e)
        {
            if (e.Servo.GetID() == 1)
            {
                Console.Write("Absolute Position " + e.Servo.ActualAbsolutePosition);
                Console.WriteLine(" | total NACK : " + herkulexController.GetNackCount());
                //Console.WriteLine("Servo " + e.Servo.GetID() + " IsMoving : " + e.Servo.IsMoving.ToString());
                //Console.WriteLine("Servo " + e.Servo.GetID() + " IsInPosition : " + e.Servo.IsInposition.ToString());
                //Console.WriteLine("Servo " + e.Servo.GetID() + " IsMotorOn : " + e.Servo.IsMotorOn.ToString());
                //Console.WriteLine("Servo " + e.Servo.GetID() + " CheckSumError : " + e.Servo.CheckSumError.ToString());
                //Console.WriteLine("Servo " + e.Servo.GetID() + " UnknownCommandError : " + e.Servo.UnknownCommandError.ToString());
                //Console.WriteLine("Servo " + e.Servo.GetID() + " ExceedRegRangeError : " + e.Servo.ExceedRegRangeError.ToString());
                //Console.WriteLine("Servo " + e.Servo.GetID() + " GarbageDetectedError : " + e.Servo.GarbageDetectedError.ToString());
                //Console.WriteLine("Servo " + e.Servo.GetID() + " Exceed_input_voltage_limit : " + e.Servo.Exceed_input_voltage_limit.ToString());
                //Console.WriteLine("Servo " + e.Servo.GetID() + " Exceed_allowed_pot_limit : " + e.Servo.Exceed_allowed_pot_limit.ToString());
                //Console.WriteLine("Servo " + e.Servo.GetID() + " Exceed_Temperature_limit : " + e.Servo.Exceed_Temperature_limit.ToString());
                //Console.WriteLine("Servo " + e.Servo.GetID() + " Invalid_packet : " + e.Servo.Invalid_packet.ToString());
                //Console.WriteLine("Servo " + e.Servo.GetID() + " Overload_detected : " + e.Servo.Overload_detected.ToString());
                //Console.WriteLine("Servo " + e.Servo.GetID() + " Driver_fault_detected : " + e.Servo.Driver_fault_detected.ToString());
                //Console.WriteLine("Servo " + e.Servo.GetID() + " EEP_REG_distorted : " + e.Servo.EEP_REG_distorted.ToString());
                //Console.WriteLine();

                herkulexController.SetPosition(2, e.Servo.ActualAbsolutePosition, 10);
            }

            if (e.Servo.GetID() == 1 && e.Servo.ActualAbsolutePosition > 512)
                herkulexController.SetLedColor(1, HerkulexDescription.LedColor.Red);
            if (e.Servo.GetID() == 1 && e.Servo.ActualAbsolutePosition <= 512)
                herkulexController.SetLedColor(1, HerkulexDescription.LedColor.Blue);
        }
    }
}
