# TODO — UltraNote

Backlog de evolução. Marque `[x]` ao concluir. Prioridade sugerida de cima para baixo.
Itens concluídos vivem em "Já entregue", no final do arquivo.

---

## 1. UX e robustez

- [x] **Adaptar para mobile** — layout mestre-detalhe abaixo de 768px: a árvore ocupa a tela
  toda até abrir uma nota, que vira editor em tela cheia com botão "Voltar" (que passa pelo
  aviso de alterações não salvas, igual trocar de nota). Toolbars com alvos de toque maiores
  e a do editor rico com scroll horizontal. Recarregar a página restaura a nota selecionada
  (destacada na árvore) sem abrir o editor automaticamente. Duas pegadinhas de layout que
  vale lembrar: input dentro de flexbox não encolhe sem `min-width: 0` (`.title-input`
  empurrava os botões de favoritar/arquivar/salvar pra fora da tela); e uma única coluna de
  grid (`1fr`) tem o mesmo problema — precisa de `minmax(0, 1fr)`, senão o grid não encolhe
  abaixo do conteúdo mínimo (`.body` ficava mais largo que a viewport, sem scroll visível).

## 2. Multiusuário (segregação de dados)

> Hoje o app é **single-user**: a allowlist controla quem entra, mas os dados são uma
> biblioteca única compartilhada (sem dono). Para abrir a vários usuários com dados
> separados, plano em 5 passos:
- [ ] Adicionar `OwnerId` em `Folder` e `Note` (Attachment herda via nota) + migration.
- [ ] Capturar a identidade do token Google (claim `sub`) como `OwnerId`; guardar e-mail/nome p/ exibir.
- [ ] Filtrar **toda** consulta por `OwnerId` e checar posse em get/update/delete (403 se não for dono).
- [ ] Trocar a allowlist de e-mail por "qualquer conta Google" (ou tabela de usuários).
- [ ] Migração: criar a coluna e backfill das notas existentes com o OwnerId atual.

## 3. Autenticação / sessão

- [ ] (Opcional) **Cloudflare Access** como camada extra na URL da web.

## 4. App Windows (desktop)

- [ ] Criar `UltraNote.Desktop` (**MAUI Blazor Hybrid**), reaproveitando os componentes `UltraNote.UI`.
  - [ ] Requer `dotnet workload install maui` (não instalado; só `maui-android`).
  - [ ] Login Google via **WebAuthenticator** (em vez do GIS da web).
- [ ] (Opcional) Cache local leve p/ resiliência offline.

## 5. Infra / Deploy / Qualidade

- [x] **Unificar o UltraNote em `/ultranote` nos 3 domínios** (`www.ultrasoft.app.br`,
  `www.ultrasoftinc.com.br`, `groo.myqnapcloud.com:8443`), com `www`/raiz reservado pro site
  institucional (`app-www`). Base href dinâmico via script inline no `index.html` (detecta
  `/ultranote/` no `location.pathname` antes de carregar o Blazor — resolveu a "pegadinha"
  que estava anotada aqui). `Program.cs` deriva a URL da API de forma única (mesma origem +
  `/ultranote/api-note/`), com fallback só pro myQNAPcloud raiz (sem prefixo). Rotas
  Cloudflare: hostname `www.<domínio>`, path `^/ultranote` (regex — cuidado, não é glob) —
  **precisa vir antes** da rota catch-all pro `app-www` no mesmo hostname (Cloudflare casa
  a primeira rota que bater, não a mais específica).
  - [x] **Aposentar as rotas antigas** — `note.*`/`note-api.*` removidas do Cloudflare, e o
    branch de fallback correspondente removido do `Program.cs`.
