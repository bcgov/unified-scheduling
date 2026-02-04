.PHONY: help build start debug stop down clean

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
	@echo "Cleaned build artifacts"