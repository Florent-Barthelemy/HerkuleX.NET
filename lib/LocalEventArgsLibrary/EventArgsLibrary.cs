using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalEventArgsLibrary
{
    /// <summary>
    /// ReliableSerialPort: data received args
    /// </summary>
    public class DataReceivedArgs : EventArgs
    {
        public byte[] Data { get; set; }
    }

    /// <summary>
    /// HerkulexReceptManager: reception packet decode args
    /// </summary>
    public class HerkulexIncommingPacketDecodedArgs : EventArgs
    {
        public byte PacketSize { get; set; }
        public byte PID { get; set; }
        public byte CMD { get; set; }
        public byte CheckSum1 { get; set; }
        public byte CheckSum2 { get; set; }
        public byte[] PacketData { get; set; }
    }
}
