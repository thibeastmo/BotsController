using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
namespace BotsController
{
    public class BotAreaControlHandler
    {
        public string SelectedBot { get; set; } = string.Empty;

        private DiscordClient _discordClient;
        private DiscordMessage _message;
        private const string HomeDir = "/home/alex/";
        private const string BeastBotsDir = "Beast-Bots/";
        private const string DiabloBotsDir = "Bot-Galactic-Swamp/";
        private const string RetrieveDiabloProcessesCmd = "ps -ef | grep '/home/alex/Bot-Galactic-Swamp' | tr -s ' ' | cut -d ' ' -f 2,9 | grep -v '.pyenv\\|grep\\|-c\\|/bin/bash'";
        // private const string RetrieveDiabloProcessesCmd = "ps -ef | grep '" + HomeDir + ".pyenv/versions/3.11.0/bin/python3.11' | tr -s ' ' | cut -d ' ' -f 2,9 | grep -v '.pyenv\\|grep\\|-c\\|/bin/bash'";
        // private const string RetrieveBeastProcessCmd = "ps -ef | grep 'x' | tr -s ' ' | cut -d ' ' -f 2,10";
        private const string RetrieveBeastProcessCmd = "ps -ef | grep 'x' | tr -s ' '";
        private const string RetrieveDiabloProcessCmd = "ps -ef | grep 'x' | tr -s ' ' | cut -d ' ' -f 2,9 | grep -v '.pyenv\\|grep\\|-c\\|/bin/bash'";
        private const string RetrieveDiabloBotsCmd = "ls " + HomeDir + DiabloBotsDir + " | grep 'Bot-'"; //ls /home/alex/Bot-Galactic-Swamp/ | grep 'Bot-'
        private const string RetrieveBeastBotsCmd = "ls " + HomeDir + BeastBotsDir + " | grep 'Bot-'"; // ls /home/alex/Beast-Bots/ | grep 'Bot-'
        private const string RetrieveMainsPy = "ls x | grep 'main.*\\.py'";
        private const string RetrieveMainPy = "ls x1 | grep 'main.*x2\\.py'";
        private const string RetrieveAllFilesWithoutExtensionCmd = "find x -type f ! -name '*.*' | grep -v 'createdump'";
        private const string KillProcessByIdCmd = "kill -9 x";
        private const string LaunchBotDiablo = "nohup python3.11 'x' &";
        private const string LaunchBotBeast = "nohup 'x' &";
        private bool _showOutput = false;
        private bool _showCommand = false;
        public BotAreaControlHandler(DiscordMessage discordMessage, DiscordClient discordClient)
        {
            _message = discordMessage;
            _discordClient = discordClient;
        }

