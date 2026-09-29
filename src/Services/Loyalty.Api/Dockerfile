FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY src/ ./src/
RUN dotnet restore src/Services/Loyalty.Api/Loyalty.Api.csproj
RUN dotnet publish src/Services/Loyalty.Api/Loyalty.Api.csproj -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER root
USER $APP_UID
ENTRYPOINT ["dotnet", "Loyalty.Api.dll"]

