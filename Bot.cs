using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotNetEnv;
using DSharpPlus;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Logging;
namespace BotsController
{
    public class Bot
    {
        private const string Version = "1.0.0";

        private string Token = "";//BotsController
        private static DiscordClient _discordClient;
        private BackgroundWorker _backgroundWorker;
        private bool _guildDownloadCompleted = false;
        private BotAreaControlHandler _botAreaControlHandler;

        public async Task RunAsync()
        {
            // Load .env file
            Env.Load();

            // Read the JSON string from the environment variable
            Token = Environment.GetEnvironmentVariable("TOKEN");

            _discordClient = new DiscordClient(new DiscordConfiguration
            {
                Token = Token,
                TokenType = TokenType.Bot,
                AutoReconnect = true
            });

            await _discordClient.ConnectAsync();
            _discordClient.GuildDownloadCompleted += DiscordClient_GuildDownloadCompleted;
            _discordClient.ComponentInteractionCreated += DiscordClientOnComponentInteractionCreated;

            _backgroundWorker = new BackgroundWorker();
            _backgroundWorker.DoWork += BackgroundWorkerOnDoWork;
            StartBackgroundTask();

            await Task.Delay(-1);
        }
        private string[] values;
        private Task DiscordClientOnComponentInteractionCreated(DiscordClient sender, ComponentInteractionCreateEventArgs e)
        {
            _ = Task.Run(async () => {
                try
                {
                    _discordClient.Logger.LogInformation("Interaction triggered:");
                    _discordClient.Logger.LogInformation("Custom id: " + e.Interaction.Data.CustomId);
                    if (e.Interaction.Data.CustomId != Constants.ComponentCustomId.DropdownListBots)
                    {
                        await LogActionInThreadChannel(e);
                    }
                    switch (e.Interaction.Data.CustomId)
                    {
                        case Constants.ComponentCustomId.DropdownListBots:
                            values = e.Values;
                            if (e.Values.Length > 1)
                            {
                                _botAreaControlHandler.SelectedBot = null;
                            }
                            else if (e.Values.Length == 1)
                            {
                                _botAreaControlHandler.SelectedBot = e.Values[0];
                            }
                            else
                            {
                                _botAreaControlHandler.SelectedBot = string.Empty;
                            }
                            _discordClient.Logger.LogInformation("Selected bot: " + _botAreaControlHandler.SelectedBot);
                            await LogActionInThreadChannel(e);
                            break;
                        case Constants.ComponentCustomId.BtnRestart:
                            for (var i = 0; i < values.Length; i++)
                            {
                                _botAreaControlHandler.SelectedBot = values[i];
                                _botAreaControlHandler.RestartBot();
                                if (i + 1 == values.Length)
                                {
                                    Thread.Sleep(100);//required since process isn't instantly in the list of processes
                                }
                            }
                            _botAreaControlHandler.SelectedBot = string.Empty;
                            values = null;
                            break;
                        case Constants.ComponentCustomId.BtnLaunch:
                            for (var i = 0; i < values.Length; i++)
                            {
                                _botAreaControlHandler.SelectedBot = values[i];
                                _botAreaControlHandler.LaunchBot();
                                if (i + 1 == values.Length)
                                {
                                    Thread.Sleep(100);//required since process isn't instantly in the list of processes
                                }
                            }
                            _botAreaControlHandler.SelectedBot = string.Empty;
                            values = null;
                            break;
                        case Constants.ComponentCustomId.BtnDisconnect:
                            for (var i = 0; i < values.Length; i++)
                            {
                                _botAreaControlHandler.SelectedBot = values[i];
                                _botAreaControlHandler.DisconnectBot();
                                if (i + 1 == values.Length)
                                {
                                    Thread.Sleep(100);//required since process isn't instantly in the list of processes
                                }
                            }
                            _botAreaControlHandler.SelectedBot = string.Empty;
                            values = null;
                            break;
                    }
                    await _botAreaControlHandler.Update(e.Interaction);
                }
                catch (Exception ex)
                {
                    _discordClient.Logger.LogInformation("Interaction triggered errored: " + ex.Message + "\n\n" + ex.StackTrace);
                }
                _discordClient.Logger.LogInformation("Interaction finished!");
            });
            return Task.CompletedTask;
        }
        private async Task LogActionInThreadChannel(ComponentInteractionCreateEventArgs e)
        {
            var botsControllerChannel = _discordClient.Guilds[Constants.Guilds.SCOUTERS].Channels[Constants.Channels.BOT_CONTROL_AREA];
            if (botsControllerChannel.Threads.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                for (var i = 0; i < values?.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }
                    sb.Append("`" + values[i] + "`");
                }
                var extraString = "";
                // if (e.Interaction.Data.CustomId == Constants.ComponentCustomId.BtnLaunch && !_botAreaControlHandler.SelectedBotIsFromDiablo()){
                //     extraString = ":warning: **Launch beastbot manually!** :warning:";
                // }
                await _discordClient.SendMessageAsync(botsControllerChannel.Threads.FirstOrDefault(),
                ">>> [" + e.User.Username + "](https://" + e.User.Id + ".user.id) has triggered `" + e.Interaction.Data.CustomId + "` for bots: " + sb + " " + extraString);
            }
        }
        private async Task DiscordClient_GuildDownloadCompleted(DiscordClient sender, GuildDownloadCompletedEventArgs e)
        {
            _discordClient.Logger.Log(LogLevel.Information, "Client( v" + Version + " ) is ready to process events.");
            _guildDownloadCompleted = true;
            var guild = e.Guilds.Single(g => g.Key == Constants.Guilds.SCOUTERS);
            var bca = guild.Value.GetChannel(Constants.Channels.BOT_CONTROL_AREA);
            var lastMessage = await bca.GetMessagesAsync(1);
            if (lastMessage.Count > 0)
            {
                _botAreaControlHandler = new BotAreaControlHandler(lastMessage[0], _discordClient);
            }
            else
            {
                //create
                var message = await bca.SendMessageAsync("Placeholder for bot control interface");
                _botAreaControlHandler = new BotAreaControlHandler(message, _discordClient);
            }
        }
        private void BackgroundWorkerOnDoWork(object sender, DoWorkEventArgs e)
        {
            _ = Task.Run(async () => {
                var updatedOnce = false;
                var lastUpdateTime = DateTime.Now.AddDays(-1).AddMinutes(-4);
                try
                {
                    while (true)
                    {
                        if (_botAreaControlHandler != null && lastUpdateTime.Minute != DateTime.Now.Minute)
                        {
                            updatedOnce = true;
                            _discordClient.Logger.LogInformation("Updating control interface");
                            await _botAreaControlHandler.Update();
                            _discordClient.Logger.LogInformation("Done updating control interface");
                            lastUpdateTime = DateTime.Now;
                        }
                        else
                        {
                            Thread.Sleep(updatedOnce ? 10000 : 1000);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _discordClient.Logger.LogError("Exception in BackgroundWorkerOnDoWork:\n" + ex.Message + "\n\n" + ex.StackTrace);
                }
            });
        }
        private void StartBackgroundTask()
        {
            while (_backgroundWorker == null)
            {
                Thread.Sleep(100);
            }
            if (!_backgroundWorker.IsBusy)
            {
                _backgroundWorker.RunWorkerAsync();
            }
        }
    }
}
