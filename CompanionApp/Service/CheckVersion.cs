using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CompanionApp.Service
{
    public static class CheckVersion
    {

        static string repoOwner = "FAB619";
        static string repoName = "CarthabotCompanion";
        static string apiUrl = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/latest";


        public static async Task<string> IsUpToDate(string currentVersion)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("request");
                    HttpResponseMessage response = await client.GetAsync(apiUrl);
                    response.EnsureSuccessStatusCode();

                    string responseBody = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(responseBody);
                    string nameField = json["name"]?.ToString();

                    // Extract version from "Release v1.0.0"
                    string latestVersion = null;
                    if (!string.IsNullOrEmpty(nameField))
                    {
                        var match = Regex.Match(nameField, @"v\d+(\.\d+)*");
                        if (match.Success)
                        {
                            latestVersion = match.Value.ToLower();
                        }
                    }

                    if (latestVersion != null && latestVersion != currentVersion.ToLower())
                    {
                        return latestVersion;
                    }
                    else
                    {
                        return "uptodate";
                    }
                }
            }
            catch (Exception)
            {
                // Optionally log the exception or handle it
            }

            return null;
        }


        public static async Task<string> GetLatestReleaseUrl()
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("request");
                HttpResponseMessage response = await client.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();
                JObject json = JObject.Parse(responseBody);
                string latestReleaseUrl = json["html_url"].ToString();

                return latestReleaseUrl;
            }
        }

        public static async void OpenNewVersion(string newVersion)
        {

            Process.Start(new ProcessStartInfo
            {
                FileName = $"{IniSupport.GetGitHubUrl()}/releases/tag/{newVersion}",
                UseShellExecute = true
            });

        }
    }
     
}
