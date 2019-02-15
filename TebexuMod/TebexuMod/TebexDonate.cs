using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

using uMod;
using uMod.Libraries;
using uMod.Libraries.Universal;

namespace uMod.Plugins
{
    [Info("Tebex Donate", "Tebex", "1.0.0")]
    [Description("Official Plugin for the Tebex Server Monetization Platform.")]
    public class TebexDonate : UniversalPlugin
    {

        public int nextCheck = 15 * 60;
        public TebexDonateUtils.WebstoreInfo information;
        private DateTime _lastCalled = DateTime.Now.AddMinutes(-14);

        public static TebexDonate tebexDonate;

        public static Timer timerRef;


        private String secret = "";
        private String baseUrl = "https://plugin.buycraft.net/";
        private bool buyEnabled = true;

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new configuration file!");
            if (Config["secret"] == null) Config["secret"] = secret;
            if (Config["baseUrl"] == null) Config["baseUrl"] = baseUrl;
            if (Config["buyEnabled"] == null) Config["buyEnabled"] = true;
            if (Config["pushCommands"] == null) Config["pushCommands"] = true;
            if (Config["pushCommandsPort"] == null) Config["pushCommandPort"] = "3000";

            //SaveConfig();            
        }

        void OnServerInitialized()
        {
            this.information = new TebexDonateUtils.WebstoreInfo();
            tebexDonate = this;
            if (string.IsNullOrEmpty((string) tebexDonate.Config["secret"]))
            {
                Puts("You have not yet defined your secret key. Use 'tebex:secret <secret>' to define your key");
            }
            else
            {
                cmdInfo(null, "tebex:info", null);
            }

            timerRef = this.timer.Every(60, () => { this.checkQueue(); });

        }

