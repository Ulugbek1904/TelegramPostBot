FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["TelegramPostBot/TelegramPostBot.csproj", "TelegramPostBot/"]
RUN dotnet restore "TelegramPostBot/TelegramPostBot.csproj"
COPY . .
WORKDIR "/src/TelegramPostBot"
RUN dotnet build "TelegramPostBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TelegramPostBot.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TelegramPostBot.dll"]
