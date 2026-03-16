# NekoHub Mintlify authoring guide

## Project context

- This directory contains the Mintlify docs site for NekoHub.
- Pages are MDX files with YAML frontmatter.
- Site configuration lives in `docs.json`.
- Run `mint dev` for local preview.
- Run `mint validate` and `mint broken-links` before finishing changes.

## Language model

- Simplified Chinese is the source language.
- English pages live under `en/` and mirror the Chinese file structure.
- Keep the same slug structure across languages. Do not translate route paths.

## Writing rules

- Follow Mintlify style: active voice, direct address, concise sentences.
- Use sentence case for headings.
- Use bold for UI labels, for example **Upload** and **Settings**.
- Use backticks for commands, paths, environment variables, headers, roles, permissions, and route paths.
- Prefer task-oriented explanations over marketing copy.

## NekoHub terminology

- Use `superAdmin`, `admin`, and `user` as the canonical role values.
- Use permission keys exactly as implemented, for example `assets.read` and `users.managePermissions`.
- API key examples must use `Authorization: Bearer <API_KEY>`.
- JWT examples must use `Authorization: Bearer <access_token>`.
- `GET /api/v1/system/bootstrap` is runtime config metadata, not bootstrap account state.

## Scope boundaries

- Document public behavior, deploy-time configuration, and supported integration patterns.
- Do not invent unsupported roles, headers, response fields, or auth flows.
- Do not describe roadmap features as if they already exist.
