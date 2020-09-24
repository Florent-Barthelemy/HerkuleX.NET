using ExtendedSerialPort;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Channels;
using System.Threading;
using System.Timers;
//using System.Threading;
using System.Xml.Schema;
using Timer = System.Timers.Timer;



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
        

        //events, autoreset
        public event EventHandler<InfosUpdatedArgs> InfosUpdatedEvent;
        public event EventHandler<HerkulexErrorArgs> HerkulexErrorEvent;

        //internal events
        private ManualResetEvent PollingTimerThreadBlock = new ManualResetEvent(false);
        private AutoResetEvent MessageEnqueuedEvent = new AutoResetEvent(false);
        private AutoResetEvent RamReadAckReceivedEvent = new AutoResetEvent(false);
        private AutoResetEvent RamWriteAckReceivedEvent = new AutoResetEvent(false);
        private AutoResetEvent IjogAckReceivedEvent = new AutoResetEvent(false);
        private AutoResetEvent SjogAckReceivedEvent = new AutoResetEvent(false);
        private AutoResetEvent StatAckReceivedEvent = new AutoResetEvent(false);

        //Lock Objects
        private readonly Object DequeueLockObj = new Object();

        private ReliableSerialPort serialPort { get; set; }
        private HerkulexDecoder decoder;

        //dictionaries, queues
        private Dictionary<byte, Servo> Servos = new Dictionary<byte, Servo>();
        //private Dictionary<byte, Servo> SyncServoBuffer = new Dictionary<byte, Servo>();
        private Queue<byte[]> FrameQueue = new Queue<byte[]>();
               

        //timers, threads
        private System.Threading.Timer PollingTimer;
        private Thread DequeueThread;

        //default values
        public bool AutoRecoverMode = false;
        private byte SynchronousPlaytime = 50;
        private int PollingInterval = 100; //10 Hz
        private int AckTimeout = 50;

        //statistics
        private long NackCount = 0;

        public HerkulexController(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            serialPort = new ReliableSerialPort(portName, baudRate, parity, dataBits, stopBits);
            decoder = new HerkulexDecoder();

            DequeueThread = new Thread(new ThreadStart(DequeueFrames));

            DequeueThread.Start();

            serialPort.DataReceived += decoder.DecodePacket;
            decoder.OnRamReadAckEvent += Decoder_OnRamReadAckEvent;
            decoder.OnRamWriteAckEvent += Decoder_OnRamWriteAckEvent;
            decoder.OnIjogAckEvent += Decoder_OnIjogAckEvent;
            decoder.OnSjogAckEvent += Decoder_OnSjogAckEvent;
            decoder.OnStatAckEvent += Decoder_OnStatAckEvent;

            serialPort.Open();

            //starts 500ms after instanciation
            PollingTimer = new System.Threading.Timer((c) =>
            {
                PollingTimerThreadBlock.WaitOne();
                foreach (var key in Servos.Keys)
                    RAM_READ(key, HerkulexDescription.RAM_ADDR.Absolute_Position, 2);

            }, null, 500, PollingInterval); 
        }

        // STAT ack event
        private void Decoder_OnStatAckEvent(object sender, Hklx_STAT_Ack_Args e)
        {
            StatAckReceivedEvent.Set();
        }

        // ram write ack event
        private void Decoder_OnRamWriteAckEvent(object sender, Hklx_RAM_WRITE_Ack_Args e)
        {
            RamWriteAckReceivedEvent.Set();
        }

        //Sjog ack
        private void Decoder_OnSjogAckEvent(object sender, Hklx_S_JOG_Ack_Args e)
        {
            SjogAckReceivedEvent.Set();
        }

        //Ijog ack
        private void Decoder_OnIjogAckEvent(object sender, Hklx_I_JOG_Ack_Args e)
        {
            IjogAckReceivedEvent.Set();
        }

        //poll (ram read) ack
        private void Decoder_OnRamReadAckEvent(object sender, Hklx_RAM_READ_Ack_Args e)
        { 
            Servo pulledServo = null;
            UInt16 actualAbsolutePosition = 0;
            if (Servos.ContainsKey(e.PID))
            {
                RamReadAckReceivedEvent.Set();

                actualAbsolutePosition = (ushort)(e.ReceivedData[1] << 8);
                actualAbsolutePosition += (ushort)(e.ReceivedData[0] << 0);

                Servos.TryGetValue(e.PID, out pulledServo);
                //getting flags and error details
                pulledServo.IsMoving = (e.StatusDetails.Contains(HerkulexDescription.ErrorStatusDetail.Moving_flag) ? (true) : (false));
                pulledServo.IsInposition = (e.StatusDetails.Contains(HerkulexDescription.ErrorStatusDetail.Inposition_flag) ? (true) : (false));
                pulledServo.IsMotorOn = (e.StatusDetails.Contains(HerkulexDescription.ErrorStatusDetail.MOTOR_ON_flag) ? (true) : (false));
                pulledServo.CheckSumError = (e.StatusDetails.Contains(HerkulexDescription.ErrorStatusDetail.CheckSumError) ? (true) : (false));
                pulledServo.UnknownCommandError = (e.StatusDetails.Contains(HerkulexDescription.ErrorStatusDetail.Unknown_Command) ? (true) : (false));
                pulledServo.ExceedRegRangeError = (e.StatusDetails.Contains(HerkulexDescription.ErrorStatusDetail.Exceed_REG_RANGE) ? (true) : (false));
                pulledServo.GarbageDetectedError = (e.StatusDetails.Contains(HerkulexDescription.ErrorStatusDetail.Garbage_detected) ? (true) : (false));
                pulledServo.ActualAbsolutePosition = actualAbsolutePosition;

                //getting errors
                pulledServo.Exceed_input_voltage_limit = (e.StatusErrors.Contains(HerkulexDescription.ErrorStatus.Exceed_input_voltage_limit)) ? (true) : (false);
                pulledServo.Exceed_allowed_pot_limit = (e.StatusErrors.Contains(HerkulexDescription.ErrorStatus.Exceed_allowed_pot_limit)) ? (true) : (false);
                pulledServo.Exceed_Temperature_limit = (e.StatusErrors.Contains(HerkulexDescription.ErrorStatus.Exceed_Temperature_limit)) ? (true) : (false);
                pulledServo.Invalid_packet = (e.StatusErrors.Contains(HerkulexDescription.ErrorStatus.Invalid_packet)) ? (true) : (false);
                pulledServo.Overload_detected = (e.StatusErrors.Contains(HerkulexDescription.ErrorStatus.Overload_detected)) ? (true) : (false);
                pulledServo.Driver_fault_detected = (e.StatusErrors.Contains(HerkulexDescription.ErrorStatus.Driver_fault_detected)) ? (true) : (false);
                pulledServo.EEP_REG_distorted = (e.StatusErrors.Contains(HerkulexDescription.ErrorStatus.EEP_REG_distorted)) ? (true) : (false);

                //if any error flag is true, set HerkulexErrorEvent
                if (pulledServo.Exceed_input_voltage_limit == true ||
                    pulledServo.Exceed_allowed_pot_limit == true ||
                    pulledServo.Exceed_Temperature_limit == true ||
                    pulledServo.Invalid_packet == true ||
                    pulledServo.Overload_detected == true ||
                    pulledServo.Driver_fault_detected == true ||
                    pulledServo.EEP_REG_distorted == true)
                {
                    OnHerkulexError(pulledServo);
                    if (AutoRecoverMode == true)
                        RecoverErrors(pulledServo);
                }
                OnInfosUpdated(pulledServo);
               
            }
        }

        //dequeuing in a thread
        //ISSUE : FrameQueue is not thread safe, errors occurs
        private void DequeueFrames()
        {
            while(true)
            {
                //timeout to avoid blocking the thread if MessageEnqueuedEvent does not set [WaitOne() for optimization]
                MessageEnqueuedEvent.WaitOne(50);
                lock (FrameQueue)
                {
                    if (FrameQueue.Count > 0)
                        for (int i = 0; i < FrameQueue.Count; i++)
                        {
                            byte[] packet = FrameQueue.Dequeue();
                            bool AckReceived = false;
                            if (packet != null)
                            {
                                serialPort.Write(packet, 0, packet.Length);
                                switch (packet[4])
                                {
                                    case (byte)HerkulexDescription.CommandSet.RAM_READ:
                                        AckReceived = RamReadAckReceivedEvent.WaitOne(AckTimeout);
                                        if (!AckReceived)
                                            NackCount++;
                                        break;

                                    case (byte)HerkulexDescription.CommandSet.RAM_WRITE:
                                        AckReceived = RamWriteAckReceivedEvent.WaitOne(AckTimeout);
                                        if (!AckReceived)
                                            NackCount++;
                                        break;

                                    case (byte)HerkulexDescription.CommandSet.I_JOG:
                                        AckReceived = IjogAckReceivedEvent.WaitOne(AckTimeout);
                                        if (!AckReceived)
                                            NackCount++;
                                        break;

                                    case (byte)HerkulexDescription.CommandSet.S_JOG:
                                        AckReceived = SjogAckReceivedEvent.WaitOne(AckTimeout);
                                        if (!AckReceived)
                                            NackCount++;
                                        break;

                                    default:
                                        break;
                                }
                            }
                        }
                }

            }
        }

        #region UserMethods

        //-----TOOLS--------

        /// <summary>
        /// Changes the ID of a servo
        /// </summary>
        /// <param name="ID">Current ID</param>
        /// <param name="newID">New ID</param>
        public void SetID(byte ID, byte newID)
        {
            _SetID(ID, newID);
        }

        /// <summary>
        /// Scans for servos on the bus
        /// </summary>
        /// <param name="timeOut">Timeout</param>
        /// <param name="minID">Min ID</param>
        /// <param name="maxID">Max ID</param>
        /// <returns></returns>
        public byte[] ScanForServoIDs(int timeOut = 70, int minID = 1, int maxID = 10)
        {
            byte[] ID_Buffer = new byte[0xFD];
            
            int count = 0;
            bool AckReceived = false;

            for(int ID = minID; ID < maxID; ID++)
            {
                Console.Write("Scanning ID " + ID);
                STAT((byte)ID);
                AckReceived = StatAckReceivedEvent.WaitOne(timeOut);
                if (AckReceived)
                {
                    ID_Buffer[count] = (byte)ID;
                    count++;
                    Console.Write(" -> Exists");
                }
                Console.WriteLine();
                
            }

            byte[] ID_Return = new byte[count];
            for (int i = 0; i < ID_Return.Length; i++)
                ID_Return[i] = ID_Buffer[i];

            return ID_Return;
        }

        //-----------SETS---------------
        /// <summary>
        /// Sets the polling frequency
        /// </summary>
        /// <param name="freq">Frequency</param>
        public void SetPollingFreq(int freq)
        {
            if (freq > 10)
                throw new Exception("Polling frequency is too high");
            if (freq != 0)
                PollingTimer.Change(0, (int)((1.0 / freq) * 1000));
            if (freq == 0)
                PollingTimer.Change(0, 0);

        }

        /// <summary>
        /// Sets the servo acknowledge timeout
        /// </summary>
        /// <param name="TimeoutMs">Timeout in ms</param>
        public void SetAckTimeout(int TimeoutMs)
        {
            AckTimeout = TimeoutMs;
        }

        /// <summary>
        /// Sets the torque mode on the servo
        /// </summary>
        /// <param name="ID">Servo ID</param>
        /// <param name="mode">Torque mode</param>
        public void SetTorqueMode(byte ID, HerkulexDescription.TorqueControl mode)
        {
            if (Servos.ContainsKey(ID))
                _SetTorqueMode(ID, mode);
            else
                throw new Exception("The servo ID is not in the dictionary");
        }

        /// <summary>
        /// Changes the servo led color
        /// </summary>
        /// <param name="ID">Servo ID</param>
        /// <param name="color">Led color</param>
        public void SetLedColor(byte ID, HerkulexDescription.LedColor color)
        {
            if (Servos.ContainsKey(ID))
            {
                Servos[ID].SetLedColor(color);
                _SetLedColor(ID, color);
            }
            else
                throw new Exception("The servo ID is not in the dictionnary");
        }

        /// <summary>
        /// Sets the target absolute position of the servo
        /// </summary>
        /// <param name="ID">Servo ID</param>
        /// <param name="absolutePosition">Absolute position</param>
        /// <param name="playTime">Playtime</param>
        public void SetPosition(byte ID, ushort absolutePosition, byte playTime, bool IsSynchronous = false)
        {
            if (Servos.ContainsKey(ID))
            {
                Servos[ID].SetAbsolutePosition(absolutePosition);
                Servos[ID].SetPlayTime(playTime);

                if (IsSynchronous)
                {
                    Servos[ID].IsNextOrderSynchronous = true; //On clear le flag à l'envoi synchrone
                }
                else
                {
                    foreach (KeyValuePair<byte, Servo> IdServoPair in Servos)
                    {
                        I_JOG(IdServoPair.Value);
                        IdServoPair.Value.IsNextOrderSynchronous = false;
                    }
                }
            }
            else
                throw new Exception("The servo ID is not in the dictionnary");
        }

        /// <summary>
        /// Sets the maximum absolute position of the servo
        /// </summary>
        /// <param name="ID">Servo ID</param>
        /// <param name="position">Maximum absolute position</param>
        /// <param name="keepAfterReboot">weither to keep the change after a servo reboot</param>
        public void SetMaximumPosition(byte ID, UInt16 position, bool keepAfterReboot = true)
        {
            _SetMaxAbsolutePosition(ID, position, keepAfterReboot);
        }

        /// <summary>
        /// Sets the minimum absolute position of the servo
        /// </summary>
        /// <param name="ID">Servo ID</param>
        /// <param name="position">Minimum absolute position</param>
        /// <param name="keepAfterReboot">weither to keep the change after a servo reboot</param>
        public void SetMinimumPosition(byte ID, UInt16 position, bool keepAfterReboot = true)
        {
            _SetMinAbsolutePosition(ID, position, keepAfterReboot);
        }
        //------------------------------

        /// <summary>
        /// returns the total number of NACKs
        /// </summary>
        /// <returns></returns>
        public long GetNackCount()
        {
            return NackCount;
        }

        /// <summary>
        /// Recovers the servo from error state
        /// </summary>
        /// <param name="servo">Servo instance</param>
        public void RecoverErrors(Servo servo)
        {
            ClearAllErrors(servo.GetID());
            SetTorqueMode(servo.GetID(), HerkulexDescription.TorqueControl.TorqueOn);
        }

        /// <summary>
        /// Servo polled event
        /// </summary>
        /// <param name="servo"></param>
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

        /// <summary>
        /// Error occured event
        /// </summary>
        /// <param name="servo"></param>
        public virtual void OnHerkulexError(Servo servo)
        {
            //Ne doit être appelé que si il y a une erreur
            var handler = HerkulexErrorEvent;
            if (handler != null)
            {
                handler(this, new HerkulexErrorArgs
                {
                    Servo = servo
                });
            }
        }

        /// <summary>
        /// Starts polling
        /// </summary>
        public void StartPolling()
        {
            PollingTimerThreadBlock.Set();
        }

        /// <summary>
        /// Stops Polling
        /// </summary>
        public void StopPolling()
        {
            PollingTimerThreadBlock.Reset();
        }

        /// <summary>
        /// Adds a servo to the controller
        /// </summary>
        /// <param name="ID">Servo ID</param>
        /// <param name="mode">JOG mode</param>
        public void AddServo(byte ID, HerkulexDescription.JOG_MODE mode, UInt16 initialPosition)
        {
            Servo servo = new Servo(ID, mode);
            Servos.Add(ID, servo);
            Servos[ID].SetAbsolutePosition(initialPosition);
            //reply to all packets
            RAM_WRITE(ID, HerkulexDescription.RAM_ADDR.ACK_Policy, 1, 0x02); //reply to I_JOG / S_JOG

            RecoverErrors(servo);
        }

        /// <summary>
        /// asynchronously clears all flags and errors on the servo (no matter the polling state)
        /// </summary>
        /// <param name="ID">Servo ID</param>
        public void ClearErrors(byte ID)
        {
            if (Servos.ContainsKey(ID))
                ClearAllErrors(ID);
            else
                throw new Exception("The servo ID is not in the dictionary");
        }

        /// <summary>
        /// Sends the synchronous servo buffer
        /// </summary>
        public void SendSynchronous(byte playtime)
        {
            SynchronousPlaytime = playtime;
            S_JOG(Servos, SynchronousPlaytime);
        }

        #endregion UserMethods

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
        private void _SetID(byte pID, byte newPID)
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
        private void _SetMinAbsolutePosition(byte pID, ushort minPosition, bool keepAfterReboot)
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
        private void _SetMaxAbsolutePosition(byte pID, ushort maxPosition, bool keepAfterReboot)
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
        private void _REBOOT(byte pID)
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
        private void _ROLLBACK(byte pID, bool skipID = true, bool skipBaud = true)
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
        /// 
        /// </summary>
        /// <param name="servos"></param>
        /// <param name="playTime"></param>
        private void S_JOG(List<Servo> servos, byte playTime)
        {
            byte[] dataToSend = new byte[1 + 4 * servos.Count];
            dataToSend[0] = playTime;
            byte dataOffset = 1;

            foreach(Servo servo in servos)
            {
                dataToSend[dataOffset + 0] = (byte)(servo.GetTargetAbsolutePosition() >> 0);
                dataToSend[dataOffset + 1] = (byte)(servo.GetTargetAbsolutePosition() >> 8);
                dataToSend[dataOffset + 2] = servo.GetSETByte();
                dataToSend[dataOffset + 3] = servo.GetID();
                dataOffset += 4;
            }

            EncodeAndSendPacket(serialPort, 0xFE, (byte)HerkulexDescription.CommandSet.S_JOG, dataToSend);
        }

        private void S_JOG(Dictionary<byte, Servo> servos, byte playTime)
        {
            byte[] dataToSend = new byte[1 + 4 * servos.Count];
            dataToSend[0] = playTime;
            byte dataOffset = 1;

            foreach(KeyValuePair<byte, Servo> servoIdPair in servos)
            {
                if (servoIdPair.Value.IsNextOrderSynchronous)
                {
                    dataToSend[dataOffset + 0] = (byte)(servoIdPair.Value.GetTargetAbsolutePosition() >> 0);
                    dataToSend[dataOffset + 1] = (byte)(servoIdPair.Value.GetTargetAbsolutePosition() >> 8);
                    dataToSend[dataOffset + 2] = servoIdPair.Value.GetSETByte();
                    dataToSend[dataOffset + 3] = servoIdPair.Value.GetID();
                    dataOffset += 4;
                    servoIdPair.Value.IsNextOrderSynchronous = false;
                }
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

            FrameQueue.Enqueue(packet);
            //Console.WriteLine("InQueue " + FrameQueue.Count);
            MessageEnqueuedEvent.Set();
            //port.Write(packet, 0, packet.Length);
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

            FrameQueue.Enqueue(packet);
            MessageEnqueuedEvent.Set();
            //port.Write(packet, 0, packet.Length);
        }

        #endregion LowLevelMethods
    }
}