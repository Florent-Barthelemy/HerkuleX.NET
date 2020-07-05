# HerkuleX.NET, a C# written library to manage Herkulex DRS servos via a serial port
---
This library allows control over Herkulex DRS Servos via a serial port in C# based applications.
The decoder makes use of the [ReliableSerialPort](https://www.vgies.com/a-reliable-serial-port-in-c/ "ReliableSerialPort page") class.
## Implemented
---
#### JOG tags and moving servos
The JOG tag structure allows the user to declare a servo's configuration.
In the ```JOG_MODE.infiniteTurn mode```, ```tag.JOG``` corresponds to the PWM.
```csharp
IJOG_TAG tag = new IJOG_TAG()
{
   tag.ID = [Servo ID],
   tag.mode = [mode refer to JOG_MODE],
   tag.playTime = [playtime],
   tag.LED_GREEN = [0 / 1]
   tag.LED_BLUE [0 / 1]
   tag.LED_RED = [0 / 1]
   tag.JOG = [Position (UInt16)]
   tag.SET  //only get accessor, set has no effect
 }
 ```
#### Sending tags
Herkulex servos has two main ways of sending tags, an async method and a synchronous one.
##### Asynchronous method
Tags can be sent one by one using the I_JOG command or simultaneously as a list of JOG tags, in this case, playtime of each JOG tag is independant.
```csharp
I_JOG(SerialPort port, JOG_TAG TAG)
I_JOG(SerialPort port, LIST<JOG_TAG> TAGS)
```
##### Synchronous method
The other way of sending tags is by using the S_JOG command, this way, playtime is ignored and all servos reaches their goal at the same time. Two methods are proposed, single tag or list.
```csharp
S_JOG(SerialPort port, JOG_TAG, TAGS, byte playTime)
S_JOG(SerialPort port, List<JOG_TAG> TAGS, byte playTime)
```
### EEP / RAM Register operations
---
EEP and RAM registers addresses are in ```RAM_ADDR``` and ```EEP_ADDR``` enums.
All values are stored with the little-endian technique. When using ```RAM_WRITE``` and ```EEP_WRITE```, the input value storage method differs:
#### WRITE operation on 1 to 2 bytes
When writing 1 to 2 bytes of data to a register, the following methods are using big-endian as an input value so they can be written naturally:
```csharp
EEP_WRITE(SerialPort port, byte pID, byte startAddr, byte length, UInt16 value)
RAM_WRITE(SerialPort port, byte pID, byte startAddr, byte length, UInt16 value)
```
#### WRITE operation with a chunk of data
The protocol allows the controller to write a chunk of data from a start address, the methods ```RAM_WRITE``` and ```EEP_WRITE``` can take a byte array as the input. When doing so, the bytes to write have to be in the little_endian order:
```csharp
EEP_WRITE(SerialPort port, byte pID, byte startAddr, byte[] data)
RAM_WRITE(SerialPort port, byte pID, byte startAddr, byte[] data) //in the next update
```
#### READ operation methods
Theese methods requests a read of either the RAM or EEP memory.
```csharp
RAM_READ(SerialPort port, byte pID, byte startAddr, byte length)
EEP_READ(SerialPort port, byte pID, byte startAddr, byte length)
```
#### Other basic commands
Reboots the desired servo:
```csharp
REBOOT(SerialPort port, byte pID)
```
Get StatusError and StatusDetail from the desired servo:
```csharp
STAT(SerialPort port, byte pID)
```
Reset back to factory defaults, skipID and skipBaud allows to reset ID and Baud rate, they are set to true by default to avoid communication errors:
```csharp
ROLLBACK(SerialPort port, byte pID, bool skipID = true, bool skipBaud = true)
```
#### ![abc](https://img.icons8.com/officexs/2x/lightning-bolt.png "oue")ACK events 
   

