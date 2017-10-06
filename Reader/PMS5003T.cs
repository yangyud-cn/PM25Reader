using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;

namespace Sensor
{
    [Serializable]
    public class SensorData
    {
        public int PM1_0 { get; set; } = -1;      // PM 1.0  ug/m^3  CF=1
        public int PM2_5 { get; set; } = -1;      // PM 2.5
        public int PM10 { get; set; } = -1;       // PM 10
        public int PM1_0A { get; set; } = -1;     // PM 1.0
        public int PM2_5A { get; set; } = -1;
        public int PM10A { get; set; } = -1;
        public int Count0_3 { get; set; } = -1;
        public int Count0_5 { get; set; } = -1;
        public int Count1_0 { get; set; } = -1;
        public int Count2_5 { get; set; } = -1;

        public float Temporature { get; set; } = float.NaN;
        public float Humidity { get; set; } = float.NaN;

        public byte[] RawData { get; set; } = null;
    }

    /// <summary>
    /// Sensor Reader that support PMS5003T
    /// </summary>
    public class PMS5003T : IDisposable
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="port">Serial port</param>
        public PMS5003T(string port)
        {
            _serialPort = new SerialPort(port)
            {
                // 8N1
                BaudRate = 9600,
                Parity = Parity.None,
                StopBits = StopBits.One,
                ReadTimeout = 2000,    // report data is always less than 1 seconds
                WriteTimeout = 2000
            };

            _serialPort.Open();
        }

