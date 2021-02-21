FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim as build
COPY ./src .
RUN dotnet test tests/Tests.csproj
RUN dotnet publish hosted/Hosted.csproj -c Release -o ./output

FROM mcr.microsoft.com/dotnet/runtime:5.0 as deploy
COPY --from=build /output .
ENV TZ America/Sao_Paulo
ENTRYPOINT ["dotnet", "Hosted.dll"]