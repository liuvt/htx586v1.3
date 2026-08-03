# Build Fix V2.3 - ErrorsPage namespace

Fixes:
- CS0234: namespace `HTX586CONTRACT.Web.Components.Pages.Errors` not found.
- RZ10012: unexpected markup element `ErrorsPage`.

The patch restores `Components/Pages/Errors/ErrorsPage.razor` and adds the explicit namespace:

```razor
@namespace HTX586CONTRACT.Web.Components.Pages.Errors
```

Copy the `src` folder over the project root, then run:

```bat
dotnet clean
for /d /r %%d in (bin,obj) do @if exist "%%d" rd /s /q "%%d"
dotnet restore
dotnet build
```
