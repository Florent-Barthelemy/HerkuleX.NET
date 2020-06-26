using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Net;
using LocalEventArgsLibrary;


/* Herkulex UART params:
Stop Bit : 1
Parity : None
Flow Control : None
Baud Rate : 57,600 / 115,200 / 0.2M / 0.25M / 0.4M / 0.5M / 0.667M

Maximum memory length for any adress in the servo memory is 2 bytes
Minimum packet lengh is 7 bytes

For 2 byte variables, using little-endian storage in both ram and rom memory (LSB first)

Last 2 bytes of ack packet is Status error, status detail
*/


namespace HerkulexController
{
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
    /// Error Status
    /// </summary>
    public enum ErrorStatus
    {
        Exceed_input_voltage_limit = 1,
        Exceed_allowed_pot_limit = 2,
        Exceed_Temperature_limit = 4,
        Invalid_packet = 8,
        Overload_detected = 16,
        Driver_fault_detected = 32,
        EEP_REG_distorted = 64,
    }

    /// <summary>
    /// Error Status Detail
    /// </summary>
    public enum ErrorStatusDetail
    {
        Moving_flag = 1,
        Inposition_flag = 2,
        CheckSumError = 4,
        Unknown_Command = 8,
        Exceed_REG_RANGE = 16,
        Garbage_detected = 32,
        MOTOR_ON_flag = 64
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
    public enum CommandSet
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
    public enum CommandAckSet
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
    /// all RAM addresses
    /// </summary>
    public enum RAM_ADDR
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

    /// <summary>
    /// all EEP addresses
    /// </summary>
    public enum EEP_ADDR
    {
        Model_No1 = 0,
        Model_NO2 = 1,
        Version1 = 2,
        Version2 = 3,
        BaudRate = 4,
        ID = 6,
        ACK_Policy = 7,
        Alarm_LED_Policy = 8,
        Torque_Policy = 9,
        Max_Temperature = 11,
        Min_Voltage = 12,
        Max_Voltage = 13,
        Acceleration_Ratio = 14,
        Max_Acceleration_Time = 15,
        Dead_Zone = 16,
        Saturator_Offset = 17,
        Saturator_Slope = 18,
        PWM_Offset = 20,
        Min_PWM = 21,
        Max_PWM = 22,
        Overload_PWM_Threshold = 24,
        Min_Position = 26,
        Max_Position = 28,
        Position_Kp = 30,
        Position_Kd = 32,
        Position_Ki = 34,
        Position_FreeForward_1st_Gain = 36,
        Position_FreeForward_2st_Gain = 38,
        LED_Blink_Period = 44,
        ADC_Fault_Check_Period = 45,
        Packet_Garbage_Check_Period = 46,
        Stop_Detection_Period = 47,
        Overload_Detection_Period = 48,
        Stop_Threshold = 49,
        Inposition_Margin = 50,
        Calibration_Difference = 53,
    }

    public class HklxController
    {
        /// <summary>
        /// Reads the specified number of bytes from RAM
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="pID">Servo ID</param>
        /// <param name="startAddr">Address to start from</param>
        /// <param name="length">Number of bytes to read</param>
        public void RAM_READ(SerialPort port, byte pID, byte startAddr, byte length)
        {
            byte pSIZE = 9;
            byte[] packet = new byte[pSIZE];
            byte[] data = { (byte)startAddr, length };

            packet[0] = 0xFF;
            packet[1] = 0xFF;
            packet[2] = pSIZE;
            packet[3] = pID;
            packet[4] = (byte)CommandSet.RAM_READ;
            packet[5] = CommonMethods.CheckSum1(packet[2], packet[3], packet[4], data);
            packet[6] = CommonMethods.CheckSum2(packet[5]);
            packet[7] = data[0];
            packet[8] = data[1];

            port.Write(packet, 0, packet.Length);
        }

        /// <summary>
        /// Writes to the specified RAM address
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="pID">Servo ID</param>
        /// <param name="addr">Start memory address</param>
        /// <param name="length">Length of the data to write</param>
        /// <param name="value">data</param>
        public void RAM_WRITE(SerialPort port, byte pID, byte addr, byte length, UInt16 value)
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
            packet[4] = (byte)CommandSet.RAM_WRITE;
            packet[5] = CommonMethods.CheckSum1(packet[2], packet[3], packet[4], data);
            packet[6] = CommonMethods.CheckSum2(packet[5]);

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
            packet[4] = (byte)CommandSet.I_JOG;
            packet[5] = CommonMethods.CheckSum1(packet[2], packet[3], packet[4], dataToSend);
            packet[6] = CommonMethods.CheckSum2(packet[5]);

