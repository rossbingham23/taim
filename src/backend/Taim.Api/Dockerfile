# Stage 1 — .NET build (build context is repo root)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /src

COPY src/backend/Taim.slnx ./
COPY src/backend/Taim.Core/ Taim.Core/
COPY src/backend/Taim.Data/ Taim.Data/
COPY src/backend/Taim.Agents/ Taim.Agents/
COPY src/backend/Taim.Workflows/ Taim.Workflows/
COPY src/backend/Taim.Memory/ Taim.Memory/
COPY src/backend/Taim.Connectors/ Taim.Connectors/
COPY src/backend/Taim.Budget/ Taim.Budget/
COPY src/backend/Taim.Notifications/ Taim.Notifications/
COPY src/backend/Taim.Providers/ Taim.Providers/
COPY src/backend/Taim.Api/ Taim.Api/
COPY src/backend/Taim.Host/ Taim.Host/
COPY src/backend/Taim.Tests/ Taim.Tests/
COPY src/backend/Taim.E2ETests/ Taim.E2ETests/

RUN dotnet restore Taim.slnx
RUN dotnet publish Taim.Api/Taim.Api.csproj -c Release -o /app/publish --no-restore

# Stage 2 — MCP server dependencies
FROM node:20-slim AS node-build
WORKDIR /mcp

COPY mcp-servers/web-search/ web-search/
COPY mcp-servers/email/ email/

RUN cd web-search && npm ci --omit=dev
RUN cd email && npm ci --omit=dev

# Stage 3 — Runtime: .NET + Node.js + claude CLI (Alpine-based)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

RUN apk add --no-cache nodejs npm && \
    npm install -g @anthropic-ai/claude-code && \
    npm cache clean --force

WORKDIR /app
COPY --from=dotnet-build /app/publish ./
COPY --from=node-build /mcp mcp-servers/
RUN mkdir -p /app/workspaces

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Taim.Api.dll"]
