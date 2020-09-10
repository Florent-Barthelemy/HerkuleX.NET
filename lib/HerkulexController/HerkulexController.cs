using ExtendedSerialPort;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Channels;
using System.Threading;
using System.Xml.Schema;



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


namespace HerkulexControl
{
    public class HerkulexController
    {
        public event EventHandler<InfosUpdatedArgs> InfosUpdatedEvent;

        private ReliableSerialPort serialPort { get; set; }
        private HerkulexDecoder decoder;

        private Dictionary<byte, Servo> Servos = new Dictionary<byte, Servo>();
        private Timer pollingTimer;

        private ManualResetEvent pause = new ManualResetEvent(false);

        public HerkulexController(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            serialPort = new ReliableSerialPort(portName, baudRate, parity, dataBits, stopBits);
            decoder = new HerkulexDecoder();
            serialPort.DataReceived += decoder.DecodePacket;
            decoder.OnDataDecodedEvent += decoder.ProcessPacket;

            decoder.OnRamReadAckEvent += Decoder_OnRamReadAckEvent;
            serialPort.Open();

            //10Hz by default
            pollingTimer = new Timer((t) =>
            {
                pause.WaitOne(); //blocks the thread if pause.set()
                foreach(KeyValuePair<byte, Servo> IdServoPair in Servos)
                {
                    I_JOG(IdServoPair.Value);

                    //asking for basic servo stats (flags and errors)
                    RAM_READ(IdServoPair.Key, HerkulexDescription.RAM_ADDR.Absolute_Position, 2);
                    STAT(IdServoPair.Key);
                }
            }, null, 0, 100);
           
        }

        private void Decoder_OnRamReadAckEvent(object sender, Hklx_RAM_READ_Ack_Args e)
        {
            Servo pulledServo = null;
            UInt16 actualAbsolutePosition = 0;
            if (Servos.ContainsKey(e.PID))
            {
                actualAbsolutePosition = (ushort)(e.ReceivedData[1] << 8);
                actualAbsolutePosition += (ushort)(e.ReceivedData[0] << 0);

                Servos.TryGetValue(e.PID, out pulledServo);
                pulledServo.IsMoving = (e.StatusDetails.Contains(HerkulexDescription.ErrorStatusDetail.Moving_flag) ? (true) : (false));
                pulledServo.IsInposition = (e.StatusDetails.Contains(HerkulexDescription.ErrorStatusDetail.Inposition_flag) ? (true) : (false));
                pulledServo.IsMotorOn = (e.StatusDetails.Contains(HerkulexDescription.ErrorStatusDetail.MOTOR_ON_flag) ? (true) : (false));
                pulledServo.CheckSumError = (e.StatusDetails.Contains(HerkulexDescription.ErrorStatusDetail.CheckSumError) ? (true) : (false));
                pulledServo.UnknownCommandError = (e.StatusDetails.Contains(HerkulexDescription.ErrorStatusDetail.Unknown_Command) ? (true) : (false));
                pulledServo.ExceedRegRangeError = (e.StatusDetails.Contains(HerkulexDescription.ErrorStatusDetail.Exceed_REG_RANGE) ? (true) : (false));
                pulledServo.GarbageDetectedError = (e.StatusDetails.Contains(HerkulexDescription.ErrorStatusDetail.Garbage_detected) ? (true) : (false));
                pulledServo.ActualAbsolutePosition = actualAbsolutePosition;
                OnInfosUpdated(pulledServo);
            }
        }

        public virtual void OnInfosUpdated(Servo servo)
        {
            var handler = InfosUpdatedEvent;
            if(handler != null)
            {
                handler(this, new InfosUpdatedArgs
                {
                    Servo = servo
                });
            }
        }

        public void SetPollingFreq(int freq)
        {
            pollingTimer.Change(0, (int)(1.0 / freq * 1000));
        }

        public void PausePolling()
        {
            pause.Reset();
        }

        public void ResumePolling()
        {
            pause.Set();
        }

        public void AddServo(byte ID, HerkulexDescription.JOG_MODE mode)
        {
            Servo servo = new Servo(ID, mode);
            Servos.Add(ID, servo);
        }

        //asynchronously clears errors (immediate  effect)
        public void ClearErrors(byte ID)
        {
            if (Servos.ContainsKey(ID))
                ClearAllErrors(ID);
            else
                throw new Exception("The servo ID is not in the dictionary");
        }
        
