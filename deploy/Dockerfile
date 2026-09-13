FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Bouncer.Worker/Bouncer.Worker.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
VOLUME ["/data"]
ENV Bouncer__Database__Path=/data/bouncer.db
ENTRYPOINT ["dotnet", "Bouncer.Worker.dll"]
