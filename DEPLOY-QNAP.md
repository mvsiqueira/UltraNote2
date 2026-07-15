# Deploy do UltraNote no QNAP (Container Station + Cloudflare)

Aplicações **independentes** no Container Station, todas na rede Docker `edge`, atrás do
mesmo `cloudflared`. Mexer/recriar um app **não derruba os outros**.

```
            rede "edge" (criada pelo app cloudflared)
   ┌──────────────┬──────────────┬─────────────────┐
cloudflared   app-teste     app-note-api      app-note-web
 (app 1)       (app 2)       (───── app 3: ultranote ─────)

  note.ultrasoft.app.br         → http://app-note-web:8080     (rota antiga)
  note-api.ultrasoft.app.br     → http://app-note-api:8080     (rota antiga)
  note.ultrasoftinc.com.br      → http://app-note-web:8080     (rota antiga)
  note-api.ultrasoftinc.com.br  → http://app-note-api:8080     (rota antiga)
  www.ultrasoft.app.br/ultranote     → http://app-note-web:8080  (rota atual)
  www.ultrasoftinc.com.br/ultranote  → http://app-note-web:8080  (rota atual)
  groo.myqnapcloud.com:8443/ultranote → (via proxy reverso do QTS, ver §7)
```

> **Esquema atual: tudo sob `/ultranote`**. Os 3 domínios de acesso (`www.ultrasoft.app.br`,
> `www.ultrasoftinc.com.br`, `groo.myqnapcloud.com:8443`) servem o UltraNote sob o mesmo
> caminho `/ultranote`, com `www`/raiz reservado pro site institucional (`app-www`). Web e API
> ficam sempre na **mesma origem** (`.../ultranote/api-note/*` — nginx do `app-note-web`
> repassa pra `app-note-api`, ver `nginx.conf`), então nem `SameSite`/CORS entram em jogo. As
> rotas antigas (`note.<domínio>` / `note-api.<domínio>`) continuam ativas em paralelo — ver
> TODO.md pra aposentá-las quando não precisar mais delas.

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
3. **Origens JavaScript autorizadas**: `https://www.ultrasoft.app.br`,
   `https://www.ultrasoftinc.com.br`, `https://groo.myqnapcloud.com:8443`,
   `http://localhost:5200`, `http://127.0.0.1:5200` — mais as antigas
   `https://note.ultrasoft.app.br`/`https://note.ultrasoftinc.com.br` enquanto as rotas
   antigas ainda estiverem no ar. GIS valida só a **origem** (domínio+porta, sem path), então
   o `/ultranote` não entra aqui.
4. Copie o **Client ID** (o *secret* não é usado).

## 2. Configurar a web

`UltraNote.Web/wwwroot/appsettings.Production.json`:
```json
{
  "ApiBaseUrl": "https://note-api.ultrasoft.app.br",
  "GoogleClientId": "SEU_CLIENT_ID.apps.googleusercontent.com"
}
```

`ApiBaseUrl` só é usado como *fallback* (dev local, ou host que não bate com nenhum padrão
conhecido). Em produção, o app deriva o endereço da API a partir da própria URL da página:
se carregado sob `/ultranote/` (esquema atual, ver visão geral acima), chama a API na mesma
origem em `.../ultranote/api-note/`; senão cai nas regras antigas (`note.X` → `note-api.X` /
myQNAPcloud raiz), mantidas só enquanto as rotas antigas convivem em paralelo — ver
`UltraNote.Web/Program.cs`.

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

Túnel `qnap` → Routes → Add route. Esquema atual: `www.<domínio>` com path `/ultranote`,
repetido pros dois domínios de produção, **mais** as rotas antigas (mantidas em paralelo até
serem aposentadas — ver TODO.md):

| Hostname | Path | Service URL |
|----------|------|-------------|
| `www.ultrasoft.app.br` | `^/ultranote` | `http://app-note-web:8080` |
| `www.ultrasoftinc.com.br` | `^/ultranote` | `http://app-note-web:8080` |
| `note.ultrasoft.app.br` *(antiga)* | — | `http://app-note-web:8080` |
| `note-api.ultrasoft.app.br` *(antiga)* | — | `http://app-note-api:8080` |
| `note.ultrasoftinc.com.br` *(antiga)* | — | `http://app-note-web:8080` |
| `note-api.ultrasoftinc.com.br` *(antiga)* | — | `http://app-note-api:8080` |

