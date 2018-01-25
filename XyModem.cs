using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace None
{
    /// <summary>
    /// https://github.com/aesirot/ymodem
    /// </summary>
    public class XyModem
    {
        private const byte SOH = 0x01; /* Start Of Header */
        private const byte STX = 0x02; /* Start Of Text (used like SOH but means 1024 block size) */
        private const byte EOT = 0x04; /* End Of Transmission */
        private const byte ACK = 0x06; /* ACKnowlege */
        private const byte NAK = 0x15; /* Negative AcKnowlege */
        private const byte CAN = 0x18; /* CANcel character */
        private const byte CPMEOF = 0x1A;
        private const int MAXERRORS = 10;
        private const int WAIT_FOR_RECEIVER_TIMEOUT = 60_000;
        private const int BLOCK_TIMEOUT = 1000;
        private const byte ST_C = (byte)'C';
        private const int SEND_BLOCK_TIMEOUT = 10_000;

        public static void SendYModem(IRS232 channel, Stream dataStream, string fileName)
        {
            Timer timer = new Timer(WAIT_FOR_RECEIVER_TIMEOUT).start();
            bool useCRC16 = waitReceiverRequest(channel, timer);

            ICrc crc;

            if (useCRC16)
                crc = new CRC16();
            else
                crc = new Crc8();

            //send block 0
            //string fileNameString = $"{fileName}\0{dataStream.Length} {ToUnixTimeString(DateTime.Now)}";
            string fileNameString = $"{fileName.ToLower()}";
            byte[] fileNameBytes = new byte[128];
            Encoding.UTF8.GetBytes(fileNameString, 0, fileNameString.Length, fileNameBytes, 0);

            sendBlock(channel, 0, fileNameBytes, 128, crc);

             waitReceiverRequest(channel, timer);
            //send data
            sendDataBlocks(channel, dataStream, 1, crc);


            sendEOT(channel);
        }

        protected static void sendDataBlocks(IRS232 channel, Stream dataStream, int blockNumber, ICrc crc)
        {
            int dataLength;
            byte[] block = new byte[1024];

            while ((dataLength = dataStream.Read(block, 0, 1024)) > 0)
            {

                sendBlock(channel, blockNumber++, block, dataLength, crc);
            }
        }

        protected static void sendEOT(IRS232 channel)
        {
            int errorCount = 0;
            Timer timer = new Timer(BLOCK_TIMEOUT);
            int character;
            while (errorCount < 10)
            {
                sendByte(channel, EOT);
                try
                {
                    character = readByte(channel, timer);

                    if (character == ACK)
                    {
                        return;
                    }
                    else if (character == CAN)
                    {
                        throw new IOException("Transmission terminated");
                    }
                }
                catch (TimeoutException ignored)
                {
                }
                errorCount++;
            }
        }

        private static bool waitReceiverRequest(IRS232 channel, Timer timer)
        {
        int character;
            while (true) {
            try {
                character = readByte(channel, timer);
                if (character == NAK)
                    return false;
                if (character == ST_C) {
                    return true;
                }
            } catch (TimeoutException e) {
                throw new IOException("Timeout waiting for receiver");
            }
        }
    }

        protected static void sendByte(IRS232 channel, byte b)
        {
            channel.Stream.WriteByte(b);
            channel.Stream.Flush();
        }

        private static void sendBlock(IRS232 channel, int blockNumber, byte[] block, int dataLength, ICrc crc)
        {
            int errorCount;
            int character;
            Timer timer = new Timer(SEND_BLOCK_TIMEOUT);

            if (dataLength < block.Length)
            {
                for(int k = dataLength; k < block.Length; k++)
                {
                    block[k] = CPMEOF;
                }
            }
            errorCount = 0;

            while (errorCount < MAXERRORS)
            {
                timer.start();

                if (block.Length == 1024)
                    channel.Stream.WriteByte(STX);
                else //128
                    channel.Stream.WriteByte(SOH);
                channel.Stream.WriteByte((byte)blockNumber);
                channel.Stream.WriteByte((byte)~blockNumber);

                channel.Stream.Write(block, 0, block.Length);
                writeCRC(channel, block, crc);
                channel.Stream.Writer.Flush();

                while (true)
                {
                    try
                    {
                        character = readByte(channel, timer);
                        if (character == ACK)
                        {
                            return;
                        }
                        else if (character == NAK)
                        {
                            errorCount++;
                            break;
                        }
                        else if (character == CAN)
                        {
                            throw new IOException("Transmission terminated");
                        }
                    }
                    catch (TimeoutException e)
                    {
                        errorCount++;
                        break;
                    }
                }

            }

            throw new IOException("Too many errors caught, abandoning transfer");
        }

        private static void writeCRC(IRS232 channel, byte[] block, ICrc crc)
        {
            byte[] crcBytes = new byte[crc.getCRCLength()];
            long crcValue = crc.calcCRC(block);
            for (int i = 0; i < crc.getCRCLength(); i++)
            {
                crcBytes[crc.getCRCLength() - i - 1] = (byte)((crcValue >> (8 * i)) & 0xFF);
            }
            channel.Stream.Write(crcBytes, 0, crcBytes.Length);
        }

        private static byte readByte(IRS232 channel, Timer timer)
        {
            while (true)
            {
                if (channel.BytesToRead > 0)
                {
                    int b = channel.Stream.ReadByte();
                    return (byte)b;
                }
                if (timer.isExpired()) {
                    throw new TimeoutException();
                }

                Thread.Sleep(10);
            }
        }

        public static string ToUnixTimeString(DateTime date)
        {
            var unixTime = ToUnixTime(date);
            return Convert.ToString(unixTime, 8);
        }

        public static long ToUnixTime(DateTime date)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return Convert.ToInt64((date - epoch).Seconds);
        }

        public interface ICrc
        {
            int getCRCLength();
            long calcCRC(byte[] block);
        }

        public class Crc8 : ICrc
        {
            public int getCRCLength()
            {
                return 1;
            }

            public long calcCRC(byte[] block)
            {
                byte checkSumma = 0;
                for (int i = 0; i < block.Length; i++)
                {
                    checkSumma += block[i];
                }
                return checkSumma;
            }
        }

        public class CRC16 : ICrc
        {

            private static int[] table = {
            0x0000, 0x1021, 0x2042, 0x3063, 0x4084, 0x50A5, 0x60C6, 0x70E7,
            0x8108, 0x9129, 0xA14A, 0xB16B, 0xC18C, 0xD1AD, 0xE1CE, 0xF1EF,
            0x1231, 0x0210, 0x3273, 0x2252, 0x52B5, 0x4294, 0x72F7, 0x62D6,
            0x9339, 0x8318, 0xB37B, 0xA35A, 0xD3BD, 0xC39C, 0xF3FF, 0xE3DE,
            0x2462, 0x3443, 0x0420, 0x1401, 0x64E6, 0x74C7, 0x44A4, 0x5485,
            0xA56A, 0xB54B, 0x8528, 0x9509, 0xE5EE, 0xF5CF, 0xC5AC, 0xD58D,
            0x3653, 0x2672, 0x1611, 0x0630, 0x76D7, 0x66F6, 0x5695, 0x46B4,
            0xB75B, 0xA77A, 0x9719, 0x8738, 0xF7DF, 0xE7FE, 0xD79D, 0xC7BC,
            0x48C4, 0x58E5, 0x6886, 0x78A7, 0x0840, 0x1861, 0x2802, 0x3823,
            0xC9CC, 0xD9ED, 0xE98E, 0xF9AF, 0x8948, 0x9969, 0xA90A, 0xB92B,
            0x5AF5, 0x4AD4, 0x7AB7, 0x6A96, 0x1A71, 0x0A50, 0x3A33, 0x2A12,
            0xDBFD, 0xCBDC, 0xFBBF, 0xEB9E, 0x9B79, 0x8B58, 0xBB3B, 0xAB1A,
            0x6CA6, 0x7C87, 0x4CE4, 0x5CC5, 0x2C22, 0x3C03, 0x0C60, 0x1C41,
            0xEDAE, 0xFD8F, 0xCDEC, 0xDDCD, 0xAD2A, 0xBD0B, 0x8D68, 0x9D49,
            0x7E97, 0x6EB6, 0x5ED5, 0x4EF4, 0x3E13, 0x2E32, 0x1E51, 0x0E70,
            0xFF9F, 0xEFBE, 0xDFDD, 0xCFFC, 0xBF1B, 0xAF3A, 0x9F59, 0x8F78,
            0x9188, 0x81A9, 0xB1CA, 0xA1EB, 0xD10C, 0xC12D, 0xF14E, 0xE16F,
            0x1080, 0x00A1, 0x30C2, 0x20E3, 0x5004, 0x4025, 0x7046, 0x6067,
            0x83B9, 0x9398, 0xA3FB, 0xB3DA, 0xC33D, 0xD31C, 0xE37F, 0xF35E,
            0x02B1, 0x1290, 0x22F3, 0x32D2, 0x4235, 0x5214, 0x6277, 0x7256,
            0xB5EA, 0xA5CB, 0x95A8, 0x8589, 0xF56E, 0xE54F, 0xD52C, 0xC50D,
            0x34E2, 0x24C3, 0x14A0, 0x0481, 0x7466, 0x6447, 0x5424, 0x4405,
            0xA7DB, 0xB7FA, 0x8799, 0x97B8, 0xE75F, 0xF77E, 0xC71D, 0xD73C,
            0x26D3, 0x36F2, 0x0691, 0x16B0, 0x6657, 0x7676, 0x4615, 0x5634,
            0xD94C, 0xC96D, 0xF90E, 0xE92F, 0x99C8, 0x89E9, 0xB98A, 0xA9AB,
            0x5844, 0x4865, 0x7806, 0x6827, 0x18C0, 0x08E1, 0x3882, 0x28A3,
            0xCB7D, 0xDB5C, 0xEB3F, 0xFB1E, 0x8BF9, 0x9BD8, 0xABBB, 0xBB9A,
            0x4A75, 0x5A54, 0x6A37, 0x7A16, 0x0AF1, 0x1AD0, 0x2AB3, 0x3A92,
            0xFD2E, 0xED0F, 0xDD6C, 0xCD4D, 0xBDAA, 0xAD8B, 0x9DE8, 0x8DC9,
            0x7C26, 0x6C07, 0x5C64, 0x4C45, 0x3CA2, 0x2C83, 0x1CE0, 0x0CC1,
            0xEF1F, 0xFF3E, 0xCF5D, 0xDF7C, 0xAF9B, 0xBFBA, 0x8FD9, 0x9FF8,
            0x6E17, 0x7E36, 0x4E55, 0x5E74, 0x2E93, 0x3EB2, 0x0ED1, 0x1EF0,
        };

            public int getCRCLength()
            {
                return 2;
            }

            public long calcCRC(byte[] block)
            {
                int crc = 0x0000;
                foreach (byte b in block)
                {
                    crc = ((crc << 8) ^ table[((crc >> 8) ^ (0xff & b))]) & 0xFFFF;
                }

                return crc;
            }
        }
    }

    class Timer
    {

        private long startTime = 0;
        private long stopTime = 0;
        private long timeout = 0;

        public Timer(long timeout)
        {
            this.timeout = timeout;
        }

        public Timer start()
        {
            this.startTime = XyModem.ToUnixTime(DateTime.Now);
            this.stopTime = 0;
            return this;
        }

        public void stop()
        {
            this.stopTime = XyModem.ToUnixTime(DateTime.Now);
        }

        public bool isExpired()
        {
            return (XyModem.ToUnixTime(DateTime.Now) > startTime + timeout);
        }

        public long getStartTime()
        {
            return this.startTime;
        }

        public long getStopTime()
        {
            return this.stopTime;
        }

        public long getTotalTime()
        {
            return this.stopTime - this.startTime;
        }

        public long getTimeout()
        {
            return timeout;
        }

        public void setTimeout(long timeout)
        {
            this.timeout = timeout;
        }

        public bool isWorking()
        {
            return (stopTime != 0);
        }
    }
}
