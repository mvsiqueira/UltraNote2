# TODO — UltraNote

Backlog de evolução. Marque `[x]` ao concluir. Prioridade sugerida de cima para baixo.
Itens concluídos vivem em "Já entregue", no final do arquivo.

---

## 1. UX e robustez

- [x] **Adaptar para mobile** — layout mestre-detalhe abaixo de 768px: a árvore ocupa a tela
  toda até abrir uma nota, que vira editor em tela cheia com botão "Voltar" (que passa pelo
  aviso de alterações não salvas, igual trocar de nota). Toolbars com alvos de toque maiores
  e a do editor rico com scroll horizontal. Recarregar a página restaura a nota selecionada
  (destacada na árvore) sem abrir o editor automaticamente. Botão de modo leitura/edição no
  topbar perde o texto abaixo de 768px, só ícone, igual os outros botões dessa faixa. Duas
  pegadinhas de layout que vale lembrar: input dentro de flexbox não encolhe sem
  `min-width: 0` (`.title-input` empurrava os botões de favoritar/arquivar/salvar pra fora da
  tela); e uma única coluna de grid (`1fr`) tem o mesmo problema — precisa de
  `minmax(0, 1fr)`, senão o grid não encolhe abaixo do conteúdo mínimo (`.body` ficava mais
  largo que a viewport, sem scroll visível).
- [x] **Modo leitura / edição** — app abre em modo leitura por padrão: título, corpo e anexos
  travados, toolbar do editor some, e o menu de formatação (clique direito/toque longo) nunca
  intercepta a seleção nativa de texto nesse modo — depende só de `editor.isEditable`, não de
  detectar toque vs. mouse (a tentativa anterior com detecção de toque era inconsistente entre
  navegadores). Botão no topbar alterna os modos, com o aviso de alterações não salvas de
  sempre ao sair do modo edição. Pegadinha: `editor.setEditable()` do TipTap dispara o evento
  de "mudança" por padrão mesmo só trocando o modo — suprimido (`emitUpdate = false`) pra não
  marcar a nota como suja sem motivo.
- [x] **Painel de anexos: mover pra lateral + redimensionar** — botão no cabeçalho do painel
  alterna entre embaixo do editor (padrão) e à direita; preferência e tamanho (largura na
  lateral, altura embaixo) persistidos em localStorage, mesma técnica de arrastar do painel
  esquerdo. Pegadinha: a variável CSS do tamanho precisa ficar no elemento **pai** que o
  painel e a alça de arrastar compartilham — variável CSS só herda de pai pra filho, então
  botar no painel sozinho deixava a alça (irmã, não filha) sempre no tamanho padrão do CSS,
  sem acompanhar o redimensionamento real (só parecia certo por coincidência antes do
  primeiro resize).