            for (int i = 0; i < dataToSend.Length; i++)
                packet[7 + i] = dataToSend[i];

            port.Write(packet, 0, packet.Length);
        }

    }

   public class HklxDecoder
    {
        ReceptionStates rcvState = ReceptionStates.Waiting;

        private byte packetSize = 0;
        private byte pID = 0;
        private byte cmd = 0;
        private byte checkSum1 = 0;
        private byte checkSum2 = 0;
        private byte[] packetData;

        private byte packetDataByteIndex = 0;

        private enum ReceptionStates
        {
            Waiting,
            sof2,
            packetSize,
            pID,
            CMD,
            checkSum1,
            checkSum2,
            data
        }

        //DataReceived input event, decoding the packet with a state machine for each received bytes
        public void DecodePacket(object sender, DataReceivedArgs e)
        {
            foreach (byte b in e.Data)
            {

                //state machine
                switch (rcvState)
                {
                    case ReceptionStates.Waiting:
                        if (b == 0xFF)
                            rcvState = ReceptionStates.sof2;
                        break;

                    case ReceptionStates.sof2:
                        if (b == 0xFF)
                            rcvState = ReceptionStates.packetSize;
                        break;

                    case ReceptionStates.packetSize:
                        packetSize = b;
                        packetData = new byte[packetSize - 7]; //init to the data size
                        rcvState = ReceptionStates.pID;
                        break;

                    case ReceptionStates.pID:
                        pID = b;
                        rcvState = ReceptionStates.CMD;
                        break;

                    case ReceptionStates.CMD:
                        cmd = b;
                        rcvState = ReceptionStates.checkSum1;
                        break;

                    case ReceptionStates.checkSum1:
                        checkSum1 = b;
                        rcvState = ReceptionStates.checkSum2;
                        break;

                    case ReceptionStates.checkSum2:
                        checkSum2 = b;
                        rcvState = ReceptionStates.data;
                        break;

                    case ReceptionStates.data:
                        if (packetDataByteIndex < packetData.Length)
                        {
                            packetData[packetDataByteIndex] = b;
                            packetDataByteIndex++;
                            if(packetDataByteIndex == packetData.Length)
                            {
                                packetDataByteIndex = 0;

                                byte chkSum1 = CommonMethods.CheckSum1(packetSize, pID, cmd, packetData);
                                byte chkSum2 = CommonMethods.CheckSum2(chkSum1);

                                byte StatusError = packetData[packetData.Length - 2];
                                byte StatusErrorDetail = packetData[packetData.Length - 1];

                                List<ErrorStatus> StatusErrors = CommonMethods.GetErrorStatusFromByte(StatusError);
                                List<ErrorStatusDetail> statusErrorDetails = GetErrorStatusDetailFromByte(StatusErrorDetail);

                                if (checkSum1 != chkSum1 || checkSum2 != chkSum1)
                                    OnCheckSumErrorOccured(checkSum1, checkSum2);
                                else
                                    OnDataDecoded(packetSize, pID, cmd, checkSum1, checkSum2, packetData);

                                if (StatusError != 0x00 || StatusErrorDetail != 0x00)
                                    OnStatusErrorFromAck(StatusErrors, statusErrorDetails);

                                rcvState = ReceptionStates.Waiting; //back to waiting
                            }
                        }
                        break;
                }
            }
        } 


        private List<ErrorStatusDetail> GetErrorStatusDetailFromByte(byte b)
        {
            List<ErrorStatusDetail> errorList = new List<ErrorStatusDetail>();
            for (int i = 0; i < 8; i++)
            {
                if ((byte)((b >> i) & 0x01) == 1)
                {
                    if (i == 0)
                        errorList.Add(ErrorStatusDetail.Moving_flag);
                    if (i == 1)
                        errorList.Add(ErrorStatusDetail.Inposition_flag);
                    if (i == 2)
                        errorList.Add(ErrorStatusDetail.CheckSumError);
                    if (i == 3)
                        errorList.Add(ErrorStatusDetail.Unknown_Command);
                    if (i == 4)
                        errorList.Add(ErrorStatusDetail.Exceed_REG_RANGE);
                    if (i == 5)
                        errorList.Add(ErrorStatusDetail.Garbage_detected);
                    if (i == 6)
                        errorList.Add(ErrorStatusDetail.MOTOR_ON_flag);
                }
            }
            return errorList;
        }

