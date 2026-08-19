# Triển khai trên Azure VM

Mục tiêu: chạy hệ thống trên một VM Ubuntu bằng Docker Compose, có HTTPS, có sao lưu, và nâng cấp được mà không mất dữ liệu.

Bản này **không** dùng App Service hay AKS. Một VM là đủ cho vài trăm học viên và rẻ hơn nhiều.

---

## 1. Chọn cấu hình VM

| Quy mô | SKU | vCPU / RAM | Ghi chú |
|---|---|---|---|
| Thử nghiệm | `Standard_B2s` | 2 / 4 GB | Đủ cho tới khi bật dịch vụ nhận dạng giọng nói |
| Có phần luyện nói | `Standard_B2ms` | 2 / 8 GB | **Khuyến nghị.** whisper lúc cao điểm chiếm ~1,6 GB |
| Vài trăm học viên | `Standard_D2s_v5` | 2 / 8 GB | CPU ổn định hơn dòng B (không burst credit) |

- **OS**: Ubuntu Server 24.04 LTS
- **Đĩa**: Premium SSD 64 GB. Postgres cần IOPS ổn định; đĩa Standard HDD sẽ làm truy vấn giật.
- **Xác thực**: khoá SSH, **không** dùng mật khẩu.

Tạo bằng CLI:

```bash
az group create --name rg-englishforit --location southeastasia

az vm create \
  --resource-group rg-englishforit \
  --name vm-englishforit \
  --image Ubuntu2404 \
  --size Standard_B2ms \
  --admin-username azureuser \
  --generate-ssh-keys \
  --os-disk-size-gb 64 \
  --storage-sku Premium_LRS
```

---

## 2. Mở cổng trên NSG

Chỉ ba cổng. Không mở 5432, không mở 8080.

```bash
az network nsg rule create -g rg-englishforit --nsg-name vm-englishforitNSG \
  --name allow-http --priority 1000 --destination-port-ranges 80 --protocol Tcp --access Allow

az network nsg rule create -g rg-englishforit --nsg-name vm-englishforitNSG \
  --name allow-https --priority 1010 --destination-port-ranges 443 --protocol Tcp --access Allow
```

SSH (22) đã mở sẵn khi tạo VM. **Siết lại theo IP của bạn:**

```bash
az network nsg rule update -g rg-englishforit --nsg-name vm-englishforitNSG \
  --name default-allow-ssh --source-address-prefixes "<IP-cua-ban>/32"
```

> Postgres chỉ nghe trong mạng bridge của Docker và không publish cổng nào ở cấu hình Azure VM.
> Nếu cần nối bằng công cụ ngoài, dùng SSH tunnel — đừng mở 5432 ra Internet.

---

## 3. Cài Docker

```bash
ssh azureuser@<ip-vm>

sudo apt-get update && sudo apt-get install -y ca-certificates curl
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc

echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] \
https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo $VERSION_CODENAME) stable" \
  | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

sudo usermod -aG docker $USER
newgrp docker

docker compose version   # phải là v2.x trở lên
```

---

## 4. Lấy mã và cấu hình

```bash
sudo mkdir -p /srv/englishforit && sudo chown $USER:$USER /srv/englishforit
git clone <repo-url> /srv/englishforit
cd /srv/englishforit

cp .env.example .env
```

Sinh bí mật:

```bash
echo "POSTGRES_PASSWORD=$(openssl rand -base64 24)"
echo "APP_MASTER_KEY=$(openssl rand -base64 48)"
```

Điền vào `.env`:

```ini
POSTGRES_PASSWORD=<giá trị vừa sinh>
APP_MASTER_KEY=<giá trị vừa sinh>

SITE_ADDRESS=english.congty.com     # có domain -> tự xin Let's Encrypt
COOKIES_SECURE=true

IMAGE_REGISTRY=myregistry.azurecr.io
IMAGE_TAG=v0.1.0
```

> **`APP_MASTER_KEY` là hằng số vĩnh viễn.** Mọi khoá API và client secret trong DB mã hoá bằng khoá dẫn xuất từ nó.
> Đổi = mất sạch, và app sẽ **im lặng** coi như chưa cấu hình chứ không báo lỗi. Cất một bản vào Azure Key Vault ngay hôm nay.

---

## 5. HTTPS

### Có domain (khuyến nghị)

Trỏ bản ghi A của domain về IP công khai của VM, rồi đặt `SITE_ADDRESS=english.congty.com`.
Caddy tự xin và tự gia hạn chứng chỉ Let's Encrypt. Không cần làm gì thêm.

Muốn nhận email cảnh báo hết hạn: mở `deploy/caddy/Caddyfile`, bỏ chú thích dòng `email` trong khối global và điền địa chỉ thật.

### Chỉ có IP

Let's Encrypt không cấp chứng chỉ cho địa chỉ IP. Dùng chứng chỉ tự ký:

```ini
SITE_ADDRESS=https://20.198.x.x
```

Trình duyệt cảnh báo một lần. Bấm "vẫn tiếp tục" → từ đó là secure context → **micro hoạt động**.

> Không có HTTPS thì **toàn bộ phần luyện nói chết**. `getUserMedia` không chạy trên HTTP thuần trừ `localhost`.
> Đây không phải khuyến nghị mà là ràng buộc của trình duyệt.

---

## 6. Image: build tại VM hay dùng ACR

### Dùng Azure Container Registry (khuyến nghị)

Build trên VM 2 vCPU mất 8–12 phút và chiếm hết RAM đang cần cho Postgres. Build ở nơi khác rồi đẩy image lên:

