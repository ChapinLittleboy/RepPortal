# RepPortal TRN ION configuration

RepPortal now routes its existing legacy MGRest IDO requests through the MT Cloud TRN ION API Gateway. The non-secret TRN gateway, tenant, OAuth endpoint, and BAT Mongoose configuration are defined in `appsettings.json`.

The four secret values must not be added to `appsettings.json` or committed to Git. For local development, copy them from the approved `BLTest.ionapi` profile into .NET user-secrets:

```powershell
dotnet user-secrets set "Csi:ClientId" "<ci value>"
dotnet user-secrets set "Csi:ClientSecret" "<cs value>"
dotnet user-secrets set "Csi:ServiceAccountKey" "<saak value>"
dotnet user-secrets set "Csi:ServiceAccountSecret" "<sask value>"
dotnet user-secrets set "Csi:UseApi" "true"
```

For IIS or another deployed host, use the equivalent environment variables or secret-provider keys:

```text
Csi__ClientId
Csi__ClientSecret
Csi__ServiceAccountKey
Csi__ServiceAccountSecret
Csi__UseApi=true
```

The working IonApiTester state identifies the BAT configuration as:

```text
KT7NT5T9DQD6T38U_TRN_BAT
```

No confirmed KENT configuration was present in the saved IonApiTester state. Leave `Csi:Configurations:KENT` blank until its TRN configuration name and permissions are verified. RepPortal skips KENT when that value is blank.

After setting the secrets, start RepPortal and exercise a low-volume customer or item lookup first. When `UseApi` is enabled, startup validation reports any missing ION setting before the first portal request.
