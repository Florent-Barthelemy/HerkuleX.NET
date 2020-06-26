using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;

using HerkulexController;
using ExtendedSerialPort;
using System.Threading;

namespace HerkuleXControlShell
{
    class Program
    {
        static ReliableSerialPort ComPort = new ReliableSerialPort("COM7", 115200, Parity.None, 8, StopBits.One);
        static HklxController HrController = new HklxController();
        static HklxDecoder HrDecoder = new HklxDecoder();

        //floating servo config
        static IJOG_TAG floatingConfig = new IJOG_TAG
        {
            ID = 0x50,
            mode = JOG_MODE.positionControlJOG,
            playTime = 10,
            LED_BLUE = 0,
            LED_GREEN = 1,
            LED_RED = 1,
            JOG = 300
        };

        static bool lockShell = true;
        static byte currentErrorStatus;

        static void Main(string[] args)
        {

            ComPort.DataReceived += HrDecoder.DecodePacket;
            HrDecoder.OnDataDecodedEvent += HrDecoder_OnDataDecodedEvent;
            HrDecoder.OnStatusErrorFromAckEvent += HrDecoder_OnStatusErrorFromAckEvent;

            ComPort.Open();

            while (lockShell)
            {
                Console.Write('>');
                string[] parsedCmd = ParseShellCmd(Console.ReadLine());

                switch(parsedCmd[0])
                {
                    //implemented, working
                    case "moveto":
                        if (parsedCmd.Length == 1)
                            Console.WriteLine("moveto <ID> <JOG>");
                        else
                        {
                            floatingConfig.ID = Convert.ToByte(parsedCmd[1]);
                            floatingConfig.JOG = Convert.ToUInt16(parsedCmd[2]);
                            HrController.I_JOG(ComPort, floatingConfig);
                        }
                        break;

                    //implemented, working
                    case "torqueControl":
                        if (parsedCmd.Length == 1)
                            Console.WriteLine("torqueControl <ID> <TRK_ON, BRK_ON, TRK_FREE>");
                        else
                        {
                            if(parsedCmd[2] == "TRK_ON")
                                HrController.RAM_WRITE(ComPort, Convert.ToByte(parsedCmd[1]), (byte)RAM_ADDR.Torque_Control, 1, 0x60);
                            if (parsedCmd[2] == "BRK_ON")
                                HrController.RAM_WRITE(ComPort, Convert.ToByte(parsedCmd[1]), (byte)RAM_ADDR.Torque_Control, 1, 0x40);
                            if (parsedCmd[2] == "TRK_FREE")
                                HrController.RAM_WRITE(ComPort, Convert.ToByte(parsedCmd[1]), (byte)RAM_ADDR.Torque_Control, 1, 0x00);
                        }
                        break;

                    case "RAM_READ":
                        if (parsedCmd.Length == 1)
                            Console.WriteLine("RAM_READ <pID> <StartAddr> <Length>");
                        else
                        {
                            HrController.RAM_READ(ComPort, Convert.ToByte(parsedCmd[1]), Convert.ToByte(parsedCmd[2]), Convert.ToByte(parsedCmd[3]));
                        }
                        break;

                    case "RAM_WRITE":
                        if (parsedCmd.Length == 1)
                            Console.WriteLine("RAM_WRITE <pID> <StartAddr> <Length> <Value> {ADDR 48 / 49 for error status}");
                        else
                        {
                            HrController.RAM_WRITE(ComPort, Convert.ToByte(parsedCmd[1]), Convert.ToByte(parsedCmd[2]),
                                                   Convert.ToByte(parsedCmd[3]), Convert.ToUInt16(parsedCmd[4]));
                        }
                        break;

                    case "initPos":
                        floatingConfig.ID = 1;
                        floatingConfig.JOG = 300;
                        floatingConfig.playTime = 100;
                        HrController.I_JOG(ComPort, floatingConfig); 

                        floatingConfig.ID = 2;
                        floatingConfig.JOG = 500;
                        floatingConfig.playTime = 100;
                        HrController.I_JOG(ComPort, floatingConfig);
                        break;

                    case "allFree":
                        HrController.RAM_WRITE(ComPort, 1, (byte)RAM_ADDR.Torque_Control, 1, 0x00);
                        HrController.RAM_WRITE(ComPort, 2, (byte)RAM_ADDR.Torque_Control, 1, 0x00);
                        break;

                    case "allOn":
                        HrController.RAM_WRITE(ComPort, 1, (byte)RAM_ADDR.Torque_Control, 1, 0x60);
                        HrController.RAM_WRITE(ComPort, 2, (byte)RAM_ADDR.Torque_Control, 1, 0x60);
                        break;

                    case "disconnect":
                        ComPort.Close();
                        break;

                    case "connect":
                        ComPort.Open();
                        break;

                    case "list":
                        Console.WriteLine("moveto <ID> <JOG>");
                        Console.WriteLine("torqueControl <ID> <TRK_ON, BRK_ON, TRK_FREE>");
                        Console.WriteLine("RAM_READ <pID> <StartAddr> <Length>");
                        Console.WriteLine("RAM_WRITE <pID> <StartAddr> <Length> <Value> {ADDR 48 / 49 for error status}");
                        Console.WriteLine("initPos");
                        Console.WriteLine("allFree");
                        Console.WriteLine("allOn");
                        Console.WriteLine("disconnect");
                        Console.WriteLine("connect");
                        break;

                    case "CheckErrors":
                        if (parsedCmd.Length == 1)
                            Console.WriteLine("CheckErrors <pID>");
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;

                            HrController.RAM_READ(ComPort, Convert.ToByte(parsedCmd[1]), (byte)RAM_ADDR.Status_Error, 1);
                        List<ErrorStatus> StatusErrors = CommonMethods.GetErrorStatusFromByte(currentErrorStatus);

                            foreach (ErrorStatus err in StatusErrors)
                            {
                                if (err == ErrorStatus.Driver_fault_detected)
                                    Console.Write("Driver fault Detected, ");

                                else if (err == ErrorStatus.EEP_REG_distorted)
                                    Console.Write("EEP Reg Distorted, ");

                                else if (err == ErrorStatus.Exceed_allowed_pot_limit)
                                    Console.Write("Allowed pot limit Exceeded, ");

                                else if (err == ErrorStatus.Exceed_input_voltage_limit)
                                    Console.Write("Input Voltage exceeds policy, ");

                                else if (err == ErrorStatus.Exceed_Temperature_limit)
                                    Console.Write("Max temperature Exceeded, ");

                                else if (err == ErrorStatus.Invalid_packet)
                                    Console.Write("Packet invalid, ");

                                else if (err == ErrorStatus.Overload_detected)
                                    Console.Write("Servo Overload");
                                else
                                    Console.Write("No errors");
                            }
                        
                        }

                        Console.ForegroundColor = ConsoleColor.White;
                        break;

                    case "ResolveErrs":
                        if (parsedCmd.Length == 1)
                            Console.WriteLine("ResolveErrs <pID>");
                        else
                        {
                            HrController.RAM_WRITE(ComPort, Convert.ToByte(parsedCmd[1]), (byte)RAM_ADDR.Status_Error, 1, 0x00);
                            HrController.RAM_WRITE(ComPort, Convert.ToByte(parsedCmd[1]), (byte)RAM_ADDR.Torque_Control, 1, 0x60);
                        }
                        break;

                    case "quit":
                        ComPort.Close();
                        lockShell = false;
                        break;
                }
            }
        }