        //asynchronously sets the torque mode (immediate effect)
        public void SetTorqueMode(byte ID, HerkulexDescription.TorqueControl mode)
        {
            if(Servos.ContainsKey(ID))
                _SetTorqueMode(ID, mode);
            else
                throw new Exception("The servo ID is not in the dictionary");
        }

        public void SetLedColor(byte ID, HerkulexDescription.LedColor color)
        {
            Servo pulledServo = null;
            if (Servos.ContainsKey(ID))
            {
                Servos.TryGetValue(ID, out pulledServo);
                pulledServo.SetLedColor(color);
            }
            else
                throw new Exception("The servo ID is not in the dictionnary");
        }

        public void SetPosition(byte ID, ushort absolutePosition, byte playTime)
        {
            Servo pulledServo = null;
            if (Servos.ContainsKey(ID))
            {
                Servos.TryGetValue(ID, out pulledServo);
                pulledServo.SetAbsolutePosition(absolutePosition);
                pulledServo.SetPlayTime(playTime);
            }
            else
                throw new Exception("The servo ID is not in the dictionnary");
        }


        #region LowLevelMethods
        /// <summary>
        /// Sets the torque control mode of the specified servo I.e BreakOn / TorqueOn / TorqueFree
        /// </summary>
        /// <param name="pID">Servo ID</param>
        /// <param name="mode">torque mode (TorqueControl enum)</param>
        private void _SetTorqueMode(byte pID, HerkulexDescription.TorqueControl mode)
        {
            RAM_WRITE(pID, HerkulexDescription.RAM_ADDR.Torque_Control, 1, (ushort)mode);
        }

        /// <summary>
        /// Sets the specified servo led color
        /// </summary>
        /// <param name="pID">Servo ID</param>
        /// <param name="color">Led color (LedColor enum)</param>
        private void _SetLedColor(byte pID, HerkulexDescription.LedColor color)
        {
            RAM_WRITE(pID, HerkulexDescription.RAM_ADDR.LED_Control, 1, (ushort)color);
        }

        /// <summary>
        /// Changes the ID of the specified servo
        /// </summary>
        /// <param name="pID">Current servo ID</param>
        /// <param name="newPID">New servo ID</param>
        private void SetID(byte pID, byte newPID)
        {
            EEP_WRITE(pID, HerkulexDescription.EEP_ADDR.ID, 1, newPID);
            RAM_WRITE(pID, HerkulexDescription.RAM_ADDR.ID, 1, newPID);
        }

        /// <summary>
        /// Sets the minimum allowed absolute position (0 to 1023)
        /// </summary>
        /// <param name="pID">Servo ID</param>
        /// <param name="minPosition">Minimum position</param>
        /// <param name="keepAfterReboot">Weither to keep the change after a reboot</param>
        private void SetMinAbsolutePosition(byte pID, ushort minPosition, bool keepAfterReboot)
        {
            RAM_WRITE(pID, HerkulexDescription.RAM_ADDR.Min_Position, 2, minPosition);

            if (keepAfterReboot)
                EEP_WRITE(pID, HerkulexDescription.EEP_ADDR.Min_Position, 2, minPosition);
        }

        /// <summary>
        /// Sets the maximum allowed absolute position (0 to 1023)
        /// </summary>
        /// <param name="pID">Servo ID</param>
        /// <param name="maxPosition">Maximum position</param>
        /// <param name="keepAfterReboot">Weither to keep the changes after a reboot</param>
        private void SetMaxAbsolutePosition(byte pID, ushort maxPosition, bool keepAfterReboot)
        {
            RAM_WRITE(pID, HerkulexDescription.RAM_ADDR.Max_Position, 2, maxPosition);

            if (keepAfterReboot)
                EEP_WRITE(pID, HerkulexDescription.EEP_ADDR.Max_Position, 2, maxPosition);
        }

        /// <summary>
        /// Clears all of the servo error statuses
        /// </summary>
        /// <param name="pID">Servo ID</param>
        private void ClearAllErrors(byte pID)
        {
            RAM_WRITE(pID, HerkulexDescription.RAM_ADDR.Status_Error, 1, 0x00);
        }


        /// <summary>
        /// Reboots the specified servo
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="pID">Servo ID</param>
        private void REBOOT(byte pID)
        {
            EncodeAndSendPacket(serialPort, pID, (byte)HerkulexDescription.CommandSet.REBOOT);
        }