- [ ] **Servir mais de um app via myQNAPcloud** — hoje `groo.myqnapcloud.com:8443` inteiro
  aponta só pro `app-note-web` (o proxy reverso do QTS não divide por caminho, só host+porta),
  então nem o `app-www` aparece lá. Sem necessidade concreta agora (não tem outro app pra
  publicar) — só entra em jogo se aparecer. Duas formas, do mais simples pro mais robusto:
  - [ ] **Estender o nginx do `app-note-web`** pra também repassar caminhos fora de
    `/ultranote` pro `app-www:3000` (mesma técnica já usada pro `/api-note/`) — zero infra
    nova, só configuração. Mais simples, escala bem pra 2-3 apps.
  - [ ] **Gateway Caddy** — mais "correto" a longo prazo (certificado Let's Encrypt de
    verdade em vez do autoassinado do QTS, config dedicada de roteamento), mas custa mais:
    liberar a porta 80 no roteador/modem (NAT duplo) e substituir o proxy reverso do QTS que
    já está funcionando. Já existe um `Caddyfile` parcialmente pronto em `../qnap-test-site`
    com `groo.myqnapcloud.com` configurado, faltando o `docker-compose` que sobe o Caddy na
    rede `edge` e o roteamento por caminho pros apps reais (hoje só aponta pro app de teste).
- [ ] Automatizar o fluxo de atualização (script: copiar p/ NAS → `docker run ... publish` → recriar app).
- [ ] **Backup** automatizado de `/share/Container/ultranote-data` e `/ultranote-assets` (nível NAS — complementa o backup/restore manual já disponível no app, ver "Já entregue").
- [ ] (Dev) Decidir se o `appsettings.json` local fica **sem** `GoogleClientId` (dev sem login, mais ágil) — produção continua protegida.
- [ ] Testes automatizados (API: endpoints; UI: componentes principais).
- [ ] Healthcheck de container / monitor de uptime.
- [ ] App Windows (desktop): `UltraNote.Desktop` — MAUI Blazor Hybrid (ver §4).

---

## Notas / armadilhas conhecidas (ver DEPLOY-QNAP.md)

- Containers neste NAS só têm internet na rede `edge` **e** precisam de `dns:` explícito
  (a API usa pra validar o token Google; sem isso → "signature key was not found").
- O `docker build` não tem internet no NAS → publicamos com `docker run` e rodamos imagens
  oficiais com os arquivos montados (sem build de imagem).
- Atualização da web = republicar `_out_web` + hard refresh (nginx serve arquivos montados).
- Dois domínios de produção (`ultrasoft.app.br` e `ultrasoftinc.com.br`), cada um com sua
  própria rota `note`/`note-api` no Cloudflare Tunnel — o cookie de sessão dos anexos é
  `SameSite=Lax`, então web e API precisam estar sempre no mesmo domínio (ver DEPLOY-QNAP.md).

---

## Já entregue

### Fundação

- [x] API ASP.NET Core + EF Core/SQLite (pastas, notas, anexos) com migrations.
- [x] Auth Google (OIDC) na web + allowlist de e-mail na API.
- [x] Web Blazor WebAssembly com "Entrar com Google".
- [x] Deploy no QNAP (3 apps na rede `edge`, atrás do cloudflared), dois domínios de produção: `ultrasoft.app.br` e `ultrasoftinc.com.br`.
- [x] Repositório no GitHub.

### Editor (o coração — reaproveitado do app original)

- [x] Migrar o editor de `contenteditable` para **TipTap** (`UltraNote.UI/RichTextEditor.razor` + `editor-src/main.js` → bundle em `wwwroot/ultranote-editor.js`).
  - [x] **Bundling de JS** (npm + esbuild) em `UltraNote.UI/editor-src/` — bundle minificado é **commitado** (o NAS não roda npm); rebuild local: `cd UltraNote.UI/editor-src && npm run build`.
  - [x] Tabelas com **resize de linha** (drag custom, portado do app original) **e coluna** (recurso nativo do TipTap).
  - [x] **Cores** de fonte e **realce** (highlight), com paleta + seletor de cor customizada.
  - [x] **Indentação** (aumentar/diminuir recuo, `data-indent` attribute).
  - [x] Menu de contexto (botão direito): formatação sempre; operações de tabela só dentro de uma tabela.
