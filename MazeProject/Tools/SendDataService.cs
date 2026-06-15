using MazeProject.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MazeProject.Tools
{
    public static class SendDataService
    {
        public static byte[] payload;
        public static List<Step> steps;
        public static string COMPort; 
        public static void SendData(List<Step> _steps)
        {
            if(_steps.Count == 0) return;
            steps = _steps;
            generatPayload();
            using(SerialPort port = new SerialPort(COMPort, 9600))
            {
                try
                {
                    port.DtrEnable = true;
                    port.Open();
                    if (port.IsOpen)
                    {
                        port.Write(payload, 0, payload.Length);
                    }
                }
                catch (Exception)
                {

                    throw;
                }
                finally { port.Close(); }

            }
        }

        private static void generatPayload()
        {
            int DataCount = steps.Count + 4;
            int index = 0;
            payload = new byte[DataCount];
            payload[index] = 0xAA;
            index++;
            payload[index] = (byte)steps.Count;
            index++;
            foreach (Step step in steps)
            {
                switch (step.Mvt)
                {

                    case mouvment.Forward:
                        payload[index] = (byte)step.Mvt; index++;
                        break;
                    case mouvment.Backward:
                        payload[index] = (byte)step.Mvt; index++;
                        break;
                    case mouvment.Left:
                        payload[index] = (byte)step.Mvt; index++;
                        break;
                    case mouvment.Right:
                        payload[index] = (byte)step.Mvt; index++;
                        break;
                }
            }
            ushort crc = ComputeCrc16(payload, index);
            payload[index++] = (byte)(crc >> 8);
            payload[index] = (byte)(crc & 0xFF);


        }

        private static ushort ComputeCrc16(byte[] data, int length)
        {
            ushort crc = 0x0000; // Initial value for CRC-16-CCITT
            ushort polynomial = 0x1021;
            for (int i = 0; i < length; i++)
            {
                crc ^= (ushort)(data[i] << 8);
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x8000) != 0)
                        crc = (ushort)((crc << 1) ^ polynomial);
                    else
                        crc <<= 1;
                }
            }
            return crc;
        }
    }
}
