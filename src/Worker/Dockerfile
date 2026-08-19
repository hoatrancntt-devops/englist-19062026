# syntax=docker/dockerfile:1

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

COPY Directory.Build.props global.json ./
COPY src/Domain/EnglishForIT.Domain.csproj src/Domain/
COPY src/Application/EnglishForIT.Application.csproj src/Application/
COPY src/Infrastructure/EnglishForIT.Infrastructure.csproj src/Infrastructure/
COPY src/Worker/EnglishForIT.Worker.csproj src/Worker/

RUN dotnet restore src/Worker/EnglishForIT.Worker.csproj

COPY src/ src/

RUN dotnet publish src/Worker/EnglishForIT.Worker.csproj \
    -c Release \
    -o /app \
    --no-restore \
    /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/runtime:10.0-noble AS runtime
WORKDIR /app

# libicu và tzdata là bắt buộc, không phải tuỳ chọn: worker quyết định gửi nhắc học theo
# giờ địa phương của từng học viên. Thiếu chúng thì mọi múi giờ rơi về UTC trong im lặng
# và nhắc học lúc 20 giờ sẽ gửi vào 3 giờ sáng giờ Việt Nam.
# libgssapi-krb5-2: Npgsql dò thư viện này lúc khởi tạo và in ra hai dòng lỗi đỏ nếu thiếu.
# Kết nối vẫn chạy, nhưng lỗi giả trong log làm người trực đêm mất thời gian.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libicu74 tzdata libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build --chown=app:app /app .

# Thư mục ghi âm dùng chung với API qua volume. Tạo sẵn để job dọn dẹp không phải
# đoán xem đường dẫn có tồn tại không.
RUN mkdir -p /app/storage/audio && chown -R app:app /app/storage

ENV DOTNET_ENVIRONMENT=Production \
    DOTNET_gcServer=0

USER app

ENTRYPOINT ["dotnet", "EnglishForIT.Worker.dll"]
