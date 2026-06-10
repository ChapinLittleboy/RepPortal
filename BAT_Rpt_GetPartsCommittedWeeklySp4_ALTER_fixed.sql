/****** Object: Procedure [dbo].[BAT_Rpt_GetPartsCommittedWeeklySp4] ******/
USE [BAT_App];
GO
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
ALTER PROCEDURE [dbo].[BAT_Rpt_GetPartsCommittedWeeklySp4](
  @type nvarchar(6)
 ,@Date datetime
 ,@Whse WhseType= null
 ,@Site nvarchar(6)= 'BAT'
 ,@IncludeKentShipSite int = 0
 ,@ExcludeCXand58 int = 0
 )
AS
Declare @beg_date   Date 
Declare @120date   Date
Declare @Wk1date   Date
Declare @Wk2date   Date
Declare @Wk3date   Date
Declare @Wk4date   Date
--Declare @type nvarchar(6)

set @beg_date = @Date

--set @type = 'WKLY'

If @type = 'WKLY'
  BEGIN
    set @120date = Dateadd(
                     dd
                    ,120
                    ,@beg_date)
    set @Wk1date = Dateadd(
                     dd
                    ,7
                    ,@beg_date)
    set @Wk2date = Dateadd(
                     dd
                    ,14
                    ,@beg_date)
    set @Wk3date = Dateadd(
                     dd
                    ,21
                    ,@beg_date)
    set @Wk4date = Dateadd(
                     dd
                    ,28
                    ,@beg_date)
  END

If @type = 'DLY'
  BEGIN
    set @120date = Dateadd(
                     dd
                    ,120
                    ,@beg_date)

    if datepart(
         dw
        ,@beg_date) = 3
      BEGIN
        set @Wk1date = Dateadd(
                         dd
                        ,1
                        ,@beg_date)
        set @Wk2date = Dateadd(
                         dd
                        ,2
                        ,@beg_date)
        set @Wk3date = Dateadd(
                         dd
                        ,3
                        ,@beg_date)
        set @Wk4date = Dateadd(
                         dd
                        ,6
                        ,@beg_date)
      END

    if datepart(
         dw
        ,@beg_date) = 4
      BEGIN
        set @Wk1date = Dateadd(
                         dd
                        ,1
                        ,@beg_date)
        set @Wk2date = Dateadd(
                         dd
                        ,2
                        ,@beg_date)
        set @Wk3date = Dateadd(
                         dd
                        ,5
                        ,@beg_date)
        set @Wk4date = Dateadd(
                         dd
                        ,6
                        ,@beg_date)
      END

    if datepart(
         dw
        ,@beg_date) = 5
      BEGIN
        set @Wk1date = Dateadd(
                         dd
                        ,1
                        ,@beg_date)
        set @Wk2date = Dateadd(
                         dd
                        ,4
                        ,@beg_date)
        set @Wk3date = Dateadd(
                         dd
                        ,5
                        ,@beg_date)
        set @Wk4date = Dateadd(
                         dd
                        ,6
                        ,@beg_date)
      END

    if datepart(
         dw
        ,@beg_date) = 6
      BEGIN
        set @Wk1date = Dateadd(
                         dd
                        ,3
                        ,@beg_date)
        set @Wk2date = Dateadd(
                         dd
                        ,4
                        ,@beg_date)
        set @Wk3date = Dateadd(
                         dd
                        ,5
                        ,@beg_date)
        set @Wk4date = Dateadd(
                         dd
                        ,6
                        ,@beg_date)
      END

    if datepart(
         dw
        ,@beg_date) = 7
      BEGIN
        set @Wk1date = Dateadd(
                         dd
                        ,2
                        ,@beg_date)
        set @Wk2date = Dateadd(
                         dd
                        ,3
                        ,@beg_date)
        set @Wk3date = Dateadd(
                         dd
                        ,4
                        ,@beg_date)
        set @Wk4date = Dateadd(
                         dd
                        ,5
                        ,@beg_date)
      END

    if datepart(
         dw
        ,@beg_date) in ('1'
                       ,'2')
      BEGIN
        set @Wk1date = Dateadd(
                         dd
                        ,1
                        ,@beg_date)
        set @Wk2date = Dateadd(
                         dd
                        ,2
                        ,@beg_date)
        set @Wk3date = Dateadd(
                         dd
                        ,3
                        ,@beg_date)
        set @Wk4date = Dateadd(
                         dd
                        ,4
                        ,@beg_date)
      END
  END

