using System;
using System.Collections.Generic;
using System.Diagnostics;
using SmoothAutoRun.Models;

namespace SmoothAutoRun.Services
{
    public class FirewallService
    {
        private const string RULE_PREFIX = "SmoothAutoRun_";

        public void BlockApplication(string exePath, string ruleName)
        {
            try
            {
                string fullRuleName = RULE_PREFIX + ruleName;
                
                // Удаляем старые правила если есть
                RunNetsh($"advfirewall firewall delete rule name=\"{fullRuleName}\"");
                RunNetsh($"advfirewall firewall delete rule name=\"{fullRuleName}_in\"");

                // Создаём новые блокирующие правила
                string addOut = $"advfirewall firewall add rule name=\"{fullRuleName}\" dir=out program=\"{exePath}\" action=block enable=yes";
                string addIn = $"advfirewall firewall add rule name=\"{fullRuleName}_in\" dir=in program=\"{exePath}\" action=block enable=yes";

                string resultOut = RunNetsh(addOut);
                string resultIn = RunNetsh(addIn);

                if (resultOut.Contains("Ok") || resultIn.Contains("Ok"))
                    Logger.Log("Firewall", $"Blocked: {exePath}");
                else
                    Logger.Log("Firewall", $"Failed to block: {exePath}. Out: {resultOut}, In: {resultIn}");
            }
            catch (Exception ex)
            {
                Logger.Log("Firewall", $"Error blocking: {ex.Message}");
            }
        }

        public void UnblockApplication(string ruleName)
        {
            try
            {
                string fullRuleName = RULE_PREFIX + ruleName;
                RunNetsh($"advfirewall firewall delete rule name=\"{fullRuleName}\"");
                RunNetsh($"advfirewall firewall delete rule name=\"{fullRuleName}_in\"");
                Logger.Log("Firewall", $"Unblocked: {ruleName}");
            }
            catch (Exception ex)
            {
                Logger.Log("Firewall", $"Error unblocking: {ex.Message}");
            }
        }

        public List<FirewallRule> GetBlockedApps()
        {
            var rules = new List<FirewallRule>();
            try
            {
                string output = RunNetsh("advfirewall firewall show rule name=all");
                string[] lines = output.Split('\n');
                string? currentName = null;
                string? currentProgram = null;

                foreach (string line in lines)
                {
                    if (line.Trim().StartsWith("Rule Name:"))
                        currentName = line.Replace("Rule Name:", "").Trim();
                    
                    if (line.Trim().StartsWith("Program:"))
                        currentProgram = line.Replace("Program:", "").Trim();

                    if (currentName != null && currentProgram != null && 
                        currentName.StartsWith(RULE_PREFIX) && !currentName.EndsWith("_in"))
                    {
                        rules.Add(new FirewallRule
                        {
                            Name = currentName.Replace(RULE_PREFIX, ""),
                            ExePath = currentProgram,
                            Blocked = true
                        });
                        currentName = null;
                        currentProgram = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Firewall", $"Error getting rules: {ex.Message}");
            }
            return rules;
        }

        public void ToggleInternet(bool enable)
        {
            try
            {
                string action = enable ? "on" : "off";
                string result = RunNetsh($"advfirewall set allprofiles state {action}");
                Logger.Log("Firewall", $"Internet {(enable ? "enabled" : "disabled")}: {result}");
            }
            catch (Exception ex)
            {
                Logger.Log("Firewall", $"Error toggling internet: {ex.Message}");
            }
        }

        private string RunNetsh(string arguments)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        Verb = "runas"
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit(10000);
                return output + error;
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}