> **Path é regex, não glob.** `/ultranote*` significa "zero ou mais `e`" (o `*` só afeta o
> caractere anterior), **não** "prefixo `/ultranote` seguido de qualquer coisa" — use
> `^/ultranote` (prefixo ancorado), igual ao exemplo que o próprio painel mostra.
>
> **Ordem importa.** As rotas de um mesmo hostname são avaliadas de cima pra baixo e a
> **primeira que bater vence** (não é "mais específica vence"). A rota `^/ultranote` de
> `www.<domínio>` precisa estar **acima** da rota catch-all (sem path) que aponta pro
> `app-www` no mesmo hostname, senão o catch-all sempre ganha e o UltraNote nunca é
> alcançado.

## 7. Acesso alternativo via myQNAPcloud (opcional)

Útil quando os domínios de produção estão bloqueados (ex.: rede corporativa). Diferente das
rotas Cloudflare, aqui web e API dividem o **mesmo hostname e a mesma porta**
(`groo.myqnapcloud.com:8443`) — o myQNAPcloud só dá um alias por conta, e uma segunda porta
pra API se mostrou pouco confiável (proxy corporativo bloqueou o `CONNECT` numa porta não-
padrão nos nossos testes). O app fica em `groo.myqnapcloud.com:8443/ultranote`, mesmo esquema
de caminho dos outros 2 domínios (§6) — o proxy reverso do QTS aponta o hostname inteiro pro
`app-note-web`, e o nginx dele resolve o `/ultranote/*` (incluindo `/ultranote/api-note/*` →
`app-note-api:8080`) igual em qualquer porta de entrada — ver `nginx.conf`.

1. A porta `8543` já é exposta em `127.0.0.1` pelo `docker-compose.qnap.yml` (não acessível
   direto da LAN/internet).
2. Painel de Controlo → **Acesso à rede → Proxy reverso** → Adicionar:

   | Nome | Origem | Destino |
   |------|--------|---------|
   | `note-web` | `https://groo.myqnapcloud.com:8443` | `http://127.0.0.1:8543` |

3. Adicionar `https://groo.myqnapcloud.com:8443` nas Origens JavaScript autorizadas do OAuth
   (§1). Não precisa de `Cors:AllowedOrigins` pra esse domínio — web e API são a mesma
   origem, então nem CORS entra em jogo.

O TLS é terminado pelo proxy reverso do QTS com o certificado (autoassinado, salvo
configuração — ver nota abaixo) do NAS; os containers continuam recebendo HTTP puro
internamente, igual ao cloudflared.

> **Certificado não confiável**: por padrão o proxy reverso do QTS usa o certificado
> autoassinado do NAS — o navegador avisa "não seguro", mas funciona (Avançado → Continuar).
> Solução definitiva pendente: ver TODO.md (gateway Caddy do `qnap-test-site`, que emite
> certificado Let's Encrypt de verdade pro myQNAPcloud).

> **NAT duplo (modem + roteador)**: se o modem da operadora também fizer roteamento (não só
> bridge), o port forward precisa ser feito **nos dois**: no modem, apontando pro IP do
> roteador (visto na rede do modem, não o gateway `192.168.x.1` do roteador); no roteador,
> apontando pro IP do NAS. Confirme o IP do WAN do roteador — se for privado, é NAT duplo.

## 8. Verificar

```
https://www.ultrasoft.app.br/ultranote/api-note/health     → {"status":"ok"}
https://www.ultrasoft.app.br/ultranote                     → login Google (negrume@gmail.com)
https://www.ultrasoftinc.com.br/ultranote                  → login Google
https://groo.myqnapcloud.com:8443/ultranote                → login Google (via proxy reverso do QTS)

# rotas antigas, enquanto ainda estiverem no ar:
https://note-api.ultrasoft.app.br/health  → {"status":"ok"}
https://note.ultrasoft.app.br             → login Google
```

---

## Atualizar o app depois (quando mudar o código)

1. Copiar o código novo para `/share/Container/ultranote-app` (via K:).
2. Rodar de novo os `docker run ... dotnet publish` do §4 (regenera `_out_api`/`_out_web`).
3. Container Station → recriar **só** a aplicação `ultranote` (ou reiniciar os 2 containers).

> Como rodamos imagens oficiais com os arquivos **montados**, não há build de imagem — a
> atualização é só publicar de novo + reiniciar.

## Notas

- **`ports:` só em `127.0.0.1`** (8543, só a web) — pro proxy reverso do QTS (§7, acesso via
  myQNAPcloud). A API não expõe porta própria: o nginx da web repassa `/api-note/*` pra ela
  por dentro da rede `edge` (ver `nginx.conf`). Quem fala com o mundo pelos domínios de
  produção continua sendo só o `cloudflared`; essa porta não fica acessível da LAN nem da
  internet direto.
- API roda como root (escreve nos volumes bind-mounted); não publicada no host.
- Trocar `GoogleClientId`/`ApiBaseUrl`: edite `appsettings.Production.json`, republique a web (§4) e reinicie.