        /// <summary>
        /// Request Status error, Status detail
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="pID">Servo ID</param>
        private void STAT(byte pID)
        {
            EncodeAndSendPacket(serialPort, pID, (byte)HerkulexDescription.CommandSet.STAT);
        }

        /// <summary>
        /// Resets the specified servo to factory defaults
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="pID">Servo ID</param>
        /// <param name="skipID">whether to skip ID (true by default)</param>
        /// <param name="skipBaud">whether to skip baud rate setting (true by default)</param>
        private void ROLLBACK(byte pID, bool skipID = true, bool skipBaud = true)
        {
            byte[] data = { (skipID == true) ? ((byte)0x01) : ((byte)0x00), (skipBaud == true) ? ((byte)0x01) : ((byte)0x00) };
            EncodeAndSendPacket(serialPort, pID, (byte)HerkulexDescription.CommandSet.ROLLBACK, data);
        }

        /// <summary>
        /// Reads the specified number of bytes from EEP
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="pID">Servo ID</param>
        /// <param name="startAddr">Address to start from</param>
        /// <param name="length">Number of bytes to read</param>
        private void EEP_READ(byte pID, byte startAddr, byte length)
        {
            byte[] data = { (byte)startAddr, length };
            EncodeAndSendPacket(serialPort, pID, (byte)HerkulexDescription.CommandSet.EEP_READ, data);
        }

        private void EEP_READ(byte pID, HerkulexDescription.EEP_ADDR startAddr, byte length)
        {
            EEP_READ(pID, (byte)startAddr, length);
        }

        /// <summary>
        /// Writes a chunk of data from the specified address
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="pID">Servo ID</param>
        /// <param name="startAddr">Address to start from</param>
        /// <param name="data">Number of bytes to read</param>
        private void EEP_WRITE(byte pID, byte startAddr, byte[] data)
        {
            byte[] dataToSend = new byte[2 + data.Length];
            dataToSend[0] = startAddr;
            dataToSend[1] = (byte)data.Length;

            for (int i = 0; i < data.Length; i++)
                dataToSend[2 + i] = data[i];

            EncodeAndSendPacket(serialPort, pID, (byte)HerkulexDescription.CommandSet.EEP_WRITE);
        }

        private void EEP_WRITE(byte pID, HerkulexDescription.EEP_ADDR startAddr, byte[] data)
        {
            EEP_WRITE(pID, (byte)startAddr, data);
        }

        /// <summary>
        /// Writes to the specified EEP address, up to 2 bytes
        /// </summary>
        /// <param name="port"></param>
        /// <param name="pID"></param>
        /// <param name="startAddr"></param>
        /// <param name="length"></param>
        /// <param name="value"></param>
        private void EEP_WRITE(byte pID, byte startAddr, byte length, UInt16 value)
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

            EncodeAndSendPacket(serialPort, pID, (byte)HerkulexDescription.CommandSet.EEP_WRITE, data);
        }

        private void EEP_WRITE(byte pID, HerkulexDescription.EEP_ADDR startAddr, byte length, UInt16 value)
        {
            EEP_WRITE(pID, (byte)startAddr, length, value);
        }

        /// <summary>
        /// Reads the specified number of bytes from RAM
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="pID">Servo ID</param>
        /// <param name="startAddr">Address to start from</param>
        /// <param name="length">Number of bytes to read</param>
        private void RAM_READ(byte pID, byte startAddr, byte length)
        {
            byte[] data = { (byte)startAddr, length };
            EncodeAndSendPacket(serialPort, pID, (byte)HerkulexDescription.CommandSet.RAM_READ, data);
        }

        private void RAM_READ(byte pID, HerkulexDescription.RAM_ADDR startAddr, byte length)
        {
            RAM_READ(pID, (byte)startAddr, length);
        }

        /// <summary>
        /// Writes to the specified RAM address, up to 2 bytes
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="pID">Servo ID</param>
        /// <param name="addr">Start memory address</param>
        /// <param name="length">Length of the data to write</param>
        /// <param name="value">data</param>
        private void RAM_WRITE(byte pID, byte addr, byte length, UInt16 value)
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

