#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0-preview-alpine AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0-preview-alpine AS build
RUN apk add --no-cache clang build-base zlib-dev libstdc++ grpc-plugins

ARG PROTOBUF_PROTOC=/usr/bin/protoc
ARG GRPC_PROTOC_PLUGIN=/usr/bin/grpc_csharp_plugin

WORKDIR /src
COPY "src/RinhaBackend" .

FROM build AS publish
RUN dotnet publish "RinhaBackend.csproj" -c Release -o /app/publish -r linux-musl-x64 --self-contained

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./RinhaBackend"]