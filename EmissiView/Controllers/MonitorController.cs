using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EmissiView.Models;

namespace EmissiView.Controllers
{
    public class MonitorController : Controller
    {
        private readonly string _dataFolder;
        private readonly string _logFile;

        public MonitorController()
        {
            _dataFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "data");
            _logFile = Path.Combine(_dataFolder, "energy_log.json");
        }

        public IActionResult List()
        {
            try
            {
                var energyData = new List<EnergyViewModel>();

                if (!System.IO.File.Exists(_logFile))
                    return View(energyData);

                var json = System.IO.File.ReadAllText(_logFile);
                if (string.IsNullOrEmpty(json))
                    return View(energyData);

                var logEntries = JsonSerializer.Deserialize<List<JsonElement>>(json);
                if (logEntries == null || logEntries.Count == 0)
                    return View(energyData);

                // Group by MDB and get latest record for each
                var groupedByMDB = new Dictionary<string, JsonElement>();
                foreach (var entry in logEntries)
                {
                    if (entry.TryGetProperty("MDB", out var mdbProp))
                    {
                        var mdb = mdbProp.GetString();
                        if (!groupedByMDB.ContainsKey(mdb) || 
                            entry.TryGetProperty("Timestamp", out var ts1) && 
                            groupedByMDB[mdb].TryGetProperty("Timestamp", out var ts2) &&
                            ts1.GetInt64() > ts2.GetInt64())
                        {
                            groupedByMDB[mdb] = entry;
                        }
                    }
                }

                var bangkokTz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                var now = TimeZoneInfo.ConvertTime(DateTime.UtcNow, bangkokTz);

                // Convert to ViewModel
                foreach (var entry in groupedByMDB.Values)
                {
                    var mdb = entry.GetProperty("MDB").GetString();
                    var plant = entry.GetProperty("Plant").GetString();
                    var kwh = entry.GetProperty("kWh").GetDouble();
                    var status = entry.GetProperty("Status").GetString();
                    var timestamp = entry.GetProperty("Timestamp").GetInt64();

                    var utcDateTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime;
                    var bangkokDateTime = TimeZoneInfo.ConvertTime(utcDateTime, bangkokTz);

                    var timeDiff = now - bangkokDateTime;
                    var isAlive = status == "Online" && timeDiff.TotalSeconds <= 300;
                    var lastSeenText = GetLastSeenText(timeDiff);

                    energyData.Add(new EnergyViewModel
                    {
                        MDB = mdb,
                        Plant = plant,
                        kWh = kwh,
                        Status = status,
                        LastUpdate = bangkokDateTime,
                        IsAlive = isAlive,
                        LastSeenText = lastSeenText
                    });
                }

                return View(energyData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error reading data: {ex.Message}" });
            }
        }

        private string GetLastSeenText(TimeSpan timeDiff)
        {
            if (timeDiff.TotalSeconds < 60)
                return $"{(int)timeDiff.TotalSeconds} seconds ago";
            if (timeDiff.TotalMinutes < 60)
                return $"{(int)timeDiff.TotalMinutes} minute{((int)timeDiff.TotalMinutes != 1 ? "s" : "")} ago";
            if (timeDiff.TotalHours < 24)
                return $"{(int)timeDiff.TotalHours} hour{((int)timeDiff.TotalHours != 1 ? "s" : "")} ago";
            return $"{(int)timeDiff.TotalDays} day{((int)timeDiff.TotalDays != 1 ? "s" : "")} ago";
        }
    }
}
