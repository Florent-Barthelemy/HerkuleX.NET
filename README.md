# HerkuleX.NET
A C# written library to manage Herkulex DRS servos via a serial port#

# Current features
Quick guides on added features

### I_JOG command and I_JOG tags
```csharp
I_JOG(SerialPort port, IJOG_TAG ServoTag)
```
The I_JOG tag allows the user to declare a servo configuration
```csharp
   TAG.ID
   TAG.mode
   TAG.playTime 
   TAG.LED_GREEN
   TAG.LED_BLUE
   TAG.LED_RED 
   TAG.JOG
   TAG.SET  //only get accessor, set has no effect
```
   

