using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using System.IO;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Markup;
using CompanionApp.Models.Classes;


namespace CompanionApp.Service
{
    public static class IniSupport
    {
        

        public static string GetVersion()
        {
            string version = "";

            string iniFilePath = System.IO.Path.Combine("Resources", "Settings.ini");

            IniFile iniFile = new IniFile(iniFilePath);

            try
            {
                version = iniFile.ReadValue("Settings", "Version");

            }
            catch (Exception)
            {

            }
            return version;
        }
        public static string GetLanguage()
        {
            string version = "";

            string iniFilePath = System.IO.Path.Combine("Resources", "Settings.ini");

            IniFile iniFile = new IniFile(iniFilePath);

            try
            {
                version = iniFile.ReadValue("Settings", "Lng");

            }
            catch (Exception)
            {

            }
            return version;
        }
        public static void UpdateLanguage(string newLng)
        {
            string iniFilePath = System.IO.Path.Combine("Resources", "Settings.ini");
            IniFile iniFile = new IniFile(iniFilePath);

            try
            {
                // Update the value in the INI file
                iniFile.WriteValue("Settings", "Lng", newLng.ToUpper());
            }
            catch (Exception)
            {
                // Handle any exceptions that may occur during the update
            }
        }

        public static string GetGitHubUrl()
        {
            string githuburl = "";

            string iniFilePath = System.IO.Path.Combine("Resources", "Settings.ini");

            IniFile iniFile = new IniFile(iniFilePath);

            try
            {
                githuburl = iniFile.ReadValue("Settings", "GithubUrl");

            }
            catch (Exception)
            {

            }
            return githuburl;
        }

        public static string GetSiteUrl()
        {
            string siteUrl = "";

            string iniFilePath = System.IO.Path.Combine("Resources", "Settings.ini");

            IniFile iniFile = new IniFile(iniFilePath);

            try
            {
                siteUrl = $"{iniFile.ReadValue("Settings", "SiteUrl")}";

            }
            catch (Exception)
            {

            }
            return siteUrl;
        }

    }
}
