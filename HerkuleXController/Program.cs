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
            ID = 0x01,
            mode = JOG_MODE.positionControlJOG,
            playTime = 10,
            LED_BLUE = 0,
            LED_GREEN = 1,
            LED_RED = 1,
            JOG = 300
        };

        static bool lockShell = true;

        static void Main(string[] args)
        {

            ComPort.DataReceived += HrDecoder.DecodePacket;
            HrDecoder.OnDataDecodedEvent += HrDecoder_OnHerkulexIncommingMessageDecodedEvent;

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
                                HrController.RAM_WRITE(ComPort, Convert.ToByte(parsedCmd[1]), RAM_ADDR.Torque_Control, 1, 0x60);
                            if (parsedCmd[2] == "BRK_ON")
                                HrController.RAM_WRITE(ComPort, Convert.ToByte(parsedCmd[1]), RAM_ADDR.Torque_Control, 1, 0x40);
                            if (parsedCmd[2] == "TRK_FREE")
                                HrController.RAM_WRITE(ComPort, Convert.ToByte(parsedCmd[1]), RAM_ADDR.Torque_Control, 1, 0x00);
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

                    case "disconnect":
                        ComPort.Close();
                        break;

                    case "connect":
                        ComPort.Open();
                        break;

                    case "quit":
                        ComPort.Close();
                        lockShell = false;
                        break;
                }
            }
        }

        private static void HrDecoder_OnHerkulexIncommingMessageDecodedEvent(object sender, LocalEventArgsLibrary.HerkulexPacketDecodedArgs e)
        {
            
        }

        private static string[] ParseShellCmd(string cmd)
        {
            string[] args = cmd.Split(' ');
            return args;
        }
    }
}
