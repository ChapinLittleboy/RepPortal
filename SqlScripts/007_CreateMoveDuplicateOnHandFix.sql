USE [Bat_App];
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'[dbo].[MoveDuplicateOnHandFixLog]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[MoveDuplicateOnHandFixLog] (
        [Id] bigint IDENTITY(1, 1) NOT NULL,
        [RunId] uniqueidentifier NOT NULL,
        [RunDate] datetime2(0) NOT NULL CONSTRAINT [DF_MoveDuplicateOnHandFixLog_RunDate] DEFAULT (SYSDATETIME()),
        [ApplyChanges] bit NOT NULL,
        [Applied] bit NOT NULL,
        [OriginalTransNum] int NOT NULL,
        [DuplicateTransNum] int NOT NULL,
        [OriginalRowPointer] uniqueidentifier NULL,
        [DuplicateRowPointer] uniqueidentifier NULL,
        [TransType] nvarchar(10) NOT NULL,
        [Item] nvarchar(60) NOT NULL,
        [Whse] nvarchar(10) NOT NULL,
        [Loc] nvarchar(40) NOT NULL,
        [RefType] nvarchar(10) NOT NULL,
        [CreatedBy] nvarchar(128) NULL,
        [MoveQty] decimal(19, 8) NOT NULL,
        [OriginalRecordDate] datetime NULL,
        [DuplicateRecordDate] datetime NULL,
        [OriginalCreateDate] datetime NULL,
        [DuplicateCreateDate] datetime NULL,
        [CreateDateSecondsApart] int NOT NULL,
        [QtyOnHandBefore] decimal(19, 8) NULL,
        [QtyOnHandAfter] decimal(19, 8) NULL,
        [Message] nvarchar(4000) NULL,
        CONSTRAINT [PK_MoveDuplicateOnHandFixLog] PRIMARY KEY CLUSTERED ([Id])
    );
END;
GO

IF COL_LENGTH(N'dbo.MoveDuplicateOnHandFixLog', N'OriginalRowPointer') IS NULL
BEGIN
    ALTER TABLE [dbo].[MoveDuplicateOnHandFixLog]
    ADD [OriginalRowPointer] uniqueidentifier NULL;
END;
GO

