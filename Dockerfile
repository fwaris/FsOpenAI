# Base .NET SDK image
FROM mcr.microsoft.com/dotnet/sdk:9.0.304-bookworm-slim AS base

COPY --from=ext *.crt /usr/local/share/ca-certificates/

# Install core tools + update CA store
RUN apt-get update && apt-get install -y --no-install-recommends \
      ca-certificates curl gnupg unzip wget libtesseract-dev libc6-dev libjpeg-dev \
 && update-ca-certificates \
 && rm -rf /var/lib/apt/lists/*

# -------------------------------
# Build stage
# -------------------------------
FROM base AS build

# Install build deps (git, cmake, compiler toolchain)
RUN apt-get update && apt-get install -y --no-install-recommends \
      git cmake build-essential \
 && rm -rf /var/lib/apt/lists/*

RUN git clone --depth 1 --branch 1.85.0 https://github.com/DanBloomberg/leptonica.git /leptonica
WORKDIR /leptonica
RUN mkdir build
WORKDIR /leptonica/build
RUN cmake .. -DBUILD_SHARED_LIBS=ON
RUN cmake --build . --config Release
RUN cmake --install . --prefix /usr/local
RUN ln -s /lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/libdl.so
RUN ldconfig

# Copy source
WORKDIR /src
COPY src . 
WORKDIR /src/FsOpenAI.Server

# Restore & publish
# RUN dotnet restore
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore
RUN --mount=type=cache,target=/root/.nuget/packages \
      dotnet publish -c Release \
    -p:DefineConstants=UNAUTHENTICATED \
    -o /app --no-restore

FROM build AS final
EXPOSE 52037

ENV ASPNETCORE_URLS=http://+:52037
ENV HOME=/home

COPY --from=ext trainData/ ${HOME}/trainData/

WORKDIR /app

# Copy published app
COPY --from=build /app . 
COPY --from=ext appsettings.json .
#COPY --from=build /src/FsOpenAI.Server/runtimes ./runtimes
RUN echo "Listing usr/local/lib contents:" && ls -la /usr/local/lib
#COPY --from=build /usr/local/lib/libleptonica.so ./x64/libleptonica-1.82.0.so
#RUN ln -s /usr/local/lib/libleptonica.so ./x64/libleptonica-1.85.0.so
RUN ln -s /usr/local/lib/libleptonica.so ./x64/libleptonica-1.85.0.dll.so
#RUN ln -s /usr/lib/x86_64-linux-gnu/libtesseract.so ./x64/libtesseract50.so
RUN ln -s /usr/lib/x86_64-linux-gnu/libtesseract.so ./x64/libtesseract55.dll.so

ENTRYPOINT ["dotnet", "FsOpenAI.Server.dll"]