        #region eventGeneration
        public event EventHandler<HklxPacketDecodedArgs> OnDataDecodedEvent;
        public event EventHandler<HklxCheckSumErrorOccured> OnCheckSumErrorOccuredEvent;
        public event EventHandler<HklxStatusErrorFromAckArgs> OnStatusErrorFromAckEvent;

        public virtual void OnCheckSumErrorOccured(byte checkSum1, byte checkSum2)
        {
            var handler = OnCheckSumErrorOccuredEvent;
            if(handler != null)
            {
                handler(this, new HklxCheckSumErrorOccured
                {
                    CheckSum1 = checkSum1,
                    CheckSum2 = checkSum2
                });
            }
        }

        public virtual void OnStatusErrorFromAck(List<ErrorStatus> statusErrors, List<ErrorStatusDetail> statusDetails)
        {
            var handler = OnStatusErrorFromAckEvent;
            if(handler != null)
            {
                handler(this, new HklxStatusErrorFromAckArgs
                {
                    StatusErrors = statusErrors,
                    StatusDetails = statusDetails
                });
            }
        }

        public virtual void OnDataDecoded(byte packetSize, byte pID, byte cmd, byte checkSum1, byte checkSum2, byte[] packetData)
        {
            var handler = OnDataDecodedEvent;
            if (handler != null)
            {
                handler(this, new HklxPacketDecodedArgs
                {
                    PacketSize = packetSize,
                    PID = pID,
                    CMD = cmd,
                    CheckSum1 = checkSum1,
                    CheckSum2 = checkSum2,
                    PacketData = packetData
                });
            }
        }
        #endregion eventGeneration
    }

   public static class CommonMethods
    {
        public static byte CheckSum1(byte pSIZE, byte pID, byte CMD, byte[] data)
        {
            byte checksum = (byte)(pSIZE ^ pID ^ CMD);
            for (int i = 0; i < data.Length; i++)
                checksum ^= data[i];
            checksum &= 0xFE;
            return checksum;
        }

        public static byte CheckSum2(byte checkSum1)
        {
            byte checkSum2 = (byte)((~checkSum1) & 0xFE);
            return checkSum2;
        }

        public static List<ErrorStatus> GetErrorStatusFromByte(byte b)
        {
            List<ErrorStatus> errorList = new List<ErrorStatus>();
            for (int i = 0; i < 8; i++)
            {
                if ((byte)((b >> i) & 0x01) == 1)
                {
                    if (i == 0)
                        errorList.Add(ErrorStatus.Exceed_input_voltage_limit);
                    if (i == 1)
                        errorList.Add(ErrorStatus.Exceed_allowed_pot_limit);
                    if (i == 2)
                        errorList.Add(ErrorStatus.Exceed_Temperature_limit);
                    if (i == 3)
                        errorList.Add(ErrorStatus.Invalid_packet);
                    if (i == 4)
                        errorList.Add(ErrorStatus.Overload_detected);
                    if (i == 5)
                        errorList.Add(ErrorStatus.Driver_fault_detected);
                    if (i == 6)
                        errorList.Add(ErrorStatus.EEP_REG_distorted);
                }
            }
            return errorList;
        }
    }

    #region EventArgs

    /// <summary>
    /// herkulex : packetDecoded args
    /// </summary>
    public class HklxPacketDecodedArgs : EventArgs
    {
        public byte PacketSize { get; set; }
        public byte PID { get; set; }
        public byte CMD { get; set; }
        public byte CheckSum1 { get; set; }
        public byte CheckSum2 { get; set; }
        public byte[] PacketData { get; set; }
    }

    /// <summary>
    /// Herkulex : status event received event args, the event can fire up only with an ACK packet, Check ACK policy
    /// </summary>
    public class HklxStatusErrorFromAckArgs : EventArgs
    {
        public List<ErrorStatus> StatusErrors { get; set; }
        public List<ErrorStatusDetail> StatusDetails { get; set; }
    }

    /// <summary>
    /// Herkulex : Checksum error occured at the reception level
    /// </summary>
    public class HklxCheckSumErrorOccured
    {
        public byte CheckSum1 { get; set; }
        public byte CheckSum2 { get; set; }
    }

    #endregion EventArgs
}