# syntax=docker/dockerfile:1.7

# ---------- build ----------
FROM node:24-alpine AS build
WORKDIR /app

# package-lock.json copy riêng để lớp npm ci được cache khi chỉ đổi mã nguồn.
COPY apps/web/package.json apps/web/package-lock.json ./
RUN npm ci

COPY apps/web/ ./
RUN npm run build

# ---------- runtime ----------
# Web là tệp tĩnh. Caddy phục vụ trực tiếp — không cần Node ở runtime,
# image nhỏ hơn và bề mặt tấn công ít hơn hẳn.
FROM caddy:2-alpine AS runtime

COPY --from=build /app/dist /srv
COPY deploy/caddy/web.Caddyfile /etc/caddy/Caddyfile

EXPOSE 8081

HEALTHCHECK --interval=15s --timeout=3s --start-period=10s --retries=3 \
    CMD wget -q --spider http://localhost:8081/ || exit 1