        public async Task Update(DiscordInteraction discordInteraction = null)
        {
            var diabloBots = GetDiabloBots(includeFoldersIfMultipleMains: false);
            var beastBots = GetBeastBots();
            var allBots = CombineArrays(diabloBots, beastBots);
            var diabloProcesses = GetDiabloProcesses();
            var beastProcesses = GetBeastProcesses();
            var allProcesses = CombineArrays(diabloProcesses, beastProcesses);
            var filterings = GetFiltering(allBots);

            var deb = new DiscordEmbedBuilder();
            deb.Title = "Bots dashboard";
            deb.Footer = new DiscordEmbedBuilder.EmbedFooter();
            deb.Footer.Text = "Updated at " + DateTime.Now;
            var fieldValues = GenerateFilteredFieldValues(filterings, allBots, allProcesses);
            for (var i = 0; i < fieldValues.Length; i++)
            {
                deb.AddField(filterings[i], fieldValues[i], true);
            }
            foreach (var field in deb.Fields)
            {
                field.Value = field.Value.Replace("Bot-", string.Empty);
            }

            var optionList = new List<DiscordSelectComponentOption>();
            var buttonList = new DiscordComponent[]
            {
                new DiscordButtonComponent(ButtonStyle.Danger, Constants.ComponentCustomId.BtnDisconnect, Constants.ComponentCustomId.BtnDisconnect.Substring(Constants.Prefixes.BTN.Length, Constants.ComponentCustomId.BtnDisconnect.Length - Constants.Prefixes.BTN.Length).Replace(Constants.Prefixes.BTN, string.Empty), SelectedBot != null && SelectedBot.Length > 0 && allBots.Contains(SelectedBot) && !allProcesses.Contains(SelectedBot)),
                new DiscordButtonComponent(ButtonStyle.Primary, Constants.ComponentCustomId.BtnRestart, Constants.ComponentCustomId.BtnRestart.Substring(Constants.Prefixes.BTN.Length, Constants.ComponentCustomId.BtnRestart.Length - Constants.Prefixes.BTN.Length).Replace(Constants.Prefixes.BTN, string.Empty), SelectedBot != null && SelectedBot.Length > 0 && allBots.Contains(SelectedBot) && !allProcesses.Contains(SelectedBot)),
                new DiscordButtonComponent(ButtonStyle.Success, Constants.ComponentCustomId.BtnLaunch, Constants.ComponentCustomId.BtnLaunch.Substring(Constants.Prefixes.BTN.Length, Constants.ComponentCustomId.BtnLaunch.Length - Constants.Prefixes.BTN.Length).Replace(Constants.Prefixes.BTN, string.Empty), SelectedBot != null && SelectedBot.Length > 0 && allBots.Contains(SelectedBot) && allProcesses.Contains(SelectedBot)),
            };
            allBots = CombineArrays(GetDiabloBots(), beastBots);
            foreach (var bot in allBots)
            {
                optionList.Add(new DiscordSelectComponentOption(bot.Replace("Bot-", string.Empty), bot, emoji: GetEmojiForBot(bot), isDefault: !string.IsNullOrEmpty(SelectedBot) && bot == SelectedBot));
            }
            if (SelectedBot != null && SelectedBot.Length == 0)
            {
                foreach (var component in buttonList)
                {
                    ((DiscordButtonComponent)component).Disable();
                }
            }
            var dropDown = new DiscordSelectComponent(Constants.ComponentCustomId.DropdownListBots, "Select bot", optionList, minOptions: 0, maxOptions: optionList.Count);
            var dmb = new DiscordMessageBuilder()
                .AddEmbed(deb)
                .AddComponents(dropDown)
                .AddComponents(buttonList);
            if (discordInteraction == null)
            {
                await _message.ModifyAsync(dmb);
            }
            else
            {
                await discordInteraction.CreateResponseAsync(
                InteractionResponseType.UpdateMessage,
                new DiscordInteractionResponseBuilder(dmb));
            }
        }

        private string ExecuteCommand(string command, bool outputExpected = true)
        {
            command = command.Replace("\n", string.Empty);
            if (_showCommand)
            {
                _discordClient.Logger.LogInformation("command:\n" + command);
            }
            var process = new Process();

            process.StartInfo.FileName = "/bin/bash";// Specify the path to the bash executable
            process.StartInfo.Arguments = $"-c \"{command}\"";// Pass the command as an argument to bash

            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.OutputDataReceived += (sender, e) => Console.WriteLine(e.Data);
            process.ErrorDataReceived += (sender, e) => Console.WriteLine(e.Data);

            process.Start();
            var output = string.Empty;
            var error = string.Empty;
            if (outputExpected)
            {
                output = process.StandardOutput.ReadToEnd();
                error = process.StandardOutput.ReadToEnd();
            }
            process.Close();

            if (_showOutput)
            {
                _discordClient.Logger.LogInformation("output:\n" + output);
                _discordClient.Logger.LogInformation("error:\n" + error);
            }
            return output;
        }

