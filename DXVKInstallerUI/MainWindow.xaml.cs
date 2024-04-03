using ByteSizeLib;
using Microsoft.WindowsAPICodePack.Dialogs;
using NLog;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using DXVKInstallerUI.Common;
using DXVKInstallerUI.Functions;
using System.Threading.Tasks;
using PromptDialog;
using System.Net;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using System.Threading;
using System.ComponentModel;
using System.Reflection;
// hi here, i'm an awful coder, so please clean up for me if it really bothers you

namespace DXVKInstallerUI
{
    public partial class MainWindow : Window
    {

        (int, int, bool, bool, bool, bool) resultvk;
        int installdxvk;
        int vram1;
        int vram2;
        bool dxvkonigpu;
        bool firstgpu = true;

        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        [STAThread]
        public static void Main()
        {
            Application app = new Application();
            MainWindow mainWindow = new MainWindow();
            app.Run(mainWindow);
        }
        public MainWindow()
        {
            if ( File.Exists("DXVKInstallerUI.txt")) { File.Delete("DXVKInstallerUI.txt"); }
            NLog.LogManager.Setup().LoadConfiguration(builder =>
            {
                builder.ForLogger().FilterMinLevel(LogLevel.Debug).WriteToFile(fileName: "DXVKInstallerUI.txt");
            });
            Logger.Info(" Initializing the main window...");
            InitializeComponent();
            Logger.Info(" Main window initialized!");

            Logger.Info(" Initializing the vulkan check...");
            resultvk = VulkanChecker.VulkanCheck();
            Logger.Info(" Vulkan check finished!");
            if (resultvk.Item6 && resultvk.Item1 == 2) { asynccheckbox.IsChecked = false; Logger.Debug($" User has an NVIDIA GPU, untoggling the async checkbox..."); }
        }


        private string GetAssemblyVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? String.Empty;
        }

