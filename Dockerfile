FROM --platform=$BUILDPLATFORM ubuntu:20.04 AS sdk-base
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    wget \
    gnupg \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*
RUN wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y --no-install-recommends \
    apt-transport-https \
    dotnet-sdk-8.0 \
    && rm -rf /var/lib/apt/lists/* \
    && rm -f packages-microsoft-prod.deb

FROM --platform=$BUILDPLATFORM sdk-base AS sdk
ENV DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    PATH="${PATH}:~/.dotnet/tools"
WORKDIR /src
RUN dotnet tool install --global dotnet-subset --version 0.3.2
RUN apt-get update && apt-get install -y --no-install-recommends \
    clang zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

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

FROM renovate/renovate:37.232.0 as final
COPY --from=build /artifacts /app
WORKDIR /app
ENTRYPOINT ["./RenovateWebhooks"]
