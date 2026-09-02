# Documento norteador de projetos web

Destilado do **Portal de Comissão da Versátil**, construído entre 11 e 14 de
agosto de 2026. Serve a portais internos com a mesma forma: **uma tela web
moderna sobre um sistema legado que já existe e não vai ser reescrito.**

Cada padrão aqui está em produção. **Os caminhos de arquivo citam a
implementação de referência, em `Sistemas\portalcomissao` — é de onde se
copia.**

> **O que este documento não é:** um framework. Não há biblioteca a instalar,
> não há gerador. É a lista do que decidimos, por que decidimos, e o que já deu
> errado — para que a mesma hora não seja gasta duas vezes.

⚠ **Este arquivo vive FORA de repositório git** (a pasta `Sistemas` não é um
repositório). Alterações aqui não têm histórico e não têm como ser desfeitas
por `git`. Ao mexer, prefira acrescentar a reescrever, e registre a data.

**Histórico até 14/08/2026:** este documento nasceu como
`portalcomissao/docs/PADROES-PORTAL-WEB.md` e está versionado até essa data no
repositório `tlana-creator/painelcomissao`.

---

## 1. A pilha, e por que cada peça

| camada | escolha | por quê |
|---|---|---|
| Servidor | **Node 24 + TypeScript + Fastify** | um runtime só no projeto inteiro; Fastify tem plugin de estático e log estruturado sem configuração |
| Tipos | **`strict` + `exactOptionalPropertyTypes` + `noUncheckedIndexedAccess`** | ver §7 |
| Testes | **Vitest** | mesmo motor do Vite; roda o servidor de verdade em teste, sem mock de HTTP |
| Front | **React 18 + Vite 5 + Mantine v7** | Mantine entrega tabela, seletor, notificação e modal prontos e com aparência coerente — o tempo economizado é dias, não horas |
| Estado no front | **Context API, e só** | sem Redux/Zustand/TanStack. Ver §9.2.1 |
| Ícones | `@tabler/icons-react` | acompanha o Mantine |
| Banco | **Postgres, e OPCIONAL** | ver §6 |
| Publicação | **Easypanel + Dockerfile** | ver §3 |

**Uma convenção atravessa tudo: identificadores, arquivos e rotas em
português.** Não é preferência estética — o vocabulário do negócio é o do
cliente, e traduzir `título`/`parcela`/`competência` para inglês no código
obriga a traduzir de volta em toda conversa. Comentários de cabeçalho registram
**a medição que justificou a decisão**, com data e número.

**Uma imagem, dois builds.** O `Dockerfile` da raiz constrói servidor e front em
etapas separadas e junta os dois. As dependências do front são grandes e não
entram na imagem final. Copie o arquivo inteiro — ele tem três coisas que se
esquece de fazer: `npm ci --omit=dev` na etapa final, `USER node` (não rodar
como root num contêiner exposto) e o `COPY --from=front /web/dist ./publico`.

---

## 2. Ordem de trabalho — o que fazer primeiro

A ordem importa. Esta funcionou:

1. **Especificação escrita e aprovada pelo cliente**, antes de qualquer código.
   Não precisa ser longa; precisa ser aprovada.
2. **O motor de cálculo, isolado e testado**, sem HTTP e sem banco. É onde as
   regras do negócio vivem. Se ele estiver certo, o resto é apresentação.
3. **O cliente do sistema legado**, provado contra a API real cedo. Descobrir
   no fim que a API omite um campo custa uma reescrita.
4. **As rotas**, com o recorte por perfil desde a primeira.
5. **As telas.**
6. **Publicar cedo, e publicar sempre.** O primeiro deploy deve acontecer com o
   portal ainda feio. Deploy acumulado é deploy que falha.

⚠ **Não construa a tela antes do motor.** É tentador — a tela é o que o cliente
vê. Mas tela sobre motor errado gera confiança em número errado, e desfazer
essa confiança custa mais que o atraso.

---

## 3. Dia 1: repositório, GitHub e Easypanel

Meia hora, feita uma vez, e nunca mais se pensa em deploy.

### 3.1 GitHub

```bash
gh repo create <organizacao>/<nome> --private --source=. --push
```

O `gh` já está instalado e autenticado nesta máquina como `tlana-creator`.

### 3.2 Serviço no Easypanel

**+ Service → App**, e preencha:

| campo | valor |
|---|---|
| Source | GitHub → o repositório recém-criado |
| Branch | `main` |
| Build | **Dockerfile** (o da raiz) |
| Port / Proxy port | `3000` |

Não configure comando de build nem de start: eles estão na imagem.

Em **Domains**, adicione o domínio e deixe o painel emitir o certificado.

### 3.3 Publicação automática

1. Serviço → aba **`Source`** → **`Auto Deploy`**. Copie a **URL de webhook**.
2. No GitHub: **Settings → Webhooks → Add webhook**.
   - Payload URL: a URL copiada
   - Content type: `application/json`
   - Events: **Just the push event**
3. Salve. O GitHub dispara um teste na hora — ✅ verde e está ligado.

**Guarde a URL do webhook fora do repositório** (aqui: `Desktop\hook.txt`). Ela
é um token: quem a tiver publica no ambiente do cliente. Nunca a imprima em
chat, log ou commit.

Para forçar uma publicação sem push:

```bash
HOOK=$(cat ~/Desktop/hook.txt | tr -d '\r\n ')
curl -s -o /dev/null -w "%{http_code}\n" "$HOOK"
```

### 3.4 ⚠ Conferir que a publicação subiu de verdade

**O 200 do gatilho só diz que o pedido entrou.** Já afirmei que nada tinha
subido quando três deploys estavam no painel, e o contrário também acontece: o
painel dizer "sucesso" e servir a imagem anterior.

A prova barata é o nome do pacote do Vite, que é derivado do conteúdo:

```bash
curl -s <url>/ | grep -o 'index-[A-Za-z0-9_-]*\.js'   # servido
ls web/dist/assets/ | grep '^index-.*\.js$'           # compilado
```

**Iguais = o que está no ar é exatamente o que você compilou e conferiu.**

⚠ **Mas o nome do pacote só muda quando o FRONT muda.** Publicação que mexe só
no servidor não tem prova nenhuma por esse caminho — metade delas fica sem
verificação. **Publique a versão no `/saude`** e trave-a por teste contra o
`package.json` e o `package-lock.json` (o `npm ci` do contêiner falha se os
dois discordarem, e só lá — nunca na sua máquina).

