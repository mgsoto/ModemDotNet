using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace mgsoto.Ports.Serial
{
    /// <summary>
    /// https://github.com/aesirot/ymodem
    /// </summary>
    public sealed class YModem : Modem
    {
        public override void Send(Stream channel, Stream dataStream, string fileName)
        {
            Timer timer = new Timer(WAIT_FOR_RECEIVER_TIMEOUT).start();
            bool useCrc16 = WaitReceiverRequest(channel, timer);
            ICrc crc = WaitReceiverRequest(channel, timer) ? Crc.Crc16 : Crc.Crc8;

            string fileNameString = $"{fileName.ToLower()}";
            byte[] fileNameBytes = new byte[128];
            Encoding.UTF8.GetBytes(fileNameString, 0, fileNameString.Length, fileNameBytes, 0);

            SendBlock(channel, 0, fileNameBytes, 128, crc);

             WaitReceiverRequest(channel, timer);
            //send data
            SendDataBlocks(channel, dataStream, 1, crc);


            SendEot(channel);
        }

        protected static void SendDataBlocks(Stream channel, Stream dataStream, int blockNumber, ICrc crc)
        {
            int dataLength;
            byte[] block = new byte[1024];

            while ((dataLength = dataStream.Read(block, 0, 1024)) > 0)
            {

                SendBlock(channel, blockNumber++, block, dataLength, crc);
            }
        }

        protected static void SendEot(Stream channel)
        {
            int errorCount = 0;
            Timer timer = new Timer(BLOCK_TIMEOUT);
            int character;
            while (errorCount < 10)
            {
                SendByte(channel, EOT);
                try
                {
                    character = ReadByte(channel, timer);

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

        private static bool WaitReceiverRequest(Stream channel, Timer timer)
        {
        int character;
            while (true) {
            try {
                character = ReadByte(channel, timer);
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

        protected static void SendByte(Stream channel, byte b)
        {
            channel.WriteByte(b);
            channel.Flush();
        }

        private static void SendBlock(Stream channel, int blockNumber, byte[] block, int dataLength, ICrc crc)
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
                    channel.WriteByte(STX);
                else //128
                    channel.WriteByte(SOH);
                channel.WriteByte((byte)blockNumber);
                channel.WriteByte((byte)~blockNumber);

                channel.Write(block, 0, block.Length);
                WriteCrc(channel, block, crc);
                channel.Flush();

                while (true)
                {
                    try
                    {
                        character = ReadByte(channel, timer);
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

        private static void WriteCrc(Stream channel, byte[] block, ICrc crc)
        {
            byte[] crcBytes = new byte[crc.Length];
            long crcValue = crc.Compute(block);
            for (int i = 0; i < crc.Length; i++)
            {
                crcBytes[crc.Length - i - 1] = (byte)((crcValue >> (8 * i)) & 0xFF);
            }
            channel.Write(crcBytes, 0, crcBytes.Length);
        }

        private static byte ReadByte(Stream channel, Timer timer)
        {
            while (true)
            {
                if (channel.Length - channel.Position > 0)
                {
                    int b = channel.ReadByte();
                    return (byte)b;
                }
                if (timer.isExpired()) {
                    throw new TimeoutException();
                }

                Thread.Sleep(10);
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
            this.startTime = DateTime.Now.ToUnixTime();
            this.stopTime = 0;
            return this;
        }

        public void stop()
        {
            this.stopTime = DateTime.Now.ToUnixTime();
        }

        public bool isExpired()
        {
            return (DateTime.Now.ToUnixTime() > startTime + timeout);
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
