# Changelog

All notable changes to this project will be documented in this file.

## [0.1.0] - 2026-03-17

### Added
- Initial public release of NekoHub image/media asset backend.
- Clean Architecture project layout (`Api/Application/Domain/Infrastructure`).
- Asset upload, detail query, pagination query, delete, and content redirect APIs.
- Local and S3-compatible storage modes.
- File-type derivative model (`AssetDerivative`) with thumbnail processing sample.
- Structured processing result model (`AssetStructuredResult`) with deterministic `basic_caption` sample.
- Dockerfile and Docker Compose deployment assets.
- Public release documentation (`README`, deployment guide).

### Notes
- This is a first public release baseline and not the final product shape.
