# UltraNote2 — Arquitetura

Porte para **Windows .NET** do app de notas (tipo Evernote) que hoje vive em
`..\UltraNote` (Tauri 2 + React + editor TipTap, *local-first* com notas `.html`
em árvore de pastas). O objetivo do porte é centralizar os dados num container
Docker no file server **QNAP**, de modo que tanto um app **Windows** quanto uma
futura versão **web** acessem as mesmas notas de qualquer lugar.

> Status: implementado e em produção no QNAP desde 2026-07. `UltraNote.Desktop` (MAUI) ainda pendente.

---

## 1. A virada de chave

O app atual é **local-first com arquivos**. O destino é **client-server com
dados centralizados**: o coração passa a ser uma **API ASP.NET Core** rodando no
QNAP; Windows e web são apenas clientes dela.

```
   ┌─────────────────┐         ┌──────────────────┐
   │  App Windows     │         │  Navegador (web) │
   │  (.NET / Blazor) │         │  futuro          │
   └────────┬─────────┘         └─────────┬────────┘
            │   HTTPS (REST/JSON)         │
            └──────────────┬──────────────┘
                           │  note-api.ultrasoft.app.br (cloudflared)
                  ┌────────▼─────────┐
                  │  API ASP.NET Core│   ← container no QNAP
                  │  (UltraNote.Api) │
                  └────┬────────┬────┘
                       │        │
              ┌────────▼──┐  ┌──▼──────────────┐
              │  SQLite   │  │ Volume anexos    │  ← volumes Docker no NAS
              │ (.db file │  │ /assets          │
              │  em vol.) │  │                  │
              └───────────┘  └──────────────────┘
```

---

## 2. Decisões (e porquês)

| Tema | Decisão | Porquê |
|------|---------|--------|
| UI .NET | **Blazor** — desktop via **Blazor Hybrid (WebView2, host MAUI)** e web via Blazor, com componentes Razor compartilhados | Máximo reaproveitamento entre desktop e web; permite **manter o editor TipTap** como componente JS no WebView |
| Editor | **Reaproveitar o TipTap** atual (interop JS), não reescrever em controle nativo | O editor atual é robusto (tabelas com resize de linha/coluna, cores, realce, indentação, menus de contexto) — é o ativo mais valioso |
| Conexão | **Online com cache local leve** | Bem mais simples que offline-first; suficiente para uso pessoal |
| Armazenamento | **SQLite** em volume + **volume separado** para anexos | Robusto para 1 usuário, busca/metadados sólidos, backup trivial (copiar o `.db`) |
| Auth | **"Entrar com Google" (OIDC)** no próprio app; API valida o token e confere allowlist de e-mail | Cobre web **e** as chamadas REST do desktop com o mesmo modelo (ver §7) |
| Deploy | Mais um app no **padrão multi-app do QNAP** (`cloudflared` + subdomínio) | Reusa a infra já existente em `..\qnap-test-site` |

---

## 3. Estrutura da solução (.NET)

Uma única solution; os projetos compartilhados são o que faz desktop e web
dividirem o mesmo código.

```
UltraNote2.sln
├─ UltraNote.Core         // modelos, DTOs, interfaces (sem dependência de UI/infra)
├─ UltraNote.Api          // ASP.NET Core Web API  → vira o container no QNAP
│   ├─ EF Core + SQLite
│   ├─ Endpoints: pastas, notas, anexos
│   └─ Auth: validação do token Google + allowlist
├─ UltraNote.Client       // HttpClient tipado que fala com a API (usado pelos dois fronts)
├─ UltraNote.UI           // Razor Class Library: componentes compartilhados (sidebar, lista, editor)
├─ UltraNote.Desktop      // MAUI Blazor Hybrid (WebView2) → app Windows
└─ UltraNote.Web          // Blazor (web) → futuro container no QNAP
```

`UltraNote.UI` contém o editor e as telas **uma vez só**; `Desktop` e `Web` são
cascas finas que hospedam esses componentes. O TipTap entra via interop JS dentro
de `UltraNote.UI`.

---

## 4. Modelo de dados (SQLite)

Saímos dos arquivos `.html` soltos para tabelas — busca, ordenação e metadados
ficam sólidos. O **conteúdo da nota continua sendo HTML** (o mesmo que o TipTap
produz hoje), guardado numa coluna.

```
Folder(Id, ParentId, Name, Path, CreatedAt, UpdatedAt)
Note(Id, FolderId, Title, ContentHtml, CreatedAt, UpdatedAt, IsFavorite, IsArchived)
Attachment(Id, NoteId, FileName, ContentType, StoragePath, CreatedAt)
```

- Anexos (imagens coladas no editor) **não** vão no banco: vão para o volume de
  anexos; a tabela guarda só o caminho (`StoragePath`).
