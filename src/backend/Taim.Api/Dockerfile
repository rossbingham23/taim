FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Taim.slnx ./
COPY Taim.Core/ Taim.Core/
COPY Taim.Data/ Taim.Data/
COPY Taim.Agents/ Taim.Agents/
COPY Taim.Workflows/ Taim.Workflows/
COPY Taim.Memory/ Taim.Memory/
COPY Taim.Connectors/ Taim.Connectors/
COPY Taim.Budget/ Taim.Budget/
COPY Taim.Notifications/ Taim.Notifications/
COPY Taim.Providers/ Taim.Providers/
COPY Taim.Api/ Taim.Api/
COPY Taim.Host/ Taim.Host/
COPY Taim.Tests/ Taim.Tests/
COPY Taim.E2ETests/ Taim.E2ETests/

RUN dotnet restore Taim.slnx
RUN dotnet publish Taim.Api/Taim.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Taim.Api.dll"]
