FROM --platform=$BUILDPLATFORM ubuntu:20.04 AS sdk-base
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    wget \
    gnupg \
    ca-certificates && \
    wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y --no-install-recommends \
    apt-transport-https \
    dotnet-sdk-8.0 \
    binutils-aarch64-linux-gnu \
    gcc-aarch64-linux-gnu \
    g++-aarch64-linux-gnu \
    && rm -f packages-microsoft-prod.deb
RUN dpkg --add-architecture arm64 && \
    echo "deb [arch=arm64] http://ports.ubuntu.com/ubuntu-ports focal main restricted universe multiverse" > /etc/apt/sources.list.d/arm64.list && \
    echo "deb [arch=arm64] http://ports.ubuntu.com/ubuntu-ports focal-updates main restricted universe multiverse" >> /etc/apt/sources.list.d/arm64.list && \
    echo "deb [arch=arm64] http://ports.ubuntu.com/ubuntu-ports focal-security main restricted universe multiverse" >> /etc/apt/sources.list.d/arm64.list && \
    sed -i -e 's/deb http/deb [arch=amd64] http/g' /etc/apt/sources.list && \
    sed -i -e 's/deb mirror/deb [arch=amd64] mirror/g' /etc/apt/sources.list
RUN apt-get update
RUN apt-get install -y \
    clang llvm binutils-aarch64-linux-gnu gcc-aarch64-linux-gnu zlib1g-dev zlib1g-dev:arm64
RUN rm -rf /var/lib/apt/lists/*

FROM --platform=$BUILDPLATFORM sdk-base AS sdk
ENV DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    PATH="${PATH}:~/.dotnet/tools"
WORKDIR /src
RUN dotnet tool install --global dotnet-subset --version 0.3.2

FROM --platform=$BUILDPLATFORM sdk AS prepare-restore

COPY . .
RUN dotnet subset restore RenovateWebhooks.sln --root-directory=. --output /restore-files

FROM --platform=$BUILDPLATFORM sdk AS build
ARG TARGETARCH
COPY . .
COPY --from=prepare-restore /restore-files/ ./
RUN dotnet restore ./RenovateWebhooks/RenovateWebhooks.csproj -a ${TARGETARCH} --no-cache
COPY . .
RUN dotnet build ./RenovateWebhooks/RenovateWebhooks.csproj -a ${TARGETARCH} -c Release
RUN dotnet publish ./RenovateWebhooks/RenovateWebhooks.csproj -a ${TARGETARCH} --no-build --no-restore --output /artifacts

FROM renovate/renovate:37.256.1 as final
COPY --from=build /artifacts /app
WORKDIR /app
ENTRYPOINT ["./RenovateWebhooks"]
