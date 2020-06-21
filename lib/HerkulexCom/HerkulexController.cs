using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Net.Sockets;
using System.Net.Http;
using System.Security.Permissions;
using System.Diagnostics.Tracing;
using System.Collections;
using System.Runtime.CompilerServices;

/* Herkulex UART params:
Stop Bit : 1
Parity : None
Flow Control : None
Baud Rate : 57,600 / 115,200 / 0.2M / 0.25M / 0.4M / 0.5M / 0.667M

Maximum memory length for any adress in the servo memory is 2 bytes
Minimum packet lengh is 7 bytes

For 2 byte variables, using little-endian storage in both ram and rom memory (LSB first)
*/


namespace HerkulexController
{
    public class HerkulexSendControl
    {

        /// <summary>
        /// Writes to the specified servo RAM memory
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="pID">Servo ID</param>
        /// <param name="addr">Start memory address</param>
        /// <param name="length">Length of the data to write</param>
        /// <param name="value">data</param>
        public void RAM_WRITE(SerialPort port, byte pID, MEM_ADDR addr, byte length, UInt16 value)
        {
            byte pSIZE = (byte)(9 + length);
            byte[] packet = new byte[pSIZE];

            byte[] data = new byte[2 + length];
            data[0] = (byte)addr;
            data[1] = length;

            if(length >= 2)
            {
                data[3] = (byte)(value >> 0); //little endian, LSB first
                data[2] = (byte)(value >> 8);
            }
            else
                data[2] = (byte)(value);
            

            packet[0] = 0xFF;
            packet[1] = 0xFF;
            packet[2] = pSIZE;
            packet[3] = pID;
            packet[4] = (byte)ToServoCommandSet.RAM_WRITE;
            packet[5] = CheckSum1(packet[2], packet[3], packet[4], data);
            packet[6] = CheckSum2(packet[5]);

            for (int i = 0; i < data.Length; i++)
                packet[7 + i] = data[i];

            port.Write(packet, 0, packet.Length);

        }

         /// <summary>
        /// Sends I_JOG command
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="TAGS">list of tags to control each servo</param>
        public void I_JOG(SerialPort port, IJOG_TAG ServoTag)
        {
            byte[] packet = new byte[12];

            byte[] dataToSend = new byte[5];
            dataToSend[0] = (byte)(ServoTag.JOG >> 0);
            dataToSend[1] = (byte)(ServoTag.JOG >> 8);
            dataToSend[2] = ServoTag.SET;
            dataToSend[3] = ServoTag.ID;
            dataToSend[4] = ServoTag.playTime;

            packet[0] = 0xFF;
            packet[1] = 0xFF;
            packet[2] = 12;
            packet[3] = ServoTag.ID;
            packet[4] = (byte)ToServoCommandSet.I_JOG;
            packet[5] = CheckSum1(packet[2], packet[3], packet[4], dataToSend);
            packet[6] = CheckSum2(packet[5]);

            for (int i = 0; i < dataToSend.Length; i++)
                packet[7 + i] = dataToSend[i];

            port.Write(packet, 0, packet.Length);
        }

       
        /// <summary>
        /// all of the two bytes length only addresses (use in GetMemoryAddrLength)
        /// </summary>
        private enum TwoBytesCMD
        {
            Saturator_Slope = 12,                   //Byte length: 2
            Max_PWM = 16,                           //Byte length: 2
            Overload_PWM_Threshold = 18,            //Byte length: 2
            Min_Position = 20,                      //Byte length: 2
            Max_Position = 22,                      //Byte length: 2
            Position_Kp = 24,                       //Byte length: 2
            Position_Kd = 26,                       //Byte length: 2
            Position_Ki = 28,                       //Byte length: 2
            Pos_FreeFoward_1st_Gain = 30,           //Byte length: 2
            Pos_FreeFoward_2nd_Gain = 32,           //Byte length: 2
            Voltage = 54,                           //Byte length: 2
            Temperature = 55,                       //Byte length: 2
            Current_Control_Mode = 56,              //Byte length: 2
            Tick = 57,                              //Byte length: 2
            Calibrated_Position = 58,               //Byte length: 2
            Absolute_Position = 60,                 //Byte length: 2
            Differential_Position = 62,             //Byte length: 2
            PWM = 64,                               //Byte length: 2
            Absolute_Goal_Position = 68,            //Byte length: 2
            Absolute_Desired_Traject_Pos = 70,      //Byte length: 2
        }

        byte CheckSum1(byte pSIZE, byte pID, byte CMD, byte[] data)
        {
            byte checksum = (byte)(pSIZE ^ pID ^ CMD);
            for (int i = 0; i < data.Length; i++)
                checksum ^= data[i];
            checksum &= 0xFE;
            return checksum;
        }

        byte CheckSum2(byte checkSum1)
        {
            byte checkSum2 = (byte)((~checkSum1) & 0xFE);
            return checkSum2;
        }

