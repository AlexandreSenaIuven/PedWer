# Build no contexto da RAIZ do repositório (precisa enxergar frontend/ e
# api/ como irmãos) — ver docs/publicacao-easypanel.md.

# ---- build do front (servido pela própria API em produção) ----
FROM node:22-alpine AS front-build
WORKDIR /front
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# ---- build da API ----
FROM node:22-alpine AS api-build
WORKDIR /api
COPY api/package.json api/package-lock.json ./
RUN npm ci
COPY api/ ./
RUN npm run build

# ---- imagem final ----
FROM node:22-alpine
WORKDIR /app
COPY api/package.json api/package-lock.json ./
RUN npm ci --omit=dev
COPY --from=api-build /api/dist ./dist
COPY --from=front-build /front/dist ./public

ENV PORT=3001
EXPOSE 3001
CMD ["node", "dist/main.js"]
