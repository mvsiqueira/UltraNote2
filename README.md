# UltraNote2

Porte .NET de um app de notas tipo Evernote (ver [ARCHITECTURE.md](ARCHITECTURE.md) para o
desenho completo). Dados centralizados numa API ASP.NET Core + SQLite, acessíveis de
qualquer lugar.

**Status:** web **no ar** em `https://note.ultrasoft.app.br` (login Google), API em
`https://note-api.ultrasoft.app.br`, hospedadas no QNAP via Docker + Cloudflare Tunnel.
App Windows (Blazor Hybrid) e a migração do editor para TipTap são os próximos passos.

## Projetos

| Projeto | Papel | Status |
|---------|-------|--------|
| `UltraNote.Core` | Modelos (entidades) e DTOs do contrato da API | ✅ |
| `UltraNote.Api` | Web API (EF Core + SQLite) + auth Google (flag) | ✅ |
| `UltraNote.Client` | Cliente HTTP tipado (desktop + web) | ✅ |
| `UltraNote.UI` | Componentes Razor compartilhados + editor | ✅ (editor = 1ª versão, ver nota) |
| `UltraNote.Web` | Versão web (Blazor WASM) + login Google | ✅ roda, fala com a API e tem "Entrar com Google" |
| `UltraNote.Desktop` | App Windows (MAUI Blazor Hybrid) | ⏳ (requer workload `maui`) |

> **Editor**: a 1ª versão usa um `contenteditable` simples (negrito/itálico/listas/link via
> `execCommand`), isolado em `UltraNote.UI/RichTextEditor.razor` + `wwwroot/ultranote-editor.js`.
> A troca pelo **TipTap** (reaproveitando o editor do app original) é um passo focado: só
> esses dois arquivos mudam.

## Rodar a API localmente

```powershell
cd UltraNote.Api
dotnet run
```

- A base SQLite (`ultranote.db`) é criada/migrada automaticamente no startup (modo WAL).
- Em ambiente Development, o Swagger fica em `/swagger`.
- Health check em `/health`.

### Configuração (`appsettings.json`)

- `ConnectionStrings:Db` — caminho do SQLite (padrão `Data Source=ultranote.db`).
- `Storage:AssetsPath` — pasta dos anexos (vazio = `<contentRoot>/assets`).
- `Auth:Enabled` — quando `true`, a API exige token Google válido e e-mail na allowlist
  (`Auth:AllowedEmails`). Em dev fica `false`. Ver "Autenticação" abaixo.

## Rodar API + Web juntos (dev)

A versão web é um Blazor WebAssembly que chama a API por HTTP (CORS liberado em dev).

```powershell
# Terminal 1 — API na porta 5099 (a Web aponta pra ela em wwwroot/appsettings.json)
cd UltraNote.Api
dotnet run --urls http://127.0.0.1:5099

# Terminal 2 — Web na porta 5200
cd UltraNote.Web
dotnet run --urls http://127.0.0.1:5200
```

Abra `http://127.0.0.1:5200`. O `ApiBaseUrl` da web fica em
`UltraNote.Web/wwwroot/appsettings.json`.

## Autenticação (Google OIDC)

Fluxo completo já implementado nas duas pontas:

- **Web** (`UltraNote.Web`): botão "Entrar com Google" via Google Identity Services. O
  navegador obtém o ID token e o envia como `Authorization: Bearer` em toda chamada
  (`BearerTokenHandler`). Controlado por `GoogleClientId` no `appsettings[.Production].json`
  — **vazio = auth desligada** (dev abre direto).
- **API** (`UltraNote.Api`): valida o token do Google (audience = `Auth:GoogleClientId`,
  issuer Google) e confere o e-mail na allowlist (`Auth:AllowedEmails`). Controlado por
  `Auth:Enabled` — **`false` = aberta** (dev).

Para ligar de verdade (produção), você precisa criar o *OAuth Client ID* no Google Cloud e
preencher os dois lados — passo a passo em [DEPLOY-QNAP.md](DEPLOY-QNAP.md) §1–2,5.

> Falta só no desktop (MAUI): lá o login usa `WebAuthenticator` em vez do GIS — passo
> futuro junto do app Windows.

## Endpoints (passo 1)

```
GET    /api/folders                 árvore (lista plana ordenada por path)
GET    /api/folders/{id}
GET    /api/folders/{id}/notes      resumos das notas da pasta
POST   /api/folders                 { parentId?, name }
PUT    /api/folders/{id}            { name?, parentId? }   (renomear/mover; recalcula paths)
DELETE /api/folders/{id}            (cascade: subpastas + notas + anexos)

GET    /api/notes/{id}
POST   /api/notes                   { folderId, title, contentHtml? }
PUT    /api/notes/{id}              { title?, contentHtml?, folderId? }
DELETE /api/notes/{id}

POST   /api/notes/{id}/attachments  multipart (campo "file")
GET    /api/attachments/{id}        download do binário
DELETE /api/attachments/{id}
```

Todos exigem `Authorization: Bearer <token Google>` quando a auth está ligada.

## Deploy (QNAP + Cloudflare)

Em produção rodam **3 aplicações independentes** no Container Station, ligadas por uma rede
Docker `edge`, atrás de um `cloudflared`: `cloudflared`, `app-teste` e `ultranote`
(API + web). Passo a passo completo em [DEPLOY-QNAP.md](DEPLOY-QNAP.md); compose em
[docker-compose.qnap.yml](docker-compose.qnap.yml).

- Banco e anexos ficam em volumes (`/data`, `/assets`); a migration roda no startup.
- **Particularidade do NAS**: a rede de build do Docker não tem internet, então não se
  builda imagem lá — o app é **publicado** (`dotnet publish` via `docker run` na rede
  `edge`) e servido por imagens oficiais (`aspnet`/`nginx`) com os arquivos montados.
- Os `Dockerfile`/`Dockerfile.web` (build multi-stage) continuam no repo para ambientes
  cujo build tenha internet.

## Migrations

```powershell
dotnet ef migrations add <Nome> --project UltraNote.Api
```
(aplicadas automaticamente no startup via `Database.Migrate()`)
