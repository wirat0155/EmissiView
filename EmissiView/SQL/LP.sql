DECLARE @txt_StartDate DATETIME = '2026-01-01';
DECLARE @txt_EndDate   DATETIME = '2026-01-31';

WITH CombinedData AS (
    -- 1. P32
    SELECT T.[TranDate]
          ,SUM(T.[TranQty]) AS TranQty
          ,'P' AS ProductType
    FROM [USUI].[dbo].[ProductTran] T
    INNER JOIN [USUI].[dbo].[Product] P ON P.[PartNo] = T.[PartNo]
    WHERE T.[TranType] = 'RP'
      AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
      AND T.[DInventoryNo] = '32' 
      AND P.[ProductType] = 'P'
    GROUP BY T.[TranDate], P.[ProductType]

    UNION ALL

    -- 2. S31 (Only UICT Parts)
    SELECT T.[TranDate]
          ,SUM(T.[TranQty]) AS TranQty
          ,'S-UICT' AS ProductType
    FROM [USUI].[dbo].[ProductTran] T
    INNER JOIN [USUI].[dbo].[Product] P ON P.[PartNo] = T.[PartNo]
    WHERE T.[TranType] = 'RP'
      AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
      AND T.[DInventoryNo] = '31' 
      AND P.[ProductType] = 'S'
	  -- ไม่เป็น PartMMTh
      AND NOT EXISTS (
          SELECT 1 
          FROM [USUI].[dbo].[PartMMth] M 
          WHERE M.PartNo = T.PartNo
      )
    GROUP BY T.[TranDate], P.[ProductType]

    UNION ALL

    -- 3. S32 MMTh
    SELECT T.[TranDate]
          ,SUM(T.[TranQty]) AS TranQty
          ,'S-MMTH' AS ProductType
    FROM [USUI].[dbo].[ProductTran] T
    INNER JOIN [USUI].[dbo].[Product] P ON P.[PartNo] = T.[PartNo]
	-- เป็น PartMMTh
    INNER JOIN [USUI].[dbo].[PartMMth] PM ON PM.[PartNo] = P.[PartNo]
    WHERE T.[TranType] = 'RP'
      AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
      AND T.[DInventoryNo] = '32' 
      AND P.[ProductType] = 'S'
    GROUP BY T.[TranDate], P.[ProductType]
)

SELECT [TranDate]
      ,[TranQty]
      ,[ProductType]
FROM CombinedData
ORDER BY 
    [TranDate] DESC,
    CASE [ProductType]
        WHEN 'P' THEN 1
        WHEN 'S-UICT' THEN 2
        WHEN 'S-MMTH' THEN 3
        ELSE 4 
    END ASC;