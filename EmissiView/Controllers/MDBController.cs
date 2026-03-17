using Microsoft.AspNetCore.Mvc;
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

        public MDBController()
        {
            _dataFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "data");
            _logFile = Path.Combine(_dataFolder, "energy_log.json");
            _consumptionFolder = Path.Combine(_dataFolder, "consumption");

            // Ensure folders exist
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
                var dateKey = timestamp.ToString("yyyy-MM-dd");
                var timeKey = timestamp.ToString("HH:mm:ss");
                var year = timestamp.Year;
                var month = timestamp.Month;

                // 1. Save raw log entry (keep existing format)
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

                var logData = new List<object>();
                if (System.IO.File.Exists(_logFile))
                {
                    var existingJson = System.IO.File.ReadAllText(_logFile);
                    if (!string.IsNullOrEmpty(existingJson))
                        logData = JsonSerializer.Deserialize<List<object>>(existingJson) ?? new List<object>();
                }
                logData.Add(logEntry);
                System.IO.File.WriteAllText(_logFile, JsonSerializer.Serialize(logData, new JsonSerializerOptions { WriteIndented = true }));

                // 2. Update consumption using JSON Lines format
                var filePath = GetConsumptionFilePath(plant, year, month);
                var reading = new DailyReading
                {
                    Date = dateKey,
                    FirstWh = model.Wh,
                    LastWh = model.Wh,
                    FirstTime = timeKey,
                    LastTime = timeKey
                };

                // Append new reading as JSON Line (fast append-only)
                var jsonLine = JsonSerializer.Serialize(reading);
                System.IO.File.AppendAllText(filePath, jsonLine + Environment.NewLine);

                return Ok(new { success = true, received = new { MDB = model.MDB, Plant = plant, kWh = model.kWh, Wh = model.Wh, Status = model.Status, Timestamp = model.Timestamp, Date = model.Date, Time = model.Time, Datetime = model.Datetime }, message = "Data received and saved successfully" });
            }
            catch (Exception ex) { return StatusCode(500, new { message = $"Error saving data: {ex.Message}" }); }
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

                    var lastReadings = new Dictionary<string, DailyReading>();

                    foreach (var line in System.IO.File.ReadLines(filePath))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var reading = JsonSerializer.Deserialize<DailyReading>(line);
                        if (reading == null) continue;

                        if (!lastReadings.ContainsKey(reading.Date))
                            lastReadings[reading.Date] = reading;
                        else
                            lastReadings[reading.Date] = reading; // Keep latest
                    }

                    dailyTotals[plant] = new Dictionary<string, double>();
                    foreach (var (date, reading) in lastReadings)
                    {
                        if (!date.StartsWith(targetMonthPrefix)) continue;
                        long consumptionWh = reading.LastWh < reading.FirstWh ? reading.LastWh : reading.LastWh - reading.FirstWh;
                        dailyTotals[plant][date] = Math.Round(consumptionWh / 1000.0, 3);
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
                        var lastReadings = new Dictionary<string, DailyReading>();
                        foreach (var line in System.IO.File.ReadLines(file))
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            var reading = JsonSerializer.Deserialize<DailyReading>(line);
                            if (reading == null) continue;
                            lastReadings[reading.Date] = reading;
                        }

                        foreach (var (date, reading) in lastReadings)
                        {
                            long consumptionWh = reading.LastWh < reading.FirstWh ? reading.LastWh : reading.LastWh - reading.FirstWh;
                            dailyTotals[plant][date] = Math.Round(consumptionWh / 1000.0, 3);
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
    }
}
