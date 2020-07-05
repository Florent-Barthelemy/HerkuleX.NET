using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Channels;
using System.Xml.Schema;
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
    public struct JOG_TAG
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
            set { _JOG = (ushort)(value & 0x7FFF); } //set bit 15 to 0
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
        ID = 0,                                 // length: 1
        ACK_Policy = 1,                         // length: 1
        Alarm_LED_Policy = 2,                   // length: 1
        Torque_policy = 3,                      // length: 1
        Max_Temperature = 5,                    // length: 1
        Min_Voltage = 6,                        // length: 1
        Max_Voltage = 7,                        // length: 1
        Acceleration_Ratio = 8,                 // length: 1
        Max_Acceleration = 9,                   // length: 1
        Dead_Zone = 10,                         // length: 1
        Saturator_Offset = 11,                  // length: 1
        Saturator_Slope = 12,                   // length: 2
        PWM_Offset = 14,                        // length: 1
        Min_PWM = 15,                           // length: 1
        Max_PWM = 16,                           // length: 2
        Overload_PWM_Threshold = 18,            // length: 2
        Min_Position = 20,                      // length: 2
        Max_Position = 22,                      // length: 2
        Position_Kp = 24,                       // length: 2
        Position_Kd = 26,                       // length: 2
        Position_Ki = 28,                       // length: 2
        Pos_FreeFoward_1st_Gain = 30,           // length: 2
        Pos_FreeFoward_2nd_Gain = 32,           // length: 2
        LED_Blink_Period = 38,                  // length: 1
        ADC_Fault_Detect_Period = 39,           // length: 1
        Packet_Garbage_Detection_Period = 40,   // length: 1
        Stop_Detection_Period = 41,             // length: 1
        Overload_Detection_Period = 42,         // length: 1
        Stop_Threshold = 41,                    // length: 1
        Inposition_Margin = 44,                 // length: 1
        Calibration_Difference = 47,            // length: 1
        Status_Error = 48,                      // length: 1
        Status_Detail = 49,                     // length: 1
        Torque_Control = 52,                    // length: 1
        LED_Control = 53,                       // length: 1
        Voltage = 54,                           // length: 2
        Temperature = 55,                       // length: 2
        Current_Control_Mode = 56,              // length: 2
        Tick = 57,                              // length: 2
        Calibrated_Position = 58,               // length: 2
        Absolute_Position = 60,                 // length: 2
        Differential_Position = 62,             // length: 2
        PWM = 64,                               // length: 2
        Absolute_Goal_Position = 68,            // length: 2
        Absolute_Desired_Traject_Pos = 70,      // length: 2
        Desired_Velocity = 72                   // length: 1
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
        /// Reboots the specified servo
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="pID">Servo ID</param>
        public void REBOOT(SerialPort port, byte pID)
        {
            EncodeAndSendPacket(port, pID, (byte)CommandSet.REBOOT);
        }

        /// <summary>
        /// Request Status error, Status detail
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="pID">Servo ID</param>
        public void STAT(SerialPort port, byte pID)
        {
            EncodeAndSendPacket(port, pID, (byte)CommandSet.STAT);
        }

        /// <summary>
        /// Resets the specified servo to factory defaults
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="pID">Servo ID</param>
        /// <param name="skipID">whether to skip ID (true by default)</param>
        /// <param name="skipBaud">whether to skip baud rate setting (true by default)</param>
        public void ROLLBACK(SerialPort port, byte pID, bool skipID = true, bool skipBaud = true)
        {
            byte[] data = { (skipID == true) ? ((byte)0x01) : ((byte)0x00), (skipBaud == true) ? ((byte)0x01) : ((byte)0x00) };
            EncodeAndSendPacket(port, pID, (byte)CommandSet.ROLLBACK, data);
        }

        /// <summary>
        /// Reads the specified number of bytes from EEP
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="pID">Servo ID</param>
        /// <param name="startAddr">Address to start from</param>
        /// <param name="length">Number of bytes to read</param>
        public void EEP_READ(SerialPort port, byte pID, byte startAddr, byte length)
        {
            byte[] data = { (byte)startAddr, length };
            EncodeAndSendPacket(port, pID, (byte)CommandSet.EEP_READ, data);
        }

        /// <summary>
        /// Writes a chunk of data from the specified address
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="pID">Servo ID</param>
        /// <param name="startAddr">Address to start from</param>
        /// <param name="data">Number of bytes to read</param>
        public void EEP_WRITE(SerialPort port, byte pID, byte startAddr, byte[] data)
        {
            byte[] dataToSend = new byte[2 + data.Length];
            dataToSend[0] = startAddr;
            dataToSend[1] = (byte)data.Length;

            for (int i = 0; i < data.Length; i++)
                dataToSend[2 + i] = data[i];

            EncodeAndSendPacket(port, pID, (byte)CommandSet.EEP_WRITE);
        }

        /// <summary>
        /// Writes to the specified EEP address, up to 2 bytes
        /// </summary>
        /// <param name="port"></param>
        /// <param name="pID"></param>
        /// <param name="startAddr"></param>
        /// <param name="length"></param>
        /// <param name="value"></param>
        public void EEP_WRITE(SerialPort port, byte pID, byte startAddr, byte length, UInt16 value)
        {
            if (length > 2)
                return;

            byte[] data = new byte[2 + length];
            data[0] = (byte)startAddr;
            data[1] = length;

            if (length >= 2)
            {
                data[2] = (byte)(value >> 0);
                data[3] = (byte)(value >> 8);
            }
            else
                data[2] = (byte)(value);

            EncodeAndSendPacket(port, pID, (byte)CommandSet.EEP_WRITE, data);
        }

        /// <summary>
        /// Reads the specified number of bytes from RAM
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="pID">Servo ID</param>
        /// <param name="startAddr">Address to start from</param>
        /// <param name="length">Number of bytes to read</param>
        public void RAM_READ(SerialPort port, byte pID, byte startAddr, byte length)
        {
            byte[] data = { (byte)startAddr, length };
            EncodeAndSendPacket(port, pID, (byte)CommandSet.RAM_READ, data);
        }

        /// <summary>
        /// Writes to the specified RAM address, up to 2 bytes
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="pID">Servo ID</param>
        /// <param name="addr">Start memory address</param>
        /// <param name="length">Length of the data to write</param>
        /// <param name="value">data</param>
        public void RAM_WRITE(SerialPort port, byte pID, byte addr, byte length, UInt16 value)
        {
            if (length > 2)
                return;

            byte[] data = new byte[2 + length];
            data[0] = (byte)addr;
            data[1] = length;

            if(length >= 2)
            {
                data[2] = (byte)(value >> 0); //little endian, LSB first
                data[3] = (byte)(value >> 8);
            }
            else
                data[2] = (byte)(value);

            EncodeAndSendPacket(port, pID, (byte)CommandSet.RAM_WRITE, data);
        }

        /// <summary>
        /// Sends a I_JOG command with a single tag
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="TAGS">I_JOG tag config</param>
        /// <param name="broadcast">Whether to send the tag with the broadcast ID (false by default)</param>
        public void I_JOG(SerialPort port, JOG_TAG ServoTag, bool broadcast = false)
        {
            byte[] dataToSend = new byte[5];
            dataToSend[0] = (byte)(ServoTag.JOG >> 0);
            dataToSend[1] = (byte)(ServoTag.JOG >> 8);
            dataToSend[2] = ServoTag.SET;

            if (broadcast)
                dataToSend[3] = 0xFE;
            else
                dataToSend[3] = ServoTag.ID;

            dataToSend[4] = ServoTag.playTime;

            EncodeAndSendPacket(port, ServoTag.ID, (byte)CommandSet.I_JOG, dataToSend);
        } 
        
        /// <summary>
        /// Sends a list of JOG tags with async playtime
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="TAGS">list of I_JOG tags configs</param>
        public void I_JOG(SerialPort port, List<JOG_TAG> TAGS)
        {
            byte[] dataToSend = new byte[5 * TAGS.Count];
            int dataOffset = 0;
            foreach(JOG_TAG TAG in TAGS)
            {
                dataToSend[dataOffset + 0] = (byte)(TAG.JOG >> 0);
                dataToSend[dataOffset + 1] = (byte)(TAG.JOG >> 8);
                dataToSend[dataOffset + 2] = TAG.SET;
                dataToSend[dataOffset + 3] = TAG.ID;
                dataToSend[dataOffset + 4] = TAG.playTime;
                dataOffset += 5;
            }

            EncodeAndSendPacket(port, 0xFE, (byte)CommandSet.I_JOG, dataToSend);
        }

        /// <summary>
        /// Sends a list of JOG tags with sync playtime (TAG.playTime is ignored)
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="TAGS">List of JOG tags</param>
        public void S_JOG(SerialPort port, List<JOG_TAG> TAGS, byte playTime)
        {
            byte[] dataToSend = new byte[1 + 4 * TAGS.Count];
            dataToSend[0] = playTime;
            byte dataOffset = 1;

            foreach(JOG_TAG TAG in TAGS)
            {
                dataToSend[dataOffset + 0] = (byte)(TAG.JOG >> 0);
                dataToSend[dataOffset + 1] = (byte)(TAG.JOG >> 8);
                dataToSend[dataOffset + 2] = TAG.SET;
                dataToSend[dataOffset + 3] = TAG.ID;
                dataOffset += 4;
            }

            EncodeAndSendPacket(port, 0xFE, (byte)CommandSet.S_JOG, dataToSend);
        }

        /// <summary>
        /// Sends a single JOG_TAG with S_JOG (TAG.playTime is ignored)
        /// </summary>
        /// <param name="port"></param>
        /// <param name="TAG"></param>
        /// <param name="playTime"></param>
        public void S_JOG(SerialPort port, JOG_TAG TAG, byte playTime)
        {
            byte[] dataToSend = new byte[5];
            dataToSend[0] = playTime;
            dataToSend[1] = (byte)(TAG.JOG >> 0);
            dataToSend[2] = (byte)(TAG.JOG >> 8);
            dataToSend[3] = TAG.SET;
            dataToSend[4] = TAG.ID;

            EncodeAndSendPacket(port, TAG.ID, (byte)CommandSet.S_JOG, dataToSend);
        }

        /// <summary>
        /// Encodes and sends a packet with the Herkulex protocol
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="pID">Servo ID</param>
        /// <param name="CMD">Command ID</param>
        /// <param name="dataToSend">Data</param>
        public void EncodeAndSendPacket(SerialPort port, byte pID, byte CMD, byte[] dataToSend)
        {
            byte packetSize = (byte)(7 + dataToSend.Length);
            byte[] packet = new byte[packetSize];

            packet[0] = 0xFF;
            packet[1] = 0xFF;
            packet[2] = packetSize;
            packet[3] = pID;
            packet[4] = CMD;
            packet[5] = CommonMethods.CheckSum1(packet[2], packet[3], packet[4], dataToSend);
            packet[6] = CommonMethods.CheckSum2(packet[5]);

            for (int i = 0; i < dataToSend.Length; i++)
                packet[7 + i] = dataToSend[i];

            port.Write(packet, 0, packet.Length);
        }

        /// <summary>
        /// Encodes and sends a packet with the Herkulex protocol
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="pID">Servo ID</param>
        /// <param name="CMD">Command ID</param>
        public void EncodeAndSendPacket(SerialPort port, byte pID, byte CMD)
        {
            byte[] packet = new byte[7];

            packet[0] = 0xFF;
            packet[1] = 0xFF;
            packet[2] = 7;
            packet[3] = pID;
            packet[4] = CMD;
            packet[5] = CommonMethods.CheckSum1(packet[2], packet[3], packet[4]);
            packet[6] = CommonMethods.CheckSum2(packet[5]);

            port.Write(packet, 0, packet.Length);
        }
    }

    public class HklxDecoder
    {
        private byte packetSize = 0;
        private byte pID = 0;
        private byte cmd = 0;
        private byte checkSum1 = 0;
        private byte checkSum2 = 0;
        private byte statusError = 0;
        private byte statusDetail = 0;
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
            data,
        }

        ReceptionStates rcvState = ReceptionStates.Waiting;

        public void DecodePacket(object sender, DataReceivedArgs e)
        {
            foreach (byte b in e.Data)
            {
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
                        packetData = new byte[packetSize - 7]; //init to the data size only -(status error, detail)
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
                        if (packetDataByteIndex < packetData.Length )
                        {
                            packetData[packetDataByteIndex] = b;
                            packetDataByteIndex++;
                        }

                        if (packetDataByteIndex == packetData.Length)
                        {
                            packetDataByteIndex = 0;

                            byte clcChksum1 = CommonMethods.CheckSum1(packetSize, pID, cmd, packetData);
                            byte clcChksum2 = CommonMethods.CheckSum2(clcChksum1);

                            if (checkSum1 == clcChksum1 && checkSum2 == clcChksum2)
                                OnDataDecoded(packetSize, pID, cmd, checkSum1, checkSum2, packetData, statusError, statusDetail);
                            else
                                OnCheckSumErrorOccured(checkSum1, checkSum2);

                            rcvState = ReceptionStates.Waiting;
                        }
                        break;
                }
            }
        } 

        public void ProcessPacket(object sender, HklxPacketDecodedArgs e)
        {
            byte[] _GetOnlyDataFromReadOperations(byte[] data)
            {
                if (data.Length <= 2)
                    return data;

                byte[] _data = new byte[data.Length - 4];
                for (int i = 0; i < _data.Length; i++)
                    _data[i] = data[i + 2];
                return _data;
            }

            int dataLen = e.PacketData.Length;
            byte statusError = e.PacketData[dataLen - 2];
            byte statusDetail = e.PacketData[dataLen - 1];
            byte[] readOperationData;

            switch(e.CMD)
            {
                case (byte)CommandAckSet.ack_EEP_READ:
                    readOperationData = _GetOnlyDataFromReadOperations(e.PacketData);
                    OnEepReadAck(statusError, statusDetail, e.PacketData[0], e.PacketData[1], readOperationData);
                    break;

                case (byte)CommandAckSet.ack_EEP_WRITE:
                    OnEepWriteAck(statusError, statusDetail);
                    break;

                case (byte)CommandAckSet.ack_RAM_READ:
                    readOperationData = _GetOnlyDataFromReadOperations(e.PacketData);
                    OnRamReadAck(statusError, statusDetail, e.PacketData[0], e.PacketData[1], readOperationData);
                    break;

                case (byte)CommandAckSet.ack_RAM_WRITE:
                    OnRamWriteAck(statusError, statusDetail);
                    break;

                case (byte)CommandAckSet.ack_I_JOG:
                    OnIjogAck(statusError, statusDetail);
                    break;

                case (byte)CommandAckSet.ack_S_JOG:
                    OnSjogAck(statusError, statusDetail);
                    break;

                case (byte)CommandAckSet.ack_STAT:
                    OnStatAck(statusError, statusDetail);
                    break;

                case (byte)CommandAckSet.ack_ROLLBACK:
                    OnRollbackAck(statusError, statusDetail);
                    break;
            }

        }
       

        #region eventGeneration

        public event EventHandler<HklxPacketDecodedArgs> OnDataDecodedEvent;
        public event EventHandler<HklxCheckSumErrorOccured> OnCheckSumErrorOccuredEvent;
        public event EventHandler<Hklx_EEP_READ_Ack_Args> OnEepReadAckEvent;
        public event EventHandler<Hklx_EEP_WRITE_Ack_Args> OnEepWriteAckEvent;
        public event EventHandler<Hklx_RAM_READ_Ack_Args> OnRamReadAckEvent;
        public event EventHandler<Hklx_RAM_WRITE_Ack_Args> OnRamWriteAckEvent;
        public event EventHandler<Hklx_I_JOG_Ack_Args> OnIjogAckEvent;
        public event EventHandler<Hklx_S_JOG_Ack_Args> OnSjogAckEvent;
        public event EventHandler<Hklx_STAT_Ack_Args> OnStatAckEvent;
        public event EventHandler<Hklx_ROLLBACK_Ack_Args> OnRollbackAckEvent;
        public event EventHandler<Hklx_REBOOT_Ack_Args> OnRebootAckEvent;

        public virtual void OnEepReadAck(byte statusError, byte statusDetail, byte address, byte length, byte[] data)
        {
            var handler = OnEepReadAckEvent;
            if (handler != null)
            {
                handler(this, new Hklx_EEP_READ_Ack_Args
                {
                    StatusErrors = CommonMethods.GetErrorStatusFromByte(statusError),
                    StatusDetails = CommonMethods.GetErrorStatusDetailFromByte(statusDetail),
                    Address = address,
                    Length = length,
                    ReceivedData = data
                });
            }
        }

        public virtual void OnEepWriteAck(byte statusError, byte statusDetail)
        {
            var handler = OnEepWriteAckEvent;
            if (handler != null)
            {
                handler(this, new Hklx_EEP_WRITE_Ack_Args
                {
                    StatusErrors = CommonMethods.GetErrorStatusFromByte(statusError),
                    StatusDetails = CommonMethods.GetErrorStatusDetailFromByte(statusDetail)
                });
            }
        }

        public virtual void OnRamReadAck(byte statusError, byte statusDetail, byte address, byte length, byte[] data)
        {
            var handler = OnRamReadAckEvent;
            if (handler != null)
            {
                handler(this, new Hklx_RAM_READ_Ack_Args
                {
                    StatusErrors = CommonMethods.GetErrorStatusFromByte(statusError),
                    StatusDetails = CommonMethods.GetErrorStatusDetailFromByte(statusDetail),
                    Address = address,
                    Length = length,
                    ReceivedData = data
                });
            }
        }

        public virtual void OnRamWriteAck(byte statusError, byte statusDetail)
        {
            var handler = OnRamWriteAckEvent;
            if (handler != null)
            {
                handler(this, new Hklx_RAM_WRITE_Ack_Args
                {
                    StatusErrors = CommonMethods.GetErrorStatusFromByte(statusError),
                    StatusDetails = CommonMethods.GetErrorStatusDetailFromByte(statusDetail)
                });
            }
        }

        public virtual void OnIjogAck(byte statusError, byte statusDetail)
        {
            var handler = OnIjogAckEvent;
            if (handler != null)
            {
                handler(this, new Hklx_I_JOG_Ack_Args
                {
                    StatusErrors = CommonMethods.GetErrorStatusFromByte(statusError),
                    StatusDetails = CommonMethods.GetErrorStatusDetailFromByte(statusDetail)
                });
            }
        }

        public virtual void OnSjogAck(byte statusError, byte statusDetail)
        {
            var handler = OnSjogAckEvent;
            if (handler != null)
            {
                handler(this, new Hklx_S_JOG_Ack_Args
                {
                    StatusErrors = CommonMethods.GetErrorStatusFromByte(statusError),
                    StatusDetails = CommonMethods.GetErrorStatusDetailFromByte(statusDetail)
                });
            }
        }

        public virtual void OnStatAck(byte statusError, byte statusDetail)
        {
            var handler = OnStatAckEvent;
            if (handler != null)
            {
                handler(this, new Hklx_STAT_Ack_Args
                {
                    StatusErrors = CommonMethods.GetErrorStatusFromByte(statusError),
                    StatusDetails = CommonMethods.GetErrorStatusDetailFromByte(statusDetail)
                });
            }
        }

        public virtual void OnRollbackAck(byte statusError, byte statusDetail)
        {
            var handler = OnRollbackAckEvent;
            if (handler != null)
            {
                handler(this, new Hklx_ROLLBACK_Ack_Args
                {
                    StatusErrors = CommonMethods.GetErrorStatusFromByte(statusError),
                    StatusDetails = CommonMethods.GetErrorStatusDetailFromByte(statusDetail)
                });
            }
        }

        public virtual void OnRebootAck(byte statusError, byte statusDetail)
        {
            var handler = OnRebootAckEvent;
            if (handler != null)
            {
                handler(this, new Hklx_REBOOT_Ack_Args
                {
                    StatusErrors = CommonMethods.GetErrorStatusFromByte(statusError),
                    StatusDetails = CommonMethods.GetErrorStatusDetailFromByte(statusDetail)
                });
            }
        }

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

        public virtual void OnDataDecoded(byte packetSize, byte pID, byte cmd, byte checkSum1, byte checkSum2, byte[] packetData, byte statusError, byte statusDetail)
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
                    PacketData = packetData,
                    StatusError = statusError,
                    StatusDetail = statusDetail
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

        public static byte CheckSum1(byte pSIZE, byte pID, byte CMD)
        {
            byte checksum = (byte)(pSIZE ^ pID ^ CMD);
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

        public static List<ErrorStatusDetail> GetErrorStatusDetailFromByte(byte b)
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

        public static float NumericToDegree(UInt16 value)
        {
            const double m = 0.3125;
            return (float)(value * m);
        }

        public static UInt16 DegreeToNumeric(float positionDeg)
        {
            const double m = 0.3125;
            return LimitToInterval(0, 1023, (UInt16)(positionDeg / m));
        }

        public static float NumericToRad(UInt16 value)
        {
            const double m = 3.14159265359 / 180;
            return (float)(NumericToDegree(value) * m);
        }

        public static UInt16 RadToNumeric(float positionRad)
        {
            const double m = 180 / 3.14159265359;
            return LimitToInterval(0, 1023, (UInt16)(positionRad * m));
        }

        private static UInt16 LimitToInterval(ushort min, ushort max, ushort value)
        {
            if (value > max)
                return max;

            else if (value < min)
                return min;

            return value;
        }

    }

    #region EventArgs

    /// <summary>
    /// Herkulex : packetDecoded args
    /// </summary>
    public class HklxPacketDecodedArgs : EventArgs
    {
        public byte PacketSize { get; set; }
        public byte PID { get; set; }
        public byte CMD { get; set; }
        public byte CheckSum1 { get; set; }
        public byte CheckSum2 { get; set; }
        public byte[] PacketData { get; set; }
        public byte StatusError;
        public byte StatusDetail;
    }

    /// <summary>
    /// Herkulex : Checksum error occured at reception
    /// </summary>
    public class HklxCheckSumErrorOccured : EventArgs
    {
        public byte CheckSum1 { get; set; }
        public byte CheckSum2 { get; set; }
    }

    /// <summary>
    /// Herkulex : EEPWRITE ack
    /// </summary>
    public class Hklx_EEP_WRITE_Ack_Args : EventArgs
    {
        public List<ErrorStatus> StatusErrors;
        public List<ErrorStatusDetail> StatusDetails;
    }    
    
    /// <summary>
    /// Herkulex : EEPREAD ack
    /// </summary>
    public class Hklx_EEP_READ_Ack_Args : EventArgs
    {
        public byte[] ReceivedData;
        public byte Address;
        public byte Length;
        public List<ErrorStatus> StatusErrors;
        public List<ErrorStatusDetail> StatusDetails;
    }

    /// <summary>
    /// Heckulex : RAMWRITE ack
    /// </summary>
    public class Hklx_RAM_WRITE_Ack_Args : EventArgs
    {
        public List<ErrorStatus> StatusErrors;
        public List<ErrorStatusDetail> StatusDetails;
    }

    /// <summary>
    /// Herkulex : RAMREAD ack
    /// </summary>
    public class Hklx_RAM_READ_Ack_Args : EventArgs
    {
        public byte[] ReceivedData;
        public byte Address;
        public byte Length;
        public List<ErrorStatus> StatusErrors;
        public List<ErrorStatusDetail> StatusDetails;
    }

    /// <summary>
    /// Herkulex : I_JOG ack
    /// </summary>
    public class Hklx_I_JOG_Ack_Args : EventArgs
    {
        public List<ErrorStatus> StatusErrors;
        public List<ErrorStatusDetail> StatusDetails;
    }

    /// <summary>
    /// Herkulex : S_JOG ack
    /// </summary>
    public class Hklx_S_JOG_Ack_Args : EventArgs
    {
        public List<ErrorStatus> StatusErrors;
        public List<ErrorStatusDetail> StatusDetails;
    }

    /// <summary>
    /// Herkulex : STAT ack
    /// </summary>
    public class Hklx_STAT_Ack_Args : EventArgs
    {
        public List<ErrorStatus> StatusErrors;
        public List<ErrorStatusDetail> StatusDetails;
    }

    /// <summary>
    /// Herkulex : ROLLBACK ack
    /// </summary>
    public class Hklx_ROLLBACK_Ack_Args : EventArgs
    {
        public List<ErrorStatus> StatusErrors;
        public List<ErrorStatusDetail> StatusDetails;
    }

    /// <summary>
    /// Hekulex : REBOOT ack
    /// </summary>
    public class Hklx_REBOOT_Ack_Args : EventArgs
    {
        public List<ErrorStatus> StatusErrors;
        public List<ErrorStatusDetail> StatusDetails;
    }


    #endregion EventArgs
}