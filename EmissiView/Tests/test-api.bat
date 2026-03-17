@echo off
REM API Test Script for EmissiView

echo.
echo ========================================
echo EmissiView API Test Suite
echo ========================================
echo.

set PASSED=0
set FAILED=0

REM Test 1: GetProductionData API
echo Testing: GET /ElectricChart/GetProductionData
curl -s -o nul -w "%%{http_code}" "http://localhost:5000/ElectricChart/GetProductionData?startDate=2026-01-01&endDate=2026-12-31" > temp.txt 2>nul
set /p CODE=<temp.txt
del temp.txt 2>nul
if "%CODE%"=="200" (
    echo [PASS] GetProductionData returns 200 OK
    set /a PASSED+=1
) else (
    echo [FAIL] GetProductionData returns %CODE%
    set /a FAILED+=1
)
echo.

REM Test 2: GetDailyTotals API
echo Testing: GET /api/MDB/GetDailyTotals
curl -s -o nul -w "%%{http_code}" "http://localhost:5000/api/MDB/GetDailyTotals?year=2026&month=3" > temp.txt 2>nul
set /p CODE=<temp.txt
del temp.txt 2>nul
if "%CODE%"=="200" (
    echo [PASS] GetDailyTotals returns 200 OK
    set /a PASSED+=1
) else (
    echo [FAIL] GetDailyTotals returns %CODE%
    set /a FAILED+=1
)
echo.

REM Test 3: GetMonthlyTotals API
echo Testing: GET /api/MDB/GetMonthlyTotals
curl -s -o nul -w "%%{http_code}" "http://localhost:5000/api/MDB/GetMonthlyTotals?year=2026" > temp.txt 2>nul
set /p CODE=<temp.txt
del temp.txt 2>nul
if "%CODE%"=="200" (
    echo [PASS] GetMonthlyTotals returns 200 OK
    set /a PASSED+=1
) else (
    echo [FAIL] GetMonthlyTotals returns %CODE%
    set /a FAILED+=1
)
echo.

REM Test 4: ReceiveData API (POST)
echo Testing: POST /api/MDB/ReceiveData
curl -s -o nul -w "%%{http_code}" -X POST -H "Content-Type: application/json" -d "{\"MDB\":\"08\",\"Plant\":\"brazing\",\"kWh\":100.0,\"Wh\":100000,\"Status\":\"Online\",\"DateTime\":\"2026-03-07 10:00:00\"}" "http://localhost:5000/api/MDB/ReceiveData" > temp.txt 2>nul
set /p CODE=<temp.txt
del temp.txt 2>nul
if "%CODE%"=="200" (
    echo [PASS] ReceiveData returns 200 OK
    set /a PASSED+=1
) else (
    echo [FAIL] ReceiveData returns %CODE%
    set /a FAILED+=1
)
echo.

REM Summary
echo ========================================
echo Test Summary
echo ========================================
echo Tests Passed: %PASSED%
echo Tests Failed: %FAILED%
echo.

if %FAILED%==0 (
    echo All tests passed!
    exit /b 0
) else (
    echo Some tests failed.
    exit /b 1
)
