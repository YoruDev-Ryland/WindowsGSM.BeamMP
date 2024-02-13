using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using Newtonsoft.Json.Linq;

namespace WindowsGSM.Plugins
{
    public class BeamMP
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.BeamMP",
            author = "YoruDev-Ryland",
            description = "WindowsGSM plugin for supporting BeamMP Server",
            version = "1.0",
            url = "https://github.com/YoruDev-Ryland/WindowsGSM.BeamMP",
            color = "#ffffff"
        };


        // - Standard Constructor and properties
        public BeamMP(ServerConfig serverData) => _serverData = serverData;
        private readonly ServerConfig _serverData;
        public string Error, Notice;


        // - Game server Fixed variables
        public string StartPath => "BeamMP-Server.exe"; // Game server start path
        public string FullName = "BeamMP Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new UT3(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()


        // - Game server default values
        public string Port = "30814"; // Default port
        public string QueryPort = "30814"; // Default query port
        public string Defaultmap = "/levels/west_coast_usa/info.json"; // Default map name
        public string Maxplayers = "12"; // Default maxplayers
        public string Additional = ""; // Additional server start parameter


        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            var configPath = ServerPath.GetServersServerFiles(_serverData.ServerID, "ServerConfig.toml");
            var sb = new StringBuilder();
            sb.AppendLine("# This is the BeamMP-Server config file.");
            sb.AppendLine("# Help & Documentation: `https://wiki.beammp.com/en/home/server-maintenance`");
            sb.AppendLine("# IMPORTANT: Fill in the AuthKey with the key you got from `https://keymaster.beammp.com/` on the left under \"Keys\"");
            sb.AppendLine("");
            sb.AppendLine("[General]");
            sb.AppendLine($"Name = \"{_serverData.ServerName}\" # DO NOT UPDATE FROM HERE. This is updated on server start from WindowsGSM => Config => Server Name page.");
            sb.AppendLine($"Port = {_serverData.ServerPort} # DO NOT UPDATE FROM HERE. This is updated on server start from WindowsGSM => Config => Server Port page.");
            sb.AppendLine("# AuthKey has to be filled out in order to run the server");
            sb.AppendLine($"AuthKey = \"{_serverData.ServerGSLT}\" # DO NOT UPDATE FROM HERE. This is updated on server start from WindowsGSM => Config => Server GSLT page.");
            sb.AppendLine("# Whether to log chat messages in the console / log");
            sb.AppendLine($"LogChat = true");
            sb.AppendLine("# Add custom identifying tags to your server to make it easier to find. Format should be TagA,TagB,TagC. Note the comma seperation.");
            sb.AppendLine($"Tags = \"Freeroam\"");
            sb.AppendLine($"Debug = false");
            sb.AppendLine($"Private = true");
            sb.AppendLine($"MaxCars = 1");
            sb.AppendLine($"MaxPlayers = {_serverData.ServerMaxPlayer} # DO NOT UPDATE FROM HERE. This is updated on server start from WindowsGSM => Config => Server Maxplayer page.");
            sb.AppendLine($"Map = \"{_serverData.ServerMap}\" # DO NOT UPDATE FROM HERE. This is updated on server start from WindowsGSM => Config => Server Start Map page.");
            sb.AppendLine($"Description = \"WindowsGSM BeamMP Server\"");
            sb.AppendLine($"ResourceFolder = \"Resources\"");
            sb.AppendLine("");
            sb.AppendLine("[Misc]");
            sb.AppendLine("# Hides the periodic update message which notifies you of a new server version. You should really keep this on and always update as soon as possible. For more information visit https://wiki.beammp.com/en/home/server-maintenance#updating-the-server. An update message will always appear at startup regardless.");
            sb.AppendLine("ImScaredOfUpdates = false");
            sb.AppendLine("# If SendErrors is `true`, the server will send helpful info about crashes and other issues back to the BeamMP developers. This info may include your config, who is on your server at the time of the error, and similar general information. This kind of data is vital in helping us diagnose and fix issues faster. This has no impact on server performance. You can opt-out of this system by setting this to `false`");
            sb.AppendLine("SendErrorsShowMessage = true");
            sb.AppendLine("# You can turn on/off the SendErrors message you get on startup here");
            sb.AppendLine("SendErrors = true");
            File.WriteAllText(configPath, sb.ToString());
        }


        public void UpdateServerCFG()
        {
            var configPath = ServerPath.GetServersServerFiles(_serverData.ServerID, "ServerConfig.toml");

            // Read the existing content of the config file
            var configContent = File.ReadAllText(configPath);

            // Update the values directly by replacing the lines.
            configContent = Regex.Replace(configContent, @"(?<=Name = "").*?(?="")", _serverData.ServerName);
            configContent = Regex.Replace(configContent, @"(?<=Port = ).*?(?=\n)", _serverData.ServerPort.ToString());
            configContent = Regex.Replace(configContent, @"(?<=AuthKey = "").*?(?="")", _serverData.ServerGSLT);
            configContent = Regex.Replace(configContent, @"(?<=MaxPlayers = ).*?(?=\n)", _serverData.ServerMaxPlayer.ToString());
            configContent = Regex.Replace(configContent, @"(?<=Map = "").*?(?="")", _serverData.ServerMap);

            // Write the updated content back to the config file
            File.WriteAllText(configPath, configContent);
        }


        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            // Get the working directory path
            string workingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID);

            // Prepare Process
            var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(workingDirectory, StartPath),
                    WorkingDirectory = workingDirectory, // Set the working directory
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false // Important for redirecting I/O if needed
                },
                EnableRaisingEvents = true
            };

            UpdateServerCFG();

            // Start Process
            try
            {
                p.Start();
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null; // return null if fail to start
            }

            return p;
        }


        // - Stop server function
        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                // Send "stop" command to game server process MainWindow
                ServerConsole.SendMessageToMainWindow(p.MainWindowHandle, "exit");
            });
        }


        // - Install server function
        public async Task<Process> Install()
        {
            // Define the URL for the GitHub API release
            string githubApiUrl = "https://api.github.com/repos/BeamMP/BeamMP-Server/releases/latest";

            // Define the path where to save the BeamMP-Server.exe
            string serverFilesPath = ServerPath.GetServersServerFiles(_serverData.ServerID);
            string beammpServerPath = Path.Combine(serverFilesPath, "BeamMP-Server.exe");

            // Use WebClient to fetch the latest release data from GitHub API
            using (var webClient = new WebClient())
            {
                // Set the user-agent
                webClient.Headers.Add("User-Agent", "WindowsGSM");

                try
                {
                    // Fetch the latest release data
                    string latestReleaseData = await webClient.DownloadStringTaskAsync(githubApiUrl);

                    // Parse the fetched JSON data to get the tag name (version)
                    JObject json = JObject.Parse(latestReleaseData);
                    string latestVersion = json["tag_name"].ToString();
                    string downloadUrl = "https://github.com/BeamMP/BeamMP-Server/releases/latest/download/BeamMP-Server.exe";

                    // Download the server executable
                    await webClient.DownloadFileTaskAsync(new Uri(downloadUrl), beammpServerPath);

                    // Log the version to a file
                    string logPath = Path.Combine(serverFilesPath, "version.log");
                    File.WriteAllText(logPath, $"Current BeamMP-Server Version: {latestVersion}");

                    // Call the CreateServerCFG() method to create or ensure the ServerConfig.toml is set up correctly
                    CreateServerCFG();
                }
                catch (Exception e)
                {
                    Error = $"Failed during the installation process: {e.Message}";
                    return null;
                }
            }

            return null;
        }


        // - Update server function
        public async Task<Process> Update()
        {
            // Get the local and remote versions
            string localVersion = GetLocalBuild();
            string remoteVersion = await GetRemoteBuild();

            // Compare the local version with the remote version
            if (!string.IsNullOrEmpty(localVersion) && !string.IsNullOrEmpty(remoteVersion) && localVersion != remoteVersion)
            {
                // Define the path where to save the BeamMP-Server.exe
                string serverFilesPath = ServerPath.GetServersServerFiles(_serverData.ServerID);
                string beammpServerPath = Path.Combine(serverFilesPath, "BeamMP-Server.exe");

                // Use WebClient to download the BeamMP-Server.exe
                using (var webClient = new WebClient())
                {
                    try
                    {
                        // Download the file from the remote version URL
                        string downloadUrl = $"https://github.com/BeamMP/BeamMP-Server/releases/latest/download/BeamMP-Server.exe";
                        await webClient.DownloadFileTaskAsync(new Uri(downloadUrl), beammpServerPath);

                        // Log the version to a file
                        string logPath = Path.Combine(serverFilesPath, "version.log");
                        File.WriteAllText(logPath, $"Current BeamMP-Server Version: {remoteVersion}");
                    }
                    catch (Exception e)
                    {
                        Error = $"Failed to download or update to the latest BeamMP-Server.exe: {e.Message}";
                        return null;
                    }
                }

                Notice = "Server updated successfully.";
            }
            else if (localVersion == remoteVersion)
            {
                Notice = "No update needed. The server is up-to-date.";
            }
            else
            {
                Error = "Failed to get local or remote version for update comparison.";
            }

            return null;
        }


        // - Check if the installation is successful
        public bool IsInstallValid()
        {
            // Get the paths to the server executable and the configuration file
            string serverExePath = ServerPath.GetServersServerFiles(_serverData.ServerID, "BeamMP-Server.exe");
            string serverConfigPath = ServerPath.GetServersServerFiles(_serverData.ServerID, "ServerConfig.toml");

            // Check if both the executable and the config file exist
            bool exeExists = File.Exists(serverExePath);
            bool configExists = File.Exists(serverConfigPath);

            // If both exist, return true, indicating a valid installation
            return exeExists && configExists;
        }


        // - Check if the directory contains BeamMP-Server.exe and ServerConfig.toml for import
        public bool IsImportValid(string path)
        {
            // Define the expected file names
            string serverExe = "BeamMP-Server.exe";
            string serverConfig = "ServerConfig.toml";

            // Build the full paths to the expected files
            string serverExePath = Path.Combine(path, serverExe);
            string serverConfigPath = Path.Combine(path, serverConfig);

            // Check if both the executable and the config file exist at the given path
            bool exeExists = File.Exists(serverExePath);
            bool configExists = File.Exists(serverConfigPath);

            // Set the error message if one of the files doesn't exist
            if (!exeExists || !configExists)
            {
                Error = $"Invalid Path! Fail to find {(exeExists ? serverConfig : serverExe)}";
            }

            // Return true if both files exist, indicating a valid import
            return exeExists && configExists;
        }


        public string GetLocalBuild()
        {
            // Define the path to the version log file
            string logPath = Path.Combine(ServerPath.GetServersServerFiles(_serverData.ServerID), "version.log");

            // Check if the version log file exists
            if (File.Exists(logPath))
            {
                // Read the version from the file
                try
                {
                    string versionLogContents = File.ReadAllText(logPath);
                    string version = versionLogContents.Replace("Current BeamMP-Server Version: ", "").Trim();
                    return version;
                }
                catch (Exception e)
                {
                    Error = $"Failed to read local version: {e.Message}";
                    return "";
                }
            }
            else
            {
                Error = "Local version file does not exist";
                return "";
            }
        }


        public async Task<string> GetRemoteBuild()
        {
            // Define the URL for the GitHub API release
            string githubApiUrl = "https://api.github.com/repos/BeamMP/BeamMP-Server/releases/latest";

            // Define the user-agent string, as GitHub requires it for API requests
            string userAgent = "WindowsGSM";

            // Use WebClient to fetch the latest release data from GitHub API
            using (var webClient = new WebClient())
            {
                // Set the user-agent
                webClient.Headers.Add("User-Agent", userAgent);

                try
                {
                    // Fetch the latest release data
                    string latestReleaseData = await webClient.DownloadStringTaskAsync(githubApiUrl);

                    // Parse the fetched JSON data to get the tag name (version)
                    JObject json = JObject.Parse(latestReleaseData);
                    string latestVersion = json["tag_name"].ToString();

                    return latestVersion;
                }
                catch (Exception e)
                {
                    Error = $"Failed to get remote version: {e.Message}";
                    return "";
                }
            }
        }
    }
}
