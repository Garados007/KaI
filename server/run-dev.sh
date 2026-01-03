#!/bin/bash

set -o allexport
source ../.env
set +o allexport

dotnet watch --no-hot-reload run --project KaI.Server/KaI.Server.csproj -- \
    -c KaI.Server/bin/cache/ \
    -d ../ui/ \
    --twitch-client-id $TWITCH_API_CLIENT_ID