- [x] **Colar/dropar/toolbar imagem** no editor → upload via API → embutida no HTML com suporte a redimensionamento e menu de contexto (excluir imagem).
- [x] **Anexos no corpo da nota** — menu de contexto do anexo (painel abaixo do editor) tem "Inserir link no texto": cria um `<a class="attachment-link">` apontando pro download, com ícone de clipe e sublinhado tracejado (mais simples que um file node customizado no TipTap; anexo continua listado no painel também).
- [x] **Remover formatação** — botão na toolbar que limpa todas as marcas (negrito, cor, realce, etc.) do texto selecionado.
- [x] **Melhorar menu de contexto** — revisar itens, agrupar melhor, adicionar ícones mais claros.
- [x] **Melhorar color picker** — paleta mais rica, preview em tempo real, histórico de cores usadas.

### Gestão de notas e pastas

- [x] **Excluir nota** — menu de contexto.
- [x] **Renomear pasta** — menu de contexto.
- [x] **Buscar** notas (por título) — caixa de busca na barra superior.
- [x] **Mover** nota/pasta — menu de contexto "Mover...", modal com seletor de destino (pastas descendentes excluídas para evitar ciclo).
- [x] **Salvar estado** (pastas abertas e fechadas e nota selecionada)
- [x] **Importar do Evernote** — importar arquivo `.enex` (formato de exportação do Evernote): criar pastas/notas preservando estrutura e conteúdo HTML.
  - [x] Erros de upload de anexo durante a importação (rede, resposta inválida da API) agora aparecem na tela em vez de falhar silenciosamente — antes um anexo que falhasse virava um link quebrado sem nenhum aviso.
  - [x] **Causa raiz do "anexo não vem" em produção**: `GET /api/attachments/{id}` exigia login, mas o link é acessado pelo navegador como requisição crua (sem o Bearer token) — sempre dava 401. Só reproduzia em produção (dev roda sem auth). Corrigido com cookie de sessão (ver "Autenticação / sessão").
- [x] **Notas favoritas** — seção "Favoritos" na barra lateral (mostrar/ocultar via toolbar, estado persistido em localStorage), toggle por estrela no cabeçalho do editor e no menu de contexto, indicador na árvore.
- [x] **Notas arquivadas** — nota fica no lugar (mesma pasta), mas oculta por padrão; toggle "Exibir arquivadas" na toolbar (persistido em localStorage) revela tudo de uma vez, com indicador visual e nota meio apagada. "Arquivar/Desarquivar" no menu de contexto e no cabeçalho do editor. Busca também ignora arquivadas por padrão. Excluir uma pasta com notas arquivadas dentro mostra aviso específico (`GET /api/folders/{id}/archived-count`, recursivo).
- [x] **Backup / restaurar biblioteca** — menu de contexto de pasta ganhou "Exportar pasta (.zip)"; Ferramentas ganhou "Exportar backup completo (.zip)" e "Importar backup (.zip)". Cada pasta vira um `.enex` dentro do zip (caminho espelha a árvore, `Viagens/Roteiros/_notes.enex`), anexos embutidos como recursos binários. Restore recria a árvore de pastas e reimporta as notas preservando cor/realce/tamanho de tabela (ao contrário do import de `.enex` do Evernote real, que descarta esses estilos de propósito).

### UX e robustez

