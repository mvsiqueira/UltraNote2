# TODO — UltraNote

Backlog de evolução. Marque `[x]` ao concluir. Prioridade sugerida de cima para baixo.

## Já entregue (contexto)

- [x] API ASP.NET Core + EF Core/SQLite (pastas, notas, anexos) com migrations.
- [x] Auth Google (OIDC) na web + allowlist de e-mail na API.
- [x] Web Blazor WebAssembly com "Entrar com Google".
- [x] Deploy no QNAP (3 apps na rede `edge`, atrás do cloudflared): `note.ultrasoft.app.br` + `note-api.ultrasoft.app.br`.
- [x] Repositório no GitHub.

---

## 1. Editor (o coração — reaproveitar o do app original)

- [ ] Migrar o editor de `contenteditable` para **TipTap** (`UltraNote.UI/RichTextEditor.razor` + `wwwroot/ultranote-editor.js`).
  - [ ] Configurar **bundling de JS** (npm + esbuild/rollup) no projeto `UltraNote.UI`.
  - [ ] Tabelas com **resize de linha/coluna**.
  - [ ] **Cores** de fonte e **realce** (highlight).
  - [ ] **Indentação** (aumentar/diminuir recuo).
  - [ ] Menus de contexto (formatação + tabela).
- [ ] **Colar imagem** no editor → upload via API de anexos → embutir no HTML (a API já suporta anexos; falta o front usar).

## 2. Gestão de notas e pastas (API já suporta; falta UI)

- [ ] **Excluir nota** pela lista.
- [ ] **Renomear pasta** pela UI.
- [ ] **Mover** nota/pasta (ação de mover ou drag-drop).
- [ ] **Busca** de notas (por título/conteúdo).

## 3. UX e robustez

- [ ] **Guarda de alterações não salvas**: avisar/bloquear ao trocar de nota/pasta com edição pendente (o app Tauri original tinha isso).
- [ ] **Mostrar erros da API na UI**: hoje operações de escrita (ex.: criar pasta) podem falhar em silêncio — exibir mensagem no status/toast.
- [ ] Indicador de "salvando…/salvo".
- [ ] Atalhos de teclado (Ctrl+S salvar, etc.).

## 4. Autenticação / sessão

- [ ] **Persistir / renovar o token** Google na web (ID token expira ~1h → hoje exige novo login). Avaliar One Tap silencioso ou refresh.
- [ ] Restringir **CORS** da API para `https://note.ultrasoft.app.br` (hoje libera qualquer origem).
- [ ] (Opcional) **Cloudflare Access** como camada extra na URL da web.

## 5. App Windows (desktop)

- [ ] Criar `UltraNote.Desktop` (**MAUI Blazor Hybrid**), reaproveitando os componentes `UltraNote.UI`.
  - [ ] Requer `dotnet workload install maui` (não instalado; só `maui-android`).
  - [ ] Login Google via **WebAuthenticator** (em vez do GIS da web).
- [ ] (Opcional) Cache local leve p/ resiliência offline.

## 6. Infra / Deploy / Qualidade

- [ ] Automatizar o fluxo de atualização (script: copiar p/ NAS → `docker run ... publish` → recriar app).
- [ ] **Backup** automatizado de `/share/Container/ultranote-data` e `/ultranote-assets`.
- [ ] (Dev) Decidir se o `appsettings.json` local fica **sem** `GoogleClientId` (dev sem login, mais ágil) — produção continua protegida.
- [ ] Testes automatizados (API: endpoints; UI: componentes principais).
- [ ] Healthcheck de container / monitor de uptime.

---

## Notas / armadilhas conhecidas (ver DEPLOY-QNAP.md)

- Containers neste NAS só têm internet na rede `edge` **e** precisam de `dns:` explícito
  (a API usa pra validar o token Google; sem isso → "signature key was not found").
- O `docker build` não tem internet no NAS → publicamos com `docker run` e rodamos imagens
  oficiais com os arquivos montados (sem build de imagem).
- Atualização da web = republicar `_out_web` + hard refresh (nginx serve arquivos montados).
