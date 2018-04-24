# ModemDotNet

This project implements Y Modem in C# as a .NET Standard 2.0 class library.

Work in this project is heavily based on an adaptation from https://github.com/aesirot/ymodem.

# Usage

```C#
using (SerialPort port = new SerialPort("COM6", 9600))
{
	port.Open();
	FileStream file = File.Open(filePath, FileMode.Open);
	await port.BaseStream.SendYModem(file, fileName);
}
```
