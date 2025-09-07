ARG DOTNET_VERSION=8.0
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS sdk
ENV DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    PATH="${PATH}:~/.dotnet/tools"
WORKDIR /src
RUN dpkg --add-architecture arm64 \
    && apt-get update \
    && apt-get install -y \
    clang \
    llvm \
    binutils-aarch64-linux-gnu \
    gcc-aarch64-linux-gnu \
    zlib1g-dev \
    zlib1g-dev:arm64 \
    && rm -rf /var/lib/apt/lists/*
RUN dotnet tool install --global dotnet-subset --version 0.3.2

FROM --platform=$BUILDPLATFORM sdk AS prepare-restore
COPY . .
RUN dotnet subset restore RenovateWebhooks.sln --root-directory=. --output /restore

FROM --platform=$BUILDPLATFORM sdk AS build
ARG TARGETARCH
COPY --from=prepare-restore /restore ./
RUN dotnet restore ./RenovateWebhooks/RenovateWebhooks.csproj -a ${TARGETARCH} --no-cache
COPY . .
RUN dotnet build ./RenovateWebhooks/RenovateWebhooks.csproj -a ${TARGETARCH} -c Release
RUN dotnet publish ./RenovateWebhooks/RenovateWebhooks.csproj -a ${TARGETARCH} --no-build --no-restore --output /artifacts

FROM renovate/renovate:37.256.1 AS final
COPY --from=build /artifacts /app
WORKDIR /app
ENTRYPOINT ["./RenovateWebhooks"]
