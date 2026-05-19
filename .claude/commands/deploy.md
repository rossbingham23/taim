Rebuild and redeploy the TAIM stack.

## Full rebuild (from scratch)

```bash
cd /home/rossb/claude/gen-site/taim
./start.sh
```

## Rebuild API only (fastest for backend changes)

```bash
docker compose build taim-api && docker compose up -d taim-api
```

## Rebuild frontend only

```bash
docker compose build taim-web && docker compose up -d taim-web
```

## Verify deployment

```bash
# Check API is up
curl http://localhost:5000/health

# Check API logs for errors
docker compose logs taim-api --tail=30

# Run smoke tests
cd src/backend && dotnet test Taim.Tests/Taim.Tests.csproj
```

## Apply a schema change without restarting

```bash
docker exec -e PGPASSWORD=taim taim-postgres-1 psql -U taim -d taim -c "YOUR SQL"
```

## Reset database (destroys all data)

```bash
docker compose down -v && ./start.sh
```