- [x] **Link de anexo inserido no texto abre em modo de exibição**, não força mais download;
  painel de anexos ganhou um botão de inserir link direto (antes só dava pelo menu de
  contexto). Notas com links antigos (gravados antes dessas mudanças) foram corrigidas por
  uma migração única na inicialização da API (ver nota sobre URLs de anexo abaixo).

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
  institucional (`app-www`). `Program.cs` deriva a URL da API de forma única (mesma origem +
  `/ultranote/api-note/`), com fallback só pro myQNAPcloud raiz (sem prefixo). Rotas
  Cloudflare: hostname `www.<domínio>`, path `^/ultranote` (regex — cuidado, não é glob) —
  **precisa vir antes** da rota catch-all pro `app-www` no mesmo hostname (Cloudflare avalia
  as rotas de cima pra baixo, a primeira que bater vence, não é "mais específica vence").
  - [x] **Aposentar as rotas antigas** — `note.*`/`note-api.*` removidas do Cloudflare, e o
    branch de fallback correspondente removido do `Program.cs`.
  - [x] **`<base href>` corrigido no nginx, não mais em JS** — a primeira versão ajustava o
    `<base href>` do `index.html` pra `/ultranote/` via um `<script>` inline rodando antes dos
    `<link>`/`<script>` seguintes. Não é garantia: o preload scanner do navegador (a
    otimização que busca recursos referenciados no HTML antes mesmo de rodar scripts da
    página) pode vencer a corrida e pedir tudo na raiz do domínio em cargas frias, gerando 404
    em cascata. Trocado por um `sub_filter` no nginx que reescreve o `<base href>` na própria
    resposta HTML (usando `$request_uri`, que reflete o caminho original mesmo depois do
    `rewrite` que tira o prefixo `/ultranote/`) — chega certo desde o primeiro byte, sem
    depender de JS nenhum. Testado com um nginx real rodando localmente (Docker/WSL não
    disponíveis nessa máquina; build oficial do nginx.org pra Windows resolveu).
  - [x] **Cache do Cloudflare/navegador pode segurar o `index.html` antigo** — sem
    `Cache-Control`, um deploy novo pode ficar invisível pra quem já visitou (aponta pra
    `_framework/*` com hash antigo, chamadas de API com URL aposentada, etc.), sem erro
    visível além de "está estranho". `nginx.conf` manda `no-store` no `index.html`; se ainda
    assim persistir depois de um deploy nas 3 URLs de produção, purgar o cache do Cloudflare
    (Caching → Configuration → Purge Everything) resolve o que já ficou preso na borda.
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
- [ ] **Vulnerabilidade em dependência da API** — `dotnet build`/`restore` de `UltraNote.Api`
  avisa (NU1902 moderada + NU1903 alta) sobre `System.Security.Cryptography.Pkcs` 9.0.0,
  que entra como dependência **transitiva** (nenhum `PackageReference` direto). Avisos:
  [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx) (alta) e
  [GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7) (moderada).
  Pra tratar: descobrir quem puxa o pacote (`dotnet nuget why UltraNote.Api System.Security.Cryptography.Pkcs`),
  e ou atualizar o pacote pai, ou fixar uma versão corrigida com um `PackageReference` direto.
  Avaliar também o impacto real (o pacote lida com CMS/PKCS#7, provavelmente via validação de
  token/Data Protection) — pode ser baixo, mas o aviso some de qualquer jeito com a atualização.
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
- [x] **Login persistente (30 dias)** — o ID token do Google dura ~1h e a renovação silenciosa
  (One Tap) falha com frequência, então reabrir o site pedia login de novo. Agora o cookie de
  sessão da API (`un_session`, antes só pra anexos, 24h) dura 30 dias com renovação deslizante
  (`Auth:SessionDays`) e vira o login persistente: ao abrir, a web tenta o token em cache, depois
  `GET /api/auth/me` pelo cookie; só carrega o GIS depois disso. A allowlist continua checada em
  toda requisição (tirar um e-mail derruba o acesso mesmo com cookie válido). "Sair" chama
  `POST /api/auth/logout` pra apagar o cookie, senão o próximo carregamento logaria de novo; um
  401 numa sessão só de cookie devolve pra tela de login. A tela de login não pisca mais durante
  a verificação (card fica no DOM, escondido, porque o botão do Google monta dentro dele).
- [x] **Acesso via myQNAPcloud** (`groo.myqnapcloud.com:8443`) — alternativa pra quando os domínios de produção estão bloqueados (rede corporativa). Web e API na mesma origem (porta única): o nginx do `app-note-web` repassa `/api-note/*` internamente pra `app-note-api:8080`, então nem CORS é necessário. Chegamos nisso depois de testar com uma segunda porta dedicada pra API e o proxy da empresa bloquear o `CONNECT` nela — proxies corporativos costumam só liberar túnel HTTPS pra portas "conhecidas". Certificado ainda é o autoassinado do QNAP (aviso "não seguro", mas funciona); solução definitiva com Caddy + Let's Encrypt fica pendente (ver Infra/Deploy).
- [x] **Links de anexo (imagem/arquivo inserido no texto) agora relativos ao domínio**, não
  mais absolutos — `GetAttachmentUrl`/`GetAttachmentDownloadUrl` geram um caminho relativo
  quando API e web estão na mesma origem (todo caso de produção hoje). Antes, o link ficava
  gravado com o domínio de quem editou a nota; se ela fosse aberta depois num *outro* dos 3
  domínios de acesso, o cookie de sessão do anexo (escopo por origem) não valia lá, quebrando
  a imagem — mesmo que o domínio antigo continuasse no ar. Notas com links já gravados com um
  domínio aposentado (`note-api.<domínio>`, de antes da migração pro `/ultranote`) foram
  corrigidas por uma rotina única e idempotente na inicialização da API, que reescreve o
  `ContentHtml` das notas afetadas.

### Visual / Identidade

- [x] **Novo logo dourado** (export do Claude Design, pasta `dourado/ultranote`) — favicon,
  ícones PWA/apple-touch-icon e launcher do Android gerados a partir do PNG de 128px (maior
  disponível; `icon-512.png` fica um pouco ampliado, mas ok pro design simples que é). Logo
  do topbar usa o SVG vetorial de verdade que veio no export (glifo com gradiente + máscara),
  substituindo o SVG desenhado à mão que tinha antes (removido junto o CSS de recoloração por
  tema, que só fazia sentido pro design anterior); `favicon.svg` antigo também removido, sem
  fonte vetorial equivalente pro logo novo. Testamos colocar o sparkle da Ultrasoft (marca-mãe,
  `dourado/ultrasoft/ultrasoft-sparkle.svg`) do lado do logo no topbar e sozinho no lugar
  dele — nenhum dos dois ficou bom; o SVG fica preparado em `UltraNote.UI/wwwroot/` sem uso,
  esperando um lugar melhor pra ele aparecer.
