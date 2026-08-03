```powershell
dotnet restore .\HTX586CONTRACT.slnx
dotnet build .\HTX586CONTRACT.slnx
dotnet run --project .\src\HTX586CONTRACT.Web\HTX586CONTRACT.Web.csproj --launch-profile https
```

## User secrets and run

```
dotnet user-secrets init --project src/HTX586CONTRACT.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..." --project src/HTX586CONTRACT.Web

dotnet clean
dotnet restore
dotnet build
dotnet ef migrations add Init --project src/HTX586CONTRACT.Infrastructure --startup-project src/HTX586CONTRACT.Web
dotnet ef database update --project src/HTX586CONTRACT.Infrastructure --startup-project src/HTX586CONTRACT.Web
dotnet watch run /a --project src/HTX586CONTRACT.Web
```
## Release

```
dotnet restore .\HTX586CONTRACT.slnx
dotnet build .\HTX586CONTRACT.slnx -c Release
dotnet publish .\src\HTX586CONTRACT.Web\HTX586CONTRACT.Web.csproj -c Release -o .\publish
```