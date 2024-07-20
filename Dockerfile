# Estágio base para a imagem final
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

# Estágio de build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MatchmakingWorker.csproj", "./"]
RUN dotnet restore "MatchmakingWorker.csproj"
COPY . .
RUN dotnet build "MatchmakingWorker.csproj" -c Release -o /app/build

# Estágio de publicação
FROM build AS publish
RUN dotnet publish "MatchmakingWorker.csproj" -c Release -o /app/publish

# Estágio final
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

COPY ./kube/config /root/.kube/config

ENTRYPOINT ["dotnet", "MatchmakingWorker.dll"]
