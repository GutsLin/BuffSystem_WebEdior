FROM node:22-alpine AS build-game
WORKDIR /build
COPY packages/game/package.json packages/game/package.json
RUN npm install --prefix packages/game --no-audit --no-fund
COPY packages/game packages/game
RUN npm run build --prefix packages/game

FROM node:22-alpine AS build-server
WORKDIR /build
COPY packages/server/package.json packages/server/package.json
RUN npm install --prefix packages/server --no-audit --no-fund
COPY packages/server packages/server
RUN npm run build --prefix packages/server

FROM node:22-alpine AS runtime
WORKDIR /app
COPY packages/server/package.json packages/server/package.json
RUN npm install --prefix packages/server --omit=dev --no-audit --no-fund
COPY --from=build-server /build/packages/server/dist ./packages/server/dist
COPY --from=build-game /build/packages/game/dist ./packages/game/dist
RUN mkdir -p /app/data
EXPOSE 3001
CMD ["node", "packages/server/dist/index.js"]