```bash
az acr create -g rg-englishforit -n myregistry --sku Basic
az acr login -n myregistry

# Trên máy dev hoặc CI:
docker build -f apps/api/Dockerfile -t myregistry.azurecr.io/api:v0.1.0 .
docker build -f apps/web/Dockerfile -t myregistry.azurecr.io/web:v0.1.0 .
docker push myregistry.azurecr.io/api:v0.1.0
docker push myregistry.azurecr.io/web:v0.1.0
```

Cho VM quyền kéo image bằng managed identity (không phải mật khẩu admin của registry):

```bash
az vm identity assign -g rg-englishforit -n vm-englishforit
PRINCIPAL=$(az vm identity show -g rg-englishforit -n vm-englishforit --query principalId -o tsv)
ACR_ID=$(az acr show -n myregistry --query id -o tsv)
az role assignment create --assignee "$PRINCIPAL" --scope "$ACR_ID" --role AcrPull
```

### Build tại VM (chấp nhận được lúc thử nghiệm)

```bash
docker compose -f docker-compose.yml -f docker-compose.azure-vm.yml build
```

---

## 7. Khởi động

```bash
cd /srv/englishforit
docker compose -f docker-compose.yml -f docker-compose.azure-vm.yml up -d

# Theo dõi tới khi sẵn sàng — lần đầu phải chạy migration
docker compose logs -f api
```

Kiểm tra:

```bash
curl -fsS http://localhost/health/ready     # {"status":"ready"}
docker compose ps                            # tất cả healthy
```

Mở `https://english.congty.com` trên trình duyệt.

---

## 8. Dữ liệu qua reboot

`pgdata` là named volume, `./media` là bind mount, mọi service đặt `restart: unless-stopped`.
Reboot VM thì Docker tự dựng lại và dữ liệu còn nguyên.

Bật Docker chạy cùng hệ thống:

```bash
sudo systemctl enable docker
```

| Lệnh | Hậu quả |
|---|---|
| `docker compose down` | Dừng, **giữ** dữ liệu |
| `docker compose down -v` | **XOÁ SẠCH** dữ liệu học viên. Không bao giờ chạy trên production |

---

## 9. Sao lưu

```bash
./deploy/scripts/backup.sh
```

Script dump DB (định dạng custom, nén), nén thư mục media, **kiểm tra bản dump đọc được**, và xoá bản cũ hơn 7 ngày.

Chạy tự động — thêm vào crontab:

```bash
crontab -e
```

```cron
0 3 * * * cd /srv/englishforit && ./deploy/scripts/backup.sh >> /var/log/efit-backup.log 2>&1
```

Đưa bản sao lưu ra khỏi VM. VM chết là mất cả đĩa:

```bash
az storage blob upload-batch \
  --destination backups \
  --source /srv/englishforit/backups \
  --account-name <storage-account> \
  --auth-mode login
```

**Diễn tập phục hồi một lần trước khi mở cho học viên.** Bản sao lưu chưa từng phục hồi thử thì coi như chưa có.

```bash
./deploy/scripts/restore.sh backups/db-20260818-030000.dump
```

---

## 10. Nâng cấp

Dùng tag cụ thể, không dùng `latest`:

```bash
./deploy/scripts/deploy-azure-vm.sh v0.2.0
```

Script làm năm bước: sao lưu → kéo image mới → cập nhật `.env` → khởi động lại → chờ `/health/ready`.
Quá 200 giây chưa sẵn sàng thì **tự quay lui** về tag cũ.

Quay lui thủ công:

```bash
sed -i 's/^IMAGE_TAG=.*/IMAGE_TAG=v0.1.0/' .env
docker compose -f docker-compose.yml -f docker-compose.azure-vm.yml up -d
```

> Quay lui image thì được, nhưng **migration đã chạy thì không tự lùi**. Migration nào xoá cột hoặc bảng
> phải làm hai bước qua hai phiên bản (ngừng dùng ở bản N, xoá ở bản N+1) — nếu không thì không quay lui được.

---

## 11. Ba môi trường

| Môi trường | Cách chạy |
|---|---|
| **Dev** (máy cá nhân) | `make up` — mở cổng ra host, log Debug, HTTP thuần |
| **Staging** (VM riêng, domain phụ) | Cùng file như production, khác `.env`: `SITE_ADDRESS=staging.congty.com`, tag `-rc` |
| **Production** | `docker-compose.yml` + `docker-compose.azure-vm.yml`, tag cụ thể |

Staging và production dùng **cùng một file compose**. Khác nhau chỉ ở `.env` và tag image — nếu khác file thì staging không còn chứng minh được điều gì.

---

## 12. Sự cố hay gặp

| Triệu chứng | Nguyên nhân | Xử lý |
|---|---|---|
| `api` khởi động lại liên tục | Không nối được DB | `docker compose logs db` — thường do sai `POSTGRES_PASSWORD` trong `.env` |
| Trình duyệt báo chứng chỉ không hợp lệ | Đang dùng IP, chứng chỉ tự ký | Bình thường. Bấm "vẫn tiếp tục" |
| Micro không bật được | Đang chạy HTTP thuần | Bắt buộc phải có HTTPS |
| Đăng nhập được nhưng request sau bị 401 | `COOKIES_SECURE=true` mà đang chạy HTTP | Bật HTTPS, hoặc đặt `false` (chỉ khi chạy localhost) |
| Đã cấu hình AI/mail mà app bảo chưa cấu hình | `APP_MASTER_KEY` đã bị đổi | Không giải mã lại được. Nhập lại khoá API |
| Đĩa đầy | Log Docker phình | Đã giới hạn ở `docker-compose.azure-vm.yml`; kiểm bằng `docker system df` |
| `port is already allocated` | Có thứ khác chiếm 80/443 | `sudo ss -tlnp | grep -E ':80|:443'` |
