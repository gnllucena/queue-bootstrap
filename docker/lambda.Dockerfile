FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim as build
WORKDIR /src
COPY ./src .
RUN dotnet test tests/Tests.csproj
RUN dotnet publish lambda/Lambda.csproj \
            --configuration Release \
            --runtime linux-x64 \
            --self-contained false \
            --output /output \
            -p:PublishReadyToRun=true

FROM public.ecr.aws/lambda/dotnet:5.0 AS deploy
WORKDIR /var/task
COPY --from=build /output .
CMD ["Lambda::Lambda.Function::Handler"]