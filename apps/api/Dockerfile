FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY apps/api/MailManager.Api.csproj apps/api/
RUN dotnet restore apps/api/MailManager.Api.csproj -p:ExcludeDesignDependencies=true
COPY apps/api/ apps/api/
RUN dotnet publish apps/api/MailManager.Api.csproj -c Release -o /app/publish --no-restore -p:ExcludeDesignDependencies=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "MailManager.Api.dll"]
