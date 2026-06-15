using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CompanionApp.Models.Classes
{
    public class IniFile
    {
        private string filePath;

        public IniFile(string filePath)
        {
            this.filePath = filePath;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(string section, string key, string defaultValue, StringBuilder value, int size, string filePath);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool WritePrivateProfileString(string section, string key, string value, string filePath);

        public string ReadValue(string section, string key, string defaultValue = "")
        {
            const int bufferSize = 255;
            StringBuilder value = new StringBuilder(bufferSize);
            GetPrivateProfileString(section, key, defaultValue, value, bufferSize, filePath);
            if (value.Length != 0)
            {
                return value.ToString();

            }
            else
            {
                return "0";
            }
        }

        public bool WriteValue(string section, string key, string value)
        {
            return WritePrivateProfileString(section, key, value, filePath);
        }
    }
}