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


}
