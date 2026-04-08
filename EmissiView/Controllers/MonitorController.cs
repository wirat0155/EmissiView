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
        private readonly string _consumptionFolder;

        public MonitorController()
        {
            _dataFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "data");
            _logFile = Path.Combine(_dataFolder, "energy_log.json");
            _consumptionFolder = Path.Combine(_dataFolder, "consumption");
        }

        private string GetPlantFromMDB(string mdb)
        {
            return mdb switch
            {
                "01" => "lp",
                "02" => "lp",
                "05" => "lp",
                "03" => "plating",
                "04" => "plating",
                "07" => "brazing",
                "08" => "brazing",
                "09" => "brazing",
                "10" => "brazing",
                _ => "unknown"
            };
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
                var currentYear = now.Year;
                var currentMonth = now.Month;

                // Calculate monthly consumption from .jsonl files
                var monthlyConsumption = CalculateMonthlyConsumption(currentYear, currentMonth);

                // Convert to ViewModel
                foreach (var entry in groupedByMDB.Values)
                {
                    var mdb = entry.GetProperty("MDB").GetString();
                    var plant = entry.GetProperty("Plant").GetString();
                    var status = entry.GetProperty("Status").GetString();
                    var timestamp = entry.GetProperty("Timestamp").GetInt64();

                    var utcDateTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime;
                    var bangkokDateTime = TimeZoneInfo.ConvertTime(utcDateTime, bangkokTz);

                    var timeDiff = now - bangkokDateTime;
                    var isAlive = status == "Online" && timeDiff.TotalSeconds <= 300;
                    var lastSeenText = GetLastSeenText(timeDiff);

                    // Get monthly consumption for this MDB
                    var kwh = 0.0;
                    var firstDate = "";
                    var lastDate = "";
                    
                    if (monthlyConsumption.ContainsKey(mdb))
                    {
                        var consumption = monthlyConsumption[mdb];
                        kwh = consumption.kwh;
                        firstDate = consumption.firstDate;
                        lastDate = consumption.lastDate;
                    }

                    energyData.Add(new EnergyViewModel
                    {
                        MDB = mdb,
                        Plant = plant,
                        kWh = kwh,
                        Status = status,
                        LastUpdate = bangkokDateTime,
                        IsAlive = isAlive,
                        LastSeenText = lastSeenText,
                        FirstDate = firstDate,
                        LastDate = lastDate
                    });
                }

                return View(energyData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error reading data: {ex.Message}" });
            }
        }

        private Dictionary<string, (double kwh, string firstDate, string lastDate)> CalculateMonthlyConsumption(int year, int month)
        {
            var result = new Dictionary<string, (double kwh, string firstDate, string lastDate)>();

            if (!Directory.Exists(_consumptionFolder))
                return result;

            try
            {
                foreach (var plantFolder in Directory.GetDirectories(_consumptionFolder))
                {
                    var filePath = Path.Combine(plantFolder, $"{year}-{month:D2}.jsonl");
                    if (!System.IO.File.Exists(filePath))
                        continue;

                    // Group by MDB, find min FirstWh and max LastWh with dates
                    var mdbData = new Dictionary<string, (long minFirstWh, string firstDate, long maxLastWh, string lastDate)>();

                    foreach (var line in System.IO.File.ReadLines(filePath))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var reading = JsonSerializer.Deserialize<DailyReading>(line);
                        if (reading == null || string.IsNullOrEmpty(reading.MDB)) continue;

                        if (!mdbData.ContainsKey(reading.MDB))
                        {
                            mdbData[reading.MDB] = (reading.FirstWh, reading.Date, reading.LastWh, reading.Date);
                        }
                        else
                        {
                            var current = mdbData[reading.MDB];
                            
                            // Update min FirstWh (take the minimum)
                            long newMinFirstWh = Math.Min(current.minFirstWh, reading.FirstWh);
                            string newFirstDate = newMinFirstWh == reading.FirstWh ? reading.Date : current.firstDate;
                            
                            // Update max LastWh (take the maximum)
                            long newMaxLastWh = Math.Max(current.maxLastWh, reading.LastWh);
                            string newLastDate = newMaxLastWh == reading.LastWh ? reading.Date : current.lastDate;
                            
                            mdbData[reading.MDB] = (newMinFirstWh, newFirstDate, newMaxLastWh, newLastDate);
                        }
                    }

                    // Calculate consumption for each MDB
                    foreach (var kvp in mdbData)
                    {
                        var mdb = kvp.Key;
                        var data = kvp.Value;
                        
                        long consumptionWh = data.maxLastWh < data.minFirstWh ? data.maxLastWh : data.maxLastWh - data.minFirstWh;
                        double consumptionKwh = Math.Round(consumptionWh / 1000.0, 3);
                        
                        result[mdb] = (consumptionKwh, data.firstDate, data.lastDate);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculating monthly consumption: {ex.Message}");
            }

            return result;
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
