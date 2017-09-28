using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Ports;

namespace Test
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
    }

    public class PMS5003T : IDisposable
    {
        public PMS5003T(string port)
        {
            _serialPort = new SerialPort(port)
            {
                // 8N1
                BaudRate = 9600,
                Parity = Parity.None,
                StopBits = StopBits.One
            };
        }

        public bool ReadData(out SensorData data)
        {
            byte[] buffer = new byte[32];

            if (!ReadPacket(buffer, 32))
            {
                data = null;
                return false;
            }

            data = new SensorData()
            {
                PM1_0 = (int)(((uint)buffer[4] << 8) | buffer[5]),
                PM2_5 = (int)(((uint)buffer[6] << 8) | buffer[7]),
                PM10  = (int)(((uint)buffer[8] << 8) | buffer[9]),
                PM1_0A = (int)(((uint)buffer[10] << 8) | buffer[11]),
                PM2_5A = (int)(((uint)buffer[12] << 8) | buffer[13]),
                PM10A = (int)(((uint)buffer[14] << 8) | buffer[15]),
                Count0_3 = (int)(((uint)buffer[16] << 8) | buffer[17]),
                Count0_5 = (int)(((uint)buffer[18] << 8) | buffer[19]),
                Count1_0 = (int)(((uint)buffer[20] << 8) | buffer[21]),
                Count2_5 = (int)(((uint)buffer[22] << 8) | buffer[23]),
                Temporature = (float)(((uint)buffer[24] << 8) | buffer[25])/10,
                Humidity = (float)(((uint)buffer[26] << 8) | buffer[27])/10,
            };

            return true;
        }

        protected bool ReadPacket(byte[] buffer, int len)
        {
            // look for sync header
            while(true)
            {
                int ch = _serialPort.ReadByte();
                if (ch != 0x42)
                {
                    continue;
                }

                buffer[0] = (byte)ch;
                ch = _serialPort.ReadByte();
                if (ch != 0x4d)
                {
                    continue;
                }

                buffer[1] = (byte)ch;
                break;
            }

            // len
            buffer[2] = (byte)_serialPort.ReadByte();
            buffer[3] = (byte)_serialPort.ReadByte();

            uint packetLen = ((uint)buffer[2] << 8) | (uint)buffer[3];

            // check lenght
            if ((uint)len - 4 != packetLen)
            {
                return false;
            }

            // read payload and crc
            for (int i = 4; i < len; i++)
            {
                buffer[i] = (byte)_serialPort.ReadByte();
            }

            // check crc
            int crc = 0;
            for (int i = 0; i < len - 2; i++)
            {
                crc += buffer[i];
            }

            // return if crc check is valid
            return (crc & 0xff) == buffer[len - 1] && (crc >> 8) == buffer[len - 2];
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
                    _serialPort.Dispose();
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
