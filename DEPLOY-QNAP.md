# Deploy do UltraNote no QNAP (Container Station + Cloudflare)

Aplicações **independentes** no Container Station, todas na rede Docker `edge`, atrás do
mesmo `cloudflared`. Mexer/recriar um app **não derruba os outros**.

```
            rede "edge" (criada pelo app cloudflared)
   ┌──────────────┬──────────────┬─────────────────┐
cloudflared   app-teste     app-note-api      app-note-web
 (app 1)       (app 2)       (───── app 3: ultranote ─────)

  note.ultrasoft.app.br         → http://app-note-web:8080
  note-api.ultrasoft.app.br     → http://app-note-api:8080
  note.ultrasoftinc.com.br      → http://app-note-web:8080
  note-api.ultrasoftinc.com.br  → http://app-note-api:8080
```

> **Dois domínios de produção** (`ultrasoft.app.br` e `ultrasoftinc.com.br`), cada um com sua
> própria rota `note`/`note-api`. Isso importa pro cookie de sessão dos anexos
> (`SameSite=Lax`, ver §7 do ARCHITECTURE.md): o app detecta o domínio de onde foi carregado e
> chama a API no `note-api.<mesmo domínio>` — se um dos dois não tiver a rota `note-api`
> configurada, os anexos daquele domínio ficam quebrados (a API "some" mesmo a web
> funcionando).

## ⚠️ Particularidade deste NAS: build não tem internet

Os containers da **rede bridge padrão** (que o `docker build` usa) **não têm saída pra
internet** neste QNAP — só as redes criadas pelo Container Station (ex.: `edge`). Por isso
**não buildamos imagem** aqui (o `dotnet restore` não alcança o NuGet no build).

Em vez disso: **publicamos** o app com `docker run` (que aceita `--network edge --dns`) e
rodamos as **imagens oficiais** `aspnet`/`nginx` montando os arquivos publicados.

---

## 1. Google: OAuth Client ID

1. <https://console.cloud.google.com/> → projeto → **Tela de consentimento OAuth** (Externo;
   `negrume@gmail.com` em *Test users*).
2. **Credenciais → ID do cliente OAuth → Aplicativo da Web**.
3. **Origens JavaScript autorizadas**: `https://note.ultrasoft.app.br`,
   `https://note.ultrasoftinc.com.br`, `https://groo.myqnapcloud.com:8443`,
   `http://localhost:5200`, `http://127.0.0.1:5200`.
4. Copie o **Client ID** (o *secret* não é usado).

## 2. Configurar a web

`UltraNote.Web/wwwroot/appsettings.Production.json`:
```json
{
  "ApiBaseUrl": "https://note-api.ultrasoft.app.br",
  "GoogleClientId": "SEU_CLIENT_ID.apps.googleusercontent.com"
}
```

`ApiBaseUrl` só é usado como *fallback* (dev local, ou se o host não bater com o padrão
`note.<domínio>`) — em produção, o app deriva o endereço da API a partir do próprio host da
página (`note.X` → `note-api.X`), pra sempre chamar a API no mesmo domínio de onde foi
carregado (ver nota sobre `SameSite=Lax` acima).

## 3. Código + volumes no NAS

- Código: `/share/Container/ultranote-app` (todas as pastas `UltraNote.*` + `nginx.conf`).
- Volumes: `/share/Container/ultranote-data` e `/share/Container/ultranote-assets`.

## 4. Publicar o .NET (via SSH)

Ligue o SSH (Painel → Telnet/SSH) e conecte: `ssh negrume@GROO`.

```sh
export DOCKER_CONFIG=/tmp/.docker

# API
docker run --rm --network edge --dns 8.8.8.8 \
  -v /share/Container/ultranote-app:/src -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet publish UltraNote.Api/UltraNote.Api.csproj -c Release -o /src/_out_api

# Web
docker run --rm --network edge --dns 8.8.8.8 \
  -v /share/Container/ultranote-app:/src -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet publish UltraNote.Web/UltraNote.Web.csproj -c Release -o /src/_out_web
```

