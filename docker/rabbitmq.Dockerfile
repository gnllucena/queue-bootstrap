FROM ecr.aws/lambda/dotnet:5.0 AS base

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim as build
WORKDIR /src
COPY ["BlueprintBaseName.csproj", "BlueprintBaseName/"]
RUN dotnet restore "BlueprintBaseName/BlueprintBaseName.csproj"

WORKDIR "/src/BlueprintBaseName"
COPY . .
RUN dotnet build "BlueprintBaseName.csproj" --configuration Release --output /app/build

FROM build AS publish
RUN dotnet publish "BlueprintBaseName.csproj" \
            --configuration Release \ 
            --runtime linux-x64 \
            --self-contained false \ 
            --output /app/publish \
            -p:PublishReadyToRun=true  

FROM base AS final
WORKDIR /var/task
COPY --from=publish /app/publish .