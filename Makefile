SHELL := /bin/bash
COMPOSE_DEV := docker compose -f docker-compose.yml -f docker-compose.dev.yml
COMPOSE_VM  := docker compose -f docker-compose.yml -f docker-compose.azure-vm.yml

.DEFAULT_GOAL := help

.PHONY: help
help: ## Liet ke cac lenh co san
	@grep -hE '^[a-zA-Z_-]+:.*?## ' $(MAKEFILE_LIST) \
		| awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-16s\033[0m %s\n", $$1, $$2}'

# ---------------------------------------------------------------------------
# Chuan bi
# ---------------------------------------------------------------------------

.PHONY: env
env: ## Tao .env va sinh ngau nhien cac bi mat
	@if [ -f .env ]; then \
		echo ".env da ton tai — khong ghi de. Xoa tay neu muon tao lai."; \
	else \
		cp .env.example .env; \
		python3 -c "import secrets,pathlib; p=pathlib.Path('.env'); s=p.read_text(); \
s=s.replace('POSTGRES_PASSWORD=','POSTGRES_PASSWORD='+secrets.token_urlsafe(24),1); \
s=s.replace('APP_MASTER_KEY=','APP_MASTER_KEY='+secrets.token_urlsafe(48),1); \
p.write_text(s)"; \
		echo "Da tao .env. Mo ra dien ADMIN_EMAIL va ADMIN_PASSWORD."; \
	fi

# ---------------------------------------------------------------------------
# Phat trien
# ---------------------------------------------------------------------------

.PHONY: up
up: ## Chay toan bo stack o che do dev
	$(COMPOSE_DEV) up -d --build
	@echo ""
	@echo "  Giao dien:  http://localhost:9090"
	@echo "  API:        http://localhost:8080"
	@echo "  OpenAPI:    http://localhost:8080/openapi/v1.json"

.PHONY: down
down: ## Dung stack, GIU nguyen du lieu
	$(COMPOSE_DEV) down

.PHONY: reset
reset: ## Dung stack va XOA SACH du lieu. Chi dung tren may dev.
	$(COMPOSE_DEV) down -v

.PHONY: logs
logs: ## Xem log tat ca dich vu
	$(COMPOSE_DEV) logs -f --tail 100

.PHONY: ps
ps: ## Trang thai cac container
	$(COMPOSE_DEV) ps

.PHONY: psql
psql: ## Mo psql trong container db
	docker exec -it englishforit-db-1 psql -U $${POSTGRES_USER:-efit} -d $${POSTGRES_DB:-englishforit}

.PHONY: web-dev
web-dev: ## Chay Vite dev server co hot reload (can API dang chay)
	cd apps/web && npm run dev

# ---------------------------------------------------------------------------
# Build va kiem tra
# ---------------------------------------------------------------------------

.PHONY: build
build: ## Build ca backend va frontend ngoai Docker
	dotnet build EnglishForIT.slnx -c Release
	cd apps/web && npm run build

.PHONY: test
test: ## Chay toan bo test .NET
	dotnet test EnglishForIT.slnx --nologo

.PHONY: test-unit
test-unit: ## Chi chay unit test (nhanh, khong can Docker)
	dotnet test tests/UnitTests/EnglishForIT.UnitTests.csproj --nologo

.PHONY: typecheck
typecheck: ## Kiem kieu TypeScript
	cd apps/web && npx tsc -b

.PHONY: lint
lint: ## Lint frontend
	cd apps/web && npm run lint

# ---------------------------------------------------------------------------
# Migration
# ---------------------------------------------------------------------------

.PHONY: migrate
migrate: ## Ap migration len DB dang chay
	dotnet ef database update --project src/Infrastructure --startup-project apps/api

.PHONY: migration
migration: ## Tao migration moi: make migration m="them bang x"
	@test -n "$(m)" || (echo "Thieu tham so. Vi du: make migration m=\"them bang x\"" && exit 1)
	dotnet ef migrations add "$(m)" --project src/Infrastructure --startup-project apps/api --output-dir Persistence/Migrations

# ---------------------------------------------------------------------------
# Van hanh
# ---------------------------------------------------------------------------

.PHONY: backup
backup: ## Sao luu DB va media vao ./backups
	./deploy/scripts/backup.sh

.PHONY: restore
restore: ## Phuc hoi tu ban sao luu: make restore f=backups/db-....dump
	@test -n "$(f)" || (echo "Thieu tham so. Vi du: make restore f=backups/db-20260818-030000.dump" && exit 1)
	./deploy/scripts/restore.sh "$(f)"

.PHONY: vm-up
vm-up: ## Khoi dong stack tren Azure VM (keo image theo tag)
	$(COMPOSE_VM) up -d

.PHONY: vm-logs
vm-logs: ## Xem log tren Azure VM
	$(COMPOSE_VM) logs -f --tail 100
