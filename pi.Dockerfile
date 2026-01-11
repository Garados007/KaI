ARG TARGET_HOSTNAME=pi5.local
ARG TARGET_PORT=5000

# This image is installed at the pi with the precompiled elm binary. This will later be exported and made available for everyone to use.
FROM elm-use AS ui-builder
WORKDIR /src
ENV TARGET_HOSTNAME=pi5.local
ENV TARGET_PORT=5000
COPY ui /src
RUN mkdir -p content/game && \
    elm make --output=content/game/index.js src/KaI/Main.elm && \
    sed -i "s@ws://localhost:8005/ws@ws://${TARGET_HOSTNAME}:${TARGET_PORT}/ws@g" content/game/index.js
RUN mkdir -p content/scoreboard && \
    sed -i "s@import KaI.Main.* as Core@import KaI.Main.Scoreboard as Core@g" src/KaI/Main.elm && \
    elm make --output=content/scoreboard/index.js src/KaI/Main.elm && \
    sed -i "s@ws://localhost:8005/ws@ws://${TARGET_HOSTNAME}:${TARGET_PORT}/ws@g" content/scoreboard/index.js

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server-builder
WORKDIR /src
COPY server /src
RUN mkdir -p /app && \
    dotnet build --nologo -c RELEASE \
        KaI.Server/KaI.Server.csproj && \
    dotnet publish --nologo -c RELEASE -o /app \
        KaI.Server/KaI.Server.csproj

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
RUN mkdir -p /app/data/game && \
    mkdir -p /app/cache
COPY cache /app/cache
COPY ui /app/data
COPY ui/index.html /app/data/game/
COPY ui/index.html /app/data/scoreboard/
COPY --from=server-builder /app /app
COPY --from=ui-builder /src/content/* /app/data/
RUN ln -s /app/data/fonts /app/data/game/fonts && \
    ln -s /app/data/img /app/data/game/img && \
    ln -s /app/data/style /app/data/game/style && \
    ln -s /app/data/fonts /app/data/scoreboard/fonts && \
    ln -s /app/data/img /app/data/scoreboard/img && \
    ln -s /app/data/style /app/data/scoreboard/style
EXPOSE 5000
RUN rm -r data
COPY data /app/data
#CMD [ "ls", "-R", "/app/data/" ]
CMD [ "/app/KaI.Server", "--port", "5000", "--data-dir", "/app/data", "--cache-dir", "/app/cache", "--twitch-client-id", "YOUR_TWITCH_CLIENT_ID_HERE" ]