IF COL_LENGTH(N'dbo.MoveDuplicateOnHandFixLog', N'DuplicateRowPointer') IS NULL
BEGIN
    ALTER TABLE [dbo].[MoveDuplicateOnHandFixLog]
    ADD [DuplicateRowPointer] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'UX_MoveDuplicateOnHandFixLog_DuplicateTransNum_Applied'
      AND [object_id] = OBJECT_ID(N'[dbo].[MoveDuplicateOnHandFixLog]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_MoveDuplicateOnHandFixLog_DuplicateTransNum_Applied]
    ON [dbo].[MoveDuplicateOnHandFixLog] ([DuplicateTransNum])
    WHERE [Applied] = 1;
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_FixDuplicateMoveOnHandQty]
    @ApplyChanges bit = 0,
    @StartCreateDate datetime = NULL,
    @EndCreateDate datetime = NULL,
    @UseLastAppliedLog bit = 1,
    @CreatedBy nvarchar(128) = N'Truck'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @RunId uniqueidentifier = NEWID();

    SET @EndCreateDate = ISNULL(@EndCreateDate, GETDATE());

    IF @StartCreateDate IS NULL
    BEGIN
        SELECT @StartCreateDate = MAX([DuplicateCreateDate])
        FROM [dbo].[MoveDuplicateOnHandFixLog]
        WHERE [Applied] = 1;

        IF @StartCreateDate IS NULL OR @UseLastAppliedLog = 0
        BEGIN
            SET @StartCreateDate = DATEADD(day, -1, @EndCreateDate);
        END;
    END;

    IF OBJECT_ID('tempdb..#DuplicateMoves') IS NOT NULL
    BEGIN
        DROP TABLE #DuplicateMoves;
    END;

    ;WITH CandidateMoves AS (
        SELECT
            mt.[trans_num],
            mt.[RowPointer],
            mt.[trans_type],
            mt.[item],
            CAST(mt.[qty] AS decimal(19, 8)) AS [qty],
            mt.[whse],
            mt.[loc],
            mt.[ref_type],
            mt.[RecordDate],
            mt.[createdate],
            mt.[CreatedBy],
            LAG(mt.[trans_num]) OVER (
                PARTITION BY mt.[trans_type], mt.[item], mt.[qty], mt.[whse], mt.[loc], mt.[ref_type], mt.[CreatedBy]
                ORDER BY mt.[createdate], mt.[trans_num]
            ) AS [OriginalTransNum],
            LAG(mt.[RowPointer]) OVER (
                PARTITION BY mt.[trans_type], mt.[item], mt.[qty], mt.[whse], mt.[loc], mt.[ref_type], mt.[CreatedBy]
                ORDER BY mt.[createdate], mt.[trans_num]
            ) AS [OriginalRowPointer],
            LAG(mt.[RecordDate]) OVER (
                PARTITION BY mt.[trans_type], mt.[item], mt.[qty], mt.[whse], mt.[loc], mt.[ref_type], mt.[CreatedBy]
                ORDER BY mt.[createdate], mt.[trans_num]
            ) AS [OriginalRecordDate],
            LAG(mt.[createdate]) OVER (
                PARTITION BY mt.[trans_type], mt.[item], mt.[qty], mt.[whse], mt.[loc], mt.[ref_type], mt.[CreatedBy]
                ORDER BY mt.[createdate], mt.[trans_num]
            ) AS [OriginalCreateDate]
        FROM [dbo].[matltran_mst] mt
        WHERE mt.[trans_type] = 'M'
          AND mt.[ref_type] = 'I'
          AND (@CreatedBy IS NULL OR mt.[CreatedBy] = @CreatedBy)
          AND mt.[qty] > 0
          AND mt.[createdate] >= DATEADD(second, -6, @StartCreateDate)
          AND mt.[createdate] < @EndCreateDate
    )
    SELECT
        [OriginalTransNum],
        [trans_num] AS [DuplicateTransNum],
        [OriginalRowPointer],
        [RowPointer] AS [DuplicateRowPointer],
        [trans_type] AS [TransType],
        [item] AS [Item],
        [whse] AS [Whse],
        [loc] AS [Loc],
        [ref_type] AS [RefType],
        [CreatedBy],
        [qty] AS [MoveQty],
        [OriginalRecordDate],
        [RecordDate] AS [DuplicateRecordDate],
        [OriginalCreateDate],
        [createdate] AS [DuplicateCreateDate],
        DATEDIFF(second, [OriginalCreateDate], [createdate]) AS [CreateDateSecondsApart],
        CAST(NULL AS decimal(19, 8)) AS [QtyOnHandBefore],
        CAST(NULL AS decimal(19, 8)) AS [QtyOnHandAfter],
        CAST(NULL AS nvarchar(4000)) AS [Message]
    INTO #DuplicateMoves
    FROM CandidateMoves
    WHERE [OriginalTransNum] IS NOT NULL
      AND DATEDIFF(second, [OriginalCreateDate], [createdate]) BETWEEN 0 AND 6
      AND [createdate] >= @StartCreateDate
      AND NOT EXISTS (
          SELECT 1
          FROM [dbo].[MoveDuplicateOnHandFixLog] log
          WHERE log.[DuplicateTransNum] = CandidateMoves.[trans_num]
            AND log.[Applied] = 1
      );

    IF OBJECT_ID('tempdb..#DuplicateMoveItemLocTotals') IS NOT NULL
    BEGIN
        DROP TABLE #DuplicateMoveItemLocTotals;
    END;

    SELECT
        [Whse],
        [Item],
        [Loc],
        SUM([MoveQty]) AS [TotalMoveQty]
    INTO #DuplicateMoveItemLocTotals
    FROM #DuplicateMoves
    GROUP BY [Whse], [Item], [Loc];

    UPDATE dm
    SET
        dm.[QtyOnHandBefore] = il.[qty_on_hand],
        dm.[QtyOnHandAfter] = il.[qty_on_hand] - totals.[TotalMoveQty],
        dm.[Message] = CASE
            WHEN il.[item] IS NULL THEN 'No matching itemloc_mst row was found; no update can be applied.'
            ELSE 'Ready.'
        END
    FROM #DuplicateMoves dm
    INNER JOIN #DuplicateMoveItemLocTotals totals
        ON totals.[Whse] = dm.[Whse]
       AND totals.[Item] = dm.[Item]
       AND totals.[Loc] = dm.[Loc]
    LEFT JOIN [dbo].[itemloc_mst] il
        ON il.[whse] = dm.[Whse]
       AND il.[item] = dm.[Item]
       AND il.[loc] = dm.[Loc];

    IF @ApplyChanges = 0
    BEGIN
        SELECT
            @RunId AS [RunId],
            @ApplyChanges AS [ApplyChanges],
            @CreatedBy AS [CreatedByFilter],
            @StartCreateDate AS [StartCreateDate],
            @EndCreateDate AS [EndCreateDate],
            [OriginalTransNum],
            [DuplicateTransNum],
            [OriginalRowPointer],
            [DuplicateRowPointer],
            [TransType],
            [Item],
            [Whse],
            [Loc],
            [RefType],
            [CreatedBy],
            [MoveQty],
            [OriginalRecordDate],
            [DuplicateRecordDate],
            [OriginalCreateDate],
            [DuplicateCreateDate],
            [CreateDateSecondsApart],
            [QtyOnHandBefore],
            [QtyOnHandAfter],
            [Message]
        FROM #DuplicateMoves
        ORDER BY [DuplicateCreateDate], [DuplicateTransNum];

        RETURN;
    END;

    BEGIN TRANSACTION;

        UPDATE dm
        SET
            dm.[QtyOnHandBefore] = il.[qty_on_hand],
            dm.[QtyOnHandAfter] = il.[qty_on_hand] - totals.[TotalMoveQty],
            dm.[Message] = 'Applied.'
        FROM #DuplicateMoves dm
        INNER JOIN #DuplicateMoveItemLocTotals totals
            ON totals.[Whse] = dm.[Whse]
           AND totals.[Item] = dm.[Item]
           AND totals.[Loc] = dm.[Loc]
        INNER JOIN [dbo].[itemloc_mst] il WITH (UPDLOCK, HOLDLOCK)
            ON il.[whse] = dm.[Whse]
           AND il.[item] = dm.[Item]
           AND il.[loc] = dm.[Loc];

        UPDATE il
        SET il.[qty_on_hand] = il.[qty_on_hand] - totals.[TotalMoveQty]
        FROM [dbo].[itemloc_mst] il
        INNER JOIN #DuplicateMoveItemLocTotals totals
            ON totals.[Whse] = il.[whse]
           AND totals.[Item] = il.[item]
           AND totals.[Loc] = il.[loc];

        INSERT INTO [dbo].[MoveDuplicateOnHandFixLog] (
            [RunId],
            [ApplyChanges],
            [Applied],
            [OriginalTransNum],
            [DuplicateTransNum],
            [OriginalRowPointer],
            [DuplicateRowPointer],
            [TransType],
            [Item],
            [Whse],
            [Loc],
            [RefType],
            [CreatedBy],
            [MoveQty],
            [OriginalRecordDate],
            [DuplicateRecordDate],
            [OriginalCreateDate],
            [DuplicateCreateDate],
            [CreateDateSecondsApart],
            [QtyOnHandBefore],
            [QtyOnHandAfter],
            [Message]
        )
        SELECT
            @RunId,
            @ApplyChanges,
            CASE WHEN [Message] = 'Applied.' THEN 1 ELSE 0 END,
            [OriginalTransNum],
            [DuplicateTransNum],
            [OriginalRowPointer],
            [DuplicateRowPointer],
            [TransType],
            [Item],
            [Whse],
            [Loc],
            [RefType],
            [CreatedBy],
            [MoveQty],
            [OriginalRecordDate],
            [DuplicateRecordDate],
            [OriginalCreateDate],
            [DuplicateCreateDate],
            [CreateDateSecondsApart],
            [QtyOnHandBefore],
            [QtyOnHandAfter],
            [Message]
        FROM #DuplicateMoves;

    COMMIT TRANSACTION;

    SELECT
        @RunId AS [RunId],
        @ApplyChanges AS [ApplyChanges],
        @CreatedBy AS [CreatedByFilter],
        @StartCreateDate AS [StartCreateDate],
        @EndCreateDate AS [EndCreateDate],
        *
    FROM #DuplicateMoves
    ORDER BY [DuplicateCreateDate], [DuplicateTransNum];
END;
GO

/*
Preview the default window:

EXEC [dbo].[sp_FixDuplicateMoveOnHandQty]
    @ApplyChanges = 0;

Preview all CreatedBy values:

EXEC [dbo].[sp_FixDuplicateMoveOnHandQty]
    @ApplyChanges = 0,
    @CreatedBy = NULL;

Apply fixes for the default window:

EXEC [dbo].[sp_FixDuplicateMoveOnHandQty]
    @ApplyChanges = 1;

Preview a specific window:

EXEC [dbo].[sp_FixDuplicateMoveOnHandQty]
    @ApplyChanges = 0,
    @StartCreateDate = '2026-06-03T00:00:00',
    @EndCreateDate = '2026-06-04T00:00:00',
    @UseLastAppliedLog = 0,
    @CreatedBy = N'Truck';
*/