### ⚠ Falha de build VAZIA e em segundos: repita antes de investigar

Em 17/08/2026 três publicações seguidas falharam em **6, 6 e 13 segundos**, com
o registro contendo só um cabeçalho `### Error` e nenhuma saída. A quarta
tentativa, **sem uma linha de código alterada**, subiu em 60 segundos.

**Código quebrado produz saída** — linha de compilador, de empacotador, de
gerenciador de pacotes. Registro vazio em segundos é o construtor, não o que
ele ia construir.

Duas horas foram gastas ali, e as duas hipóteses que levantei estavam
**numericamente erradas** — vale registrar para não repetir o raciocínio:
*disco cheio* (uma dúzia de builds não enche disco) e *limite do Docker Hub*
(etapas que usam a mesma imagem base fazem **um** download por build, não um
por etapa).

O que valeu a pena naquelas duas horas foi **descartar o código com método**:
clonar o repositório do zero e rodar as etapas do `Dockerfile` à mão. Deu o
pacote esperado, byte a byte — e a partir daí a conversa deixou de ser "será
que quebrei alguma coisa".

### 3.5 Trocar de cliente — o que muda, e o que morde

Levantado em 14/08/2026 varrendo o código, não de memória.

**A boa notícia:** o código de produção **não tem nada da Versátil cravado**. As
únicas ocorrências do nome estão em comentários, nos dados fictícios da
demonstração e nas ferramentas de diagnóstico. Trocar de cliente é trocar
configuração — não é caçar constante.

#### O que pedir ao cliente novo, antes de começar

| item | para quê |
|---|---|
| **URL da WebAPI Fenícia 2** | `ERP_URL`. Confirme o caminho: aqui é `/table/List2`, **sem** `/webapifenicia2/`, ao contrário da documentação interna |
| **Usuário e senha da API** | `ERP_USUARIO` / `ERP_SENHA`. **De consulta, somente leitura** |
| **Quais empresas existem** | os códigos de dois dígitos (`01`, `02`…). Carregar só uma produz total que parece completo e não é |
| **Quem é gestor** | `USUARIOS_GESTORES` |
| **O domínio do portal** | para o Easypanel emitir o certificado |

Gere um `SESSAO_SEGREDO` novo por instalação — nunca reaproveite o de outro
cliente. `DATABASE_URL` pode nascer vazia (§6).

#### O que NÃO muda — informado pelo Lana em 14/08/2026

Eu havia listado duas coisas como "conferir antes". O dono e mantenedor do ERP
resolveu as duas, e a informação vale mais que a minha cautela:

- **A cifra da senha é sempre a mesma**, em todos os clientes. `src/auth/`
  copia direto e o login funciona. *(Eu suspeitava que o `artcol.VCT` variasse
  por instalação — não varia.)*
- **Todo cliente multiempresa segue o padrão `fenwin`.** O prefixo cravado em
  `src/erp/consultas.ts:24` está correto para eles.

⚠ **A ressalva que sobra é o cliente de UMA empresa só.** A frase acima é
sobre multiempresa; onde não há multiempresa, o prefixo de caminho pode
simplesmente não existir. Antes de escrever tela, rode **uma consulta mínima a
`cadmov` que devolva uma linha conhecida** — com prefixo e sem. Custa dois
minutos, e o modo de falhar é traiçoeiro: pela ambiguidade do 404 (§5.1), tabela
inexistente devolve **vazio sem erro**, o que parece "período sem movimento" e
não erro de configuração.

#### ⚠ Armadilha real: a ferramenta que aponta para a Versátil sozinha

`src/cli/sondar-erp.ts:23` tem a URL da Versátil como **valor padrão**:

```ts
ERP_URL: process.env.ERP_URL ?? 'https://apiversatile.iuven.com.br/table/List2'
```

Rodar essa ferramenta sem `.env` carregado **bate na produção da Versátil** —
num projeto de outro cliente, sem nada avisando. Ao clonar o chassi para
cliente novo, **remova esse padrão e faça a ferramenta falhar sem `ERP_URL`**.
Falhar é a resposta certa: consultar o ERP errado em silêncio, não.

### 3.6 Segurança do painel

O Easypanel deste cliente responde em **`http://` num IP público**: senha do
painel e token de deploy trafegam **sem cifra**. Vale um domínio com TLS no
próprio painel assim que houver folga. Registre isso no projeto novo em vez de
supor que alguém lembra.

---

## 4. Autenticação contra o sistema legado

O padrão que resolve o problema real destes portais: **o usuário não pode ter
senha nova.** Ele já tem a do ERP, e vai esquecer qualquer outra.

### 4.1 A regra

**A senha é conferida NO SERVIDOR, contra a tabela de usuários do legado.** A
tela de acesso manda o que foi digitado para `POST /api/entrar` e recebe
"entrou" ou "não entrou". Ela não sabe cifrar nada.

Copiar de: `web/src/paginas/Entrada.tsx`, `src/auth/credencial.ts`,
`src/auth/cifra.ts`, `src/api/autenticacao.ts`.

---

### 4.2 Conferir a senha de um legado que guarda cifra, não hash

**A parte cara, e a que se reaproveita em qualquer portal contra um legado
assim.** O `caduser` do ERP não guarda hash: guarda uma **cifra reversível** do
próprio sistema. Conferir a senha significa **cifrar o que foi digitado e
comparar com o que está gravado**.

O que faz disto um problema de *web*, e não de FoxPro: **o portal nunca toca o
DBF.** Ele pede a linha do usuário **pela API**, e recebe a senha já
transformada em texto JSON por um intermediário que decodifica os bytes por
conta própria. A conferência acontece nessa ponta — e é lá que ela quebra
(§ *A fronteira da API*, abaixo).

> ⚠ Autorização: o Lana autorizou reconstruir isto de forma explícita —
> *"autorizo, o programa é meu"* —, e ele é o dono e mantenedor do ERP. Sem
> essa autorização, não se faz.

#### A fonte do algoritmo

`Sistemas\Fox\Classes\artcol.VCT` — pasta **`Classes`**, não `classe` —, método
**`encstring`**, offset 23934. Assinatura `Para cString, lCripto`; os ramos
`Else` são os que **decifram**.