        void client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)delegate
            {
                Logger.Debug(" Successfully downloaded.");
                downloadfinished = true;
                installdxvkbtn.Content = $"Installing...";
            });
        }

        private void aboutButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug(" User opened the About window.");
            MessageBox.Show(
                "This software is made by Gillian. Below is debug text, you don't need it normally.\n\n" +
                $"Install DXVK: {installdxvk}\n" +
                $"dGPU DXVK Support: {resultvk.Item1}\n" +
                $"iGPU DXVK Support: {resultvk.Item2}\n" +
                $"iGPU Only: {resultvk.Item3}\n" +
                $"dGPU Only: {resultvk.Item4}\n" +
                $"Intel iGPU: {resultvk.Item5}\n" +
                $"NVIDIA GPU: {resultvk.Item6}\n\n" +
                $"Version: {GetAssemblyVersion()}",
                "Information");
        }

        private void async_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug(" User toggled async.");
            if (tipscheck.IsChecked == true)
            {
                Logger.Debug(" Displaying a tip...");
                MessageBox.Show("DXVK with async should provide better performance for most, but under some conditions it may provide worse performance instead. Without async, you might stutter the first time you see different areas. It won't stutter the next time in the same area.\n\nNote, however, that performance on NVIDIA when using DXVK 2.0+ may be worse. Feel free to experiment by re-installing DXVK.");
            }
        }
        private void vsync_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug(" User toggled VSync.");
            if (tipscheck.IsChecked == true)
            {
                Logger.Debug(" Displaying a tip...");
                MessageBox.Show("The in-game VSync implementation produces framepacing issues. DXVK's VSync implementation should be preferred.\n\nIt's recommended to keep this on and in-game's implementation off.");
            }
        }

        private void latency_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug(" User toggled Max Frame Latency.");
            if (tipscheck.IsChecked == true)
            {
                Logger.Debug(" Displaying a tip...");
                MessageBox.Show("This option may help avoiding further framepacing issues. It's recommended to keep this on.");
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug(" User is selecting the game folder...");
            while (true)
            {
                CommonOpenFileDialog dialog = new CommonOpenFileDialog();
                dialog.InitialDirectory = "C:\\";
                dialog.IsFolderPicker = true;
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    Logger.Debug(" User selected a folder, proceeding...");
                    if (File.Exists($"{dialog.FileName}\\dsound.dll"))
                    {
                        Logger.Info("dsound.dll detected, warning the user...");
                        var result = MessageBox.Show("You appear to have an outdated ASI Loader (dsound.dll). Consider removing it.\n\nPress 'Yes' to get redirected to download to the latest version - rename dinput8.dll to xlive.dll if the game uses GFWL and you don't plan to use it.", "Outdated ASI loader.", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result == MessageBoxResult.Yes)
                        {
                            ProcessStartInfo psi = new ProcessStartInfo
                            {
                                FileName = "cmd",
                                Arguments = $"/c start {"https://github.com/ThirteenAG/Ultimate-ASI-Loader/releases/latest"}",
                                CreateNoWindow = true,
                                UseShellExecute = false,
                            };
                            Process.Start(psi);
                        }
                    }
                    directorytxt.Text = "Game Directory:";
                    directorytxt.FontWeight = FontWeights.Normal;
                    directorytxt.TextDecorations = null;
                    tipsnote.TextDecorations = TextDecorations.Underline;
                    gamedirectory.Text = dialog.FileName;

                    if (resultvk.Item1 == 0 && resultvk.Item2 == 0)
                    {
                        dxvkPanel.IsEnabled = false;
                        Logger.Debug(" DXVK is not supported - throwing an error.");
                        MessageBox.Show("Your device does not support DXVK - the app will close now.");
                        System.Windows.Application.Current.Shutdown(0);
                    }
                    Logger.Debug(" Enabled the DXVK panel.");
                    dxvkPanel.IsEnabled = true;
                    break;
                }
                else
                {
                    break;
                }

            }
        }

        bool downloadfinished = false;
        bool extractfinished = false;

        private async Task InstallDXVK(string downloadUrl)
        {
            try
            {
                Logger.Debug(" Downloading the .tar.gz...");
                Thread thread = new Thread(() =>
                {
                    Logger.Debug(" Downloading the selected release...");
                    WebClient client = new WebClient();
                    client.DownloadProgressChanged += new DownloadProgressChangedEventHandler(client_DownloadProgressChanged);
                    client.DownloadFileCompleted += new AsyncCompletedEventHandler(client_DownloadFileCompleted);
                    client.DownloadFileAsync(new Uri(downloadUrl), "./dxvk.tar.gz");
                });
                thread.Start();
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error downloading DXVK");
                throw;
            }
        }

        void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)delegate
            {
                double bytesIn = double.Parse(e.BytesReceived.ToString());
                double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
                double percentage = bytesIn / totalBytes * 100;
                int percentageInt = Convert.ToInt16(percentage);
                installdxvkbtn.Content = $"Downloading... ({percentageInt}%)";
            });
        }
        private async Task ExtractDXVK(string installationDir, List<string> dxvkConf)
        {

            Logger.Debug(" Extracting the d3d9.dll from the archive...");
            using (FileStream fsIn = new FileStream("./dxvk.tar.gz", FileMode.Open))
            using (GZipInputStream gzipStream = new GZipInputStream(fsIn))
            using (TarInputStream tarStream = new TarInputStream(gzipStream))
            {
                TarEntry entry;
                while ((entry = tarStream.GetNextEntry()) != null)
                {
                    if (entry.Name.EndsWith("x32/d3d9.dll"))
                    {
                        using (FileStream fsOut = File.Create(Path.Combine(installationDir, "d3d9.dll")))
                        {
                            tarStream.CopyEntryContents(fsOut);
                            Logger.Debug(" d3d9.dll extracted into the game folder.");
                        }
                        break;
                    }
                }
            }

            Logger.Debug(" Deleting the .tar.gz...");
            File.Delete("dxvk.tar.gz");

            Logger.Debug(" Writing the dxvk.conf...");
            using (StreamWriter confWriter = File.CreateText(Path.Combine(installationDir, "dxvk.conf")))
            {
                foreach (string option in dxvkConf)
                {
                    confWriter.WriteLine(option);
                }
            }
            Logger.Debug(" dxvk.conf successfully written to game folder.");
            extractfinished = true;
        }
        private async Task downloaddxvk(string link, List<string> dxvkconf, bool gitlab, bool githubalt)
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Other");
            var firstResponse = await httpClient.GetAsync(link);
            firstResponse.EnsureSuccessStatusCode();
            var firstResponseBody = await firstResponse.Content.ReadAsStringAsync();
            var parsed = JsonDocument.Parse(firstResponseBody).RootElement;
            string downloadUrl = null;
            switch (gitlab, githubalt)
            {
                case (false, false):
                    {
                        downloadUrl = parsed.GetProperty("assets")[0].GetProperty("browser_download_url").GetString();
                        break;
                    }
                case (true, false):
                    {
                        downloadUrl = parsed[0].GetProperty("assets").GetProperty("links")[0].GetProperty("url").GetString();
                        break;
                    }
                case (false, true):
                    {
                        downloadUrl = parsed.GetProperty("browser_download_url").GetString();
                        break;
                    }

            }
            InstallDXVK(downloadUrl!);
            while (!downloadfinished)
            {
                await Task.Delay(500);
            }
            downloadfinished = false;
            ExtractDXVK(gamedirectory.Text, dxvkconf);
        }
            private async void installdxvkbtn_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug(" User clicked on the Install DXVK button.");
            dxvkPanel.IsEnabled = false;
            installdxvkbtn.Content = "Installing...";
            int dgpu_dxvk_support = resultvk.Item1;
            int igpu_dxvk_support = resultvk.Item2;
            bool igpuonly = resultvk.Item3;
            bool dgpuonly = resultvk.Item4;
            bool inteligpu = resultvk.Item5;

            if (igpuonly && !dgpuonly)
            {
                switch (igpu_dxvk_support)
                {
                    case 1:
                        Logger.Debug(" User's PC only has an iGPU. Setting Install DXVK to 1.");
                        installdxvk = 1;
                        break;
                    case 2:
                        Logger.Debug(" User's PC only has an iGPU. Setting Install DXVK to 2.");
                        installdxvk = 2;
                        break;
                }
            }
            else if (!igpuonly && dgpuonly)
            {
                switch (dgpu_dxvk_support)
                {
                    case 1:
                        Logger.Debug(" User's PC only has a dGPU. Setting Install DXVK to 1.");
                        installdxvk = 1;
                        break;
                    case 2:
                        Logger.Debug(" User's PC only has a dGPU. Setting Install DXVK to 2.");
                        installdxvk = 2;
                        break;
                }
            }
            else if (!igpuonly && !dgpuonly)
            {
                Logger.Debug(" User's PC has both an iGPU and a dGPU. Doing further checks...");
                switch ((dgpu_dxvk_support, igpu_dxvk_support))
                {
                    case (0, 1):
                    case (0, 2):
                        Logger.Debug(" User PC's iGPU supports DXVK, but their dGPU does not - asking them what to do...");
                        var result = MessageBox.Show("Your iGPU supports DXVK but your GPU doesn't - do you still wish to install?", "Install DXVK?", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result == MessageBoxResult.Yes)
                        {
                            Logger.Debug(" User chose to install DXVK for the iGPU");
                            dxvkonigpu = true;
                            switch (igpu_dxvk_support)
                            {
                                case 1:
                                    Logger.Debug(" Setting Install DXVK to 1.");
                                    installdxvk = 1;
                                    break;
                                case 2:
                                    Logger.Debug(" Setting Install DXVK to 2.");
                                    installdxvk = 2;
                                    break;
                            }
                        }
                        else
                        {
                            Logger.Debug(" User chose not to install DXVK.");
                        }
                        break;
                    case (1, 2):
                        Logger.Debug(" User PC's iGPU supports DXVK, but their dGPU supports an inferior version - asking them what to do...");
                        var resultVer = MessageBox.Show("Your iGPU supports a greater version of DXVK than your GPU - which version do you wish to install?\n\nPress 'Yes' to install the version matching your GPU.\n\nPress 'No' to install the version matching your iGPU instead.", "Which DXVK version to install?", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (resultVer == MessageBoxResult.Yes)
                        {
                            Logger.Debug(" User chose to install DXVK for the dGPU. Setting Install DXVK to 1.");
                            installdxvk = 1;
                        }
                        else
                        {
                            Logger.Debug(" User chose to install DXVK for the iGPU. Setting Install DXVK to 2.");
                            dxvkonigpu = true;
                            installdxvk = 2;
                        }
                        break;
                    case (2, 2):
                    case (1, 1):
                    case (2, 1):
                    case (2, 0):
                        Logger.Debug(" User's GPU supports the same or a better version of DXVK as the iGPU.");
                        switch (dgpu_dxvk_support)
                        {
                            case 1:
                                Logger.Debug(" Setting Install DXVK to 1.");
                                installdxvk = 1;
                                break;
                            case 2:
                                Logger.Debug(" Setting Install DXVK to 2.");
                                installdxvk = 2;
                                break;
                        }
                        break;
                }
            }

            if (inteligpu && igpuonly)
            {
                Logger.Debug(" User's PC only has an Intel iGPU. Prompting them to install DXVK 1.10.1.");
                MessageBoxResult result = MessageBox.Show("Your PC only has an Intel iGPU on it. While it does support more modern versions on paper, it's reported that DXVK 1.10.1 might be your only supported version. Do you wish to install it?\n\nIf 'No' is selected, DXVK will be installed following the normal procedure.", "Message", MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                {
                    Logger.Debug(" Setting Install DXVK to 3 - a special case to install 1.10.1 for Intel iGPU's.");
                    installdxvk = 3;
                }
            }

            List<string> dxvkconf = new List<string> { };

            Logger.Debug(" Setting up dxvk.conf in accordance with user's choices.");

            if (vsynccheckbox.IsChecked == true)
            {
                Logger.Debug(" Adding d3d9.presentInterval = 1 and d3d9.numBackBuffers = 3");
                dxvkconf.Add("d3d9.presentInterval = 1");
                dxvkconf.Add("d3d9.numBackBuffers = 3");
            }
            if (framelatencycheckbox.IsChecked == true)
            {
                Logger.Debug(" Adding d3d9.maxFrameLatency = 1");
                dxvkconf.Add("d3d9.maxFrameLatency = 1");
            }

            Logger.Debug(" Quering links to install DXVK...");

            switch (installdxvk)
            {
                case 1:
                    /// we're using the "if" in each case because of the async checkbox
                    if (asynccheckbox.IsChecked == true)
                    {
                        Logger.Info(" Installing DXVK-async 1.10.3...");
                        dxvkconf.Add("dxvk.enableAsync = true");
                        downloaddxvk("https://api.github.com/repos/Sporif/dxvk-async/releases/assets/73567231", dxvkconf, false, true);
                        while (!extractfinished)
                        {
                            await Task.Delay(500);
                        }
                        extractfinished = false;
                        MessageBox.Show($"DXVK-async 1.10.3 has been installed!\n\nConsider going to Steam - Settings - Downloads and disable `Enable Shader Pre-caching` - this may improve your performance.");
                        Logger.Info(" DXVK-async 1.10.3 has been installed!");
                    }
                    else
                    {
                        Logger.Info(" Installing DXVK 1.10.3...");
                        downloaddxvk("https://api.github.com/repos/doitsujin/dxvk/releases/assets/73461736", dxvkconf, false, true);
                        while (!extractfinished)
                        {
                            await Task.Delay(500);
                        }
                        extractfinished = false;
                        MessageBox.Show($"DXVK 1.10.3 has been installed!\n\nConsider going to Steam - Settings - Downloads and disable `Enable Shader Pre-caching` - this may improve your performance.");
                        Logger.Info(" DXVK 1.10.3 has been installed!");
                    }
                    break;
                case 2:
                    if (asynccheckbox.IsChecked == true)
                    {
                        Logger.Info(" Installing Latest DXVK-gplasync...");
                        dxvkconf.Add("dxvk.enableAsync = true");
                        dxvkconf.Add("dxvk.gplAsyncCache = true");
                        downloaddxvk("https://gitlab.com/api/v4/projects/43488626/releases/", dxvkconf, true, false);
                        while (!extractfinished)
                        {
                            await Task.Delay(500);
                        }
                        extractfinished = false;
                        MessageBox.Show($"Latest DXVK-gplasync has been installed!\n\nConsider going to Steam - Settings - Downloads and disable `Enable Shader Pre-caching` - this may improve your performance.");
                        Logger.Info(" Latest DXVK-gplasync has been installed!");
                    }
                    else
                    {
                        Logger.Info(" Installing Latest DXVK...");
                        downloaddxvk("https://api.github.com/repos/doitsujin/dxvk/releases/latest", dxvkconf, false, false);
                        while (!extractfinished)
                        {
                            await Task.Delay(500);
                        }
                        extractfinished = false;
                        MessageBox.Show($"Latest DXVK has been installed!\n\nConsider going to Steam - Settings - Downloads and disable `Enable Shader Pre-caching` - this may improve your performance.");
                        Logger.Info(" Latest DXVK has been installed!");
                    }
                    break;
                case 3:
                    if (asynccheckbox.IsChecked == true)
                    {
                        Logger.Info(" Installing DXVK-async 1.10.1...");
                        dxvkconf.Add("dxvk.enableAsync = true");
                        downloaddxvk("https://api.github.com/repos/Sporif/dxvk-async/releases/assets/60677007", dxvkconf, false, true);
                        while (!extractfinished)
                        {
                            await Task.Delay(500);
                        }
                        extractfinished = false;
                        MessageBox.Show($"DXVK-async 1.10.1 has been installed!\n\nConsider going to Steam - Settings - Downloads and disable `Enable Shader Pre-caching` - this may improve your performance.");
                        Logger.Info(" DXVK-async 1.10.1 has been installed!");
                    }
                    else
                    {
                        Logger.Info(" Installing DXVK 1.10.1...");
                        downloaddxvk("https://api.github.com/repos/doitsujin/dxvk/releases/assets/60669426", dxvkconf, false, true);
                        while (!extractfinished)
                        {
                            await Task.Delay(500);
                        }
                        extractfinished = false;
                        MessageBox.Show($"DXVK 1.10.1 has been installed!\n\nConsider going to Steam - Settings - Downloads and disable `Enable Shader Pre-caching` - this may improve your performance.", "Information");
                        Logger.Info(" DXVK 1.10.1 has been installed!");
                    }
                    break;
            }
            Logger.Debug(" DXVK installed.");
            installdxvkbtn.Content = "Reinstall DXVK";
            dxvkPanel.IsEnabled = true;
        }
        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Logger.Debug(" User clicked on a hyperlink from the main window.");
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = $"/c start {e.Uri.AbsoluteUri}",
                CreateNoWindow = true,
                UseShellExecute = false,
            };
            Process.Start(psi);
        }
    }
}