/*
PRINT datepart(dw, @beg_date)
PRINT @beg_date
PRINT @Wk1date
PRINT @Wk2date
PRINT @Wk3date
PRINT @Wk4date
*/
If @Site = 'BAT'
  BEGIN    ;

With
       WCP
       as
         (Select
           coitem.item
          ,item.description
					,item.stat
          ,item.p_m_t_code
          ,item.plan_code
          ,Case
             When Coalesce(
                    due_date
                   ,promise_date) <= @120date
             then
               qty_ordered - qty_shipped
             else
               0
           end as Qty120Day
          ,Case
             When Coalesce(
                    due_date
                   ,promise_date) <= @beg_date
             then
               qty_ordered - qty_shipped
             else
               0
           end as BK1
          ,Case
             When Coalesce(
                    due_date
                   ,promise_date) <= @Wk1date and
                  Coalesce(
                    due_date
                   ,promise_date) > @beg_date
             then
               qty_ordered - qty_shipped
             else
               0
           end as BK2
          ,Case
             When Coalesce(
                    due_date
                   ,promise_date) <= @Wk2date and
                  Coalesce(
                    due_date
                   ,promise_date) > @Wk1date
             then
               qty_ordered - qty_shipped
             else
               0
           end as BK3
          ,Case
             When Coalesce(
                    due_date
                   ,promise_date) <= @Wk3date and
                  Coalesce(
                    due_date
                   ,promise_date) > @Wk2date
             then
               qty_ordered - qty_shipped
             else
               0
           end as BK4
          ,Case
             When Coalesce(
                    due_date
                   ,promise_date) <= @Wk4date and
                  Coalesce(
                    due_date
                   ,promise_date) > @Wk3date
             then
               qty_ordered - qty_shipped
             else
               0
           end as BK5
          from
          bat_app.dbo.coitem_mst coitem
          Join bat_app.dbo.co_mst co on co.co_num = coitem.co_num
          Join bat_app.dbo.item_mst item
            on item.item = coitem.item and
               (left(
                  product_code
                 ,2) not in ('F0'
                            ,'F1'
                            ,'F2'
                            ,'F3'
                            ,'F4'
                            ,'F5') or
                product_code in ('F415', 'F400')) and
               (substring(
                  item.item
                 ,2
                 ,1) = '-' or
                item.family_code in ('PTS'
                                    ,'MFG')
                                    or item.item = '4993')
          Where
           coitem.stat not in ('C'
                              ,'F'
                              ,'H') and
           co.stat not in ('C'
                          ,'W') and
           coitem.item not like 'Q%' and
           coitem.item <> 'EDIITEM' and
           qty_ordered <> 0 and
           qty_ordered - qty_shipped <> 0
               AND (
        ship_site = 'BAT' OR
        @IncludeKentShipSite = 1
    )
           ),
       JOB
       as
         (Select
           job.item
          ,CONCAT(LTRIM(RTRIM(CONVERT(nvarchar(30), job.job))), '-', LTRIM(RTRIM(CONVERT(nvarchar(10), job.suffix)))) as JobOrder
          ,(Select
             max(start_date)
            from
            bat_app.dbo.job_sch_mst jobsch
            where
             jobsch.job = job.job AND
             jobsch.suffix = job.suffix) as Job_Date
          ,(Select
             max(end_date)
            from
            bat_app.dbo.job_sch_mst jobsch
            where
             jobsch.job = job.job AND
             jobsch.suffix = job.suffix) as End_Date
          from
          bat_app.dbo.job_mst job
          Join bat_app.dbo.item_mst item
            on item.item = job.item and
               (left(
                  product_code
                 ,2) not in ('F0'
                            ,'F1'
                            ,'F2'
                            ,'F3'
                            ,'F4'
                            ,'F5') or
                product_code in ('F415', 'F400'))
          where
           (job.stat = 'R' or
            (job.stat = 'F' AND
             job.type <> 'S')) --and job.item = '20027'
                   and job.job <> '[SYMIX]000000001'             --order by item
       ),
       WOH
       as
         (SELECT 
    il.item, 
    SUM(il.qty_on_hand) AS onhand
FROM 
    itemloc_mst il
WHERE 
           (il.whse = @Whse or  @Whse is null)
               AND (
        @ExcludeCXand58 = 0
        OR (il.loc NOT LIKE '58%' AND il.loc not like 'Cx%' AND @ExcludeCXand58 = 1)
    )
GROUP BY 
    il.item
          )
     Select
      wcp.item
			,wcp.stat
     ,description
     ,wcp.p_m_t_code
     ,wcp.plan_code
     ,woh.onhand
     ,sum(qty120day) as qty120day
     ,Case
        when Nullif(
               sum(bk1)
              ,0) <> 0
        then
          woh.onhand - sum(bk1)
        else
          Coalesce(
            Nullif(
              sum(bk1)
             ,0)
           ,woh.onhand)
      end as bk1
     ,Case
        when Nullif(
               sum(bk1 + bk2)
              ,0) <> 0
        then
          woh.onhand - sum(bk1 + bk2)
        else
          Coalesce(
            Nullif(
              sum(bk2)
             ,0)
           ,woh.onhand)
      end as bk2
     ,Case
        when Nullif(
               sum(bk1 + bk2 + bk3)
              ,0) <> 0
        then
          woh.onhand - sum(bk1 + bk2 + bk3)
        else
          Coalesce(
            Nullif(
              sum(bk3)
             ,0)
           ,woh.onhand)
      end as bk3
     ,Case
        when Nullif(
               sum(bk1 + bk2 + bk3 + bk4)
              ,0) <> 0
        then
          woh.onhand - sum(bk1 + bk2 + bk3 + bk4)
        else
          Coalesce(
            Nullif(
              sum(bk4)
             ,0)
           ,woh.onhand)
      end as bk4
     ,Case
        when Nullif(
               sum(bk1 + bk2 + bk3 + bk4 + bk5)
              ,0) <> 0
        then
          woh.onhand - sum(bk1 + bk2 + bk3 + bk4 + bk5)
        else
          Coalesce(
            Nullif(
              sum(bk5)
             ,0)
           ,woh.onhand)
      end as bk5
     ,Coalesce(
        (Select
          top 1 JOB.JobOrder
         from
         JOB
         where
          JOB.item = WCP.item)
       ,'') as JobOrder
     ,Coalesce(
        (Select
          top 1 JOB.Job_Date
         from
         JOB
         where
          JOB.item = WCP.item)
       ,null) as JobDate
     ,Coalesce(
        (Select
          top 1 JOB.End_Date
         from
         JOB
         where
          JOB.item = WCP.item)
       ,null) as EndDate
     from
      WCP Join WOH on WOH.item = WCP.item
     group by
     WCP.item
		 ,wcp.stat
    ,description
    ,wcp.p_m_t_code, wcp.plan_code
    ,WOH.onhand
     having
      sum(qty120day) > WOH.onhand
     order by
      WCP.item
  END -- end of 'BAT'