O deslocamento é **posicional**, com `i` começando em 1:

| condição | deslocamento aplicado ao código do caractere |
|---|---|
| `Len > 10` | `+ 3 + (i + Len)` |
| `Len <= 10` | `- 13 + (i * Len)` |

O ERP o chama com dois parâmetros — `encstring(ALLTRIM(senha), .T.)` —, onde o
segundo seleciona cifrar (`.T.`) ou decifrar (`.F.`).

**Casos de borda do original**, e o segundo tem consequência operacional:

- chamada com zero ou um parâmetro devolve `""`;
- **string vazia — ou só de espaços, que é o `Empty()` do VFP — devolve o
  literal `!@#$%&`.**

Esse literal é um **marcador**: `caduser.senha` igual a `!@#$%&` significa que
aquele usuário **está sem senha**. O portal recusa esse caso (§divergências
abaixo), então quem estiver assim no cadastro **não entra até cadastrar uma
senha**. Vale avisar o cliente antes de publicar, não depois do primeiro
telefonema.

#### O domínio da cifra, e por que ele importa

Duas condições fazem uma senha ser **irrepresentável** pelo ERP — e, portanto,
impossível de estar gravada:

1. **caractere fora da CP1252** — o ERP não conseguiria guardá-lo;
2. **deslocamento fora de 0..255** — o `Chr()` do VFP falha, então aquela senha
   nunca foi escrita por ele.

Trate as duas como **"não confere"**, nunca como erro do servidor — senão
"senha errada" vira "o portal quebrou".

⚠ **Dimensione o risco pelo número, não pelo medo.** Medido em 14/08/2026: o
estouro de 255 só acontece a partir de **66 caracteres** (senha toda de `z`;
78 para `a`, 94 para maiúsculas, 103 para dígitos). O limite inferior **nunca**
é atingido por caractere digitável. Ou seja: na prática o domínio só barra
**caractere fora da CP1252** — acento exótico, emoji, cirílico. A guarda de
faixa existe por correção, não porque alguém vá esbarrar nela.

**Implemente apenas o sentido de CIFRAR.** Decifrar não é necessário para
autenticar, e uma função de decifrar no repositório é um passivo — quem obtiver
o código obtém o cadastro de senhas inteiro. Em `src/auth/cifra.ts` a direção
inversa foi deliberadamente omitida.

#### As três armadilhas que custaram tempo — não redescobrir

**1. A tradução do C# que circula tem um erro de CP1252.** Ela mapeia
`0x92 → U+2018`. O correto é **U+2019** — o U+2018 é o `0x91`.

**2. Os quatro `.replace()` do fim daquela tradução NÃO existem no
`encstring`.** O original faz `Chr()` e produz **bytes**. Aqueles quatro
cobriam 4 dos 27 caracteres da faixa **0x80–0x9F**, que é justamente onde a
cifra mora: a senha `portalcomissao` põe **11 dos 14 bytes** nessa faixa.

O certo é montar a tabela **CP1252 completa** uma vez, com
`TextDecoder('windows-1252')` sobre os 256 bytes (disponível no Node 24, fiel
nos 256 — os 5 buracos viram os controles C1) e indexar por byte.

**3. Erro meu, registrado:** declarei a função "falsa" com base em dois pares
que ele me passou e que eram de **um caractere cada**. Em `Len = 1` o
deslocamento é −12 nos dois ramos, então os dois pares eram compatíveis com
qualquer hipótese. Conclusão categórica sobre dois pontos de dado — e logo
depois de censurar ele por fazer o mesmo. Os pares: `1`→`%`, `S`→`G`.

#### A fronteira da API — o ponto propriamente web, e o risco vivo

Tudo acima descreve os **bytes que o legado grava**. Mas o portal não lê bytes:
ele lê **texto JSON que a API produziu ao decodificar aqueles bytes**.

Ou seja, entre o dado e a comparação existe **um decodificador que não é seu**,
e cuja tabela você não escolheu. Se ele não usar a mesma que você usou, o texto
que você calcula não coincide com o que ele devolve — e **toda senha é
recusada, sem erro nenhum a acusar**. Não é dedutível do código do legado:
**exige medição.**

⚠ **Neste projeto isso segue NÃO MEDIDO.** A conferência funciona hoje, mas os
pares validados são curtos; falta um par que atravesse a faixa 0x80–0x9F pela
API. `it.todo` em `tests/cifra.test.ts`.

**Para validar uma reimplementação, exija pares de comprimentos diferentes**
atravessando os dois ramos do algoritmo — aqui, um de 8 e um de 12 — e colete
**os dois lados**: o hex do campo no banco **e** o texto que a API devolve em
JSON. Comparar só contra o hex prova metade, e é a metade que não é a sua.

Regra geral, além deste ERP: **quando um valor binário atravessa uma API que o
converte para texto, a conversão é parte do contrato** — e precisa ser medida
como qualquer outra parte dele.

#### Três divergências deliberadas em relação ao ERP

A comparação não deve imitar o legado. `senhaConfere` diverge de propósito:

1. **Senha em branco é RECUSADA**, mesmo que o cadastro a aceite.
2. **Preenchimento removido só com espaço `0x20`, nunca `trimEnd()`** — o
   `trimEnd` comeria o NBSP `0xA0`, que é **byte real da cifra**. Senha
   legítima seria recusada, de forma intermitente e inexplicável.
3. **Comparação em tempo constante** (`timingSafeEqual`), não `===`.

E a leitura do campo "inativo" **falha fechado**: só é ativo o que se reconhece
explicitamente como falso; valor estranho recusa o acesso e emite aviso.

#### O destino, e por que a implementação local é interina

O certo é a conferência viver **num endpoint do próprio ERP**. O motivo não é o
que parece: a cifra **não tem chave** — é deslocamento posicional e acompanha
cada instalação —, então "a chave viveria no Git" é argumento falso.

O motivo real: a implementação local obriga o portal a **ler
`caduser.senha`**, e a credencial que ele usa é de consulta SQL livre. Invadir
o contêiner expõe o cadastro de senhas inteiro. **Restringir a tabela por
credencial é parte do pedido**, não extra — sem isso, mover a conferência para
a API do ERP não reduz risco nenhum.

Mantenha a conferência **isolada atrás de uma função** (aqui,
`criarVerificadorLocal`), para que virar chamada HTTP não mexa em mais nada.

