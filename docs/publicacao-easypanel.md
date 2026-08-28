# Publicação da parte central no EasyPanel

## O que vai para o EasyPanel — e o que NÃO vai

A aplicação tem 4 componentes; só **2** são candidatos a rodar em container:

| Componente | Vai pro EasyPanel? | Por quê |
|---|---|---|
| **api** (Node/NestJS) — regras + fila de comandos + serve o front publicado | **Sim** | Sem dependência de Windows. |
| **nucleo** (Nucleo.Api, C#/.NET) — precificação/comissão/crédito/fiscal | **Sim** | Confirmado (26/08/2026): zero referências a `System.Data.OleDb` ou qualquer API só de Windows — `net9.0` puro. |
| **frontend** (React/Vite) | Não é um serviço à parte — o próprio `api` serve o build publicado (`ServeStaticModule`, já configurado). Um `Dockerfile` só cuida dos dois. |
| **integrador** (console C#) | **Nunca.** Precisa do VFPOLEDB (COM 32 bits, só Windows) e de acesso direto aos arquivos DBF do cliente — impossível num container Linux. Continua como serviço Windows **no servidor de cada cliente** (ver `docs/instalacao-console-cliente.md`), apontando para a URL pública que o EasyPanel der ao `api`. |

```
Servidor do CLIENTE (Windows, sem Docker)          EasyPanel (Linux, containers)
┌─────────────────────────────┐                    ┌───────────────────────────┐
│ Integrador.exe (serviço)     │ ── HTTPS (saída) ─►│ api  (Node — serve o front │
│  lê DBF via VFPOLEDB          │                    │  publicado + regras)       │
└─────────────────────────────┘                    │        │                    │
                                                     │        ▼                    │
                                                     │  nucleo (.NET — cálculo)   │
                                                     └───────────────────────────┘
```

## Arquivos já preparados neste repositório

- `api/Dockerfile` — build em 3 estágios: compila o front (Vite), compila a API (Nest), imagem final só com `dist/` + `node_modules` de produção + o front publicado em `public/`.
- `nucleo/Dockerfile` — build .NET padrão (SDK → runtime ASP.NET).
- `docker-compose.yml`, na raiz — os dois serviços já ligados (`api` chama `nucleo` pelo nome interno `http://nucleo:5060`). O EasyPanel importa isso direto como um "Compose Project".

**Importante sobre o contexto de build**: os dois `Dockerfile` ficam dentro de `api/` e `nucleo/`, mas o **contexto do build é a raiz do repositório** (`.`) — é assim que o Dockerfile da API consegue copiar `frontend/` também, e o do núcleo consegue copiar `motor-regras/`. Se for criar os serviços manualmente no EasyPanel (em vez de importar o `docker-compose.yml`), configure "Build Path" = raiz do repo e "Dockerfile Path" = `api/Dockerfile` (ou `nucleo/Dockerfile`) — não aponte o build para dentro da subpasta.

## Variáveis de ambiente a configurar no EasyPanel

| Serviço | Variável | Valor |
|---|---|---|
| `api` | `PORT` | `3001` (ou o que preferir — é só ajustar a porta do serviço no EasyPanel também) |
| `api` | `NUCLEO_API_URL` | `http://nucleo:5060` (nome interno do serviço `nucleo` dentro da rede do EasyPanel — se os dois estiverem no mesmo Compose Project, isso já vem certo do `docker-compose.yml`) |
| `nucleo` | — | Nenhuma variável obrigatória. |

Depois de publicado, configure no EasyPanel um domínio para o `api` (ex.: `pedwer.iuven.com.br`, com HTTPS automático) — é essa URL pública que:
1. os vendedores acessam no navegador;
2. cada `Integrador.exe` instalado num cliente aponta (`servico https://pedwer.iuven.com.br`, ver `docs/instalacao-console-cliente.md`).

O `nucleo` não precisa de domínio público — só o `api` fala com ele, pela rede interna do Compose.

## ⚠ Antes de considerar isto "produção de verdade"

A API guarda a cópia sincronizada dos dados (clientes, produtos, preços...) **e a fila de comandos pendentes do console em memória** — não em banco de dados. Isso já está documentado como lacuna desde o início do projeto, mas em containers importa mais do que importava no servidor Windows fixo:

- **Todo redeploy apaga esse estado.** No EasyPanel, um novo build/deploy do serviço `api` reinicia o container do zero — a cópia sincronizada volta em ~30s (o console resincroniza sozinho), mas **qualquer comando pendente naquele instante exato (um pedido sendo gravado) se perde**, porque nada persiste esse estado em disco fora do processo.
- Plataformas de container também podem reiniciar o serviço sozinhas por outros motivos (falta de memória, atualização da plataforma, etc.) — mais frequente do que um servidor Windows dedicado que só reinicia quando alguém manda.

**Recomendação**: antes de usar isto com clientes reais (não só para testar a publicação), trocar o cache em memória e a fila de comandos por PostgreSQL — o desenho original já previa isso desde o plano inicial (ver memória do projeto). O EasyPanel cria um serviço de Postgres com um clique; a mudança de código fica para quando for decidido priorizar essa etapa.

## Passo a passo no EasyPanel

1. Repositório no Git acessível pelo EasyPanel (GitHub/GitLab, ou outro Git genérico com deploy key) — ver seção de Git abaixo.
2. No EasyPanel: **Criar Projeto** → **Compose** → apontar para este repositório, branch e o `docker-compose.yml` da raiz.
3. Configurar `NUCLEO_API_URL` no serviço `api` (se o compose não preencher sozinho — confira no `docker-compose.yml` já commitado).
4. Configurar domínio + HTTPS no serviço `api` (a opção de domínio/certificado automático do próprio EasyPanel).
5. Deploy. Conferir logs do serviço `api` — deve subir normalmente e responder em `GET /empresas` (lista vazia até o primeiro console sincronizar).
6. Apontar (ou reapontar) o(s) `Integrador.exe` já instalados para a nova URL pública.