> A rede `edge` precisa existir (é criada pelo app `cloudflared`). Se ainda não criou as 3
> aplicações, crie pelo menos o `cloudflared` antes deste passo.

Confirme: `ls /share/Container/ultranote-app/_out_api/UltraNote.Api.dll` e
`.../_out_web/wwwroot/index.html`.

## 5. Criar as 3 aplicações no Container Station

Aplicações → **Criar**, colando o YAML. Ordem (o `cloudflared` cria a rede `edge`):

| Ordem | Aplicação | YAML |
|-------|-----------|------|
| 1º | `cloudflared` | `../qnap-test-site/docker-compose.cloudflared.yml` |
| 2º | `app-teste` | `../qnap-test-site/docker-compose.app-teste.yml` |
| 3º | `ultranote` | `docker-compose.qnap.yml` (usa imagens oficiais + arquivos publicados; sem build) |

## 6. Rotas Cloudflare

Túnel `qnap` → Routes → Add route (uma entrada por domínio completo, repetir pra cada domínio
de produção):

| Hostname | Service URL |
|----------|-------------|
| `note.ultrasoft.app.br` | `http://app-note-web:8080` |
| `note-api.ultrasoft.app.br` | `http://app-note-api:8080` |
| `note.ultrasoftinc.com.br` | `http://app-note-web:8080` |
| `note-api.ultrasoftinc.com.br` | `http://app-note-api:8080` |

## 7. Acesso alternativo via myQNAPcloud (opcional)

Útil quando os domínios de produção estão bloqueados (ex.: rede corporativa). Diferente das
rotas Cloudflare, aqui web e API dividem o **mesmo hostname** (`groo.myqnapcloud.com`) e são
diferenciadas por **porta**, não subdomínio — o myQNAPcloud só dá um alias por conta.

1. As portas `8543` (web) e `8544` (API) já são expostas em `127.0.0.1` pelo
   `docker-compose.qnap.yml` (não acessíveis direto da LAN/internet).
2. Painel de Controlo → **Acesso à rede → Proxy reverso** → Adicionar, uma regra pra cada:

   | Nome | Origem | Destino |
   |------|--------|---------|
   | `note-web` | `https://groo.myqnapcloud.com:8443` | `http://127.0.0.1:8543` |
   | `note-api` | `https://groo.myqnapcloud.com:8444` | `http://127.0.0.1:8544` |

3. Adicionar `https://groo.myqnapcloud.com:8443` nas Origens JavaScript autorizadas do OAuth
   (§1) e em `Cors:AllowedOrigins` (já presente no `docker-compose.qnap.yml`).

O TLS é terminado pelo proxy reverso do QTS com o certificado do myQNAPcloud; os containers
continuam recebendo HTTP puro internamente, igual ao cloudflared.

## 8. Verificar

```
https://note-api.ultrasoft.app.br/health  → {"status":"ok"}
https://note.ultrasoft.app.br             → login Google (negrume@gmail.com)
https://groo.myqnapcloud.com:8443         → login Google (via proxy reverso do QTS)
```

---

## Atualizar o app depois (quando mudar o código)

1. Copiar o código novo para `/share/Container/ultranote-app` (via K:).
2. Rodar de novo os `docker run ... dotnet publish` do §4 (regenera `_out_api`/`_out_web`).
3. Container Station → recriar **só** a aplicação `ultranote` (ou reiniciar os 2 containers).

> Como rodamos imagens oficiais com os arquivos **montados**, não há build de imagem — a
> atualização é só publicar de novo + reiniciar.

## Notas

- **`ports:` só em `127.0.0.1`** (8543 web / 8544 API) — pro proxy reverso do QTS (§7,
  acesso via myQNAPcloud). Quem fala com o mundo pelos domínios de produção continua sendo
  só o `cloudflared`; essas portas não ficam acessíveis da LAN nem da internet direto.
- API roda como root (escreve nos volumes bind-mounted); não publicada no host.
- Trocar `GoogleClientId`/`ApiBaseUrl`: edite `appsettings.Production.json`, republique a web (§4) e reinicie.
