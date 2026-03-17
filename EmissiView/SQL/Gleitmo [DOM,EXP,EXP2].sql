DECLARE @txt_StartDate DATETIME = '2026-01-01';
DECLARE @txt_EndDate   DATETIME = '2026-01-31';

WITH GleitmoData AS (
	-- Database 1: SPdb_Exp
	select 
	T.[TranDate]
	,SUM(T.[TranQty]) AS TranQty
	,'G' AS ProductType
	from [SPdb_Exp].[dbo].materialtran T
	where T.trantype = 'RG'
	and T.DInventoryNo = '11'
	AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
	GROUP BY T.[TranDate]

	UNION ALL

	-- Database 2: SPdb_Exp2
	select 
	T.[TranDate]
	,SUM(T.[TranQty]) AS TranQty
	,'G' AS ProductType
	from [SPdb_Exp2].[dbo].materialtran T
	where T.trantype = 'RG'
	and T.DInventoryNo = '11'
	AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
	GROUP BY T.[TranDate]

	UNION ALL
	-- Database 3: SPdb_Dom
	select 
	T.[TranDate]
	,SUM(T.[TranQty]) AS TranQty
	,'G' AS ProductType
	from [SPdb_Dom].[dbo].materialtran T
	where T.trantype = 'RG'
	and T.DInventoryNo = '11'
	AND T.[TranDate] BETWEEN @txt_StartDate AND @txt_EndDate
	GROUP BY T.[TranDate]
)
select [TranDate]
      ,SUM([TranQty]) AS TotalTranQty
      ,[ProductType] from GleitmoData
GROUP BY [TranDate], [ProductType]
ORDER BY [TranDate] DESC