        /// <summary>
        /// returns the memory length at the corresponing address
        /// </summary>
        /// <param name="ADDR">address</param>
        /// <returns></returns>
        public byte GetMemoryAddrLength(byte ADDR)
        {
            foreach (TwoBytesCMD cmd in Enum.GetValues(typeof(TwoBytesCMD)))
            {
                if (ADDR == (byte)cmd)
                {
                    return 0x02; //if the address belongs to the two bytes length set, return 2, exit func
                }
            }

            return 0x01; //if the address does not belongs to the two bytes length set, return 1, exit func
        }

    }

    /// <summary>
    /// Holds the Servo configuration for S_JOG / I_JOG
    /// </summary>
    public struct IJOG_TAG
    {
        public JOG_MODE mode;
        public byte ID;
        public byte playTime;
        public byte LED_GREEN;
        public byte LED_BLUE;
        public byte LED_RED;

        private byte _SET;
        public byte SET
        {
            get
            {
                _SET = 0;
                _SET |= (byte)((byte)mode << 1);
                _SET |= (byte)(LED_GREEN << 2);
                _SET |= (byte)(LED_BLUE << 3);
                _SET |= (byte)(LED_RED << 4);
                return _SET;
            }
        }

        private UInt16 _JOG;
        public UInt16 JOG
        {
            get => _JOG;
            set { _JOG = (ushort)(value & 0xEFFF); } //set bit 15 to 0
        }
    }

    /// <summary>
    /// Jog mode
    /// </summary>
    public enum JOG_MODE
    {
        positionControlJOG = 0,
        infiniteTurn = 1
    }

    /// <summary>
    ///all controller commands set
    /// </summary>
    public enum ToServoCommandSet
    {
        EEP_WRITE = 0x01,
        EEP_READ = 0x02,
        RAM_WRITE = 0x03,
        RAM_READ = 0x04,
        I_JOG = 0x05,
        S_JOG = 0x06,
        STAT = 0x07,
        ROLLBACK = 0x08,
        REBOOT = 0x09
    }

    /// <summary>
    /// all commands ACK set
    /// </summary>
    public enum ToControllerAckSet
    {
        ack_EEP_WRITE = 0x41,
        ack_EEP_READ = 0x42,
        ack_RAM_WRITE = 0x43,
        ack_RAM_READ = 0x44,
        ack_I_JOG = 0x45,
        ack_S_JOG = 0x46,
        ack_STAT = 0x47,
        ack_ROLLBACK = 0x48,
        ack_REBOOT = 0x49
    }

    /// <summary>
    /// all of the register addrs
    /// </summary>
    public enum MEM_ADDR
    {
        ID = 0,                                 //Byte length: 1
        ACK_Policy = 1,                         //Byte length: 1
        Alarm_LED_Policy = 2,                   //Byte length: 1
        Torque_policy = 3,                      //Byte length: 1
        Max_Temperature = 5,                    //Byte length: 1
        Min_Voltage = 6,                        //Byte length: 1
        Max_Voltage = 7,                        //Byte length: 1
        Acceleration_Ratio = 8,                 //Byte length: 1
        Max_Acceleration = 9,                   //Byte length: 1
        Dead_Zone = 10,                         //Byte length: 1
        Saturator_Offset = 11,                  //Byte length: 1
        Saturator_Slope = 12,                   //Byte length: 2
        PWM_Offset = 14,                        //Byte length: 1
        Min_PWM = 15,                           //Byte length: 1
        Max_PWM = 16,                           //Byte length: 2
        Overload_PWM_Threshold = 18,            //Byte length: 2
        Min_Position = 20,                      //Byte length: 2
        Max_Position = 22,                      //Byte length: 2
        Position_Kp = 24,                       //Byte length: 2
        Position_Kd = 26,                       //Byte length: 2
        Position_Ki = 28,                       //Byte length: 2
        Pos_FreeFoward_1st_Gain = 30,           //Byte length: 2
        Pos_FreeFoward_2nd_Gain = 32,           //Byte length: 2
        LED_Blink_Period = 38,                  //Byte length: 1
        ADC_Fault_Detect_Period = 39,           //Byte length: 1
        Packet_Garbage_Detection_Period = 40,   //Byte length: 1
        Stop_Detection_Period = 41,             //Byte length: 1
        Overload_Detection_Period = 42,         //Byte length: 1
        Stop_Threshold = 41,                    //Byte length: 1
        Inposition_Margin = 44,                 //Byte length: 1
        Calibration_Difference = 47,            //Byte length: 1
        Status_Error = 48,                      //Byte length: 1
        Status_Detail = 49,                     //Byte length: 1
        Torque_Control = 52,                    //Byte length: 1
        LED_Control = 53,                       //Byte length: 1
        Voltage = 54,                           //Byte length: 2
        Temperature = 55,                       //Byte length: 2
        Current_Control_Mode = 56,              //Byte length: 2
        Tick = 57,                              //Byte length: 2
        Calibrated_Position = 58,               //Byte length: 2
        Absolute_Position = 60,                 //Byte length: 2
        Differential_Position = 62,             //Byte length: 2
        PWM = 64,                               //Byte length: 2
        Absolute_Goal_Position = 68,            //Byte length: 2
        Absolute_Desired_Traject_Pos = 70,      //Byte length: 2
        Desired_Velocity = 72                   //Byte length: 1
    }
}