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

        // Owner/repo come from the GithubUrl in Resources\Settings.ini so the
        // "Our GitHub" link and the update check always target the same repository.
        static string GetApiUrl()
        {
            string githubUrl = IniSupport.GetGitHubUrl();
            if (string.IsNullOrEmpty(githubUrl))
                return null;

            string[] parts = githubUrl.TrimEnd('/').Split('/');
            if (parts.Length < 2)
                return null;

            string repoOwner = parts[parts.Length - 2];
            string repoName = parts[parts.Length - 1];
            return $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/latest";
        }

        static bool TryParseVersion(string text, out Version version)
        {
            version = null;
            if (string.IsNullOrEmpty(text))
                return false;

            string cleaned = text.TrimStart('v', 'V');
            if (!cleaned.Contains("."))
                cleaned += ".0";

            return Version.TryParse(cleaned, out version);
        }

        public static async Task<string> IsUpToDate(string currentVersion)
        {
            try
            {
                string apiUrl = GetApiUrl();
                if (apiUrl == null)
                    return null;

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("request");
                    HttpResponseMessage response = await client.GetAsync(apiUrl);
                    response.EnsureSuccessStatusCode();

                    string responseBody = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(responseBody);
                    string nameField = json["tag_name"]?.ToString();
                    if (string.IsNullOrEmpty(nameField))
                        nameField = json["name"]?.ToString();

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

                    // Only offer an update when the release is strictly newer,
                    // so an older release is never proposed as a downgrade.
                    if (TryParseVersion(latestVersion, out Version latest) &&
                        TryParseVersion(currentVersion, out Version current) &&
                        latest > current)
                    {
                        return latestVersion;
                    }

                    return "uptodate";
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
                HttpResponseMessage response = await client.GetAsync(GetApiUrl());
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
