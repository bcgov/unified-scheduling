.PHONY: help build start debug stop down clean ci ci-backend ci-web

help:
	@echo "Unified Scheduling - Makefile Commands"
	@echo ""
	@echo "Usage: make [target]"
	@echo ""
	@echo "Targets:"
	@echo "  build       Build Docker images"
	@echo "  start       Start services in production mode"
	@echo "  debug       Start services in development mode with hot reload"
	@echo "  stop        Stop services (non-destructive)"
	@echo "  down        Stop services and remove volumes (destructive)"
	@echo "  clean       Clean build artifacts"
	@echo "  ci          Run all CI checks (backend + web)"
	@echo "  ci-backend  Run backend CI checks (format, build, test)"
	@echo "  ci-web      Run web CI checks (lint, format, build, test)"
	@echo ""

build:
	@cd docker && ./manage build

start:
	@cd docker && ./manage start

debug:
	@cd docker && ./manage debug

stop:
	@cd docker && ./manage stop

down:
	@cd docker && ./manage down

clean:
	@find . -name "bin" -type d -exec rm -rf {} + 2>/dev/null || true
	@find . -name "obj" -type d -exec rm -rf {} + 2>/dev/null || true

ci: ci-backend ci-web

ci-backend:
	@echo "==> Backend: restore"
	dotnet restore
	@echo "==> Backend: csharpier check"
	dotnet csharpier check .
	@echo "==> Backend: format style check"
	dotnet format style --verify-no-changes
	@echo "==> Backend: format analyzers check"
	dotnet format analyzers --verify-no-changes
	@echo "==> Backend: build"
	dotnet build --configuration Release --no-restore
	@echo "==> Backend: test"
	dotnet test --no-build --configuration Release

ci-web:
	@echo "==> Web: install"
	cd web && npm ci
	@echo "==> Web: lint"
	cd web && npm run lint
	@echo "==> Web: prettier check"
	cd web && npm run prettier:check
	@echo "==> Web: build"
	cd web && npm run build
	@echo "==> Web: test"
	cd web && npm run test
	@echo "Cleaned build artifacts"