---

### 4.3 O que NÃO fazer — e é o erro que o Base44 cometeu

> O portal anterior validava a senha **no navegador**, com o algoritmo da cifra
> embutido no JavaScript da página. Quem abrisse o portal recebia a função que
> decifra senha de qualquer usuário do ERP.

Um `import` distraído reproduz isso, e **o empacotador não reclama** — ele
apenas inclui o módulo no pacote público. Por isso existe uma guarda
automatizada (§8).

### 4.4 O login tem de ser honesto sobre a causa

Três respostas distintas, nunca duas:

| situação | o que o operador lê |
|---|---|
| senha errada | "usuário ou senha inválidos" |
| legado fora do ar | **"o sistema de cadastro não está respondendo — a sua senha está certa"** |
| sem permissão | recusa explícita |

Juntar as duas primeiras num "acesso negado" faz o operador trocar a senha
durante uma queda do ERP. `src/auth/credencial.ts` separa isso com um motivo
`ERP_INDISPONIVEL`.

#### E entrar com o legado fora do ar

Copiar de `src/auth/lembranca.ts`. A cada login bem-sucedido **contra o ERP**,
grava-se um `scrypt` da senha (custo 16.384, sal de 16 bytes). Quando o ERP
cai, esse hash permite entrar.

Três regras que tornam isso aceitável — e as três precisam valer juntas:

1. **Só entra em cena quando o erro é `ErpIndisponivel`.** Nunca como
   alternativa a uma recusa.
2. **Só serve a quem já entrou antes** — não cria acesso, prolonga um existente.
3. **Validade de 30 dias**, e **falhar ao gravar não derruba o login**.

⚠ Guarde o **hash**, jamais a senha cifrada do ERP: um vazamento do banco do
portal não pode entregar credencial do sistema legado.

⚠ Neste projeto isso **nunca foi exercitado com o ERP realmente fora do ar** —
só em teste. Registrado como pendência, e vale registrar no próximo também.

### 4.5 Sessão sem estado no servidor

Copiar de `src/auth/sessao.ts`.

**O contêiner reinicia a cada publicação.** Sessão guardada em memória
deslogaria todo mundo a cada deploy — e como a orientação aqui é *publicar
sempre*, isso inviabilizaria a própria disciplina. A saída é um token
autocontido: `<corpo base64url>.<assinatura HMAC-SHA256>`.

```
portal_sessao=<token>; Path=/; HttpOnly; SameSite=Lax; Max-Age=43200[; Secure]
```

- **12 horas** de validade. Um turno de trabalho.
- `SameSite=Lax` basta: front e API vivem no mesmo domínio e no mesmo contêiner
  (§9.5). Sem CORS, sem segundo lugar para esquecer de publicar.
- `Secure` **condicional**, por variável de ambiente — `http://localhost`
  recusa cookie seguro. Verdadeiro quando nada é dito.

Duas coisas que se erram:

1. **base64url não é cifra.** O conteúdo do cookie é legível por qualquer um. O
   que a assinatura garante é que ninguém troque `VENDEDOR` por `GESTOR`. Não
   ponha nada ali que não possa ser lido.
2. **Assinatura prova origem, não sanidade.** Valide a *forma* do conteúdo
   mesmo com assinatura válida — um token que você mesmo emitiu numa versão
   anterior do software pode ter campo que não existe mais.

O front guarda uma **cópia de exibição** (nome, perfil) em `sessionStorage`,
que não autentica nada: adulterá-la não concede acesso, porque a autoridade é o
cookie e quem decide é a rota. A senha nunca sai do estado do componente.

### 4.6 Limitador de tentativas

Copiar de `src/api/autenticacao.ts`. Valores em produção: **10 tentativas**,
janela de **5 minutos**, espera de **60 s**, teto de 5.000 chaves.

Dois detalhes que fazem diferença:

- **Chaveie por usuário E por origem, simultaneamente.** Só por usuário permite
  varrer logins de um IP; só por IP pune escritório inteiro atrás de um NAT.
- **Sucesso limpa a chave do usuário, nunca a do IP.** Senão um acerto no meio
  de uma varredura zera o contador do atacante.
- **Legado fora do ar não conta como tentativa** — e responde **503**, não 401
  (§4.4).

### 4.7 Perfil por variável de ambiente, não por lista no código

`USUARIOS_GESTORES=L` no ambiente. A lista de gestores muda sem recompilar, e
não vaza no pacote do navegador. Mantenha a decisão **numa linha só** — aqui é
literalmente um `Set.has()` — para que trocá-la por consulta a uma tabela, no
dia em que houver uma, não exija caçar a regra espalhada.

### 4.8 Onde o recorte por perfil acontece DE VERDADE

Três camadas, e só a terceira vale:

| camada | dá para burlar? |
|---|---|
| menu escondido | **sim** — basta digitar a URL |
| trecho oculto na tela | **sim** — é só navegador |
| **a rota, no servidor** | **não** |

Esconder no menu é ergonomia. **Recusar na rota é segurança.** Faça as duas, e
nunca confunda a primeira com a segunda.

**A trava se declara como lista de EXCEÇÕES.** Um gancho `preHandler` exige
sessão em tudo sob `/api/`, e a lista enumera o que é livre (entrar, sair) — o
contrário de listar o que é protegido. Assim **rota nova nasce protegida**, e
esquecer de anotá-la é a falha segura.

Detalhe que se esquece: usuário pedindo dado alheio recebe **lista vazia**, não
erro. Recusar com mensagem confirma que o dado existe.

Registre a autenticação **antes** das rotas de dados, para que o gancho já
exista quando elas nascerem.

---

## 5. Falar com um sistema legado

### 5.1 Presuma que a API mente

Cada uma destas foi descoberta em produção, custando horas:

1. **Campo vazio some do JSON.** Não vem `null` — a chave não existe. Código
   que lê `linha.campo` recebe `undefined` sem erro nenhum.
2. **Erro de tamanho de consulta devolve 404** — o mesmo código de "sem
   registros". A consulta grande demais **parece um período vazio**.
3. **A paginação reexecuta a consulta inteira a cada página.**
4. **A API estoura a memória**, e devolve `OutOfMemoryException` dentro do
   corpo de um HTTP **400**. O teto **cai conforme o servidor é usado**: a
   mesma carga levou 95 s de manhã e 927 s à tarde.

