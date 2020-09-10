using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using ExtendedSerialPort;
using System.Threading.Tasks;

namespace HerkulexControl
{
    public class Servo
    {
        private byte ID;
        private HerkulexDescription.JOG_MODE Mode;
        private HerkulexDescription.LedColor LEDState;
        private UInt16 TargetAbsolutePosition;

        public byte _playtime;

        private byte _SET;

        //values
        public UInt16 ActualAbsolutePosition;

        //flags
        public bool IsMoving;
        public bool IsInposition;
        public bool IsMotorOn;

        //errors / details
        public bool CheckSumError;
        public bool UnknownCommandError;
        public bool ExceedRegRangeError;
        public bool GarbageDetectedError;

        public Servo(byte pID, HerkulexDescription.JOG_MODE mode)
        {
            ID = pID;
            Mode = mode;
        }

        public byte GetSETByte()
        {
            _SET = 0;
            _SET |= (byte)((byte)Mode << 1);
            _SET |= (byte)((byte)LEDState << 2);
            return _SET;
        }

        public void SetAbsolutePosition(ushort absolutePosition)
        {
            TargetAbsolutePosition = absolutePosition;
        }

        public ushort GetTargetAbsolutePosition()
        {
            return TargetAbsolutePosition;
        }

        public void SetPlayTime(byte playTime)
        {
            _playtime = playTime;
        }

        public byte GetPlaytime()
        {
            return _playtime;
        }

        public void SetLedColor(HerkulexDescription.LedColor color)
        {
            LEDState = color;
        }

        public HerkulexDescription.LedColor GetTargetLedColor()
        {
            return LEDState;
        }

        public byte GetID()
        {
            return ID;
        }
    }
}
