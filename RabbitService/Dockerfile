#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["RabbitService/RabbitService.csproj", "RabbitService/"]
RUN dotnet restore "RabbitService/RabbitService.csproj"
COPY . .
WORKDIR "/src/RabbitService"
RUN dotnet build "RabbitService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "RabbitService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RabbitService.dll"]