**A lição geral:** num legado, *ausência de erro não é evidência de acerto*.
Toda leitura precisa de uma conferência independente — contagem, invariante ou
número conhecido.

### 5.1.1 Meça os limites, e recuse localmente

Os dois tetos que importam foram descobertos medindo, não lendo documentação:

**Tamanho da consulta** — 1.540 caracteres devolveram os 19 registros
esperados; **2.240 devolveram zero, sem erro nenhum**. O custo real dessa
descoberta: lotes de 60 chaves geravam 4.279 caracteres, voltavam vazios, e
**2.280 notas foram classificadas como "sem título no financeiro"**. Um relatório
inteiro errado, sem uma linha de log.

O cliente HTTP passou a **recusar localmente** acima de 1.800 caracteres, antes
de enviar. Erro que você mesmo levanta é infinitamente melhor que resposta
vazia plausível.

**Registros por página** — medido em 12/08/2026, mesmo período:

| por página | tempo | requisições |
|---:|---:|---:|
| 1.000 | 157,7 s | 15 |
| **2.000** | **90,9 s** | 8 |
| 5.000 | 43,0 s | 4 |
| 10.000 | **HTTP 400** | — |

Ficou em 2.000 por decisão do cliente: valor já provado em produção, com folga
até o teto. Registre também **o que não funcionou** — remover 4 dos 5 JOINs
economizou 12%, tirar `ALLTRIM` não mudou nada. Sem isso alguém tenta de novo.

### 5.1.2 Onde o legado esconde o motivo do erro

A mensagem que interessa vinha **no FIM do corpo do 400**: a API ecoava a
consulta inteira e só então acrescentava `[Exceção do tipo '...' foi
acionada.]`. Truncar o corpo pelo começo — o reflexo natural — descartava
exatamente a informação procurada.

Ao registrar corpo de erro de um legado, **guarde as duas pontas** ou procure
por padrão, nunca só o prefixo.

### 5.2 Resiliência: o que repetir e o que não

Copiar de `src/erp/resiliencia.ts`.

| erro | repete? | por quê |
|---|---|---|
| rede, timeout, 500/502/503/504 | **sim**, com espera crescente | é transitório |
| **400** | **não** | aqui significa "consulta grande demais". Repetir só espera mais para falhar igual — a saída é dividir a faixa |
| 404 | **não** | ambíguo, e repetir não desambigua |
| 401 / 403 | **não** | credencial não melhora com insistência |

Valores que funcionaram: timeout **45 s**, **3** tentativas, esperas de **1 s e
3 s**. Retentativa **por página**, não pela consulta inteira.

### 5.3 Carga agendada, com travas

Copiar de `src/api/agenda.ts`. Três horários — **0h, 6h e 12h** — e as travas
que importam:

- só mexe no **período corrente**;
- **nunca toca em período já fechado**;
- **não roda em cima de outra carga** em andamento;
- falhar **não derruba o agendador**: registra o motivo e tenta no horário
  seguinte.

O terceiro horário é decisão do cliente e vale como princípio geral:
**redundância no lugar de procedimento.** Se a carga da madrugada falha, a das
6h corrige antes de o escritório abrir, e o manual não precisa ensinar ninguém
a reagir a uma falha. Manual que ensina a reagir é manual que ninguém lê na
hora certa.

#### ⚠ O FUSO DO CONTÊINER — errado em 17/08/2026, e nada acusou

Tarefa agendada decide por `new Date().getHours()`, ou seja pelo horário
**local do processo**. Contêiner sem fuso sobe em **UTC**, e as três cargas
combinadas aconteceram às **21h, 3h e 9h** de Brasília — três horas adiantadas
em relação ao que os manuais prometiam ao cliente.

**Nada acusou, e vale entender por quê** — são quatro camadas que deveriam
pegar e não pegam:

1. o `/saude` respondia em ISO, que é **sempre UTC**;
2. os testes fingem a hora com `Date.parse('...T12:00:00')` — string **sem
   fuso**, que o Node lê como hora local da máquina de quem testa. Passam, e
   não dizem nada sobre o contêiner;
3. a tela mostra a hora convertida para o fuso do **navegador**, que é o do
   operador, não o do servidor;
4. a carga rodou e completou. Não havia erro nenhum a investigar.

Só apareceu porque o operador estranhou o horário na barra do topo.

**A correção são DUAS linhas, e a segunda sozinha não funciona:**

```dockerfile
RUN apk add --no-cache tzdata
ENV TZ=America/Sao_Paulo
```

O Alpine não traz o banco de fusos. Com `ENV TZ` sozinho, `getHours()` continua
em UTC **em silêncio** — o pior modo de falhar, porque parece configurado.

Fica no `Dockerfile`, não numa variável do painel, para **viajar com a
imagem**: publicar noutro serviço não pode reintroduzir o defeito. Continua
sobreponível por `TZ` no ambiente, para cliente de outro fuso.

**E publique o fuso no `/saude`.** Propriedade invisível de fora é propriedade
que ninguém confere:

```json
"fuso": { "nome": "America/Sao_Paulo", "deslocamento": "-03:00",
          "horaLocal": "2026-08-17 10:12", "horariosDaCarga": [0, 6, 12] }
```

Regra geral: **se o comportamento depende do relógio local do servidor, o
relógio local do servidor tem de ser observável.**

---

## 6. O banco de dados é opcional — e comece sem ele

O portal sobe com `DATABASE_URL` vazia e mantém tudo em memória. O banco entra
quando houver algo que **precise sobreviver a um reinício** (aqui: os
fechamentos congelados).

Ganho concreto: o primeiro deploy acontece no dia 1, sem esperar decisão de
persistência. E o `GET /saude` informa o estado do banco em vez de o servidor
recusar subir.

**Migrações numeradas e idempotentes**, com *backfill* quando a forma dos dados
muda (`src/dados/esquema.ts`). Nunca uma migração que perca dado sem dizer.

---

## 7. TypeScript: as três opções que pagam

No `tsconfig.json` da raiz:

```json
"strict": true,
"noUncheckedIndexedAccess": true,
"exactOptionalPropertyTypes": true
```

- `noUncheckedIndexedAccess` — `array[0]` passa a ser `T | undefined`. Irrita no
  começo e evita a classe inteira de defeito "a lista veio vazia".