        /// <summary>
        /// Probe the port to check if there is a PMS5003x device attached
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        public static bool ProbePort(string port)
        {
            try
            {
                using (var pms = new PMS5003T(port))
                {
                    SensorData data;
                    if (pms.ReadData(out data))
                    {
                        return true;
                    }

                    return pms.SetStandbyMode(false);
                }
            }
            catch(Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Read the device
        /// </summary>
        /// <param name="data">output of sensor data</param>
        /// <returns>true if read ok, else read error</returns>
        public bool ReadData(out SensorData data)
        {
            byte[] buffer = new byte[32];
            bool timeout;

            int cnt = ReadPacket(buffer, 32, out timeout);
            if (cnt != 32)
            {
                data = null;
                return false;
            }

            data = new SensorData()
            {
                PM1_0 = (int)(((uint)buffer[4] << 8) | buffer[5]),
                PM2_5 = (int)(((uint)buffer[6] << 8) | buffer[7]),
                PM10 = (int)(((uint)buffer[8] << 8) | buffer[9]),
                PM1_0A = (int)(((uint)buffer[10] << 8) | buffer[11]),
                PM2_5A = (int)(((uint)buffer[12] << 8) | buffer[13]),
                PM10A = (int)(((uint)buffer[14] << 8) | buffer[15]),
                Count0_3 = (int)(((uint)buffer[16] << 8) | buffer[17]),
                Count0_5 = (int)(((uint)buffer[18] << 8) | buffer[19]),
                Count1_0 = (int)(((uint)buffer[20] << 8) | buffer[21]),
                Count2_5 = (int)(((uint)buffer[22] << 8) | buffer[23]),
                Temporature = (float)(((uint)buffer[24] << 8) | buffer[25]) / 10,
                Humidity = (float)(((uint)buffer[26] << 8) | buffer[27]) / 10,
                RawData = buffer,
            };

            return true;
        }

        /// <summary>
        /// Set the data read mode
        /// </summary>
        /// <param name="passive">true: passive mode, default is active mode after power on</param>
        public bool SetReadMode(bool passive)
        {
            WriteCommand(0xe1, passive ? 0 : 1);
            byte[] buf = new byte[8];
            bool timeout;
            int len = ReadPacket(buf, 8, out timeout);
            return len == 8 && buf[4] == 0xe1;
        }

        /// <summary>
        /// Set the standby mode
        /// Note: when it leaves standy mode, it will take a few seconds to allow the fan to spin up.  It also reset the read mode to active mode.
        /// </summary>
        /// <param name="standby">true: enter standby mode, false: go back to active mode, </param>
        public bool SetStandbyMode(bool standby)
        {
            WriteCommand(0xe4, standby ? 0 : 1);
            if (!standby)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                SensorData data;
                bool actived;
                do
                {
                    actived = ReadData(out data);
                }
                while (!actived && sw.Elapsed.TotalSeconds < 4) ;
                return actived;
            }
            else
            {
                byte[] buf = new byte[8];
                bool timeout;
                int len = ReadPacket(buf, 8, out timeout);
                return len == 8 && buf[4] == 0xe4;
            }
        }

        /// <summary>
        /// Read one sample in passive mode
        /// </summary>
        public bool PassiveRead(out SensorData data)
        {
            WriteCommand(0xe2, 0);
            Thread.Sleep(100);
            return ReadData(out data);
        }

        /// <summary>
        /// Write command
        /// </summary>
        /// <param name="cmd">command</param>
        /// <param name="payload">16 bit payload of command</param>
        protected void WriteCommand(byte cmd, int payload)
        {
            byte[] cmdbuf = new byte[7];
            int csum = 0x42 + 0x4d + cmd + ((payload >> 8)&0xff) + (payload & 0xff); 
            cmdbuf[0] = 0x42;
            cmdbuf[1] = 0x4d;
            cmdbuf[2] = cmd;
            cmdbuf[3] = (byte)((payload>>8)&0xff);
            cmdbuf[4] = (byte)(payload &0xff);
            cmdbuf[5] = (byte)((csum>>8)&0xff);
            cmdbuf[6] = (byte)(csum &0xff);

            _serialPort.Write(cmdbuf, 0, 7);
        }

        /// <summary>
        /// Read a packet from the sensor
        /// </summary>
        /// <param name="buffer">buffer at least len bytes to receive the full packet</param>
        /// <param name="len">expected packet length</param>
        /// <param name="timeOut">if read timeout or failed to find sync head in more than two packet's length</param>
        /// <returns>actual length of packet, -1 for error</returns>
        protected int ReadPacket(byte[] buffer, int len, out bool timeOut)
        {
            try
            {
                timeOut = false;
                // look for sync header
                int sync = len * 2 + 1;
                while (sync > 0)
                {
                    int ch = _serialPort.ReadByte();
                    if (ch != 0x42)
                    {
                        sync--;
                        continue;
                    }

                    buffer[0] = (byte)ch;
                    ch = _serialPort.ReadByte();
                    if (ch != 0x4d)
                    {
                        sync--;
                        continue;
                    }

                    buffer[1] = (byte)ch;
                    break;
                }

                // check if we haven't found sync
                if (sync == 0)
                {
                    timeOut = true;
                    return -1;
                }

                // len
                buffer[2] = (byte)_serialPort.ReadByte();
                buffer[3] = (byte)_serialPort.ReadByte();

                uint packetLen = ((uint)buffer[2] << 8) | (uint)buffer[3];

                int realLen = packetLen + 4 < len ? (int)packetLen + 4 : len;

                // read payload and crc
                for (int i = 4; i < realLen; i++)
                {
                    buffer[i] = (byte)_serialPort.ReadByte();
                }

                // check crc
                int crc = 0;
                for (int i = 0; i < realLen - 2; i++)
                {
                    crc += buffer[i];
                }

                bool crcValid = (crc & 0xff) == buffer[realLen - 1] && (crc >> 8) == buffer[realLen - 2];

                // return if crc check is valid
                return realLen;
            }
            catch (TimeoutException)
            {
                timeOut = true;
                return -1;
            }
            catch (Exception)
            {
                timeOut = false;
                return -1;
            }
        }

        protected SerialPort _serialPort;

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _serialPort.Close();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

    }
}
