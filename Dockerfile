FROM mcr.microsoft.com/dotnet/sdk:8.0-preview-alpine AS build
RUN apk add --no-cache clang build-base zlib-dev libstdc++ grpc-plugins
RUN dotnet dev-certs https -ep /https/aspnetapp.pfx -p password
ARG PROTOBUF_PROTOC=/usr/bin/protoc
ARG GRPC_PROTOC_PLUGIN=/usr/bin/grpc_csharp_plugin

WORKDIR /src
COPY src .

FROM build AS publish
RUN dotnet publish "RinhaBackend/RinhaBackend.csproj" -c Release -o /app/publish -r linux-musl-x64 --self-contained

FROM alpine AS final
RUN apk add --no-cache libstdc++
ENV \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

EXPOSE 80
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=build /https/* /https/
ENTRYPOINT ["./RinhaBackend"]