- `exactOptionalPropertyTypes` — impede confundir "campo ausente" com "campo
  igual a `undefined`". Num portal que fala com legado, essa distinção é o
  problema (§5.1).

### A regra que atravessa o projeto inteiro: **`null` não é zero**

Palavras do cliente: **"R$ 0,00 é uma afirmação"**.

Valor desconhecido, oculto ou não apurado sai como `null`, e a tela mostra um
travessão. Somar tratando `null` como zero produz um total menor que o real
**com cara de total completo** — e ninguém desconfia de um número que existe.

Na agregação, `null` **contamina** a soma:

```ts
const somar = (a: number | null, b: number | null) =>
  (a === null || b === null ? null : a + b);
```

Ao ocultar um valor, oculte também **o que permite reconstruí-lo**: esconder a
comissão e deixar a base e o percentual não esconde nada
(`src/api/ocultar.ts`).

---

## 8. Guardas de arquitetura — testes que impedem regressão de decisão

O padrão mais barato e mais subestimado deste projeto:
`tests/guardas-arquitetura.test.ts`. São testes que **leem o próprio código
fonte** e quebram o build quando uma decisão de segurança é desfeita por
descuido. Nenhum testa comportamento novo.

Os quatro que valem copiar:

1. **Nenhum arquivo do front importa o módulo de cifra** — nem qualquer coisa
   de fora de `web/`.
2. **Nenhum parâmetro de URL concede sessão.** Existiu um atalho `?entrar=` que
   gravava sessão sem senha; o teste garante que ele não volta.
3. **Toda rota `/api` nasce protegida** — o teste sobe o servidor, varre as
   rotas declaradas no fonte e exige 401 em todas menos entrar e sair. Rota
   nova esquecida sem sessão **quebra o build**.
4. **O recorte por vendedor lê a sessão, não o parâmetro.**

⚠ Ao escrever a guarda nº 1, verifique o **especificador de importação**, não o
texto do arquivo: um comentário que cite `src/auth/cifra.ts` para explicar onde
a conferência acontece é documentação correta e não empacota nada. A primeira
versão da guarda acusou exatamente isso.

Vale também `tests/telas-nao-divergem.test.ts`: duas telas que somam a mesma
coisa **têm de dar o mesmo número**. Foi o que pegou uma incoerência que o
cliente sentiu antes de mim — *"tô achando estranho"*.

### 8.1 O legado falso — o helper que sustenta a suíte

Copiar de `tests/ajuda-servidor.ts`. **Nenhum teste toca a API do cliente**, e
isso não depende de disciplina: o cliente do legado entra **por parâmetro** na
criação do servidor (`opcoes.consultar ?? clienteReal()`), e os testes passam
um falso.

O falso é uma função `(sql) => Promise<Linha[]>` que **roteia por expressão
regular sobre o próprio SQL**. Sem servidor HTTP de mentira, sem mock de
módulo, sem biblioteca.

Quatro decisões que fazem esse falso valer alguma coisa:

1. **As senhas falsas são geradas chamando a cifra REAL.** Se o falso guardasse
   texto puro, o teste de login provaria apenas que o falso concorda consigo
   mesmo.
2. **Os comprimentos das senhas são escolhidos para exercitar os dois ramos**
   do algoritmo do legado (aqui: 9 e 12 caracteres, atravessando o `Len > 10`).
3. **O falso reproduz os DEFEITOS da API**, não a versão idealizada dela —
   inclusive omitir a chave do JSON quando o valor é vazio (§5.1). Falso limpo
   demais deixa passar exatamente a classe de defeito que ele deveria pegar.
4. **Os dados distinguem "o filtro funciona" de "o falso só tinha dado de um
   usuário".** Duas notas de vendedores diferentes; dois clientes sumidos,
   sendo um fora da carteira; um total que **não** é `qtd × valor`, para
   flagrar tela que recalcule em vez de transportar.

Somam-se helpers de fluxo: `comServidor()` (com fechamento garantido),
`entrar()` autenticando **pela rota real**, e `cookieDe()`. O login de teste
passa pelo mesmo caminho do login de produção — atalho de teste que pula
autenticação é a origem clássica do atalho que sobra em produção.

### 8.2 Convenção de nome de teste

- **Teste de unidade** leva o nome do módulo: `cifra.test.ts` ↔
  `src/auth/cifra.ts`.
- **Teste de regra de negócio ou de regressão** leva o nome da **afirmação que
  prova**: `vendedor-so-ve-o-seu`, `telas-nao-divergem`,
  `titulo-antigo-em-aberto`, `transporte-de-comissao`.

O segundo grupo é o que se lê meses depois. `rateio.test.ts` não diz nada;
`vendedor-so-ve-o-seu.test.ts` diz o que se perde se ele for apagado.

---

## 9. O front

### 9.1 Estrutura

```
web/src/
  paginas/      uma tela por arquivo
  shell/        Layout · Sidebar · Topbar
  componentes/  o que se repete entre telas (+ BarreiraDeErro)
  styles/       tokens.css · shell.css · impressao.css
  fonte.tsx     contexto global de dados (e o modo demonstração)
  api.ts        cliente HTTP e os tipos que atravessam a ponte
  nav.tsx       o menu, e quais itens cada perfil vê
```

`tokens.css` guarda cor, espaçamento e tipografia como variáveis. Trocar a cara
do portal para outro cliente é mexer nesse arquivo.

Três regras de aparência que valem mais que a paleta:

- **`font-variant-numeric: tabular-nums`** numa classe `.num` aplicada a toda
  coluna de valor. É o que faz dígito ficar sob dígito, e é a diferença visual
  entre uma tabela de ERP e uma planilha desalinhada.
- **Tipografia um degrau abaixo do padrão** da biblioteca (aqui 11–18 px). Tela
  de ERP é densa; o tamanho de blog desperdiça metade do monitor.
- **Fontes empacotadas via `@fontsource`, nunca de CDN.** O cliente opera em
  rede fechada — fonte de CDN não carrega e a tela chega torta sem explicação.

### 9.1.1 Os interstícios ficam no Layout, não nas telas

"Carregando", "sem dados ainda" e "deu erro" são resolvidos **uma vez**, no
Layout, para que as quinze telas respondam igual. Repetir isso por tela produz
quinze redações diferentes da mesma situação — e catorze delas envelhecem.

