using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using SmoothAutoRun.Models;

namespace SmoothAutoRun.Services
{
    public class DiskService
    {
        public List<DiskInfo> ScanAllDisks()
        {
            var disks = new List<DiskInfo>();

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");

                foreach (ManagementObject disk in searcher.Get())
                {
                    try
                    {
                        string mediaType = disk["MediaType"]?.ToString() ?? "";
                        string interfaceType = disk["InterfaceType"]?.ToString() ?? "";
                        string model = disk["Model"]?.ToString()?.Trim() ?? "Неизвестно";

                        // Пропускаем съёмные диски и USB
                        if (mediaType.Contains("Removable") || interfaceType == "USB") continue;

                        var info = new DiskInfo
                        {
                            Model = model,
                            SerialNumber = disk["SerialNumber"]?.ToString()?.Trim() ?? "N/A",
                            Size = FormatBytes(disk["Size"]),
                            InterfaceType = interfaceType,
                            MediaType = mediaType,
                            Index = disk["Index"]?.ToString() ?? "0"
                        };

                        // Тип диска
                        if (mediaType.Contains("NVMe") || interfaceType.Contains("NVMe") || model.Contains("NVMe"))
                            info.Type = "NVMe SSD";
                        else if (mediaType.Contains("SSD") || model.Contains("SSD"))
                            info.Type = "SSD";
                        else
                            info.Type = "HDD";

                        // Буквы дисков
                        info.DriveLetter = GetDriveLetters(disk);

                        // S.M.A.R.T. данные
                        GetSmartData(info, disk);

                        disks.Add(info);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("DiskService", $"Disk error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("DiskService", $"Scan error: {ex.Message}");
            }

            return disks;
        }

        private void GetSmartData(DiskInfo info, ManagementObject disk)
        {
            try
            {
                string? pnpId = disk["PNPDeviceID"]?.ToString();
                if (string.IsNullOrEmpty(pnpId)) return;

                // Извлекаем SCSI-порт и номер
                string scsiSearch = pnpId.Contains("SCSI") ? "SCSI" : "IDE";
                string instanceSearch = $"PHYSICALDRIVE{info.Index}";

                // Пробуем несколько WMI-классов
                GetSmartFromWmi(info, instanceSearch);
                
                // Если не получилось — пробуем через другой класс
                if (info.Temperature == "N/A" && info.HealthStatus == "N/A")
                {
                    GetFailurePredictStatus(info, instanceSearch);
                }
            }
            catch { }

            // Значения по умолчанию
            if (string.IsNullOrEmpty(info.HealthStatus)) { info.HealthStatus = "OK"; info.HealthColor = "#00FF00"; }
            if (string.IsNullOrEmpty(info.Temperature)) info.Temperature = "N/A";
            if (string.IsNullOrEmpty(info.PowerOnHours)) info.PowerOnHours = "N/A";
            if (string.IsNullOrEmpty(info.PowerOnCount)) info.PowerOnCount = "N/A";
        }

        private void GetSmartFromWmi(DiskInfo info, string instanceSearch)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "ROOT\\WMI",
                    "SELECT * FROM MSStorageDriver_ATAPISmartData");

                foreach (ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        string? instanceName = obj["InstanceName"]?.ToString();
                        if (instanceName == null || !instanceName.Contains(instanceSearch)) continue;

                        byte[]? data = obj["VendorSpecific"] as byte[];
                        if (data == null || data.Length < 512) continue;

                        int healthScore = 100;
                        bool hasData = false;

                        // Парсим атрибуты S.M.A.R.T.
                        for (int i = 2; i < data.Length - 11; i += 12)
                        {
                            int attrId = data[i];
                            int rawValue = BitConverter.ToInt32(data, i + 7);

                            if (attrId == 194) // Температура
                            {
                                info.Temperature = $"{rawValue}°C";
                                hasData = true;
                            }
                            else if (attrId == 9) // Power-On Hours
                            {
                                info.PowerOnHours = rawValue.ToString();
                                hasData = true;
                            }
                            else if (attrId == 12) // Power Cycle Count
                            {
                                info.PowerOnCount = rawValue.ToString();
                                hasData = true;
                            }
                            else if (attrId == 5 || attrId == 196) // Reallocated sectors
                            {
                                if (rawValue > 0) healthScore = Math.Min(healthScore, 100 - rawValue);
                                hasData = true;
                            }
                            else if (attrId == 197 || attrId == 198) // Pending/Uncorrectable
                            {
                                if (rawValue > 0) healthScore = Math.Min(healthScore, 100 - rawValue * 10);
                                hasData = true;
                            }
                        }

                        if (hasData)
                        {
                            info.HealthStatus = $"{Math.Max(healthScore, 0)}%";
                            info.HealthColor = healthScore > 70 ? "#00FF00" : (healthScore > 30 ? "#FFA500" : "#FF0000");
                            return;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void GetFailurePredictStatus(DiskInfo info, string instanceSearch)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "ROOT\\WMI",
                    "SELECT * FROM MSStorageDriver_FailurePredictStatus");

                foreach (ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        string? instanceName = obj["InstanceName"]?.ToString();
                        if (instanceName == null || !instanceName.Contains(instanceSearch)) continue;

                        if (obj["Temperature"] != null)
                        {
                            info.Temperature = obj["Temperature"].ToString() + "°C";
                        }
                        if (obj["PowerOnHours"] != null)
                        {
                            info.PowerOnHours = obj["PowerOnHours"].ToString();
                        }
                        if (obj["PowerOnCount"] != null)
                        {
                            info.PowerOnCount = obj["PowerOnCount"].ToString();
                        }

                        // Статус
                        if (obj["PredictFailure"] != null)
                        {
                            bool predictFail = (bool)obj["PredictFailure"];
                            info.HealthStatus = predictFail ? "5%" : "100%";
                            info.HealthColor = predictFail ? "#FF0000" : "#00FF00";
                        }
                        else if (obj["Reason"] != null)
                        {
                            info.HealthStatus = "100%";
                            info.HealthColor = "#00FF00";
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private string GetDriveLetters(ManagementObject disk)
        {
            try
            {
                string deviceId = disk["DeviceID"]?.ToString() ?? "";
                deviceId = deviceId.Replace("\\", "\\\\");

                var letters = new List<string>();

                // Получаем разделы диска
                using var partitionSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{deviceId}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition");

                foreach (ManagementObject partition in partitionSearcher.Get())
                {
                    try
                    {
                        string? partId = partition["DeviceID"]?.ToString()?.Replace("\\", "\\\\");
                        if (string.IsNullOrEmpty(partId)) continue;

                        using var logicalSearcher = new ManagementObjectSearcher(
                            $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partId}'}} WHERE AssocClass = Win32_LogicalDiskToPartition");

                        foreach (ManagementObject logical in logicalSearcher.Get())
                        {
                            string? letter = logical["DeviceID"]?.ToString();
                            if (!string.IsNullOrEmpty(letter) && letter.Contains(":"))
                                letters.Add(letter);
                        }
                    }
                    catch { }
                }

                return letters.Count > 0 ? string.Join(", ", letters.Distinct()) : "N/A";
            }
            catch { return "N/A"; }
        }

        private string FormatBytes(object? sizeObj)
        {
            try
            {
                if (sizeObj == null) return "N/A";
                ulong bytes = Convert.ToUInt64(sizeObj);
                string[] sizes = { "Б", "КБ", "МБ", "ГБ", "ТБ" };
                int order = 0;
                double size = bytes;
                while (size >= 1024 && order < sizes.Length - 1) { order++; size /= 1024; }
                return $"{size:0.##} {sizes[order]}";
            }
            catch { return "N/A"; }
        }
    }
}