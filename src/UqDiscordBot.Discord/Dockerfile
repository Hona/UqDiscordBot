#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:5.0-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS build
WORKDIR /src
COPY ["src/UqDiscordBot.Discord/UqDiscordBot.Discord.csproj", "src/UqDiscordBot.Discord/"]
RUN dotnet restore "src/UqDiscordBot.Discord/UqDiscordBot.Discord.csproj"
COPY . .
WORKDIR "/src/src/UqDiscordBot.Discord"
RUN dotnet build "UqDiscordBot.Discord.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "UqDiscordBot.Discord.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UqDiscordBot.Discord.dll"]