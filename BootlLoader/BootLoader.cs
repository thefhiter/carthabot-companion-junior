using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BootlLoader
{
    public static class BootLoader
    {

        public async static Task<bool> EnterBootloaderMode()
        {
            try
            {
                bool res = IsRP2StoragePresent();
                if (res)
                {
                    return true;
                }
                else
                {
                    SetupSerialPort("COM21");
                    await Task.Delay(250);
                    return IsRP2StoragePresent();
                }
            }
            catch (Exception ex)
            {

                return false;
            }

        }
        private static bool IsRP2StoragePresent()
        {
            List<DriveInfo> allDrives = new List<DriveInfo>(DriveInfo.GetDrives());
            return allDrives.Any(drive => drive.IsReady && drive.VolumeLabel == "RPI-RP2");
        }

        private static void SetupSerialPort(string portName)
        {
            using (SerialPort _serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One))
            {
                _serialPort.ReadTimeout = 500;
                _serialPort.WriteTimeout = 500;

                try
                {
                    _serialPort.Open();
                    _serialPort.WriteLine("import machine");
                    _serialPort.Write(new byte[] { 0x0D }, 0, 1);
                    _serialPort.WriteLine("machine.bootloader()");
                    _serialPort.Write(new byte[] { 0x0D }, 0, 1);

                    _serialPort.DtrEnable = true;
                    _serialPort.RtsEnable = true;
                    System.Threading.Thread.Sleep(100);
                    _serialPort.DtrEnable = false;
                    _serialPort.RtsEnable = false;

                }
                catch (Exception ex)
                {

                }

            }

        }
    }

}