        public void LogInfo(String msg)
        {
            Puts(msg);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["WebstoreUrl"] = "To buy packages from our webstore, please visit {webstoreUrl}.",
            }, this);
        }

        private void Unload()
        {
            timerRef.Destroy();
        }

        private void checkQueue()
        {
            if ((DateTime.Now - this._lastCalled).TotalSeconds > this.nextCheck)
            {
                this._lastCalled = DateTime.Now;
                //Do Command Check             
                cmdForcecheck(null, "tebex:forcecheck", null);
            }
        }

        public void UpdateConfig()
        {
            this.SaveConfig();
        }

        public IPlayer getPlayerById(String id)
        {
            return players.FindPlayerById(id);
        }

        public bool isPlayerOnline(IPlayer player)
        {
            return player.IsConnected;
        }

        public void runCommand(string cmd)
        {
            server.Command(cmd);
        }

        [Command("tebex:info")]
        void cmdInfo(IPlayer player, String cmd, String[] args)
        {
            if (player == null || player.IsAdmin)
            {
                TebexDonateUtils.CommandTebexInfo cmdCommandTebexInfo = new TebexDonateUtils.CommandTebexInfo();
                cmdCommandTebexInfo.Execute(player, cmd, args);
            }
        }

        [Command("tebex:secret")]
        void cmdSecret(IPlayer player, String cmd, String[] args)
        {
            if (player.IsServer)
            {
                TebexDonateUtils.CommandTebexSecret cmdCommandTebexSecret = new TebexDonateUtils.CommandTebexSecret();
                cmdCommandTebexSecret.Execute(player, cmd, args);
            }
        }

        [Command("tebex:forcecheck")]
        void cmdForcecheck(IPlayer player, String cmd, String[] args)
        {
            if (player == null || player.IsAdmin)
            {
                TebexDonateUtils.CommandTebexForcecheck cmdCommandTebexForcecheck = new TebexDonateUtils.CommandTebexForcecheck();
                cmdCommandTebexForcecheck.Execute(player, cmd, args);
            }
        }

        [Command("buy")]
        void cmdBuy(IPlayer player, String cmd, String[] args)
        {
            player.Message(lang.GetMessage("WebstoreUrl", this, player.Id)
                .Replace("{webstoreUrl}", this.information.domain));
        }

    }

    namespace TebexDonateUtils
    {


        public interface ITebexCommand
        {
            void Execute(IPlayer player, String cmd, String[] args);

            void HandleResponse(JObject response);

            void HandleError(Exception e);

        }

        public class CommandTebexSecret : ITebexCommand
        {

            public void Execute(IPlayer player, String cmd, String[] args)
            {

                String secret = args[0];

                TebexDonate.tebexDonate.Config["secret"] = secret;
                TebexDonate.tebexDonate.UpdateConfig();
                // Set a custom timeout (in milliseconds)
                var timeout = 2000f;

                // Set some custom request headers (eg. for HTTP Basic Auth)
                var headers = new Dictionary<string, string>
                    {{"X-Buycraft-Secret", (string) TebexDonate.tebexDonate.Config["secret"]}};
                WebRequests webrequest = new WebRequests();
                webrequest.Enqueue((string) TebexDonate.tebexDonate.Config["baseUrl"] + "information", null,
                    (code, response) =>
                    {
                        if (response == null || code != 200)
                        {
                            HandleError(new Exception("Error: code" + code.ToString()));
                            webrequest.Shutdown();
                            return;
                        }

                        HandleResponse(JObject.Parse(response));
                        webrequest.Shutdown();

                    }, TebexDonate.tebexDonate, RequestMethod.GET, headers, timeout);

            }

            public void HandleResponse(JObject response)
            {
                TebexDonate.tebexDonate.information.id = (int) response["account"]["id"];
                TebexDonate.tebexDonate.information.domain = (string) response["account"]["domain"];
                TebexDonate.tebexDonate.information.gameType = (string) response["account"]["game_type"];
                TebexDonate.tebexDonate.information.name = (string) response["account"]["name"];
                TebexDonate.tebexDonate.information.currency =
                    (string) response["account"]["currency"]["iso_4217"];
                TebexDonate.tebexDonate.information.currencySymbol =
                    (string) response["account"]["currency"]["symbol"];
                TebexDonate.tebexDonate.information.serverId = (int) response["server"]["id"];
                TebexDonate.tebexDonate.information.serverName = (string) response["server"]["name"];

                TebexDonate.tebexDonate.LogInfo("Your secret key has been validated! Webstore Name: " +
                                                TebexDonate.tebexDonate.information.name);
            }

            public void HandleError(Exception e)
            {
                TebexDonate.tebexDonate.LogInfo("We were unable to validate your secret key.");
            }
        }

        public class CommandTebexInfo : ITebexCommand
        {
            public void Execute(IPlayer player, String cmd, String[] args)
            {
                // Set a custom timeout (in milliseconds)
                var timeout = 2000f;

                // Set some custom request headers (eg. for HTTP Basic Auth)
                var headers = new Dictionary<string, string>
                    {{"X-Buycraft-Secret", (string) TebexDonate.tebexDonate.Config["secret"]}};

                WebRequests webrequest = new WebRequests();
                webrequest.Enqueue((string) TebexDonate.tebexDonate.Config["baseUrl"] + "information", null,
                    (code, response) =>
                    {
                        if (response == null || code != 200)
                        {
                            HandleError(new Exception("Error"));
                            webrequest.Shutdown();
                            return;
                        }

                        HandleResponse(JObject.Parse(response));
                        webrequest.Shutdown();

                    }, TebexDonate.tebexDonate, RequestMethod.GET, headers, timeout);

            }

            public void HandleResponse(JObject response)
            {
                TebexDonate.tebexDonate.information.id = (int) response["account"]["id"];
                TebexDonate.tebexDonate.information.domain = (string) response["account"]["domain"];
                TebexDonate.tebexDonate.information.gameType = (string) response["account"]["game_type"];
                TebexDonate.tebexDonate.information.name = (string) response["account"]["name"];
                TebexDonate.tebexDonate.information.currency =
                    (string) response["account"]["currency"]["iso_4217"];
                TebexDonate.tebexDonate.information.currencySymbol =
                    (string) response["account"]["currency"]["symbol"];
                TebexDonate.tebexDonate.information.serverId = (int) response["server"]["id"];
                TebexDonate.tebexDonate.information.serverName = (string) response["server"]["name"];

                TebexDonate.tebexDonate.LogInfo("Server Information");
                TebexDonate.tebexDonate.LogInfo("=================");
                TebexDonate.tebexDonate.LogInfo("Server " + TebexDonate.tebexDonate.information.serverName +
                                                " for webstore " + TebexDonate.tebexDonate.information.name + "");
                TebexDonate.tebexDonate.LogInfo("Server prices are in " + TebexDonate.tebexDonate.information.currency +
                                                "");
                TebexDonate.tebexDonate.LogInfo("Webstore domain: " + TebexDonate.tebexDonate.information.domain + "");
            }

            public void HandleError(Exception e)
            {
                TebexDonate.tebexDonate.LogInfo(
                    "We are unable to fetch your server details. Please check your secret key.");
            }
        }

        public class CommandTebexForcecheck : ITebexCommand
        {

            public void Execute(IPlayer player, String cmd, String[] args)
            {
                TebexDonate.tebexDonate.LogInfo("Checking for commands to be executed...");
                // Set a custom timeout (in milliseconds)
                var timeout = 2000f;

                // Set some custom request headers (eg. for HTTP Basic Auth)
                var headers = new Dictionary<string, string>
                    {{"X-Buycraft-Secret", (string) TebexDonate.tebexDonate.Config["secret"]}};

                WebRequests webrequest = new WebRequests();
                webrequest.Enqueue((string) TebexDonate.tebexDonate.Config["baseUrl"] + "queue", null,
                    (code, response) =>
                    {
                        if (response == null || code != 200)
                        {
                            HandleError(new Exception("Error"));
                            webrequest.Shutdown();
                            return;
                        }

                        HandleResponse(JObject.Parse(response));
                        webrequest.Shutdown();

                    }, TebexDonate.tebexDonate, RequestMethod.GET, headers, timeout);

            }

            public void HandleResponse(JObject response)
            {
                if ((int) response["meta"]["next_check"] > 0)
                {
                    TebexDonate.tebexDonate.nextCheck = (int) response["meta"]["next_check"];
                }

                if ((bool) response["meta"]["execute_offline"])
                {
                    try
                    {
                        TebexCommandRunner.doOfflineCommands();
                    }
                    catch (Exception e)
                    {
                        TebexDonate.tebexDonate.LogInfo(e.ToString());
                    }
                }

                JArray players = (JArray) response["players"];

                foreach (var player in players)
                {
                    try
                    {
                        IPlayer targetPlayer = TebexDonate.tebexDonate.getPlayerById((string) player["uuid"]);

                        if (targetPlayer != null && TebexDonate.tebexDonate.isPlayerOnline(targetPlayer))
                        {
                            TebexDonate.tebexDonate.LogInfo("Execute commands for " + targetPlayer.Name + "(ID: " +
                                                            targetPlayer.Id + ")");
                            TebexCommandRunner.doOnlineCommands((int) player["id"], (string) targetPlayer.Name,
                                targetPlayer.Id);
                        }
                    }
                    catch (Exception e)
                    {
                        TebexDonate.tebexDonate.LogInfo(e.Message);
                    }
                }
            }

            public void HandleError(Exception e)
            {
                TebexDonate.tebexDonate.LogInfo(
                    "We are unable to fetch your server queue. Please check your secret key.");
                TebexDonate.tebexDonate.LogInfo(e.ToString());
            }
        }

        public class TebexCommandRunner
        {

            public static int deleteAfter = 3;

            public static void doOfflineCommands()
            {

                String url = TebexDonate.tebexDonate.Config["baseUrl"] + "queue/offline-commands";

                // Set a custom timeout (in milliseconds)
                var timeout = 2000f;

                // Set some custom request headers (eg. for HTTP Basic Auth)
                var headers = new Dictionary<string, string>
                    {{"X-Buycraft-Secret", (string) TebexDonate.tebexDonate.Config["secret"]}};

                WebRequests webrequest = new WebRequests();
                webrequest.Enqueue(url, null, (code, response) =>
                {
                    JObject json = JObject.Parse(response);
                    JArray commands = (JArray) json["commands"];

                    int exCount = 0;
                    List<int> executedCommands = new List<int>();

                    foreach (var command in commands.Children())
                    {
                        String commandToRun = buildCommand((string) command["command"],
                            (string) command["player"]["name"],
                            (string) command["player"]["uuid"]);

                        TebexDonate.tebexDonate.LogInfo("Run command " + commandToRun);
                        TebexDonate.tebexDonate.runCommand(commandToRun);
                        executedCommands.Add((int) command["id"]);

                        exCount++;

                        if (exCount % deleteAfter == 0)
                        {
                            try
                            {
                                deleteCommands(executedCommands);
                                executedCommands.Clear();
                            }
                            catch (Exception ex)
                            {
                                TebexDonate.tebexDonate.LogInfo(ex.ToString());
                            }
                        }

                    }

                    TebexDonate.tebexDonate.LogInfo(exCount.ToString() + " offline commands executed");
                    if (exCount % deleteAfter != 0)
                    {
                        try
                        {
                            deleteCommands(executedCommands);
                            executedCommands.Clear();
                        }
                        catch (Exception ex)
                        {
                            TebexDonate.tebexDonate.LogInfo(ex.ToString());
                        }
                    }

                    webrequest.Shutdown();
                }, TebexDonate.tebexDonate, RequestMethod.GET, headers, timeout);

            }

            public static void doOnlineCommands(int playerPluginId, string playerName, string playerId)
            {

                TebexDonate.tebexDonate.LogInfo("Running online commands for " + playerName + " (" + playerId + ")");


                String url = TebexDonate.tebexDonate.Config["baseUrl"] + "queue/online-commands/" +
                             playerPluginId.ToString();

                // Set a custom timeout (in milliseconds)
                var timeout = 2000f;

                // Set some custom request headers (eg. for HTTP Basic Auth)
                var headers = new Dictionary<string, string>
                    {{"X-Buycraft-Secret", (string) TebexDonate.tebexDonate.Config["secret"]}};


                WebRequests webrequest = new WebRequests();
                webrequest.Enqueue(url, null, (code, response) =>
                {
                    JObject json = JObject.Parse(response);
                    JArray commands = (JArray) json["commands"];

                    int exCount = 0;
                    List<int> executedCommands = new List<int>();

                    foreach (var command in commands.Children())
                    {

                        String commandToRun = buildCommand((string) command["command"], playerName, playerId);

                        TebexDonate.tebexDonate.LogInfo("Run command " + commandToRun);

                        TebexDonate.tebexDonate.runCommand(commandToRun);
                        executedCommands.Add((int) command["id"]);

                        exCount++;

                        if (exCount % deleteAfter == 0)
                        {
                            try
                            {
                                deleteCommands(executedCommands);
                                executedCommands.Clear();
                            }
                            catch (Exception ex)
                            {
                                TebexDonate.tebexDonate.LogInfo(ex.ToString());
                            }
                        }


                    }

                    TebexDonate.tebexDonate.LogInfo(exCount.ToString() + " online commands executed for " + playerName);
                    if (exCount % deleteAfter != 0)
                    {
                        try
                        {
                            deleteCommands(executedCommands);
                            executedCommands.Clear();
                        }
                        catch (Exception ex)
                        {
                            TebexDonate.tebexDonate.LogInfo(ex.ToString());
                        }
                    }

                    webrequest.Shutdown();

                }, TebexDonate.tebexDonate, RequestMethod.GET, headers, timeout);
            }

            public static void deleteCommands(List<int> commandIds)
            {

                String url = TebexDonate.tebexDonate.Config["baseUrl"] + "queue?";
                String amp = "";

                foreach (int CommandId in commandIds)
                {
                    url = url + amp + "ids[]=" + CommandId;
                    amp = "&";
                }

                // Set a custom timeout (in milliseconds)
                var timeout = 2000f;

                // Set some custom request headers (eg. for HTTP Basic Auth)
                var headers = new Dictionary<string, string>
                    {{"X-Buycraft-Secret", (string) TebexDonate.tebexDonate.Config["secret"]}};

                WebRequests webrequest = new WebRequests();
                webrequest.Enqueue(url, "", (code, response) => { webrequest.Shutdown(); },
                    TebexDonate.tebexDonate, RequestMethod.DELETE, headers, timeout);
            }

            public static string buildCommand(string command, string username, string id)
            {
                return command.Replace("{id}", id).Replace("{username}", username);
            }
        }

        public class WebstoreInfo
        {
            public int id;
            public string name;
            public string domain;
            public string currency;
            public string currencySymbol;
            public string gameType;
            public string serverName;
            public int serverId;
        }
    }
}