            EncodeAndSendPacket(serialPort, pID, (byte)HerkulexDescription.CommandSet.RAM_WRITE, data);
        }

        private void RAM_WRITE(byte pID, HerkulexDescription.RAM_ADDR addr, byte length, UInt16 value)
        {
            RAM_WRITE(pID, (byte)addr, length, value);
        }

        /// <summary>
        /// Sends a I_JOG command with a single tag
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="TAGS">I_JOG tag config</param>
        /// <param name="broadcast">Whether to send the tag with the broadcast ID (false by default)</param>
        private void I_JOG(HerkulexDescription.JOG_TAG ServoTag, bool broadcast = false)
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

            EncodeAndSendPacket(serialPort, ServoTag.ID, (byte)HerkulexDescription.CommandSet.I_JOG, dataToSend);
        }

        private void I_JOG(Servo servo)
        {
            byte[] dataToSend = new byte[5];
            dataToSend[0] = (byte)(servo.GetTargetAbsolutePosition() >> 0);
            dataToSend[1] = (byte)(servo.GetTargetAbsolutePosition() >> 8);
            dataToSend[2] = servo.GetSETByte();
            dataToSend[3] = servo.GetID();

            dataToSend[4] = servo.GetPlaytime();

            EncodeAndSendPacket(serialPort, servo.GetID(), (byte)HerkulexDescription.CommandSet.I_JOG, dataToSend);
        }

        /// <summary>
        /// Sends a list of JOG tags with async playtime
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="TAGS">list of I_JOG tags configs</param>
        private void I_JOG(List<HerkulexDescription.JOG_TAG> TAGS)
        {
            byte[] dataToSend = new byte[5 * TAGS.Count];
            int dataOffset = 0;
            foreach(HerkulexDescription.JOG_TAG TAG in TAGS)
            {
                dataToSend[dataOffset + 0] = (byte)(TAG.JOG >> 0);
                dataToSend[dataOffset + 1] = (byte)(TAG.JOG >> 8);
                dataToSend[dataOffset + 2] = TAG.SET;
                dataToSend[dataOffset + 3] = TAG.ID;
                dataToSend[dataOffset + 4] = TAG.playTime;
                dataOffset += 5;
            }

            EncodeAndSendPacket(serialPort, 0xFE, (byte)HerkulexDescription.CommandSet.I_JOG, dataToSend);
        }

        /// <summary>
        /// Sends a list of JOG tags with sync playtime (TAG.playTime is ignored)
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="TAGS">List of JOG tags</param>
        private void S_JOG(List<HerkulexDescription.JOG_TAG> TAGS, byte playTime)
        {
            byte[] dataToSend = new byte[1 + 4 * TAGS.Count];
            dataToSend[0] = playTime;
            byte dataOffset = 1;

            foreach(HerkulexDescription.JOG_TAG TAG in TAGS)
            {
                dataToSend[dataOffset + 0] = (byte)(TAG.JOG >> 0);
                dataToSend[dataOffset + 1] = (byte)(TAG.JOG >> 8);
                dataToSend[dataOffset + 2] = TAG.SET;
                dataToSend[dataOffset + 3] = TAG.ID;
                dataOffset += 4;
            }

            EncodeAndSendPacket(serialPort, 0xFE, (byte)HerkulexDescription.CommandSet.S_JOG, dataToSend);
        }

        /// <summary>
        /// Sends a single JOG_TAG with S_JOG (TAG.playTime is ignored)
        /// </summary>
        /// <param name="port"></param>
        /// <param name="TAG"></param>
        /// <param name="playTime"></param>
        private void S_JOG(HerkulexDescription.JOG_TAG TAG, byte playTime)
        {
            byte[] dataToSend = new byte[5];
            dataToSend[0] = playTime;
            dataToSend[1] = (byte)(TAG.JOG >> 0);
            dataToSend[2] = (byte)(TAG.JOG >> 8);
            dataToSend[3] = TAG.SET;
            dataToSend[4] = TAG.ID;

            EncodeAndSendPacket(serialPort, TAG.ID, (byte)HerkulexDescription.CommandSet.S_JOG, dataToSend);
        }

        /// <summary>
        /// Encodes and sends a packet with the Herkulex protocol
        /// </summary>
        /// <param name="port">Serial port to use</param>
        /// <param name="pID">Servo ID</param>
        /// <param name="CMD">Command ID</param>
        /// <param name="dataToSend">Data</param>
        private void EncodeAndSendPacket(SerialPort port, byte pID, byte CMD, byte[] dataToSend)
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
        private void EncodeAndSendPacket(SerialPort port, byte pID, byte CMD)
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

        #endregion LowLevelMethods
    }

}