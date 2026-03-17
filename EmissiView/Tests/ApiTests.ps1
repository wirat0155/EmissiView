# API Test Assertions for EmissiView
# This script tests all APIs in the system

param(
    [string]$BaseUrl = "http://localhost:5000",
    [switch]$CleanData = $false
)

$ErrorActionPreference = "Stop"

# Colors for output
function Write-TestResult {
    param([string]$Message, [bool]$Success)
    if ($Success) {
        Write-Host "✓ $Message" -ForegroundColor Green
    }
    else {
        Write-Host "✗ $Message" -ForegroundColor Red
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "EmissiView API Test Suite" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$testsPassed = 0
$testsFailed = 0

# Test 1: GetProductionData API
Write-Host "Testing: GET /ElectricChart/GetProductionData" -ForegroundColor Yellow
try {
    $url = "$BaseUrl/ElectricChart/GetProductionData?startDate=2026-01-01`&endDate=2026-12-31"
    $response = Invoke-WebRequest -Uri $url -Method GET -ContentType "application/json"
    if ($response.StatusCode -eq 200) {
        $data = $response.Content | ConvertFrom-Json
        if ($data -is [Array]) {
            Write-TestResult "GetProductionData returns 200 OK with valid JSON array" $true
            $testsPassed++
        }
        else {
            Write-TestResult "GetProductionData returns valid response but not an array" $false
            $testsFailed++
        }
    }
    else {
        Write-TestResult "GetProductionData returns status $($response.StatusCode)" $false
        $testsFailed++
    }
}
catch {
    Write-TestResult "GetProductionData failed: $($_.Exception.Message)" $false
    $testsFailed++
}

# Test 2: GetDailyTotals API
Write-Host "`nTesting: GET /api/MDB/GetDailyTotals" -ForegroundColor Yellow
try {
    $url = "$BaseUrl/api/MDB/GetDailyTotals?year=2026`&month=3"
    $response = Invoke-WebRequest -Uri $url -Method GET -ContentType "application/json"
    if ($response.StatusCode -eq 200) {
        $data = $response.Content | ConvertFrom-Json
        if ($data.PSObject.Properties.Name -contains "dailyTotals") {
            Write-TestResult "GetDailyTotals returns 200 OK with dailyTotals property" $true
            $testsPassed++
        }
        else {
            Write-TestResult "GetDailyTotals returns valid response but missing dailyTotals property" $false
            $testsFailed++
        }
    }
    else {
        Write-TestResult "GetDailyTotals returns status $($response.StatusCode)" $false
        $testsFailed++
    }
}
catch {
    Write-TestResult "GetDailyTotals failed: $($_.Exception.Message)" $false
    $testsFailed++
}

# Test 3: GetMonthlyTotals API
Write-Host "`nTesting: GET /api/MDB/GetMonthlyTotals" -ForegroundColor Yellow
try {
    $url = "$BaseUrl/api/MDB/GetMonthlyTotals?year=2026"
    $response = Invoke-WebRequest -Uri $url -Method GET -ContentType "application/json"
    if ($response.StatusCode -eq 200) {
        $data = $response.Content | ConvertFrom-Json
        if ($data.PSObject.Properties.Name -contains "dailyTotals") {
            Write-TestResult "GetMonthlyTotals returns 200 OK with dailyTotals property" $true
            $testsPassed++
        }
        else {
            Write-TestResult "GetMonthlyTotals returns valid response but missing dailyTotals property" $false
            $testsFailed++
        }
    }
    else {
        Write-TestResult "GetMonthlyTotals returns status $($response.StatusCode)" $false
        $testsFailed++
    }
}
catch {
    Write-TestResult "GetMonthlyTotals failed: $($_.Exception.Message)" $false
    $testsFailed++
}

# Test 4: ReceiveData API (POST)
Write-Host "`nTesting: POST /api/MDB/ReceiveData" -ForegroundColor Yellow
try {
    $testData = @{
        MDB      = "08"
        Plant    = "brazing"
        kWh      = 100.0
        Wh       = 100000
        Status   = "Online"
        DateTime = "2026-03-07 10:00:00"
    } | ConvertTo-Json

    $response = Invoke-WebRequest -Uri "$BaseUrl/api/MDB/ReceiveData" -Method POST -Body $testData -ContentType "application/json"
    if ($response.StatusCode -eq 200 -or $response.StatusCode -eq 201) {
        Write-TestResult "ReceiveData returns success status $($response.StatusCode)" $true
        $testsPassed++
    }
    else {
        Write-TestResult "ReceiveData returns status $($response.StatusCode)" $false
        $testsFailed++
    }
}
catch {
    Write-TestResult "ReceiveData failed: $($_.Exception.Message)" $false
    $testsFailed++
}

# Clean Data if requested
if ($CleanData) {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "Cleaning Test Data" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
    
    # Backup current consumption.json
    $consumptionPath = "wwwroot/data/consumption.json"
    if (Test-Path $consumptionPath) {
        $backupPath = "wwwroot/data/consumption.json.backup"
        Copy-Item $consumptionPath $backupPath -Force
        Write-Host "Backed up consumption.json to consumption.json.backup" -ForegroundColor Green
        
        # Reset to original minimal data
        $originalData = @{
            brazing = @{
                "2026-03-07" = @{
                    FirstWh   = 13416833
                    LastWh    = 13425833
                    FirstTime = "08:48:01"
                    LastTime  = "09:18:01"
                }
            }
        } | ConvertTo-Json -Depth 10
        
        Set-Content -Path $consumptionPath -Value $originalData -Encoding UTF8
        Write-Host "Reset consumption.json to original state" -ForegroundColor Green
    }
    
    # Backup and reset energy_log.json
    $energyLogPath = "wwwroot/data/energy_log.json"
    if (Test-Path $energyLogPath) {
        $backupPath = "wwwroot/data/energy_log.json.backup"
        Copy-Item $energyLogPath $backupPath -Force
        Write-Host "Backed up energy_log.json to energy_log.json.backup" -ForegroundColor Green
        
        # Reset to empty array
        Set-Content -Path $energyLogPath -Value "[]" -Encoding UTF8
        Write-Host "Reset energy_log.json to empty array" -ForegroundColor Green
    }
    
    Write-Host "`nData cleaned successfully!" -ForegroundColor Green
}

# Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Tests Passed: $testsPassed" -ForegroundColor $(if ($testsPassed -gt 0) { "Green" } else { "Yellow" })
Write-Host "Tests Failed: $testsFailed" -ForegroundColor $(if ($testsFailed -gt 0) { "Red" } else { "Green" })
Write-Host ""

if ($testsFailed -eq 0) {
    Write-Host "All tests passed! ✓" -ForegroundColor Green
    exit 0
}
else {
    Write-Host "Some tests failed. ✗" -ForegroundColor Red
    exit 1
}
