#!/bin/sh
set -e

if [ -f /app/certs/server.crt ]; then
    cp /app/certs/server.crt /usr/local/share/ca-certificates/zeebe.crt
    update-ca-certificates
fi

exec gosu app dotnet Agent.Api.dll