- SQLite em modo **WAL** aguenta com folga o uso pessoal.
- Backup = copiar `ultranote.db` + a pasta de anexos.

---

## 5. Contrato da API (REST/JSON)

Espelha os comandos que o app já tem hoje.

```
GET    /api/folders                     → árvore de pastas
POST   /api/folders                     → cria
PUT    /api/folders/{id}                → renomeia/move
DELETE /api/folders/{id}                → exclui (com filhos)
GET    /api/folders/{id}/notes          → notas da pasta
GET    /api/folders/{id}/archived-count → notas arquivadas na pasta + descendentes (aviso pré-exclusão)
GET    /api/notes/{id}                  → nota + conteúdo
GET    /api/notes/favorites             → notas favoritadas
GET    /api/notes/archived-count        → total de notas arquivadas na biblioteca
POST   /api/notes                       → cria
PUT    /api/notes/{id}                  → salva
DELETE /api/notes/{id}
POST   /api/notes/{id}/attachments      → upload de imagem/anexo
```

Todos exigem `Authorization: Bearer <token Google>` **ou** o cookie de sessão
(`POST /api/auth/session`, ver §7) — a API confere o e-mail na allowlist antes
de responder, por qualquer um dos dois caminhos.

---

## 6. Cache leve (online)

O `UltraNote.Client` mantém um SQLite local pequeno no desktop só para
resiliência: ao abrir, mostra o último estado conhecido e revalida contra a API.
**Sem** fila de sincronização nem resolução de conflito (isso seria offline-first,
fora de escopo por ora). Salvar exige conexão.

---

## 7. Autenticação com Google

**Escolhido — "Entrar com Google" no app (OIDC):** o app autentica no Google,
recebe um token, a **API** valida o token e confere se o e-mail está na allowlist
(`negrume@gmail.com`). Só então libera. Cobre web e as chamadas REST do desktop
com o mesmo modelo. Requer registrar um *OAuth Client* no Google Cloud Console.

**Cookie de sessão para anexos:** `<img src>`/`<a href>` embutidos no HTML da
nota apontam pra `GET /api/attachments/{id}` — o navegador busca esses recursos
como requisição "crua" (não passa pelo `HttpClient` autenticado do app), então
nunca carrega o Bearer token. Solução: o app chama `POST /api/auth/session`
(com o Bearer) toda vez que obtém um token novo (login e renovação silenciosa),
e a API devolve um cookie `HttpOnly; Secure; SameSite=Lax` (esquema de auth
separado, `AttachmentCookie`, ver `GoogleAuth.cs`) que o navegador passa a
enviar sozinho em qualquer requisição pro domínio da API — incluindo imagens e
cliques em link. O cookie é *stateless* (claims assinadas pelo próprio
ASP.NET Core Data Protection), expira em 24h com renovação deslizante, e
qualquer endpoint aceita Bearer **ou** esse cookie (mesma policy de allowlist).
Exige CORS com origem específica + `AllowCredentials` em produção (`Cors:AllowedOrigins`
no `appsettings`/`docker-compose.qnap.yml`) — em dev, sem origens configuradas,
cai no CORS permissivo de sempre (auth desligada localmente, o fluxo nem roda).

**Descartado como principal — Cloudflare Access (Zero Trust):** protegeria
`note.ultrasoft.app.br` na borda usando Google como provedor, sem código de auth
no app. Problema: as chamadas REST do desktop (Blazor Hybrid) rodam fora do
navegador e não carregam o cookie da Cloudflare, exigindo *service tokens*.
Pode, opcionalmente, virar **camada extra** só na URL web no futuro.

---

## 8. Deploy no QNAP

Segue o padrão multi-app já existente (ver `..\qnap-test-site\MULTI-APP.md`): API e
web são services com `expose`, atrás do mesmo `cloudflared`, em
`note-api.ultrasoft.app.br` e `note.ultrasoft.app.br`. Dois volumes persistentes no NAS
(SQLite + anexos). O passo a passo completo, com o `docker-compose` pronto e as rotas
Cloudflare, está em [DEPLOY-QNAP.md](DEPLOY-QNAP.md) / [docker-compose.qnap.yml](docker-compose.qnap.yml).

---

## 9. Ordem de construção

1. `Core` + `Api` + SQLite + endpoints (testável via curl/Swagger).
2. Auth Google (registro no Google Cloud + validação na API).
3. `UI` (componentes Razor) + integração do TipTap.
4. `Desktop` (MAUI Blazor Hybrid) consumindo a API → primeiro app utilizável.
5. Dockerizar a API e subir no QNAP.
6. Mais adiante: `UltraNote.Web`.
