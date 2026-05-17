FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/GDD.Core/GDD.Core.csproj src/GDD.Core/
COPY src/GDD.Headless/GDD.Headless.csproj src/GDD.Headless/
RUN dotnet restore src/GDD.Headless/GDD.Headless.csproj
COPY src/ src/
COPY GDD-MANUAL.md .
RUN dotnet publish src/GDD.Headless/GDD.Headless.csproj -c Release -o /app --no-restore
RUN dotnet tool install --global PowerShell
ENV PATH="$PATH:/root/.dotnet/tools"
ENV PLAYWRIGHT_BROWSERS_PATH=/app/.browsers
RUN pwsh /app/playwright.ps1 install chromium

FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update && apt-get install -y --no-install-recommends \
    libnss3 libnspr4 libatk1.0-0 libatk-bridge2.0-0 libcups2 libdrm2 \
    libxkbcommon0 libxcomposite1 libxdamage1 libxrandr2 libgbm1 \
    libpango-1.0-0 libcairo2 libasound2 libxshmfence1 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV PLAYWRIGHT_BROWSERS_PATH=/app/.browsers
EXPOSE 9700
ENTRYPOINT ["dotnet", "GDD.Headless.dll", "--headless"]
