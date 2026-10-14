FROM mcr.microsoft.com/dotnet/sdk:10.0.402-noble AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Domain/TechCurse.Domain.csproj src/Domain/
COPY src/Application/TechCurse.Application.csproj src/Application/
COPY src/Infrastructure/TechCurse.Infrastructure.csproj src/Infrastructure/
COPY src/Api/TechCurse.Api.csproj src/Api/
COPY src/Sonda/TechCurse.Sonda.csproj src/Sonda/
RUN dotnet restore src/Api/TechCurse.Api.csproj \
 && dotnet restore src/Sonda/TechCurse.Sonda.csproj

COPY src/ src/
RUN dotnet publish src/Api/TechCurse.Api.csproj -c Release --no-restore -o /app \
 && dotnet publish src/Sonda/TechCurse.Sonda.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13-noble-chiseled-extra
WORKDIR /app
COPY --from=build /app .

USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
  CMD ["dotnet", "/app/TechCurse.Sonda.dll"]

ENTRYPOINT ["dotnet", "/app/TechCurse.Api.dll"]
