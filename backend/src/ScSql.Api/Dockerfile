FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY backend/src/ScSql.Api/ScSql.Api.csproj backend/src/ScSql.Api/
RUN dotnet restore backend/src/ScSql.Api/ScSql.Api.csproj

COPY backend/src/ScSql.Api/ backend/src/ScSql.Api/
RUN dotnet publish backend/src/ScSql.Api/ScSql.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 2008
ENV ASPNETCORE_URLS=http://+:2008
ENTRYPOINT ["dotnet", "ScSql.Api.dll"]