# HerkuleX.NET
A C# written library to manage Herkulex DRS servos via a serial port

# Console commands
Features
### Commands
moveto (I_JOG)
```
moveto <ID> <JOG>
```
torqueControl
```
torqueControl <ID> <TRK_ON, BRK_ON, TRK_FREE>
```
RAM_READ
```
RAM_READ <pID> <StartAddr> <Length>
```

# Implemented

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
# RAM_READ, RAM_WRITE
---
   

