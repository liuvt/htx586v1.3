FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore HTX586CONTRACT.slnx
RUN dotnet publish src/HTX586CONTRACT.Web/HTX586CONTRACT.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Chỉ cài font/fontconfig cho SkiaSharp vẽ tiếng Việt; không cài Word hoặc LibreOffice.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 fonts-liberation \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
RUN mkdir -p /app/wwwroot/uploads/contracts

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "HTX586CONTRACT.Web.dll"]
