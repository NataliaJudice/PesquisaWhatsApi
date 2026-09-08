# Chatbot no WhatsApp — guia rápido

O chatbot montado no construtor visual roda no WhatsApp por dois provedores possíveis,
escolhidos em `WhatsApp:Provedor` no `appsettings.json`:

- **`Meta`** (padrão) — WhatsApp Cloud API, a API oficial. Número de teste gratuito, sem cartão.
- **`Twilio`** — mantido para quem já usa a conta de lá.

O motor do chatbot, o construtor, os fluxos e o simulador são os mesmos nos dois casos.
Muda apenas a camada de entrada/saída.

---

## Caminho A — Meta Cloud API (recomendado)

### 1. Criar o app

Em [developers.facebook.com](https://developers.facebook.com) › **Meus apps** › **Criar app** ›
tipo **Empresa** › adicione o produto **WhatsApp**.

Na tela **WhatsApp › Configuração da API** você encontra:

| Onde | O que copiar |
|---|---|
| Identificação do número de telefone | `PhoneNumberId` |
| Token de acesso | `AccessToken` |
| Configurações › Básico › Chave Secreta do App | `AppSecret` |

Guarde no cofre de secrets (não vai para o Git):

```bash
dotnet user-secrets set "WhatsApp:Meta:PhoneNumberId" "123456789012345"
```

```bash
dotnet user-secrets set "WhatsApp:Meta:AccessToken" "EAAG..."
```

```bash
dotnet user-secrets set "WhatsApp:Meta:AppSecret" "chave-secreta-do-app"
```

> O token da tela de configuração **expira em 24 horas** — serve para testar.
> Para deixar rodando de verdade, gere um token permanente por um *usuário do sistema*
> no Business Manager.

### 2. Expor a aplicação

A Meta precisa alcançar a sua máquina.

**No Visual Studio:** *setinha ao lado do ▶ › Túneis de Desenvolvimento › Criar um túnel*,
tipo **Persistente** e acesso **Público**.

**Com ngrok:** `ngrok http <porta>` (a porta aparece no painel do WhatsApp).

A aplicação lê os cabeçalhos `X-Forwarded-*`, então a tela do WhatsApp mostra a URL pública
sozinha depois que você recarregar a página.

### 3. Cadastrar o webhook

Em **WhatsApp › Configuração › Webhook › Editar**:

- **URL de callback**: `https://SEU-TUNEL/api/whatsapp/meta`
- **Token de verificação**: o valor de `WhatsApp:Meta:VerifyToken` (o painel mostra qual é)

Clique em **Verificar e salvar**. Depois, em **Gerenciar**, assine o campo **`messages`** —
sem isso a Meta não envia nada.

### 4. Liberar o seu número

Enquanto o app está em modo de desenvolvimento, só números cadastrados conversam com ele.
Em **WhatsApp › Configuração da API**, campo **Para** › *Gerenciar lista de números de telefone*,
adicione o seu (chega um código por WhatsApp para confirmar).

### 5. Conversar

Mande mensagem para o número de teste da Meta. A primeira mensagem abre o fluxo publicado.

---

## Caminho B — Twilio

Preencha `WhatsApp:Provedor` com `Twilio` e as credenciais:

```bash
dotnet user-secrets set "WhatsApp:Twilio:AccountSid" "ACxxxxxxxx"
```

```bash
dotnet user-secrets set "WhatsApp:Twilio:AuthToken" "seu-auth-token"
```

Webhook: `https://SEU-TUNEL/api/whatsapp/webhook` (método **POST**), em
*Messaging › Try it out › WhatsApp Sandbox Settings*. Cada celular entra no sandbox
enviando `join <código>` uma vez.

---

## Publicar um fluxo

Um fluxo precisa estar marcado como **padrão** para atender no WhatsApp:
tela **WhatsApp › passo 1 › Publicar**, ou o ícone de celular no cartão do fluxo.
Quem mandar mensagem cai no bloco marcado como início desse fluxo.

## Como o motor decide a resposta

- **Bloco sequencial** (tem "próximo passo"): envia a mensagem e já continua para a próxima,
  encadeando até chegar num menu ou no fim.
- **Bloco de menu** (tem opções): envia e espera. A resposta é comparada com os gatilhos sem
  diferenciar maiúsculas; gatilhos com mais de 2 letras também casam por trecho
  ("quero suporte" → `suporte`). Respostas por botão/lista da Meta entram pelo título do botão.
- **Bloco sem saída**: encerra a conversa. A próxima mensagem recomeça o fluxo.

Comandos válidos em qualquer ponto:

| Digitou | Acontece |
|---|---|
| `menu`, `reiniciar`, `início`, `voltar` | volta ao começo do fluxo |
| `sair`, `encerrar`, `finalizar` | encerra o atendimento |

## Endpoints

| Rota | Uso |
|---|---|
| `GET /api/whatsapp/meta` | Handshake de verificação da Meta (e health check no navegador) |
| `POST /api/whatsapp/meta` | Recebe as mensagens da Meta; responde via Graph API |
| `POST /api/whatsapp/webhook` | Webhook da Twilio (responde em TwiML) |
| `GET /api/whatsapp/webhook` | Health check do webhook da Twilio |
| `POST /api/whatsapp/status` | Callback opcional de status de entrega (Twilio) |

## Segurança

`WhatsApp:ValidarAssinatura` confere a origem das chamadas — `X-Hub-Signature-256` (HMAC com
o App Secret) na Meta, `X-Twilio-Signature` na Twilio. Em `appsettings.Development.json` ele
vem como `false` para facilitar os testes; **deixe `true` em produção**.

Lembre também que, com o túnel público, todas as telas da aplicação ficam acessíveis por quem
souber a URL — desligue o túnel quando não estiver testando.

## Testar sem provedor nenhum

O **Simulador** usa o mesmo motor, num canal separado (`simulador`). Também dá para simular
o webhook por linha de comando:

```bash
curl -X POST http://localhost:5277/api/whatsapp/meta -H "Content-Type: application/json" -d "{\"entry\":[{\"changes\":[{\"value\":{\"contacts\":[{\"profile\":{\"name\":\"Teste\"}}],\"messages\":[{\"from\":\"5511999999999\",\"type\":\"text\",\"text\":{\"body\":\"oi\"}}]}}]}]}"
```