A **barreira de erro** envolve só o conteúdo, **não o portal inteiro**: uma
tela quebrada não pode levar junto a navegação. E ela leva
`key={pathname}`, para reiniciar ao navegar — sem isso, o usuário fica preso na
tela de erro mesmo clicando em outro item do menu.

### 9.2 Proxy no Vite — economiza uma hora de confusão

Sem ele, `/api/*` bate no Vite e volta o `index.html`; o `fetch` recebe HTML
onde espera JSON, e o erro aparece como **"token < in JSON"**, que não aponta
para lugar nenhum.

```ts
server: { proxy: {
  '/api':   { target: 'http://localhost:3000', changeOrigin: true },
  '/saude': { target: 'http://localhost:3000', changeOrigin: true },
} }
```

### 9.2.1 Estado global: Context API basta

Não há Redux, Zustand nem TanStack Query neste portal. Um provedor
(`fonte.tsx`) busca, guarda e distribui; as telas consomem **hooks derivados
das mesmas listas**. A regra estrutural: **nenhuma tela busca dado por conta
própria.** Se duas telas mostram números diferentes do mesmo conceito, é
defeito — e o jeito de garantir que não aconteça é elas lerem a mesma fonte.

Quatro comportamentos que a experiência impôs:

- **Revalidação silenciosa.** Mostre "carregando" só na primeira vez. Anunciar
  a cada revalidação desmonta o componente, e o mês que o usuário tinha
  escolhido volta sozinho ao padrão.
- **Sondagem adaptativa.** 6 s enquanto há trabalho em andamento, 20 s em
  espera, **nada** quando está pronto. Intervalo fixo ou martela o servidor ou
  demora a perceber.
- **Transporte, nunca recálculo.** A tela mostra o número que o motor produziu;
  não o recalcula a partir das partes. Recalcular perdeu o sinal de uma
  devolução e o centavo do maior resto.
- **Tipos redeclarados no front, de propósito.** O front compila separado do
  servidor; a divergência entre os dois aparece em tempo de compilação, em vez
  de virar `undefined` em produção.

⚠ **Deduplique opções de seletor defensivamente.** Uma opção duplicada derruba
a tela inteira na biblioteca de componentes — e o duplicado costuma vir do
legado, não do seu código.

⚠ **Rótulo, não seletor, quando não há o que escolher.** Um campo que parece
selecionável e não seleciona é pior que um texto.

### 9.3 Modo demonstração desde o começo

`?demo=1` faz o portal servir dados fictícios, com **tarja visível** no topo.
Serve para três coisas, e todas apareceram:

- mostrar o portal ao cliente antes de existir integração;
- tirar os prints do manual **sem expor a carteira de um vendedor real** a
  todos os outros;
- verificar telas em máquina sem credencial.

⚠ **Derive os dados fictícios uns dos outros**, não escreva constantes soltas.
Um resumo fixo em `[]` fez um relatório sair vazio, e a folha montava com a
mensagem correta para dado ausente — parecia funcionar. Se o Analítico e o
Resumo somam as mesmas notas fictícias, eles concordam de graça.

⚠ **O modo demonstração respeita o mesmo recorte por perfil** que os dados
reais. Sem isso ele mentiria de forma perigosa: mostraria ao gestor uma tela
dizendo "é isto que o vendedor vê" com as notas de todos os colegas dentro.

⚠ **Demonstração NÃO concede acesso.** Este portal chegou a ter `?demo=gestor`
gravando sessão de gestor sem senha, e `?entrar=<login>` fazendo o mesmo com
qualquer login. Os dois foram removidos por segurança, e hoje uma guarda
automatizada (§8) impede que voltem. Se o parâmetro de demonstração puder
autenticar, ele é uma porta dos fundos — não um modo de exibição.

Note o efeito colateral: a remoção do atalho **quebrou a captura de telas do
manual**, e os manuais ficaram um mês sem poder ser refeitos. Ao remover um
atalho de conveniência, procure quem dependia dele **na mesma leva**.

### 9.4 Depois do login, recarregue a página inteira

Não navegue por dentro do React. O provedor de dados fica acima das rotas e não
se re-executa numa navegação client-side: ele continua servindo o que decidiu
antes do login — que, sem sessão, é lista vazia. **O gestor entrava e via
"R$ 0,00 · 0 notas" como se fosse a apuração**, sem carregamento, sem erro e
sem aviso.

```ts
window.location.assign('/resumo');
```

Vale para qualquer troca de identidade (assumir e encerrar a visão de outro
usuário, também).

### 9.5 ⚠ O `publico/` não se atualiza sozinho

Em produção o `Dockerfile` copia `web/dist` para `publico/`. **Localmente é
cópia manual**, e esquecer faz o servidor servir uma versão antiga *sem erro
nenhum*:

```bash
cd web && npm run build && cd ..
rm -rf publico && cp -r web/dist publico
```

Custou uma conferência inteira: os relatórios "continuavam com o defeito"
depois de corrigidos.

---

## 10. Relatórios impressos

`window.print()` com CSS de impressão, sem biblioteca de PDF. O navegador já
imprime e já salva em PDF.

Três detalhes que não são óbvios:

1. **A folha vive fora da vista, não fora do DOM.** Use deslocamento
   (`.folha-oculta`), não `display: none` — é dela que o PDF sai.
2. **Imprima no efeito, não no clique.** `window.print()` chamado dentro do
   próprio `onClick` imprime o estado anterior: o modelo recém-escolhido ainda
   não está no DOM.
3. **O cabeçalho da folha lê a MESMA fonte que o corpo.** Ver §11.

---

## 11. A disciplina de verificação

O que separou este projeto do anterior. Não é ferramenta — é hábito.

### 11.1 Renderize e **olhe**

Onde não há teste automático (tela, folha impressa, imagem de manual), a única
verificação é abrir e ler. Não é preciosismo:

- dez capturas de tela do manual saíram **da tela de login**, três vezes
  seguidas, por três causas diferentes. Nenhuma emitiu erro. **Só apareceu
  porque abri as imagens.**
- um relatório saía **vazio**, montando com a mensagem correta para dado
  ausente;
- o cabeçalho de uma folha dizia **"Fechamento: julho"** sobre linhas de junho.
  Impressa, ela vira documento arquivado com o mês errado.

Nenhum dos três seria pego por teste: o dado existia, era válido e tinha o
formato certo. **Só a leitura humana percebe que discordam.**