        public void DisconnectBot()
        {
            _discordClient.Logger.LogInformation("Disconnecting bot: " + SelectedBot);
            if (SelectedBotIsFromDiablo())
            {
                var splitted = SelectedBot.Split('/');
                splitted[0] = HomeDir + DiabloBotsDir + splitted[0];
                if (splitted.Length > 1)
                {
                    KillProcess(splitted[0] + "/main_" + splitted[1] + ".py");
                }
                else
                {
                    var mains = GetAllMains(splitted[0]);
                    foreach (var main in mains)
                    {
                        KillProcess(splitted[0]);
                    }
                }
            }
            else
            {
                var name = SelectedBot.Split('-')[2].ToLower() + "bot";
                KillProcess(name, isDiabloBot: false);
            }

            void KillProcess(string processName, bool isDiabloBot = true)
            {
                var ids = "";
                if (isDiabloBot)
                {
                    ids = ExecuteCommand(RetrieveDiabloProcessCmd.Replace("x", processName)).Split(' ')[0];
                }
                else
                {
                    var actualProcessName = GetBeastExecutable(processName);
                    var cmd = RetrieveBeastProcessCmd.Replace("x", actualProcessName);
                    var output = ExecuteCommand(cmd);
                    ids = output.Split(' ')[1];
                }
                var lines = SplitLinesAndRemoveEmptyStrings(ids);
                foreach (var line in lines)
                {
                    ExecuteCommand(KillProcessByIdCmd.Replace("x", line));
                }
            }
        }

        private string GetActualProcessName(string processName)
        {
            var actualProcessName = "";
            if (processName.ToLower().Contains("tba"))
            {
                actualProcessName = "TrueBloodAlly3";
            }
            else
            {
                actualProcessName = "TrueBloodAlly3Bot";
            }
            return actualProcessName;
        }

        private string GetBeastExecutable(string partialBeastBotName)
        {
            if (partialBeastBotName.ToLower().Contains("tba"))
            {
                return "TrueBloodAlly3Bot";
            }
            if (partialBeastBotName.ToLower().Contains("gs"))
            {
                return "GalacticSwampBot";
            }
            return "BrotherhoodBot";
        }

        public void LaunchBot()
        {
            _discordClient.Logger.LogInformation("Launching bot: " + SelectedBot);
            var splitted = SelectedBot.Split('/');
            if (SelectedBotIsFromDiablo())
            {
                splitted[0] = HomeDir + DiabloBotsDir + splitted[0];
                if (splitted.Length > 1)
                {
                    var mainFile = ExecuteCommand(RetrieveMainPy.Replace("x1", splitted[0]).Replace("x2", splitted[1]));
                    var line = SplitLinesAndRemoveEmptyStrings(mainFile)[0];
                    StartProcessDiablo(splitted[0] + "/" + line); ;
                }
                else
                {
                    var mains = GetAllMains(splitted[0]);
                    foreach (var main in mains)
                    {
                        StartProcessDiablo(splitted[0] + "/" + main); ;
                    }
                }
            }
            else
            {
                var partialBotName = splitted[0].Split("Beast-")[1];
                StartProcessBeast("/home/alex/Beast-Bots/Bot-Beast-" + partialBotName + "/" + GetBeastExecutable(partialBotName)); ;
            }
            void StartProcessDiablo(string processName)
            {
                bool wasShowingCommand = _showCommand;
                _showCommand = true;
                ExecuteCommand(LaunchBotDiablo.Replace("x", processName), outputExpected: false);
                _showCommand = wasShowingCommand;
            }
            void StartProcessBeast(string processName)
            {
                bool wasShowingCommand = _showCommand;
                _showCommand = true;
                ExecuteCommand(LaunchBotBeast.Replace("x", processName), outputExpected: false);
                _showCommand = wasShowingCommand;
            }
        }

        public void RestartBot()
        {
            _discordClient.Logger.LogInformation("Restarting bot: " + SelectedBot);
            DisconnectBot();
            LaunchBot();
        }