- [x] **Guarda de alterações não salvas** — modal de confirmação ao trocar de nota com edição pendente (cancelar/descartar), + aviso nativo do navegador ao fechar/recarregar a aba.
- [x] **Mostrar erros da API na UI** — toda operação de escrita (criar/renomear/excluir/salvar) reporta erro no status bar.
- [x] Indicador de "salvando…" — botão mostra "Salvando…" e fica desabilitado durante a chamada.
- [x] Atalho de teclado **Ctrl+S** (ou Cmd+S) salva a nota aberta, sem disparar o "Salvar página" do navegador.
- [x] **Barra de ferramentas acima da árvore** — ações rápidas (nova pasta, nova nota, recolher tudo, expandir tudo) + nó raiz "Notas" para criação de pastas na raiz.
- [x] **Tela de About** — versão do app, créditos, links (GitHub, etc.).
- [x] **Redimensionar a barra lateral** — arrastar a borda entre a sidebar e o editor (180–480px), estado persistido em localStorage, sem round-trip pro Blazor (JS puro).
- [x] **Mover pastas e notas por drag-and-drop** — arrastar um item da árvore e soltar sobre uma pasta (ou sobre a linha "Notas" pra mandar uma pasta pra raiz) reaproveita a mesma lógica do "Mover..." do menu de contexto. Precisou de `StateHasChanged()` explícito no fim do drop — o evento é despachado pro `TreeNode` (componente filho) que recebeu o drop, então o Blazor só re-renderiza aquela instância por padrão; sem forçar a partir da raiz, a pasta/nota de origem (instância irmã) ficava com aparência desatualizada até um clique nela.

### Autenticação / sessão

- [x] **Persistir / renovar o token** Google na web — token em localStorage (sobrevive a refresh) + auto-select silencioso renovando antes de expirar.
- [x] Restringir **CORS** da API para os domínios de produção (`Cors:AllowedOrigins`, `AllowCredentials`); dev sem origens configuradas continua permissivo.
- [x] **Bloquear login de contas fora da allowlist** — antes, qualquer conta Google válida entrava na UI (só as chamadas de API voltavam vazias/com erro). Agora `POST /api/auth/session` é checado *antes* de liberar acesso; conta não-autorizada cai numa tela de "sem acesso" em vez de ver o app. Corrigido também um bug relacionado: falha transitória (rede/CORS) na verificação só é tolerada se for renovação da *mesma* conta já logada — trocar de conta durante uma falha não deixa mais entrar sem checar o e-mail da conta nova.
- [x] **Cookie de sessão para anexos** — `<img src>`/`<a href>` embutidos na nota não carregam o Bearer token (requisição crua do navegador). `POST /api/auth/session` (chamado a cada login/renovação de token) planta um cookie `HttpOnly; Secure; SameSite=Lax` (esquema `AttachmentCookie`, expira em 24h com renovação deslizante) que a API aceita como alternativa ao Bearer em qualquer endpoint. Revogável trocando a chave de Data Protection — ao contrário do GUID público que era usado antes.
  - [x] Chave de Data Protection persistida em `/data/keys` (mesmo volume do SQLite) — sem isso, toda recriação do container trocava a chave e invalidava todo cookie já emitido.
  - [x] Web deriva o endereço da API a partir do próprio host da página (`note.X` → `note-api.X`) em vez de um endereço fixo — mantém web e API sempre no mesmo domínio, necessário pro `SameSite=Lax` funcionar em qualquer um dos dois domínios de produção.
- [x] **Acesso via myQNAPcloud** (`groo.myqnapcloud.com:8443`) — alternativa pra quando os domínios de produção estão bloqueados (rede corporativa). Web e API na mesma origem (porta única): o nginx do `app-note-web` repassa `/api-note/*` internamente pra `app-note-api:8080`, então nem CORS é necessário. Chegamos nisso depois de testar com uma segunda porta dedicada pra API e o proxy da empresa bloquear o `CONNECT` nela — proxies corporativos costumam só liberar túnel HTTPS pra portas "conhecidas". Certificado ainda é o autoassinado do QNAP (aviso "não seguro", mas funciona); solução definitiva com Caddy + Let's Encrypt fica pendente (ver Infra/Deploy).

### Visual / Identidade

- [x] **Alterar logo** — ícone SVG inline no brand do topbar (papel com dobra + sparkle dourado Ultrasoft); tema escuro/claro via CSS.
- [x] **Alterar favicon** — favicon SVG + PNG (32×32, 192×192, 512×512) com ícone do UltraNote.
