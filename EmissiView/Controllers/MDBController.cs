using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EmissiView.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MDBController : ControllerBase
    {
        private readonly string _dataFolder;
        private readonly string _logFile;
        private readonly string _consumptionFolder;
        private readonly int _retentionDays;

        public MDBController(IConfiguration configuration)
        {
            _dataFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "data");
            _logFile = Path.Combine(_dataFolder, "energy_log.json");
            _consumptionFolder = Path.Combine(_dataFolder, "consumption");
            _retentionDays = configuration.GetValue<int>("DataRetention:RetentionDays", 180);

            if (!Directory.Exists(_dataFolder))
                Directory.CreateDirectory(_dataFolder);
            if (!Directory.Exists(_consumptionFolder))
                Directory.CreateDirectory(_consumptionFolder);
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

        private string GetConsumptionFilePath(string plant, int year, int month)
        {
            var plantFolder = Path.Combine(_consumptionFolder, plant);
            if (!Directory.Exists(plantFolder))
                Directory.CreateDirectory(plantFolder);
            return Path.Combine(plantFolder, $"{year}-{month:D2}.jsonl");
        }

        [HttpPost("ReceiveData")]
        public IActionResult ReceiveData([FromBody] MDBDataModel model)
        {
            if (model == null) return BadRequest(new { message = "Invalid data received" });

            try
            {
                var plant = GetPlantFromMDB(model.MDB);
                var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(model.Timestamp).DateTime;

                // Use device-provided Date/Time (local time) instead of UTC timestamp
                // model.Date format: "08/04/2026", model.Time format: "16:38:01"
                DateTime localDateTime;
                if (!string.IsNullOrEmpty(model.Date) && !string.IsNullOrEmpty(model.Time) &&
                    DateTime.TryParseExact($"{model.Date} {model.Time}", "dd/MM/yyyy HH:mm:ss",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out localDateTime))
                {
                    // use device local time
                }
                else
                {
                    // fallback to Bangkok time from timestamp
                    var bangkokTz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                    localDateTime = TimeZoneInfo.ConvertTime(timestamp, bangkokTz);
                }

                var dateKey = localDateTime.ToString("yyyy-MM-dd");
                var timeKey = localDateTime.ToString("HH:mm:ss");
                var year = localDateTime.Year;
                var month = localDateTime.Month;

                // 1. Save raw log entry and purge old entries for this MDB
                var logEntry = new
                {
                    MDB = model.MDB,
                    Plant = plant,
                    kWh = model.kWh,
                    Wh = model.Wh,
                    Status = model.Status,
                    Timestamp = model.Timestamp,
                    DateTime = timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    Date = model.Date,
                    Time = model.Time
                };

                var logData = new List<JsonElement>();
                if (System.IO.File.Exists(_logFile))
                {
                    var existingJson = System.IO.File.ReadAllText(_logFile);
                    if (!string.IsNullOrEmpty(existingJson))
                        logData = JsonSerializer.Deserialize<List<JsonElement>>(existingJson) ?? new List<JsonElement>();
                }

                // Purge ALL entries older than RetentionDays regardless of MDB
                var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);
                logData = logData.Where(e =>
                {
                    if (e.TryGetProperty("Timestamp", out var tsProp))
                        return DateTimeOffset.FromUnixTimeMilliseconds(tsProp.GetInt64()).UtcDateTime >= cutoff;
                    return true;
                }).ToList();

                // Append new entry as raw JsonElement via re-serialization
                var newEntryJson = JsonSerializer.Serialize(logEntry);
                var newEntryElement = JsonSerializer.Deserialize<JsonElement>(newEntryJson);
                logData.Add(newEntryElement);

                System.IO.File.WriteAllText(_logFile, JsonSerializer.Serialize(logData, new JsonSerializerOptions { WriteIndented = true }));

                // 2. Update consumption using JSON Lines format
                var filePath = GetConsumptionFilePath(plant, year, month);

                // Read existing readings for this date+MDB to get FirstWh
                long firstWh = model.Wh;
                string firstTime = timeKey;

                if (System.IO.File.Exists(filePath))
                {
                    foreach (var line in System.IO.File.ReadLines(filePath))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var existingReading = JsonSerializer.Deserialize<DailyReading>(line);
                        if (existingReading != null && existingReading.Date == dateKey && existingReading.MDB == model.MDB)
                        {
                            firstWh = existingReading.FirstWh;
                            firstTime = existingReading.FirstTime;
                            break;
                        }
                    }
                }

                var reading = new DailyReading
                {
                    Date = dateKey,
                    MDB = model.MDB,
                    FirstWh = firstWh,
                    LastWh = model.Wh,
                    FirstTime = firstTime,
                    LastTime = timeKey
                };

                // Append new reading as JSON Line (ensure file ends with newline first)
                var jsonLine = JsonSerializer.Serialize(reading);
                if (System.IO.File.Exists(filePath))
                {
                    // Check if file ends with newline, if not add one
                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite);
                    if (fs.Length > 0)
                    {
                        fs.Seek(-1, SeekOrigin.End);
                        int lastByte = fs.ReadByte();
                        if (lastByte != '\n')
                            fs.WriteByte((byte)'\n');
                    }
                }
                System.IO.File.AppendAllText(filePath, jsonLine + Environment.NewLine);

                // 3. Compact old JSONL records (>RetentionDays) across ALL plant folders/files
                CompactAllPlantFolders();

                return Ok(new { success = true, received = new { MDB = model.MDB, Plant = plant, kWh = model.kWh, Wh = model.Wh, Status = model.Status, Timestamp = model.Timestamp, Date = model.Date, Time = model.Time, Datetime = model.Datetime }, message = "Data received and saved successfully" });
            }
            catch (Exception ex) { return StatusCode(500, new { message = $"Error saving data: {ex.Message}" }); }
        }

        /// <summary>
        /// Compacts JSONL records older than RetentionDays into one record per Date+MDB
        /// (keeps FirstWh/FirstTime from earliest, LastWh/LastTime from latest).
        /// Recent records are left as-is (append-only behaviour preserved).
        /// </summary>
        private void CompactAllPlantFolders()
        {
            if (!Directory.Exists(_consumptionFolder)) return;
            foreach (var plantFolder in Directory.GetDirectories(_consumptionFolder))
                foreach (var file in Directory.GetFiles(plantFolder, "*.jsonl"))
                    CompactOldJsonlRecords(file);
        }

        private void CompactOldJsonlRecords(string filePath)
        {
            if (!System.IO.File.Exists(filePath)) return;

            var cutoffDate = DateTime.UtcNow.AddDays(-_retentionDays).ToString("yyyy-MM-dd");

            var oldRecords = new Dictionary<string, DailyReading>(); // key = Date|MDB
            var recentLines = new List<string>();

            foreach (var line in System.IO.File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var r = JsonSerializer.Deserialize<DailyReading>(line);
                if (r == null) continue;

                if (string.Compare(r.Date, cutoffDate, StringComparison.Ordinal) < 0)
                {
                    // Old record — merge into compacted dictionary
                    var key = $"{r.Date}|{r.MDB ?? ""}";
                    if (!oldRecords.ContainsKey(key))
                    {
                        oldRecords[key] = r;
                    }
                    else
                    {
                        var existing = oldRecords[key];
                        // Keep earliest FirstWh/FirstTime, latest LastWh/LastTime
                        if (r.FirstWh < existing.FirstWh || (r.FirstWh == existing.FirstWh && string.Compare(r.FirstTime, existing.FirstTime) < 0))
                        {
                            existing.FirstWh = r.FirstWh;
                            existing.FirstTime = r.FirstTime;
                        }
                        if (r.LastWh > existing.LastWh || (r.LastWh == existing.LastWh && string.Compare(r.LastTime, existing.LastTime) > 0))
                        {
                            existing.LastWh = r.LastWh;
                            existing.LastTime = r.LastTime;
                        }
                        oldRecords[key] = existing;
                    }
                }
                else
                {
                    recentLines.Add(line);
                }
            }

            // Only rewrite if there were old records to compact
            if (oldRecords.Count == 0) return;

            var compactedOldLines = oldRecords.Values
                .OrderBy(r => r.Date).ThenBy(r => r.MDB)
                .Select(r => JsonSerializer.Serialize(r));

            var allLines = compactedOldLines.Concat(recentLines);
            System.IO.File.WriteAllLines(filePath, allLines);
        }

        [HttpGet("MigrateAddMDB")]
        public IActionResult MigrateAddMDB([FromQuery] string plant, [FromQuery] string mdb, [FromQuery] int year, [FromQuery] int month)
        {
            if (string.IsNullOrEmpty(plant) || string.IsNullOrEmpty(mdb))
                return BadRequest(new { message = "plant and mdb are required" });

            try
            {
                var filePath = GetConsumptionFilePath(plant, year, month);
                if (!System.IO.File.Exists(filePath))
                    return NotFound(new { message = $"File not found: {filePath}" });

                var lines = System.IO.File.ReadAllLines(filePath);
                int updated = 0;
                var newLines = new List<string>();

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) { newLines.Add(line); continue; }
                    var reading = JsonSerializer.Deserialize<DailyReading>(line);
                    if (reading == null) { newLines.Add(line); continue; }

                    if (string.IsNullOrEmpty(reading.MDB))
                    {
                        reading.MDB = mdb;
                        updated++;
                    }
                    newLines.Add(JsonSerializer.Serialize(reading));
                }

                System.IO.File.WriteAllLines(filePath, newLines);
                return Ok(new { success = true, updated, total = lines.Length });
            }
            catch (Exception ex) { return StatusCode(500, new { message = $"Error migrating data: {ex.Message}" }); }
        }

        [HttpGet("GetDailyTotals")]

        public IActionResult GetDailyTotals([FromQuery] int year, [FromQuery] int month)
        {
            try
            {
                var dailyTotals = new Dictionary<string, Dictionary<string, double>>();
                var targetMonthPrefix = $"{year}-{month:D2}";

                if (!Directory.Exists(_consumptionFolder)) return Ok(new { dailyTotals });

                foreach (var plantFolder in Directory.GetDirectories(_consumptionFolder))
                {
                    var plant = Path.GetFileName(plantFolder);
                    var filePath = Path.Combine(plantFolder, $"{year}-{month:D2}.jsonl");

                    if (!System.IO.File.Exists(filePath)) continue;

                    // Key: Date+MDB -> latest reading
                    var lastReadings = new Dictionary<string, DailyReading>();

                    foreach (var line in System.IO.File.ReadLines(filePath))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var reading = JsonSerializer.Deserialize<DailyReading>(line);
                        if (reading == null) continue;

                        var key = $"{reading.Date}|{reading.MDB ?? ""}";
                        lastReadings[key] = reading; // Keep latest per Date+MDB
                    }

                    dailyTotals[plant] = new Dictionary<string, double>();

                    // Group by Date, SUM consumption across all MDBs
                    var byDate = lastReadings.Values
                        .Where(r => r.Date.StartsWith(targetMonthPrefix))
                        .GroupBy(r => r.Date);

                    foreach (var group in byDate)
                    {
                        double totalKwh = 0;
                        foreach (var reading in group)
                        {
                            long consumptionWh = reading.LastWh < reading.FirstWh ? reading.LastWh : reading.LastWh - reading.FirstWh;
                            totalKwh += consumptionWh / 1000.0;
                        }
                        dailyTotals[plant][group.Key] = Math.Round(totalKwh, 3);
                    }
                }

                Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                Response.Headers["Pragma"] = "no-cache";
                Response.Headers["Expires"] = "0";
                return Ok(new { dailyTotals });
            }
            catch (Exception ex) { return StatusCode(500, new { message = $"Error reading data: {ex.Message}" }); }
        }

        [HttpGet("GetMonthlyTotals")]
        public IActionResult GetMonthlyTotals([FromQuery] int year)
        {
            try
            {
                var dailyTotals = new Dictionary<string, Dictionary<string, double>>();

                if (!Directory.Exists(_consumptionFolder)) return Ok(new { dailyTotals });

                foreach (var plantFolder in Directory.GetDirectories(_consumptionFolder))
                {
                    var plant = Path.GetFileName(plantFolder);
                    dailyTotals[plant] = new Dictionary<string, double>();

                    foreach (var file in Directory.GetFiles(plantFolder, $"{year}-*.jsonl"))
                    {
                        // Key: Date+MDB -> latest reading
                        var lastReadings = new Dictionary<string, DailyReading>();
                        foreach (var line in System.IO.File.ReadLines(file))
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            var reading = JsonSerializer.Deserialize<DailyReading>(line);
                            if (reading == null) continue;
                            var key = $"{reading.Date}|{reading.MDB ?? ""}";
                            lastReadings[key] = reading; // Keep latest per Date+MDB
                        }

                        // Group by Date, SUM consumption across all MDBs
                        var byDate = lastReadings.Values.GroupBy(r => r.Date);
                        foreach (var group in byDate)
                        {
                            double totalKwh = 0;
                            foreach (var reading in group)
                            {
                                long consumptionWh = reading.LastWh < reading.FirstWh ? reading.LastWh : reading.LastWh - reading.FirstWh;
                                totalKwh += consumptionWh / 1000.0;
                            }
                            dailyTotals[plant][group.Key] = Math.Round(totalKwh, 3);
                        }
                    }
                }

                Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                Response.Headers["Pragma"] = "no-cache";
                Response.Headers["Expires"] = "0";
                return Ok(new { dailyTotals });
            }
            catch (Exception ex) { return StatusCode(500, new { message = $"Error reading data: {ex.Message}" }); }
        }
    }

    public class MDBDataModel
    {
        public string MDB { get; set; }
        public double kWh { get; set; }
        public long Wh { get; set; }
        public string Status { get; set; }
        public long Timestamp { get; set; }
        public string Date { get; set; }
        public string Time { get; set; }
        public string Datetime { get; set; }
    }

    public class DailyReading
    {
        public string Date { get; set; }
        public long FirstWh { get; set; }
        public long LastWh { get; set; }
        public string FirstTime { get; set; }
        public string LastTime { get; set; }
        public string MDB { get; set; }
    }
}
