# Build stage: restore and publish with the .NET 11 SDK.
FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src
COPY global.json ./
COPY src/Claims/Claims.csproj src/Claims/
RUN dotnet restore src/Claims/Claims.csproj
COPY src/Claims/ src/Claims/
RUN dotnet publish src/Claims/Claims.csproj -c Release --no-restore -o /app

# Runtime stage: Ubuntu chiseled ASP.NET Core image (no shell or package manager, non-root by default).
FROM mcr.microsoft.com/dotnet/aspnet:11.0-resolute-chiseled
WORKDIR /app
COPY --from=build /app .
ARG RELEASE=local
ENV CLAIMS_RELEASE=$RELEASE \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Claims.dll"]
