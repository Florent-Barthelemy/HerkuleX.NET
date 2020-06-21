# HerkuleX.NET
A C# written library to manage Herkulex DRS servos via a serial port

# Current features
Quick guides on added features

### I_JOG command and I_JOG tags
```csharp
I_JOG(SerialPort port, IJOG_TAG ServoTag)
```
The I_JOG tag structure allows the user to declare a servo's I_JOG configuration
```csharp
IJOG_TAG tag = new IJOG_TAG()
```
params
```csharp
tag.ID
tag.mode
tag.playTime 
tag.LED_GREEN
tag.LED_BLUE
tag.LED_RED 
tag.JOG
tag.SET  //only get accessor, set has no effect
```
   

