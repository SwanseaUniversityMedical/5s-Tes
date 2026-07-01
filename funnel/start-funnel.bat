@echo off
echo Starting Funnel server...

echo Removing existing container (if any)...
docker rm -f funnel-server 2>nul

echo Starting funnel-server container...
docker run -d ^
  --name funnel-server ^
  -p 8000:8000 ^
  -p 9090:9090 ^
  -v C:\funnel\config.yaml:/etc/funnel/config.yaml ^
  -v C:\funnel\data:/data ^
  quay.io/ohsu-comp-bio/funnel:latest ^
  server run --config /etc/funnel/config.yaml

echo Waiting for container to start...
timeout /t 3 /nobreak >nul

echo.
echo Container status:
docker ps --filter name=funnel-server

echo.
echo Logs:
docker logs funnel-server

echo.
echo Funnel UI: http://localhost:8000
echo Funnel RPC: localhost:9090
