using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using HerkulexControl;
using ExtendedSerialPort;
using System.Windows.Threading;
using SharpDX.XInput;
using System.Diagnostics;
using System.Windows.Forms.VisualStyles;
using SharpDX;
using System.Threading;
using System.Windows.Forms;
using System.Timers;

namespace HerkuleXControlShell
{
    class Program
    {
        
        static ReliableSerialPort ComPort = new ReliableSerialPort("COM7", 115200, Parity.None, 8, StopBits.One);
        static HklxController HrController = new HklxController();
        static HklxDecoder HrDecoder = new HklxDecoder();

        //floating servo config
        static JOG_TAG floatingConfig = new JOG_TAG
        {
            mode = JOG_MODE.positionControlJOG,
            playTime = 10,
            LED_BLUE = 0,
            LED_GREEN = 1,
            LED_RED = 1,
            JOG = 300
        };

        static JOG_TAG Ser1Config = new JOG_TAG
        {
            mode = JOG_MODE.positionControlJOG,
            ID = 1,
            playTime = 60,
            LED_BLUE = 1,
            LED_RED = 0,
            LED_GREEN = 0,
            JOG = 512
        };

        static JOG_TAG Ser2Config = new JOG_TAG
        {
            mode = JOG_MODE.positionControlJOG,
            ID = 2,
            playTime = 10,
            LED_BLUE = 1,
            LED_RED = 1,
            LED_GREEN = 0,
            JOG = 512
        };

        static List<JOG_TAG> ROBOT_ARM_CONFIG;

        static bool lockShell = true;

        static void Main(string[] args)
        {
            HrController.port = ComPort;

            ComPort.DataReceived += HrDecoder.DecodePacket;

            HrDecoder.OnDataDecodedEvent += HrDecoder.ProcessPacket;
            HrDecoder.OnStatAckEvent += HrDecoder_OnStatAckEvent;
            HrDecoder.OnIjogAckEvent += HrDecoder_OnIjogAckEvent;
            HrDecoder.OnSjogAckEvent += HrDecoder_OnSjogAckEvent;
            HrDecoder.OnRamReadAckEvent += HrDecoder_OnRamReadAckEvent;
            HrDecoder.OnEepReadAckEvent += HrDecoder_OnEepReadAckEvent;
            ComPort.Open();

            while (lockShell)
            {
                
                Console.Write('>');
                string[] parsedCmd = ParseShellCmd(Console.ReadLine());

                switch(parsedCmd[0])
                {
                    case "REBOOT":
                        if (parsedCmd.Length == 1)
                            Console.WriteLine("REBOOT <pID>");
                        else
                            HrController.REBOOT(Convert.ToByte(parsedCmd[1]));
                        break;
                    case "ROOLBACK":
                        if (parsedCmd.Length == 1)
                            Console.WriteLine("ROLLBACK <ID>");
                        else
                            HrController.ROLLBACK(Convert.ToByte(parsedCmd[1]));
                        break;

                    case "EEP_WRITE":
                        if (parsedCmd.Length == 1)
                            Console.WriteLine("EEP_WRITE <pID> <StartAddr> <Length> <DataDecimal>");
                        else
                            HrController.EEP_WRITE(Convert.ToByte(parsedCmd[1]), Convert.ToByte(parsedCmd[2]),
                                                  Convert.ToByte(parsedCmd[3]), Convert.ToUInt16(parsedCmd[4]));
                        break;

                    case "EEP_READ":
                        if (parsedCmd.Length == 1)
                            Console.WriteLine("EEP_READ <pID> <StartAddr> <Length>");
                        else
                            HrController.EEP_READ(Convert.ToByte(parsedCmd[1]), Convert.ToByte(parsedCmd[2]), Convert.ToByte(parsedCmd[3]));
                        break;

                    case "STAT":
                        if (parsedCmd.Length == 1)
                            Console.WriteLine("STAT <pID>");
                        else
                            HrController.STAT(Convert.ToByte(parsedCmd[1]));
                        break;

                    //for testing incredible things
                    case "debug":
                        if (parsedCmd.Length == 1)
                            Console.WriteLine("debug <JOG1> <JOG2>");
                        else
                        {
                            Ser1Config.JOG = Convert.ToUInt16(parsedCmd[1]);
                            Ser2Config.JOG = Convert.ToUInt16(parsedCmd[2]);
                            
                            ROBOT_ARM_CONFIG = new List<JOG_TAG>
                             {
                                 Ser1Config,
                                 Ser2Config
                             };

                            HrController.S_JOG(ROBOT_ARM_CONFIG, 0x3C);
                        }
                        break;

                    //implemented, working
                    case "moveto":
                        if (parsedCmd.Length == 1)
                            Console.WriteLine("moveto <ID> <JOG>");
                        else
                        {
                            floatingConfig.ID = Convert.ToByte(parsedCmd[1]);
                            floatingConfig.JOG = Convert.ToUInt16(parsedCmd[2]);
                            HrController.S_JOG(floatingConfig, 10);
                        }
                        break;

                    case "GetPos":
                        if (parsedCmd.Length == 1)
                            Console.WriteLine("GetPos <pID>");
                        else
                        {
                            HrController.RAM_READ(Convert.ToByte(parsedCmd[1]), (byte)RAM_ADDR.Absolute_Position, 2);
                        }
                        break;

                    //implemented, working
                    case "torqueControl":
                        if (parsedCmd.Length == 1)
                            Console.WriteLine("torqueControl <ID> <TRK_ON, BRK_ON, TRK_FREE>");
                        else
                        {
                            if(parsedCmd[2] == "TRK_ON")
                                HrController.RAM_WRITE(Convert.ToByte(parsedCmd[1]), (byte)RAM_ADDR.Torque_Control, 1, 0x60);
                            if (parsedCmd[2] == "BRK_ON")
                                HrController.RAM_WRITE(Convert.ToByte(parsedCmd[1]), (byte)RAM_ADDR.Torque_Control, 1, 0x40);
                            if (parsedCmd[2] == "TRK_FREE")
                                HrController.RAM_WRITE(Convert.ToByte(parsedCmd[1]), (byte)RAM_ADDR.Torque_Control, 1, 0x00);
                        }
                        break;

                    case "RAM_READ":
                        if (parsedCmd.Length == 1)
                            Console.WriteLine("RAM_READ <pID> <StartAddr> <Length>");
                        else
                        {
                            HrController.RAM_READ(Convert.ToByte(parsedCmd[1]), Convert.ToByte(parsedCmd[2]), Convert.ToByte(parsedCmd[3]));
                        }
                        break;

                    case "RAM_WRITE":
                        if (parsedCmd.Length == 1)
                            Console.WriteLine("RAM_WRITE <pID> <StartAddr> <Length> <Value> {ADDR 48 / 49 for error status}");
                        else
                        {
                            HrController.RAM_WRITE(Convert.ToByte(parsedCmd[1]), Convert.ToByte(parsedCmd[2]),
                                                   Convert.ToByte(parsedCmd[3]), Convert.ToUInt16(parsedCmd[4]));
                        }
                        break;

                    case "initPos":
                        floatingConfig.ID = 1;
                        floatingConfig.JOG = 512;
                        floatingConfig.playTime = 100;
                        HrController.I_JOG(floatingConfig); 

                        floatingConfig.ID = 2;
                        floatingConfig.JOG = 512;
                        floatingConfig.playTime = 100;
                        HrController.I_JOG(floatingConfig);
                        break;

                    case "allFree":
                        HrController.RAM_WRITE(1, (byte)RAM_ADDR.Torque_Control, 1, 0x00);
                        HrController.RAM_WRITE(2, (byte)RAM_ADDR.Torque_Control, 1, 0x00);
                        break;

                    case "allOn":
                        HrController.RAM_WRITE(1, (byte)RAM_ADDR.Torque_Control, 1, 0x60);
                        HrController.RAM_WRITE(2, (byte)RAM_ADDR.Torque_Control, 1, 0x60);
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

                    case "ResolveErrs":
                        if (parsedCmd.Length == 1)
                            Console.WriteLine("ResolveErrs <pID>");
                        else
                        {
                            HrController.RAM_WRITE(Convert.ToByte(parsedCmd[1]), (byte)RAM_ADDR.Status_Error, 1, 0x00);
                            HrController.RAM_WRITE(Convert.ToByte(parsedCmd[1]), (byte)RAM_ADDR.Torque_Control, 1, 0x60);
                        }
                        break;

                    case "quit":
                        ComPort.Close();
                        lockShell = false;
                        break;
                }
            }
        }