        private static void HrDecoder_OnDataDecodedEvent(object sender, HklxPacketDecodedArgs e)
        {
            if(e.CMD == (byte)CommandAckSet.ack_RAM_READ)
            {
                currentErrorStatus = e.PacketData[2];
            }
        }

        private static void HrDecoder_OnStatusErrorFromAckEvent(object sender, HklxStatusErrorFromAckArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;

            foreach(ErrorStatus err in e.StatusErrors)
            {
                if (err == ErrorStatus.Driver_fault_detected)
                    Console.Write("Driver fault Detected, ");

                if(err == ErrorStatus.EEP_REG_distorted)
                    Console.Write("EEP Reg Distorted, ");

                if (err == ErrorStatus.Exceed_allowed_pot_limit)
                    Console.Write("Allowed pot limit Exceeded, ");

                if (err == ErrorStatus.Exceed_input_voltage_limit)
                    Console.Write("Input Voltage exceeds policy, ");

                if (err == ErrorStatus.Exceed_Temperature_limit)
                    Console.Write("Max temperature Exceeded, ");

                if (err == ErrorStatus.Invalid_packet)
                    Console.Write("Packet invalid, ");

                if (err == ErrorStatus.Overload_detected)
                    Console.Write("Servo Overload");
            }

            Console.ForegroundColor = ConsoleColor.White;
        }

        private static string[] ParseShellCmd(string cmd)
        {
            string[] args = cmd.Split(' ');
            return args;
        }
    }
}
