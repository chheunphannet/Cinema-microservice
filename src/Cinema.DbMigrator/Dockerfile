FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Cinema.DbMigrator/Cinema.DbMigrator.csproj", "src/Cinema.DbMigrator/"]
RUN dotnet restore "src/Cinema.DbMigrator/Cinema.DbMigrator.csproj"
COPY src/Cinema.DbMigrator/ src/Cinema.DbMigrator/
# Copy the migrations directory so it can be embedded in the image
COPY infra/postgres/migrations/ /app/infra/postgres/migrations/
COPY infra/postgres/bootstrap/ /app/infra/postgres/bootstrap/
WORKDIR "/src/src/Cinema.DbMigrator"
RUN dotnet build "Cinema.DbMigrator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Cinema.DbMigrator.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Copy migrations from build stage
COPY --from=build /app/infra/ /app/infra/
ENTRYPOINT ["dotnet", "Cinema.DbMigrator.dll"]
