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

- [x] **Excluir nota** — menu de contexto.
- [x] **Renomear pasta** — menu de contexto.
- [x] **Buscar** notas (por título) — caixa de busca na barra superior.
- [x] **Mover** nota/pasta — menu de contexto "Mover...", modal com seletor de destino (pastas descendentes excluídas para evitar ciclo).

## 3. UX e robustez

- [x] **Guarda de alterações não salvas** — modal de confirmação ao trocar de nota com edição pendente (cancelar/descartar), + aviso nativo do navegador ao fechar/recarregar a aba.
- [x] **Mostrar erros da API na UI** — toda operação de escrita (criar/renomear/excluir/salvar) reporta erro no status bar.
- [x] Indicador de "salvando…" — botão mostra "Salvando…" e fica desabilitado durante a chamada.
- [x] Atalho de teclado **Ctrl+S** (ou Cmd+S) salva a nota aberta, sem disparar o "Salvar página" do navegador.

## 3b. Multiusuário (segregação de dados)

> Hoje o app é **single-user**: a allowlist controla quem entra, mas os dados são uma
> biblioteca única compartilhada (sem dono). Para abrir a vários usuários com dados
> separados, plano em 5 passos:
- [ ] Adicionar `OwnerId` em `Folder` e `Note` (Attachment herda via nota) + migration.
- [ ] Capturar a identidade do token Google (claim `sub`) como `OwnerId`; guardar e-mail/nome p/ exibir.
- [ ] Filtrar **toda** consulta por `OwnerId` e checar posse em get/update/delete (403 se não for dono).
- [ ] Trocar a allowlist de e-mail por "qualquer conta Google" (ou tabela de usuários).
- [ ] Migração: criar a coluna e backfill das notas existentes com o OwnerId atual.

## 4. Autenticação / sessão

- [x] **Persistir / renovar o token** Google na web — token em localStorage (sobrevive a refresh) + auto-select silencioso renovando antes de expirar.
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
