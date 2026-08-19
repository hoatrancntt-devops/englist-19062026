# syntax=docker/dockerfile:1.7

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

# Copy riêng file dự án trước để lớp restore được cache lại khi chỉ đổi mã nguồn.
COPY global.json Directory.Build.props ./
COPY src/Domain/*.csproj src/Domain/
COPY src/Application/*.csproj src/Application/
COPY src/Infrastructure/*.csproj src/Infrastructure/
COPY apps/api/*.csproj apps/api/
RUN dotnet restore apps/api/EnglishForIT.Api.csproj

COPY src/ src/
COPY apps/api/ apps/api/

RUN dotnet publish apps/api/EnglishForIT.Api.csproj \
    -c Release \
    -o /app \
    --no-restore \
    /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app

# curl để healthcheck; libicu và tzdata để TimeZoneInfo tra được id IANA —
# thiếu chúng thì mọi múi giờ rơi về UTC trong im lặng.
# Xoá cache apt ngay trong cùng lệnh để không phình image.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl libicu74 tzdata \
    && rm -rf /var/lib/apt/lists/*

# Chạy bằng user không phải root. Image aspnet đã có sẵn user 'app' (uid 1654).
COPY --from=build --chown=app:app /app .

# Nội dung học đi kèm image, không nằm ngoài: một image tag là một phiên bản nội dung
# xác định, nên quay lui image cũng quay lui đúng bộ nội dung tương ứng.
COPY --chown=app:app content/ ./content/

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=0

USER app
EXPOSE 8080

# start-period rộng vì lần khởi động đầu phải chạy migration.
HEALTHCHECK --interval=15s --timeout=5s --start-period=90s --retries=5 \
    CMD curl -fsS http://localhost:8080/health/ready || exit 1

ENTRYPOINT ["dotnet", "EnglishForIT.Api.dll"]
