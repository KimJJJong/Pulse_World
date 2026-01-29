#!/bin/bash

# 1. .env Check & Create
if [ ! -f .env ]; then
    echo "[start.sh] .env not found. Creating from .env.example..."
    if [ -f .env.example ]; then
        cp .env.example .env
        echo "[start.sh] .env created."
    else
        echo "[Error] .env.example not found!"
        exit 1
    fi
fi

# 2. Cleanup Legacy Data (Fixes Permission Denied)
if [ -d "data/postgres" ]; then
    echo "[start.sh] Removing legacy data/postgres to prevent permission errors..."
    # Try removing. If fails, ask user for sudo.
    rm -rf data/postgres 2>/dev/null || sudo rm -rf data/postgres
fi

# 3. Add execute permission (just in case)
chmod +x start.sh 2>/dev/null

# 3. Docker Compose Up
echo "[start.sh] Starting Docker Compose..."
docker compose up --build -d

echo "[start.sh] Done! Logs:"
docker compose logs -f