- [x] **Estrelas de favorito em amarelo** (`#FFC107`, era `#C89B3C`) — o dourado do logo novo
  brigava visualmente com o dourado que as estrelas usavam. O botão de arquivadas, que
  compartilhava a mesma regra CSS de "ativo" com o de favoritos, passa a usar a cor de
  destaque padrão do app em vez do dourado — eram duas coisas diferentes usando a mesma cor
  sem necessidade.
- [x] **Título "UltraNote" no topbar em dourado + fonte Poppins** — cor um pouco mais escura
  (`#C4892C`) que o dourado do logo, pra legibilidade em texto pequeno. Fonte escolhida por
  comparação visual (Artifact com preview lado a lado de 4 opções — Poppins, Playfair Display,
  Space Grotesk, Fraunces — já que descrever fontes em texto não ajuda a decidir), carregada
  via Google Fonts (mesmo padrão de CDN já usado pros ícones Tabler).
- [x] **Splash screen animado** — trocado o texto estático "UltraNote" (que usava a cor de
  destaque genérica do app) pelo logo animado do export `dourado/ultranote/ultranote-splash.html`:
  o mesmo glifo SVG do topbar (gradiente + máscara) flutuando em loop (`translateY`) ao lado do
  wordmark em Poppins dourado, com 4 sparkles piscando em delays escalonados ao redor do glifo.
  Escala geral ajustada com `transform: scale()` no contêiner (mesma técnica do export original)
  pra ficar mais proeminente. Respeita `prefers-reduced-motion` (desliga as animações pra quem
  pediu menos movimento no sistema).

### App Android

- [x] **App Android encapsulando o site em WebView** (`android/`, projeto Gradle/Kotlin separado
  da solução .NET, `applicationId br.com.ultrasoftinc.ultranote`) — carrega
  `https://www.ultrasoftinc.com.br/ultranote` direto. Cobre upload/download de anexos (seletor
  de arquivo + `DownloadManager` repassando o cookie de sessão), botão voltar navega o
  histórico da página antes de sair do app, puxar-para-atualizar, ícone reaproveitado do
  `icon-512.png`.
  - [x] **Login Google dentro de WebView** — o Google bloqueia login numa WebView "genérica"
    (erro `disallowed_useragent`); contornado removendo o marcador `; wv` do user-agent (prática
    aceita, não é spoofing completo). Teve uma pegadinha: a primeira versão "sequestrava" a
    janela de popup do login pra carregar na mesma WebView — quebrava o `window.close()` que o
    fluxo do Google dispara ao terminar, deixando a tela travada (login já tinha funcionado,
    só não voltava). Corrigido com um popup de verdade (`Dialog` com sua própria `WebView`,
    fechado via `onCloseWindow`).
  - [ ] **Build release assinado** — hoje só existe o `.apk` de debug (`assembleDebug`), instalado
    via `adb`. Pra publicar na Play Store ou distribuir de verdade precisa gerar uma keystore de
    release e configurar a assinatura no `app/build.gradle.kts`.
  - [x] **Insets do Android 15 (edge-to-edge)** — `targetSdk 35` tornou edge-to-edge o padrão;
    sem tratar isso, a WebView desenhava por baixo da barra de status e da barra de navegação
    (conteúdo escondido atrás delas) **e** um gesto de arrastar perto do topo da tela era
    capturado pelo "puxar para notificações" do sistema em vez de chegar na página, já que não
    sobrava margem nenhuma separando os dois. Corrigido aplicando o padding das window insets
    (`ViewCompat.setOnApplyWindowInsetsListener`) no layout raiz.
  - [x] **Puxar-para-atualizar só na faixa do topbar** — depois do fix de insets acima, o
    `SwipeRefreshLayout` passou a capturar qualquer arrasto pra baixo, inclusive os que deveriam
    rolar o conteúdo da nota. A página em si nunca rola (quem rola são `div`s internas com
    `overflow-y: auto`), então o `canChildScrollUp()` do SwipeRefreshLayout não tem como saber
    se dá pra rolar ali dentro ou não — ele sempre acha que pode. Resolvido restringindo o
    refresh a arrastos que começam dentro da faixa do topbar (56dp, que não tem rolagem
    própria), via `setOnTouchListener` observando `ACTION_DOWN` e ligando/desligando
    `swipeRefresh.isEnabled` conforme a posição Y do toque.
