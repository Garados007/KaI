#!/bin/bash
elm-live \
    --port=8000 \
    --start-page=index.html \
    src/KaI/Main.elm \
    -- --output=index.js
