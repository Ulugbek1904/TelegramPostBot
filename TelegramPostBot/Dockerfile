# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# TelegramPostBot.csproj joylashgan joyga qarab moslang
COPY TelegramPostBot/TelegramPostBot.csproj TelegramPostBot/
RUN dotnet restore TelegramPostBot/TelegramPostBot.csproj

COPY . .
WORKDIR /src/TelegramPostBot
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TelegramPostBot.dll"]