        private static void HrDecoder_OnRamReadAckEvent(object sender, Hklx_RAM_READ_Ack_Args e)
        {
            int errCount = e.StatusErrors.Count;
            Console.WriteLine("RAM_READ ACK RECEIVED, Got " + errCount + " error(s)\nStart Address : " + e.Address + "\nLength : " + e.Length + "\n");
            Console.Write("DATA: ");
            foreach (byte b in e.ReceivedData)
                Console.Write(b.ToString("X2") + " ");

            Console.WriteLine();
        }

        private static void HrDecoder_OnEepReadAckEvent(object sender, Hklx_EEP_READ_Ack_Args e)
        {
            int errCount = e.StatusErrors.Count;
            Console.WriteLine("EEP_READ ACK RECEIVED, Got " + errCount + " error(s)\nStart Address : " + e.Address + "\nLength : " + e.Length + "\n");
            Console.Write("DATA: ");
            foreach (byte b in e.ReceivedData)
                Console.Write(b.ToString("X2") + " ");

            Console.WriteLine();
        }

        private static void HrDecoder_OnSjogAckEvent(object sender, Hklx_S_JOG_Ack_Args e)
        {
            int errCount = e.StatusErrors.Count;
            Console.WriteLine("S_JOG ACK RECEIVED, Got " + errCount + " error(s)");
        }

        private static void HrDecoder_OnIjogAckEvent(object sender, Hklx_I_JOG_Ack_Args e)
        {
            int errCount = e.StatusErrors.Count;
            Console.WriteLine("I_JOG ACK RECEIVED, Got " + errCount + " error(s)");
        }

        private static void HrDecoder_OnStatAckEvent(object sender, Hklx_STAT_Ack_Args e)
        {
            int errCount = e.StatusErrors.Count;
            List<ErrorStatus> errs = e.StatusErrors;
            Console.WriteLine("STAT ACK RECEIVED, Got " + errCount + " error(s)");
        }

        private static string[] ParseShellCmd(string cmd)
        {
            string[] args = cmd.Split(' ');
            return args;
        }
    }
}
