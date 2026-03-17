DECLARE @txt_StartDate DATETIME = '2026-01-01';
DECLARE @txt_EndDate   DATETIME = '2026-01-31';

WITH CombinedData AS (
    -----------------------------------------------------------
    -- ส่วนที่ 1: Product Type 'S' (จาก ProductTran)
    -----------------------------------------------------------
    -- Database 1: SPdb_Exp (S)
    SELECT T.[TranDate]
          ,SUM(T.[TranQty]) AS TranQty
          ,'S' AS ProductType
    FROM [SPdb_Exp].[dbo].[ProductTran] T
    INNER JOIN [SPdb_Exp].[dbo].[Product] P ON P.[PartNo] = T.[PartNo]
    WHERE T.[TranType] = 'RP'
      AND T.[DInventoryNo] = '31'
      AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
      AND P.[ProductType] = 'S'
      AND EXISTS (SELECT 1 FROM [SPdb_Exp].[dbo].[ProcessDetail] PD WHERE PD.PartNo = T.PartNo AND PD.ProcessAreaNo = '6')
    GROUP BY T.[TranDate]

    UNION ALL

    -- Database 2: SPdb_Exp2 (S)
    SELECT T.[TranDate]
          ,SUM(T.[TranQty]) AS TranQty
          ,'S' AS ProductType
    FROM [SPdb_Exp2].[dbo].[ProductTran] T
    INNER JOIN [SPdb_Exp2].[dbo].[Product] P ON P.[PartNo] = T.[PartNo]
    WHERE T.[TranType] = 'RP'
      AND T.[DInventoryNo] = '31'
      AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
      AND P.[ProductType] = 'S'
      AND EXISTS (SELECT 1 FROM [SPdb_Exp2].[dbo].[ProcessDetail] PD WHERE PD.PartNo = T.PartNo AND PD.ProcessAreaNo = '6')
    GROUP BY T.[TranDate]

    UNION ALL

    -- Database 3: SPdb_Dom (S)
    SELECT T.[TranDate]
          ,SUM(T.[TranQty]) AS TranQty
          ,'S' AS ProductType
    FROM [SPdb_Dom].[dbo].[ProductTran] T
    INNER JOIN [SPdb_Dom].[dbo].[Product] P ON P.[PartNo] = T.[PartNo]
    WHERE T.[TranType] = 'RP'
      AND T.[DInventoryNo] = '31'
      AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
      AND P.[ProductType] = 'S'
      AND EXISTS (SELECT 1 FROM [SPdb_Dom].[dbo].[ProcessDetail] PD WHERE PD.PartNo = T.PartNo AND PD.ProcessAreaNo = '6')
    GROUP BY T.[TranDate]

    -----------------------------------------------------------
    -- ส่วนที่ 2: Product Type 'G' (Gleitmo) (จาก MaterialTran)
    -----------------------------------------------------------
    UNION ALL

    -- Database 1: SPdb_Exp (G)
    SELECT T.[TranDate]
          ,SUM(T.[TranQty]) AS TranQty
          ,'G' AS ProductType
    FROM [SPdb_Exp].[dbo].materialtran T
    WHERE T.trantype = 'RG'
      AND T.DInventoryNo = '11'
      AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
    GROUP BY T.[TranDate]

    UNION ALL

    -- Database 2: SPdb_Exp2 (G)
    SELECT T.[TranDate]
          ,SUM(T.[TranQty]) AS TranQty
          ,'G' AS ProductType
    FROM [SPdb_Exp2].[dbo].materialtran T
    WHERE T.trantype = 'RG'
      AND T.DInventoryNo = '11'
      AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
    GROUP BY T.[TranDate]

    UNION ALL

    -- Database 3: SPdb_Dom (G)
    SELECT T.[TranDate]
          ,SUM(T.[TranQty]) AS TranQty
          ,'G' AS ProductType
    FROM [SPdb_Dom].[dbo].materialtran T
    WHERE T.trantype = 'RG'
      AND T.DInventoryNo = '11'
      AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
    GROUP BY T.[TranDate]
)

SELECT [TranDate]
      ,SUM([TranQty]) AS TotalTranQty
      ,[ProductType]
FROM CombinedData
GROUP BY [TranDate], [ProductType]
ORDER BY [TranDate] DESC, [ProductType] DESC;