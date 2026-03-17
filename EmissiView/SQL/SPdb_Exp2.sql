DECLARE @txt_StartDate DATETIME = '2026-01-01';
DECLARE @txt_EndDate   DATETIME = '2026-01-31';

SELECT T.[TranDate]
      ,SUM(T.[TranQty]) AS TranQty
      ,P.[ProductType]
FROM [SPdb_Exp2].[dbo].[ProductTran] T
INNER JOIN [SPdb_Exp2].[dbo].[Product] P ON P.[PartNo] = T.[PartNo]
WHERE T.[TranType] = 'RP'
  AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
  -- ตัด Part ที่มี ProcessArea 6 (Plating) ออก
  AND NOT EXISTS (
      SELECT 1 
      FROM [SPdb_Exp2].[dbo].[ProcessDetail] PD 
      WHERE PD.PartNo = T.PartNo 
      AND PD.ProcessAreaNo = '6'
  )
  AND (
       (T.[DInventoryNo] = '32' AND P.[ProductType] = 'P')
       OR 
       (T.[DInventoryNo] = '31' AND P.[ProductType] = 'S')
  )

GROUP BY T.[TranDate], P.[ProductType]
ORDER BY T.[TranDate] DESC, P.[ProductType] ASC;