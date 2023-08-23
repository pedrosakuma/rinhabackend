#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0-alpine AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine AS build
RUN apk add --no-cache libstdc++
WORKDIR /src
COPY . .

FROM build AS publish
RUN dotnet publish "RinhaBackend.csproj" -c Release -o /app/publish -r linux-musl-x64 --self-contained

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./RinhaBackend"]