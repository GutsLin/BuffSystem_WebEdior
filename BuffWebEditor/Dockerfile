FROM node:22-alpine AS build

WORKDIR /workspace

COPY apps/api/package.json apps/api/package.json
COPY apps/web/package.json apps/web/package.json
RUN npm install --prefix apps/api --no-audit --no-fund
RUN npm install --prefix apps/web --no-audit --no-fund

COPY apps ./apps
RUN npm run build --prefix apps/api
RUN npm run build --prefix apps/web

FROM node:22-alpine AS runtime

WORKDIR /app

ENV NODE_ENV=production
ENV PORT=3000
ENV DATA_DIR=/app/data

COPY apps/api/package.json apps/api/package.json
RUN npm install --prefix apps/api --omit=dev --no-audit --no-fund

COPY --from=build /workspace/apps/api/dist ./apps/api/dist
COPY --from=build /workspace/apps/web/dist ./apps/web/dist

RUN mkdir -p /app/data
EXPOSE 3000

CMD ["node", "apps/api/dist/main.js"]
