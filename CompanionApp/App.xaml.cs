using CompanionApp.Modules;
using CompanionApp.ViewModels;
using CompanionApp.Views;
using MazeProject.ViewModels;
using MazeProject.Views;
using Prism.Ioc;
using Prism.Modularity;
using Syncfusion.Licensing;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace CompanionApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterDialogWindow<SelectMapImageShell>("SelectMapImageShell");
            containerRegistry.RegisterDialogWindow<PlugAndPowerOnShell>("PlugAndPowerOnShell");


            
            containerRegistry.RegisterDialog<SelectMapImageView, SelectMapImageViewModel>();
            containerRegistry.RegisterDialog<PlugAndPowerOnView, PlugAndPowerOnViewModel>();

        }
        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            base.ConfigureModuleCatalog(moduleCatalog);
            moduleCatalog.AddModule<MainModule>();
            moduleCatalog.AddModule<AtelierModule>();



        }


        public App()
        {
            SyncfusionLicenseProvider.RegisterLicense("MzY3MDAxMEAzMjM4MmUzMDJlMzBiTXlWc0N5K001R0hvZDJROFR2WGJBand6K25ZWktjS2NXVng2UHZVcjJRPQ==");
            SetupGlobalErrorHandling();
        }

        // Safety net: no single feature (a dropped WiFi link, a missing COM port, a 3D-render
        // hiccup) should ever take the whole app down. UI-thread errors are caught and the app
        // keeps running; every error is written to a crash log so problems can be diagnosed.
        private void SetupGlobalErrorHandling()
        {
            DispatcherUnhandledException += (s, e) =>
            {
                LogCrash("UI", e.Exception);
                MessageBox.Show(
                    "Something went wrong, but CarthaBot Companion kept running.\n\n" +
                    e.Exception.Message +
                    "\n\nA detailed report was saved to:\n" + LogPath,
                    "CarthaBot Companion", MessageBoxButton.OK, MessageBoxImage.Warning);
                e.Handled = true; // keep the app alive
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogCrash("AppDomain", e.ExceptionObject as Exception);

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogCrash("Task", e.Exception);
                e.SetObserved();
            };
        }

        private static string LogPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "CarthaBot", "crash.log");

        private static void LogCrash(string source, Exception ex)
        {
            try
            {
                var path = LogPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.AppendAllText(path,
                    $"==== {DateTime.Now:yyyy-MM-dd HH:mm:ss} [{source}] ====\n" +
                    (ex?.ToString() ?? "(no exception object)") + "\n\n");
            }
            catch { /* logging must never throw */ }
        }
    }
}
