# Chap SureShip history loader

Loads historical BAT and KENT SureShip BOL data from the on-premises databases into
the MT custom IDOs `ue_Chap_SS_History_BOLs` and
`ue_Chap_SS_History_BOLOrders` through ION/MGRest.

The default is a read-only 100-row-per-stage dry run:

```powershell
dotnet run --project Tools\ChapSureShipHistoryLoader
```

To load all data in restartable batches:

```powershell
dotnet run --project Tools\ChapSureShipHistoryLoader -- --commit
```

If the configured `Sites` connections intentionally omit the SQL password and your
Windows account has source read access, add `--source-integrated-security`.

Useful scoped examples:

```powershell
dotnet run --project Tools\ChapSureShipHistoryLoader -- --sites BAT --stage headers --max-rows 1000
dotnet run --project Tools\ChapSureShipHistoryLoader -- --commit --sites BAT,KENT --batch-size 100 --start-date 2025-01-01 --end-date-exclusive 2026-01-01
```

Commit mode reads the same `Csi` and `Sites` settings as RepPortal, including
RepPortal's user secrets. Before inserting each batch it loads the existing target
keys and skips them. After a successful batch it atomically updates
`artifacts\chap-sureship-history-loader\checkpoint.json`. If interrupted, run the
same command again to resume. Separate cursors are maintained automatically for
each date-filter combination.

Run with `--help` for all options. The batch size is capped at 500 to keep ION
requests bounded. No delete or update actions are issued; the loader only inserts
missing keys. Multiple inserts use Mongoose's `POST json/{IDO}/additems` API.
