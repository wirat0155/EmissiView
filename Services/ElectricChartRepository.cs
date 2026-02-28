using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using EmissiView.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EmissiView.Services
{
    public class ElectricChartRepository : IElectricChartRepository
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public ElectricChartRepository(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("NM");
        }

        public async Task<IEnumerable<ProductionDataModel>> GetProductionDataAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    /* 
                       CACHE LOGIC START 
                       Check for cached data if the requested range is in the past (excluding today)
                    */
                    bool isHistorical = endDate.Date < DateTime.Today;

                    // 1. Ensure Cache Table Exists (Idempotent)
                    string createTableQuery = @"
                        IF NOT EXISTS (SELECT * FROM [Brazing].sys.tables WHERE name = 'iot_pdvolume' AND type = 'U')
                        BEGIN
                            CREATE TABLE [Brazing].[dbo].[iot_pdvolume] (
                                Id INT IDENTITY(1,1) PRIMARY KEY,
                                TranDate DATETIME,
                                Plant NVARCHAR(50),
                                TranQty DECIMAL(18,2),
                                CreatedDate DATETIME DEFAULT GETDATE()
                            );
                            CREATE INDEX IDX_iot_pdvolume_Date ON [Brazing].[dbo].[iot_pdvolume](TranDate);
                        END";
                    await connection.ExecuteAsync(createTableQuery);

                    if (isHistorical)
                    {
                        string cacheQuery = @"
                            SELECT TranDate, TranQty, Plant 
                            FROM [Brazing].[dbo].[iot_pdvolume]
                            WHERE TranDate BETWEEN @txt_StartDate AND @txt_EndDate";

                        var cachedData = await connection.QueryAsync<ProductionDataModel>(cacheQuery, new { txt_StartDate = startDate, txt_EndDate = endDate });

                        if (cachedData.Any())
                        {
                            return cachedData;
                        }
                    }

                    /* LIVE QUERY */
                    string query = @"
                    /* Brazing Query */
                    SELECT 
                        T.[TranDate],
                        SUM(T.[TranQty]) AS TranQty,
                        'brazing' AS Plant
                    FROM [Brazing].[dbo].[ProductTran] T
                    INNER JOIN [Brazing].[dbo].[Product] P ON P.[PartNo] = T.[PartNo]
                    WHERE T.[TranType] = 'RP'
                        AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
                        AND (
                               (T.[DInventoryNo] = '32' AND P.[ProductType] = 'P')
                        )
                    GROUP BY T.[TranDate]

                    UNION ALL

                    /* LP Query */
                    SELECT 
                        T.[TranDate],
                        SUM(T.[TranQty]) AS TranQty,
                        'lp' AS Plant
                    FROM [USUI].[dbo].[ProductTran] T
                    INNER JOIN [USUI].[dbo].[Product] P ON P.[PartNo] = T.[PartNo]
                    WHERE T.[TranType] = 'RP'
                        AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
                        AND T.[DInventoryNo] = '32' 
                        AND P.[ProductType] = 'P'
                    GROUP BY T.[TranDate]
                    
                    UNION ALL

                    /* Plating - Sub (S) Query */
                    SELECT 
                        T.[TranDate],
                        SUM(T.[TranQty]) AS TranQty,
                        'plating-sub' AS Plant
                    FROM (
                        -- Database 1: SPdb_Exp
                        SELECT T.TranDate, T.TranQty
                        FROM [SPdb_Exp].[dbo].[ProductTran] T
                        INNER JOIN [SPdb_Exp].[dbo].[Product] P ON P.[PartNo] = T.[PartNo]
                        WHERE T.[TranType] = 'RP'
                          AND T.[DInventoryNo] = '31'
                          AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
                          AND P.[ProductType] = 'S'
                          AND EXISTS (SELECT 1 FROM [SPdb_Exp].[dbo].[ProcessDetail] PD WHERE PD.PartNo = T.PartNo AND PD.ProcessAreaNo = '6')
                        
                        UNION ALL
                        
                        -- Database 2: SPdb_Exp2
                        SELECT T.TranDate, T.TranQty
                        FROM [SPdb_Exp2].[dbo].[ProductTran] T
                        INNER JOIN [SPdb_Exp2].[dbo].[Product] P ON P.[PartNo] = T.[PartNo]
                        WHERE T.[TranType] = 'RP'
                          AND T.[DInventoryNo] = '31'
                          AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
                          AND P.[ProductType] = 'S'
                          AND EXISTS (SELECT 1 FROM [SPdb_Exp2].[dbo].[ProcessDetail] PD WHERE PD.PartNo = T.PartNo AND PD.ProcessAreaNo = '6')

                        UNION ALL

                        -- Database 3: SPdb_Dom
                        SELECT T.TranDate, T.TranQty
                        FROM [SPdb_Dom].[dbo].[ProductTran] T
                        INNER JOIN [SPdb_Dom].[dbo].[Product] P ON P.[PartNo] = T.[PartNo]
                        WHERE T.[TranType] = 'RP'
                          AND T.[DInventoryNo] = '31'
                          AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
                          AND P.[ProductType] = 'S'
                          AND EXISTS (SELECT 1 FROM [SPdb_Dom].[dbo].[ProcessDetail] PD WHERE PD.PartNo = T.PartNo AND PD.ProcessAreaNo = '6')
                    ) T
                    GROUP BY T.[TranDate]
                    
                    UNION ALL

                    /* Plating - Greitmo (G) Query */
                    SELECT 
                        T.[TranDate],
                        SUM(T.[TranQty]) AS TranQty,
                        'plating-greitmo' AS Plant
                    FROM (
                        -- Database 1: SPdb_Exp
                        SELECT T.TranDate, T.TranQty
                        FROM [SPdb_Exp].[dbo].materialtran T
                        WHERE T.trantype = 'RG'
                          AND T.DInventoryNo = '11'
                          AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
                        
                        UNION ALL
                        
                        -- Database 2: SPdb_Exp2
                        SELECT T.TranDate, T.TranQty
                        FROM [SPdb_Exp2].[dbo].materialtran T
                        WHERE T.trantype = 'RG'
                          AND T.DInventoryNo = '11'
                          AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate

                        UNION ALL

                        -- Database 3: SPdb_Dom
                        SELECT T.TranDate, T.TranQty
                        FROM [SPdb_Dom].[dbo].materialtran T
                        WHERE T.trantype = 'RG'
                          AND T.DInventoryNo = '11'
                          AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
                    ) T
                    GROUP BY T.[TranDate]
                    
                    UNION ALL

                    /* Phase 4: DOM Query */
                    SELECT 
                        T.[TranDate],
                        SUM(T.[TranQty]) AS TranQty,
                        'dom' AS Plant
                    FROM [SPdb_Dom].[dbo].[ProductTran] T
                    INNER JOIN [SPdb_Dom].[dbo].[Product] P ON P.[PartNo] = T.[PartNo]
                    WHERE T.[TranType] = 'RP'
                        AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
                        AND T.[DInventoryNo] = '32' 
                        AND P.[ProductType] = 'P'
                        AND NOT EXISTS (
                            SELECT 1 FROM [SPdb_Dom].[dbo].[ProcessDetail] PD 
                            WHERE PD.PartNo = T.PartNo AND PD.ProcessAreaNo = '6'
                        )
                    GROUP BY T.[TranDate]

                    UNION ALL

                    /* Phase 4: EXP Query */
                    SELECT 
                        T.[TranDate],
                        SUM(T.[TranQty]) AS TranQty,
                        'exp' AS Plant
                    FROM [SPdb_Exp].[dbo].[ProductTran] T
                    INNER JOIN [SPdb_Exp].[dbo].[Product] P ON P.[PartNo] = T.[PartNo]
                    WHERE T.[TranType] = 'RP'
                        AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
                        AND T.[DInventoryNo] = '32' 
                        AND P.[ProductType] = 'P'
                        AND NOT EXISTS (
                            SELECT 1 FROM [SPdb_Exp].[dbo].[ProcessDetail] PD 
                            WHERE PD.PartNo = T.PartNo AND PD.ProcessAreaNo = '6'
                        )
                    GROUP BY T.[TranDate]

                    UNION ALL

                    /* Phase 4: EXP2 Query */
                    SELECT 
                        T.[TranDate],
                        SUM(T.[TranQty]) AS TranQty,
                        'exp2' AS Plant
                    FROM [SPdb_Exp2].[dbo].[ProductTran] T
                    INNER JOIN [SPdb_Exp2].[dbo].[Product] P ON P.[PartNo] = T.[PartNo]
                    WHERE T.[TranType] = 'RP'
                        AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
                        AND T.[DInventoryNo] = '32' 
                        AND P.[ProductType] = 'P'
                        AND NOT EXISTS (
                            SELECT 1 FROM [SPdb_Exp2].[dbo].[ProcessDetail] PD 
                            WHERE PD.PartNo = T.PartNo AND PD.ProcessAreaNo = '6'
                        )
                    GROUP BY T.[TranDate]
                    
                    ORDER BY Plant, TranDate DESC;";

                    var liveData = await connection.QueryAsync<ProductionDataModel>(query, new { txt_StartDate = startDate, txt_EndDate = endDate }, commandTimeout: 300);

                    /* SAVE TO CACHE (If Historical) */
                    if (isHistorical && liveData.Any())
                    {
                        // Remove old cache for this range to prevent duplicates
                        string deleteQuery = "DELETE FROM [Brazing].[dbo].[iot_pdvolume] WHERE TranDate BETWEEN @txt_StartDate AND @txt_EndDate";
                        await connection.ExecuteAsync(deleteQuery, new { txt_StartDate = startDate, txt_EndDate = endDate });

                        // Batch Insert
                        string insertQuery = @"
                            INSERT INTO [Brazing].[dbo].[iot_pdvolume] (TranDate, Plant, TranQty)
                            VALUES (@TranDate, @Plant, @TranQty)";

                        await connection.ExecuteAsync(insertQuery, liveData);
                    }

                    return liveData;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ElectricChartRepository.GetProductionDataAsync: {ex.Message}");
                throw;
            }
        }
    }
}
