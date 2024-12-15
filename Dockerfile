FROM mcr.microsoft.com/dotnet/sdk:8.0.404-bookworm-slim AS base
EXPOSE 52037
#EXPOSE 52035

ENV ASPNETCORE_URLS=http://+:52037
#ENV ASPNETCORE_URLS=https://+:52035
#ENV ASPNETCORE_Kestrel__Certificates__Default__Path=/usr/local/share/ca-certificates/localhost-tm1.pfx
#ENV ASPNETCORE_Kestrel__Certificates__Default__Password=tm1
#need 'docker_extra' folder in the same directory as the Dockerfile
#the contents of this folder are not in the repo
# WORKDIR /usr/local/share/ca-certificates
# COPY ./certs .
# COPY ./docker_extra/localhost-tm1.pfx .
# RUN update-ca-certificates

FROM base AS prepped

RUN cd /tmp \
	&& apt-get update \
	&& apt-get install -y wget \
	&& apt-get install -y gnupg \
	&& apt-get install -y unzip \
	&& apt-get update \
	&& wget -O- https://apt.repos.intel.com/intel-gpg-keys/GPG-PUB-KEY-INTEL-SW-PRODUCTS.PUB \
	| gpg --dearmor | tee /usr/share/keyrings/oneapi-archive-keyring.gpg > /dev/null \
	&& echo "deb [signed-by=/usr/share/keyrings/oneapi-archive-keyring.gpg] https://apt.repos.intel.com/oneapi all main" \
	| tee /etc/apt/sources.list.d/oneAPI.list \
	&& apt-get update \
	&& apt install -y intel-basekit \
	&& apt-get install -y libsasl2-modules-gssapi-mit \
	&& apt-get install -y libsasl2-dev \
	&& apt-get install -y krb5-user \
	&& apt-get install -y librdkafka-dev \
	&& find /opt -name "libiomp5.so" \
	&& ldconfig /opt/intel/compilers_and_libraries_2020.0.166/linux/compiler/lib/intel64_lin 
#	&& wget https://databricks-bi-artifacts.s3.us-east-2.amazonaws.com/simbaspark-drivers/odbc/2.8.0/SimbaSparkODBC-2.8.0.1002-Debian-64bit.zip \
#	&& unzip SimbaSparkODBC-2.8.0.1002-Debian-64bit.zip \
#	&& dpkg -i  simbaspark_2.8.0.1002-2_amd64.deb \
#	&& apt-get install -y unixodbc-dev

FROM base AS build
WORKDIR /src
COPY src .
WORKDIR /src/FsOpenAI.Server
RUN dotnet workload update

# RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
#     --mount=type=secret,id=nugetconfig \
# 	dotnet restore -r linux-x64
# RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
#     --mount=type=secret,id=nugetconfig \
# 	dotnet publish -c Release -o /app --no-restore


RUN dotnet restore -r linux-x64
RUN dotnet publish -c:Release -p:DefineConstants=UNAUTHENTICATED -o:/app --no-restore

FROM prepped AS final
WORKDIR /app
COPY --from=build /app .
#COPY ./docker_extra .
#ENV ODBCINI=/app/odbc.ini
ENTRYPOINT ["dotnet", "FsOpenAI.Server.dll"]