IF @Site = 'KENT'
  BEGIN    ;

With
       WCP
       as
         (Select
           coitem.item
          ,item.description
					,item.stat
          ,item.p_m_t_code
          ,item.plan_code
          ,Case
             When Coalesce(
                    due_date
                   ,promise_date) <= @120date
             then
               qty_ordered - qty_shipped
             else
               0
           end as Qty120Day
          ,Case
             When Coalesce(
                    due_date
                   ,promise_date) <= @beg_date
             then
               qty_ordered - qty_shipped
             else
               0
           end as BK1
          ,Case
             When Coalesce(
                    due_date
                   ,promise_date) <= @Wk1date and
                  Coalesce(
                    due_date
                   ,promise_date) > @beg_date
             then
               qty_ordered - qty_shipped
             else
               0
           end as BK2
          ,Case
             When Coalesce(
                    due_date
                   ,promise_date) <= @Wk2date and
                  Coalesce(
                    due_date
                   ,promise_date) > @Wk1date
             then
               qty_ordered - qty_shipped
             else
               0
           end as BK3
          ,Case
             When Coalesce(
                    due_date
                   ,promise_date) <= @Wk3date and
                  Coalesce(
                    due_date
                   ,promise_date) > @Wk2date
             then
               qty_ordered - qty_shipped
             else
               0
           end as BK4
          ,Case
             When Coalesce(
                    due_date
                   ,promise_date) <= @Wk4date and
                  Coalesce(
                    due_date
                   ,promise_date) > @Wk3date
             then
               qty_ordered - qty_shipped
             else
               0
           end as BK5
          from
          kent_app.dbo.coitem_mst coitem
          Join kent_app.dbo.co_mst co on co.co_num = coitem.co_num
          Join kent_app.dbo.item_mst item
            on item.item = coitem.item and
               (left(
                  product_code
                 ,2) not in ('F0'
                            ,'F1'
                            ,'F2'
                            ,'F3'
                            ,'F4'
                            ,'F5') or
                product_code in ('F415', 'F400')) and
               (substring(
                  item.item
                 ,2
                 ,1) = '-' or
                item.family_code in ('PTS'
                                    ,'MFG')
                  or item.item = '4993'                  
                                    )
          Where
           coitem.stat not in ('C'
                              ,'F'
                              ,'H') and
           co.stat not in ('C'
                          ,'W') and
           coitem.item not like 'Q%' and
           coitem.item <> 'EDIITEM' and
           qty_ordered <> 0 and
           qty_ordered - qty_shipped <> 0),
       JOB
       as
         (Select
           job.item
          ,CONCAT(LTRIM(RTRIM(CONVERT(nvarchar(30), job.job))), '-', LTRIM(RTRIM(CONVERT(nvarchar(10), job.suffix)))) as JobOrder
          ,(Select
             max(start_date)
            from
            kent_app.dbo.job_sch_mst jobsch
            where
             jobsch.job = job.job AND
             jobsch.suffix = job.suffix) as Job_Date
          ,(Select
             max(end_date)
            from
            kent_app.dbo.job_sch_mst jobsch
            where
             jobsch.job = job.job AND
             jobsch.suffix = job.suffix) as End_Date
          from
          kent_app.dbo.job_mst job
          Join kent_app.dbo.item_mst item
            on item.item = job.item and
               (left(
                  product_code
                 ,2) not in ('F0'
                            ,'F1'
                            ,'F2'
                            ,'F3'
                            ,'F4'
                            ,'F5') or
                product_code in ('F415', 'F400')
                )
          where
           (job.stat = 'R' or
            (job.stat = 'F' AND
             job.type <> 'S')) --and job.item = '20027'
                               --order by item
       ),
       WOH
       as
         (Select
           item
          ,sum(qty_on_hand) as onhand
          from
          kent_app.dbo.itemwhse_mst
          --where whse = 'WH1'
          --GDK 12/14/21 Add Whse
          --where whse = 'BAT'
          where
           whse = @Whse or
           @Whse is null
          group by
          item)
     Select
      wcp.item
     ,description
		 ,wcp.stat
     ,wcp.p_m_t_code
     ,wcp.plan_code
     ,woh.onhand
     ,sum(qty120day) as qty120day
     ,Case
        when Nullif(
               sum(bk1)
              ,0) <> 0
        then
          woh.onhand - sum(bk1)
        else
          Coalesce(
            Nullif(
              sum(bk1)
             ,0)
           ,woh.onhand)
      end as bk1
     ,Case
        when Nullif(
               sum(bk1 + bk2)
              ,0) <> 0
        then
          woh.onhand - sum(bk1 + bk2)
        else
          Coalesce(
            Nullif(
              sum(bk2)
             ,0)
           ,woh.onhand)
      end as bk2
     ,Case
        when Nullif(
               sum(bk1 + bk2 + bk3)
              ,0) <> 0
        then
          woh.onhand - sum(bk1 + bk2 + bk3)
        else
          Coalesce(
            Nullif(
              sum(bk3)
             ,0)
           ,woh.onhand)
      end as bk3
     ,Case
        when Nullif(
               sum(bk1 + bk2 + bk3 + bk4)
              ,0) <> 0
        then
          woh.onhand - sum(bk1 + bk2 + bk3 + bk4)
        else
          Coalesce(
            Nullif(
              sum(bk4)
             ,0)
           ,woh.onhand)
      end as bk4
     ,Case
        when Nullif(
               sum(bk1 + bk2 + bk3 + bk4 + bk5)
              ,0) <> 0
        then
          woh.onhand - sum(bk1 + bk2 + bk3 + bk4 + bk5)
        else
          Coalesce(
            Nullif(
              sum(bk5)
             ,0)
           ,woh.onhand)
      end as bk5
     ,Coalesce(
        (Select
          top 1 JOB.JobOrder
         from
         JOB
         where
          JOB.item = WCP.item )
       ,'') as JobOrder
     ,Coalesce(
        (Select
          top 1 JOB.Job_Date
         from
         JOB
         where
          JOB.item = WCP.item)
       ,null) as JobDate
     ,Coalesce(
        (Select
          top 1 JOB.End_Date
         from
         JOB
         where
          JOB.item = WCP.item)
       ,null) as EndDate
     from
      WCP Join WOH on WOH.item = WCP.item
     group by
     WCP.item
    ,description
		,wcp.stat
    ,wcp.p_m_t_code, wcp.plan_code
    ,WOH.onhand
     having
      sum(qty120day) > WOH.onhand
     order by
      WCP.item
  END -- end of 'KENT'
GO



