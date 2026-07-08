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

## 2. Gestão de notas e pastas (API já suporta; falta UI)

- [x] **Excluir nota** — menu de contexto.
- [x] **Renomear pasta** — menu de contexto.
- [x] **Buscar** notas (por título) — caixa de busca na barra superior.
- [x] **Mover** nota/pasta — menu de contexto "Mover...", modal com seletor de destino (pastas descendentes excluídas para evitar ciclo).
- [x] **Salvar estado** (pastas abertas e fechadas e nota selecionada)
- [x] **Importar do Evernote** — importar arquivo `.enex` (formato de exportação do Evernote): criar pastas/notas preservando estrutura e conteúdo HTML.
  - [x] Erros de upload de anexo durante a importação (rede, resposta inválida da API) agora aparecem na tela em vez de falhar silenciosamente — antes um anexo que falhasse virava um link quebrado sem nenhum aviso.
  - [x] **Causa raiz do "anexo não vem" em produção**: `GET /api/attachments/{id}` exigia login, mas o link é acessado pelo navegador como requisição crua (sem o Bearer token) — sempre dava 401. Só reproduzia em produção (dev roda sem auth). Corrigido com cookie de sessão (ver item em "Autenticação / sessão").
- [x] **Notas favoritas** — seção "Favoritos" na barra lateral (mostrar/ocultar via toolbar, estado persistido em localStorage), toggle por estrela no cabeçalho do editor e no menu de contexto, indicador na árvore.

## 3. UX e robustez

- [x] **Guarda de alterações não salvas** — modal de confirmação ao trocar de nota com edição pendente (cancelar/descartar), + aviso nativo do navegador ao fechar/recarregar a aba.
- [x] **Mostrar erros da API na UI** — toda operação de escrita (criar/renomear/excluir/salvar) reporta erro no status bar.
- [x] Indicador de "salvando…" — botão mostra "Salvando…" e fica desabilitado durante a chamada.
- [x] Atalho de teclado **Ctrl+S** (ou Cmd+S) salva a nota aberta, sem disparar o "Salvar página" do navegador.
- [ ] **Adaptar para mobile** — layout responsivo: sidebar recolhível, toolbar do editor adaptada para toque, área de edição ocupa tela cheia.
- [x] **Barra de ferramentas acima da árvore** — ações rápidas (nova pasta, nova nota, recolher tudo, expandir tudo) + nó raiz "Notas" para criação de pastas na raiz.
- [x] **Tela de About** — versão do app, créditos, links (GitHub, etc.).

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
- [x] Restringir **CORS** da API para `https://note.ultrasoft.app.br` em produção (`Cors:AllowedOrigins`, `AllowCredentials`); dev sem origens configuradas continua permissivo.
- [x] **Cookie de sessão para anexos** — `<img src>`/`<a href>` embutidos na nota não carregam o Bearer token (requisição crua do navegador). `POST /api/auth/session` (chamado a cada login/renovação de token) planta um cookie `HttpOnly; Secure; SameSite=Lax` (esquema `AttachmentCookie`, expira em 24h com renovação deslizante) que a API aceita como alternativa ao Bearer em qualquer endpoint. Revogável trocando a chave de Data Protection — ao contrário do GUID público que era usado antes.
- [ ] (Opcional) **Cloudflare Access** como camada extra na URL da web.

## 5. App Windows (desktop)

- [ ] Criar `UltraNote.Desktop` (**MAUI Blazor Hybrid**), reaproveitando os componentes `UltraNote.UI`.
  - [ ] Requer `dotnet workload install maui` (não instalado; só `maui-android`).
  - [ ] Login Google via **WebAuthenticator** (em vez do GIS da web).
- [ ] (Opcional) Cache local leve p/ resiliência offline.

## 6. Visual / Identidade

- [x] **Alterar logo** — ícone SVG inline no brand do topbar (papel com dobra + sparkle dourado Ultrasoft); tema escuro/claro via CSS.
- [x] **Alterar favicon** — favicon SVG + PNG (32×32, 192×192, 512×512) com ícone do UltraNote.

## 7. Infra / Deploy / Qualidade

- [ ] Automatizar o fluxo de atualização (script: copiar p/ NAS → `docker run ... publish` → recriar app).
- [ ] **Backup** automatizado de `/share/Container/ultranote-data` e `/ultranote-assets`.
- [ ] (Dev) Decidir se o `appsettings.json` local fica **sem** `GoogleClientId` (dev sem login, mais ágil) — produção continua protegida.
- [ ] Testes automatizados (API: endpoints; UI: componentes principais).
- [ ] Healthcheck de container / monitor de uptime.
- [ ] App Windows (desktop): `UltraNote.Desktop` — MAUI Blazor Hybrid (ver §5).

---

## Notas / armadilhas conhecidas (ver DEPLOY-QNAP.md)

- Containers neste NAS só têm internet na rede `edge` **e** precisam de `dns:` explícito
  (a API usa pra validar o token Google; sem isso → "signature key was not found").
- O `docker build` não tem internet no NAS → publicamos com `docker run` e rodamos imagens
  oficiais com os arquivos montados (sem build de imagem).
- Atualização da web = republicar `_out_web` + hard refresh (nginx serve arquivos montados).