        public bool SelectedBotIsFromDiablo()
        {
            var allBeastBots = GetBeastBots();
            foreach (var s in allBeastBots)
            {
                if (SelectedBot == s) return false;
            }
            return true;
        }

        private string[] CombineArrays(string[] array1, string[] array2)
        {
            var list = array1.ToList();
            list.AddRange(array2);
            list.Sort();
            list.Reverse();
            return list.ToArray();
        }

        private string[] GetFiltering(string[] allBots)
        {
            var filters = new List<string>();
            for (var i = 0; i < allBots.Length; i++)
            {
                var splitted = allBots[i].Split('/')[0].Split('-');
                var filterValue = splitted[splitted.Length - 1].ToUpper();
                if (!filters.Contains(filterValue))
                {
                    filters.Add(filterValue);
                }
            }
            return filters.ToArray();
        }

        private string[] GetAllMains(string dirName)
        {
            var output = ExecuteCommand(RetrieveMainsPy.Replace("x", dirName));
            return SplitLinesAndRemoveEmptyStrings(output);
        }
        private string[] GetAllMainsAndFormat(string dirName, bool includeFoldersIfMultipleMains = true)
        {
            var mains = GetAllMains(dirName);
            var mainsList = new List<string>();
            dirName = Path.GetFileName(dirName);
            if (mains.Length > 1)
            {
                foreach (var main in mains)
                {
                    mainsList.Add(FormatMain(dirName + "/" + main));
                }
            }
            if (mains.Length == 1 || includeFoldersIfMultipleMains)
            {
                mainsList.Add(dirName);
            }
            return mainsList.Distinct().ToArray();
        }
        private string FormatMain(string text)
        {
            try
            {
                _discordClient.Logger.LogInformation("text: " + text);
                var parts = text.Split('/');
                _discordClient.Logger.LogInformation("parts length: " + parts.Length);
                string mainFileName = parts[parts.Length - 1];
                _discordClient.Logger.LogInformation("mainFileName: " + mainFileName);
                var dirName = Path.GetFileName(Path.GetDirectoryName(text));
                mainFileName = mainFileName.Substring(0, mainFileName.Length - 3);
                if (mainFileName.ToLower() != "main")
                {
                    mainFileName = mainFileName.Split("main")[1].TrimStart('_');
                    return dirName + "/" + mainFileName;
                }
                return dirName;
            }
            catch (Exception ex)
            {
                _discordClient.Logger.LogError("Error in FormatMain for \"" + text + "\": " + ex.Message + Environment.NewLine + ex.StackTrace);
                throw;
            }
        }
        private string TrimStart(string text, char character)
        {
            while (text.Length > 0 && text[0] == character)
            {
                text = text.Substring(1);
            }
            return text;
        }
        private string[] GetDiabloBots(bool includeFoldersIfMultipleMains = true)
        {
            var diabloBotsString = ExecuteCommand(RetrieveDiabloBotsCmd);
            var splitted = SplitLinesAndRemoveEmptyStrings(diabloBotsString);
            var bots = new List<string>();
            for (var i = 0; i < splitted.Length; i++)
            {
                bots.AddRange(GetAllMainsAndFormat(HomeDir + DiabloBotsDir + splitted[i], includeFoldersIfMultipleMains: includeFoldersIfMultipleMains));
            }
            return bots.ToArray();
        }
        private string[] GetBeastBots()
        {
            var beastBotsString = ExecuteCommand(RetrieveBeastBotsCmd);
            var splitted = SplitLinesAndRemoveEmptyStrings(beastBotsString);
            for (var i = 0; i < splitted.Length; i++)
            {
                splitted[i] = splitted[i];
            }
            return splitted;
        }
        private string[] GetDiabloProcesses()
        {
            var diabloProcessesString = ExecuteCommand(RetrieveDiabloProcessesCmd);
            var splitted = SplitLinesAndRemoveEmptyStrings(diabloProcessesString);
            for (var i = 0; i < splitted.Length - 1; i++)
            {
                splitted[i] = splitted[i].Split(' ')[1];
                splitted[i] = FormatMain(splitted[i]);
            }
            return splitted;
        }
        private string[] GetBeastProcesses()
        {
            var beastBotsString = ExecuteCommand(RetrieveBeastBotsCmd);
            var splitted = SplitLinesAndRemoveEmptyStrings(beastBotsString);
            var list = new List<string>();
            for (var i = 0; i < splitted.Length; i++)
            {
                var cmd = RetrieveAllFilesWithoutExtensionCmd.Replace("x", HomeDir + BeastBotsDir + splitted[i] + "/*");
                var output = ExecuteCommand(cmd);
                var file = output.Split('\n')[0];
                cmd = RetrieveBeastProcessCmd.Replace("x", Path.GetFileName(file));
                output = ExecuteCommand(cmd);
                if (SplitLinesAndRemoveEmptyStrings(output).Length > 2) list.Add(splitted[i]);
            }
            return list.ToArray();
        }
        private string[] GenerateFilteredFieldValues(string[] filtering, string[] allBots, string[] allProcesses)
        {
            var sbDictionary = new Dictionary<string, Tuple<StringBuilder, StringBuilder>>();
            foreach (var filterValue in filtering)
            {
                sbDictionary.Add(filterValue, new Tuple<StringBuilder, StringBuilder>(new StringBuilder(), new StringBuilder()));
            }
            foreach (var bot in allBots)
            {
                var sbs = sbDictionary.Single(x => bot.Split('/')[0].ToLower().EndsWith(x.Key.ToLower()));
                if (allProcesses.Contains(bot))
                {
                    sbs.Value.Item1.AppendLine(GenerateBotString(bot, true));
                }
                else
                {
                    sbs.Value.Item2.AppendLine(GenerateBotString(bot, false));
                }
            }
            var result = new string[filtering.Length];
            for (var i = 0; i < result.Length; i++)
            {
                var kvp = sbDictionary.Single(x => x.Key == filtering[i]);
                result[i] = CombineOnAndOffBots(kvp.Value.Item1, kvp.Value.Item2);
            }
            return result;
        }
        private string GenerateBotString(string bot, bool isOn)
        {
            if (isOn)
            {
                return "🟢 " + bot;
            }
            else
            {
                return "🔴 " + bot;
            }
        }
        private string[] SplitLinesAndRemoveEmptyStrings(string text)
        {
            var list = new List<string>();
            var splitted = text.Split('\n');
            foreach (var s in splitted)
            {
                if (!string.IsNullOrEmpty(s))
                {
                    list.Add(s);
                }
            }
            return list.ToArray();
        }
        private string CombineOnAndOffBots(StringBuilder sbOn, StringBuilder sbOff)
        {
            var sb = new StringBuilder();
            if (sbOn.Length > 0)
            {
                sb.Append("```yaml\n");
                sb.Append(sbOn);
                sb.Append("```");
            }
            if (sbOff.Length > 0)
            {
                sb.Append("```arm\n");
                sb.Append(sbOff);
                sb.Append("```");
            }
            if (sb.Length == 0)
            {
                sb.Append("No bots");
            }
            return sb.ToString();
        }

        private DiscordComponentEmoji GetEmojiForBot(string bot)
        {
            bot = bot.ToLower();
            if (bot.Contains("leak")) return GetEmoji("ringed_planet");
            if (bot.Contains("war")) return GetEmoji("boom");
            if (bot.Contains("ocr")) return GetEmoji("writing_hand_tone1");
            if (bot.Contains("command")) return GetEmoji("robot");
            return bot.Contains("beast") ? GetEmoji("O_") : GetEmoji("sparkles");
            DiscordComponentEmoji GetEmoji(string text)
            {
                return new DiscordComponentEmoji(DiscordEmoji.FromName(_discordClient, ":" + text + ":"));
            }
        }
    }
}