### 11.2 Depois de olhar, automatize o que você olhou

Toda vez que a leitura pegar algo, **converta em verificação repetível** antes
de seguir. `docs/manual/conferir-relatorios.mjs` nasceu assim: renderiza as
cinco folhas e afirma linhas, valores, ausência de erro de JS e a concordância
entre o mês do cabeçalho e o do seletor.

O padrão técnico vale copiar: **subir o portal local com legado falso e dirigir
um navegador headless pelo protocolo de depuração do Chrome.** Sem credencial
real, sem tocar no ambiente do cliente. Ver `docs/manual/capturar.mjs`.

### 11.3 Confira contra número conhecido

Antes de mostrar o portal a qualquer usuário, escolha **dois ou três valores
que alguém já conhece** e confira. Aqui foram duas vendedoras em junho — uma
bateu ao centavo, a outra ficou R$ 2,40 fora, e essa diferença virou pendência
registrada em vez de descoberta constrangedora.

**Se não baterem, pare.** A diferença precisa ser entendida antes, não depois.

### 11.4 Registre o que NÃO foi verificado

Tão importante quanto o que foi. "A carga agendada nunca disparou de verdade"
tem de estar escrito, ou daqui a três meses alguém supõe que sim.

---

## 12. Documentação: o que escrever, e para quem

| documento | leitor | regra |
|---|---|---|
| `README.md` | quem abre o repositório | mantenha vivo. O deste projeto dizia "49 testes" quando eram 513 |
| `CONTINUIDADE.md` | você, daqui a seis meses | estado, mapa, o que morde, o que ficou aberto, **e os erros cometidos** |
| `REGRAS.md` | quem for mexer no cálculo | uma seção por regra, com o arquivo onde ela vive |
| `PUBLICAR.md` | quem for publicar | passo a passo, incluindo o que fazer quando "não vai" |
| manual do operador | o usuário final | ver abaixo |

### 12.1 Manual por perfil, e em linguagem simples

Dois manuais, um por perfil — as rotinas são diferentes. E, para o usuário
final, a régua que o cliente definiu: **"o manual tem que ser pra uma pessoa de
6 anos ler"**.

Na prática: frases curtas, uma ideia por linha, sem termo técnico. Palavras
como *competência*, *apuração* e *provisionado* não entram. **Se uma frase
precisa de explicação, ela está errada.**

Prints do manual saem do **modo demonstração** — nunca de dados reais.

### 12.2 Documentação desatualizada é pior que ausente

Três documentos deste projeto chegaram a afirmar coisas falsas. Ao mexer em
comportamento, **procure quem o descrevia** e corrija na mesma leva.

---

## 13. Erros que já custaram caro — não repita

1. **Afirmar sem verificar.** Disse que nada tinha subido com três deploys no
   painel. Conferir antes de negar.
2. **Inventar um problema em código não lido.** Descrevi um furo que não
   existia; o cliente corrigiu de cabeça. Ler antes de diagnosticar.
3. **Corrigir o que não estava quebrado.** Aquela "correção" introduziu
   contagem em dobro e foi para produção.
4. **Confiar em teste que passou pelo motivo errado.** Duas vezes um teste
   passou porque o padrão de busca nunca casou. Teste que passa na primeira
   tentativa merece um minuto de desconfiança.
5. **Ler o próprio verificador como se fosse verdade.** Meu conferidor acusou
   "folha sem valor" nas cinco folhas — o defeito era o padrão de busca dele.
   Quando *tudo* falha, suspeite do instrumento.

O padrão dos cinco: **o sistema não reclamou.** Num portal sobre legado, isso
nunca é evidência de acerto.

---

## 14. O que este projeto NÃO tem — e o que fazer a respeito

Registrado para não presumir, e porque duas destas valem corrigir no próximo.

| ausente | consequência | recomendação |
|---|---|---|
| **CI** (nenhum workflow) | a suíte só roda quando alguém lembra; o deploy automático publica **sem** rodar teste nenhum | **ligar no dia 1.** Um workflow que roda `npm test` no push já elimina a pior falha: publicar com teste vermelho |
| **ESLint / Prettier** | há `eslint-disable` no código **sem o arquivo de configuração correspondente** — comentários que não desativam nada | ou configurar, ou remover os comentários órfãos |
| **Teste de front** | tela e folha impressa só se verificam abrindo (§11) | as guardas por análise estática (§8) cobrem o essencial de segurança; para o resto, o roteiro headless de §11.2 |
| Autenticação por API do legado | a conferência de senha é reimplementação local da cifra | isolado atrás de uma função, para virar chamada HTTP sem mudar mais nada |

**A ausência de CI é a mais séria**, porque interage mal com o deploy
automático: `git push` publica, e nada garante que a suíte passou. Enquanto não
houver, a disciplina manual é rodar `npm test` **antes** de todo push — e ela
falha exatamente no dia em que alguém está com pressa.

---

## 15. Checklist do projeto novo

**Dia 1**
- [ ] Especificação escrita e **aprovada pelo cliente**
- [ ] Repositório criado e no GitHub
- [ ] Serviço no Easypanel, com Dockerfile e porta 3000
- [ ] Webhook de publicação automática ligado e testado
- [ ] **CI rodando a suíte no push** (a lacuna do projeto anterior — §14)
- [ ] URL do webhook guardada **fora** do repositório
- [ ] `.env.example` com os **nomes** das variáveis, sem valores
- [ ] Servidor recusa subir com variável faltando, **nomeando todas de uma vez**
- [ ] `GET /saude` respondendo
- [ ] Primeiro deploy, ainda feio

**Antes da primeira tela**
- [ ] Motor de cálculo isolado e testado
- [ ] Cliente do legado provado contra a API **real**
- [ ] Login conferindo senha **no servidor**
- [ ] Guardas de arquitetura no lugar
- [ ] Modo demonstração funcionando

**Antes de mostrar ao cliente**
- [ ] Conferido contra **dois ou três números que alguém já conhece**
- [ ] Todas as telas abertas e **olhadas**
- [ ] `README` e manual condizentes com o que existe
- [ ] O que **não** foi verificado, escrito

**Toda publicação**
- [ ] Suíte inteira passa
- [ ] Front compilado e `publico/` atualizado
- [ ] Nome do pacote servido **igual** ao compilado
- [ ] `/saude` respondendo
