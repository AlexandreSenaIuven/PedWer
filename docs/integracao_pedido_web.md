# Superfície de dados e integração para o lançamento de pedido web

**Documento de arquitetura de integração**
Data: 18/08/2026
Objeto: substituição do lançamento de pedido do aplicativo WER (`ped_wer.scx` +
`fechapedwer.scx` + `pcondpg2.prg`) por aplicação web consumindo a base Fenícia.
Base medida: `Fox\WER` (snapshot de 15/07/2026).
Documento antecedente obrigatório: **`analise_wer_app.md`** (mesma pasta) — este
documento **não repete** o mapeamento de fluxo, as regras de preço/comissão/crédito
nem os defeitos ali estabelecidos; referencia-os por seção.

---

## 0. Método e convenções

| Marca | Significado |
|---|---|
| `FATO MEDIDO` | Medido nesta máquina, nesta data, sobre o arquivo citado. Reprodutível. |
| `HIPÓTESE` | Inferência plausível a partir de código lido, não exercitada. |
| `NÃO VERIFICADO` | Não foi medido nem inferido com segurança. Acompanha sempre o que responderia a pergunta. |

Todas as medições de DBF foram feitas por leitura binária, **somente leitura**.
Nenhum arquivo foi alterado. **Nenhuma requisição HTTP foi emitida a qualquer host
de cliente** — a WebAPI de produção não foi tocada.

Os `.prg` foram lidos em CP1252. Os `.scx` foram despejados com
`Fox\_claude_ferramentas\dumpscx.ps1`; as citações `(dump)` referem-se à linha do
despejo, não do binário.

**Correção metodológica registrada:** a primeira passagem de medição sobre campos
numéricos de `clientes.dbf` usou a cultura local do PowerShell, que lê `"1.00"`
como `100` (ponto = separador de milhar em pt-BR). Todos os números de campo
numérico neste documento vêm da segunda passagem, com `InvariantCulture`. A
primeira passagem produziu, por exemplo, "0 clientes com regime Constante", que é
falso — o valor correto é 577.

---

## 1. Veredito sobre escrita pela WebAPI Fenícia 2

### 1.1 O que existe hoje

`FATO MEDIDO` — Varredura de toda a pasta `Sistemas` (código, documentação e
medições) encontra **exatamente dois** caminhos de WebAPI Fenícia 2 nomeados:

| Endpoint | Método | Estado | Onde está documentado |
|---|---|---|---|
| `/table/List2` (ou `/webapifenicia2/table/list2`) | `GET` | **Em produção, medido duas vezes** | `portalcomissao\docs\medicoes\2026-08-11-contrato-real-da-webapi.md`; `Controle de fichas\API_CLIENTES_JULIANA.md` (medido em 06/08/2026) |
| `/webapifenicia2/autenticar` | `POST` | **NÃO EXISTE — é uma especificação solicitada ao mantenedor da WebAPI** | `portalcomissao\docs\specs\2026-08-11-contrato-autenticacao.md` |

O segundo é um **pedido de contrato**, não um endpoint vivo. O próprio documento o
declara: o portal *"já autentica, com uma implementação local"*, e a troca pelo
endpoint *"é uma variável de ambiente"* ainda não acionada. O
`DOC_NORTEADOR_PROJETOS.md` §14 confirma, listando "Autenticação por API do legado"
como **ausente**.

O cliente de produção da casa declara a natureza do que consome no cabeçalho do
próprio arquivo (`portalcomissao\src\erp\cliente.ts:68`):

```
Cliente da WebAPI Fenícia 2. Somente leitura.
```

`FATO MEDIDO` — Busca por método HTTP mutante (`POST`/`PUT`/`PATCH`/`DELETE`) em
direção ao ERP, e por `INSERT`/`UPDATE` dirigidos ao ERP, em todo o
`portalcomissao`: **nenhuma ocorrência**. Os `INSERT INTO` encontrados são todos
contra o Postgres do próprio portal (`src/dados/esquema.ts`,
`src/dados/repositorio.ts`).

### 1.2 O veredito

> **`NÃO VERIFICADO`: não é possível afirmar que a WebAPI Fenícia 2 recusa escrita,
> nem que a aceita.**

O que se pode afirmar com segurança é mais estreito, e é o seguinte:

1. `FATO MEDIDO` — **Nenhum endpoint de escrita é conhecido, documentado ou usado**
   por qualquer projeto desta casa.
2. `FATO MEDIDO` — O único endpoint vivo transporta uma **instrução SQL arbitrária
   fornecida pelo chamador**, no cabeçalho `Tabela`. Nada no contrato medido diz
   que ela é obrigatoriamente um `SELECT`.
3. `FATO MEDIDO` — A pergunta **já estava aberta e registrada** antes deste
   documento: `Controle de fichas\LEVANTAMENTO.md:53` lista como não verificado
   *"se há endpoint de escrita (insert/update) além do `list2`"*, e `:275` repete
   como pendência: *"Existe endpoint de escrita na API, ou ela é somente leitura?"*.

O item 2 é o ponto delicado. Uma API que recebe SQL cru **pode** aceitar
`INSERT`/`UPDATE` sem ter sido projetada para isso. Isso não é uma via aceitável —
ver §5.3 — mas impede o veredito categórico "a API é somente leitura".

### 1.3 O que responderia a pergunta, em ordem de custo

| # | Verificação | Custo | Onde |
|---|---|---|---|
| 1 | **Perguntar ao mantenedor da WebAPI** qual é a lista de rotas. É a mesma pessoa a quem o contrato de `/autenticar` foi endereçado. | minutos | conversa |
| 2 | Ler o **código-fonte da WebAPI Fenícia 2** e enumerar os controladores. A `list2` fica sob um controlador `table` — o nome sugere irmãos. | horas | fora desta árvore; **não está no `Sistemas`** (`FATO MEDIDO`) |
| 3 | Requisitar `GET /table/` e `OPTIONS` no host, ou um `swagger.json` | minutos | ⚠ **é requisição a host de cliente — fora do escopo desta análise, e não deve ser feita contra produção** |
| 4 | Enviar um `INSERT` a uma **tabela descartável** num ambiente de homologação | horas | exige ambiente de homologação da WebAPI, que **não se sabe existir** (`NÃO VERIFICADO`) |

**Consequência para a decisão:** enquanto o item 1 não for respondido, "integrador"
não é escolha de estilo — é a **única via conhecida** de gravar pedido. Isso precisa
estar explícito em qualquer plano, e é a informação de maior alavancagem em todo
este documento: uma pergunta de cinco minutos decide a arquitetura.

---

## 2. Topografia: o que é por empresa e o que é único

Esta seção precede os mapas porque errar aqui produz número que parece completo e
não é — o modo de falhar descrito no `DOC_NORTEADOR_PROJETOS.md` §3.5.

`FATO MEDIDO` — Contagem de containers e de tabelas físicas em `Fox\WER`:

| Container (`.dbc`) | Existe na raiz | Existe em `fenwin<emp>\` | Conclusão |
|---|---|---|---|
| `FENICIA.DBC` (15,2 MB) | sim | **sim** (01, 02, 03, 99) | **partido**: parte das tabelas é por empresa, parte não |
| `PEDIDO.DBC` (3,3 MB) | sim | **não** | **único e global** |
| `negocia.dbc` (13,9 KB) | sim | **não** | **único e global** (container local do WER) |
| `CUSTEIO.DBC` | sim | não | único |

A consequência prática é que **"o `fenicia` é por empresa" está errado como regra
geral**. O que é por empresa são tabelas específicas dentro dele. A lista física
medida (`fenwin02\` tem 132 DBFs; `fenwin01\`, 20):

**Por empresa** (`fenwin<emp>\`): `cadmat`, `cadmov`, `cplmov`, `cplmovi`,
`cadicm`, `cadsub`, `tabcol`, `tabgrp`, `cadinv`, `cadmattb`, `toponf`, `itensnf`,
e os livros fiscais `artln00`/`artlt00`/`artpl00`/`artsl00`.

**Único / global** (raiz): `clientes`, `pedido`, `pedemp`, `pedhead`, `negocia`,
`tabcomis`, `vendedor`, `caduser`, `tipos`, `tabcor`, `vencim`, `vencimr`,
`familia`, `ccusto`, `tabplan`, `artficha`, `apagar`, `compras`, `fornece`,
`forneced`, `fenlog`, **`wareas`**, **`wareascp`**, **`wareasb`**.

Dois pontos merecem destaque, porque são a raiz de problemas nas §4 e §6:

- **`pedido` é uma tabela única, com coluna `COD_EMPR C(2)`** — 102.104 registros,
  não existe `fenwin02\pedido.dbf`. O pedido de todas as empresas vive no mesmo
  arquivo (`FATO MEDIDO`).
- **`wareas` tem 1 registro só, na raiz.** O contador de pedido `CONT_PED` é
  portanto **global a todas as empresas** (`FATO MEDIDO`).

Isso também confirma, por outra via, o defeito D2 da análise (§4.1): `negocia` é
global e `cadmat` é por empresa — não é uma anomalia de dado, é a topografia.

---

## 3. Mapa de LEITURA

### 3.1 Fonte do mapa

Duas origens, ambas lidas: as chamadas `abertura(<container>,<tabela>,<modo>[,<índice>])`
do `Load` de `ped_wer.scx` (dump 2653-2746), mais as tabelas efetivamente
consultadas em `valt2w.prg`, `valida6wer.prg` e `pcondpg2.prg`.

Modo `"U"` = aberta para atualização; `"N"` = somente leitura.

### 3.2 O mapa

| Tabela | Container | Escopo | Registros (medido) | Chave de acesso | Papel no lançamento | Modo |
|---|---|---|---|---:|---|---|
| `clientes` | fenicia | **única** | 13.086 | `codigo` C(7), tag `clientes`; e **`SUBSTR(cgc,1,10)` sem índice** | cadastro, os 6 fatores da §7, limite de crédito, `cond_pag`, 2 vendedores | U |
| `cadmat` | fenicia | **por empresa** | 31.555 (02) · 22.499 (03) · 28 (01) | `grupo+referencia`, tag `cadmat` | preço de tabela, `gradecol`, `qtd_emb`, saldo. ⚠ **também é ESCRITA durante a digitação** — §4.4 Janela 4 | U |
| `negocia` | **negocia** | **única** | 11.996 | `cod_cli+grupo+referencia+tipo_prc`, tag `negocia2` | preço negociado (§4.1 da análise) | U |
| `tabcomis` | **negocia** | **única** | **10** | `negocio` (1 ou 2) | escada desconto→comissão | N |
| `vendedor` | fenicia | única | 193 | `codigo` | `tipo_vend` escolhe a escada (`"V"`→1, resto→2) | N |
| `tipos` | fenicia | única | 487 | `tipo` C(3), tag `tipos` | `tipo_es`, `posicao`, `status`, `ind_qtd`, `ind_valb` | N |
| `wareas` | fenicia | única | **1** | registro único | `cont_ped`, `comp_seq`, `ind_ped_dc`, `ind_ver_fi`, `tp_pedido`, `cons_min`, `ind_grade`, `ind_tot_n`, `ind_impped` | U (grava contador) |
| `wareascp` | fenicia | única | **1** | registro único | `especifico` (em branco), `ind_prazo`, `ind_largco` | N |
| `wareasb` | fenicia | única | **1** | registro único | `indcredito`, `limite_ped`, `bloq_pedid` | N |
| `vencim` | fenicia | única | 99 | `codigo`, tag `vencim` | condição de pagamento: `resumido`, `lim_venda`, `ind_dia_pf` | N |
| `vencimr` | fenicia | única | **5** | `resumido` C(16), tag `vencimr` | `ind_credit` — desliga a checagem de crédito por condição | N |
| `artficha` | fenicia | única (coluna `cod_empre`) | 144.287 — 02: 116.939 · 03: 27.345 | `codigo`, tag `artfich6`; `notafis`, tag `artfich3` | **saldo devedor do grupo econômico** e atraso | U (a alteração deleta) |
| `caduser` | fenicia | única | 57 | `usuario` | permissão (`vefuncao`), `cod_empr`, alçada de senha | N |
| `tabcol` | fenicia | **por empresa** | **2** | `codigo`, via `cadmat.gradecol` | segunda parcela do teto de desconto: `VAL(tabcol.Nome)` | N |
| `tabcor` | fenicia | única | 382 | `codigo`, via `cadmat.gradecor` | cubagem (usada na baixa) | N |
| `cadicm` | fenicia | **por empresa** | 55 | UF | alíquota de ICMS | N |
| `cadsub` | fenicia | **por empresa** | 649 | `grupo+referencia+uf+enquadra`, com recuo por `NCM+uf+enquadra` | MVA / ST / pauta (§4.6 da análise) | N (via `totitem`) |
| `ccusto` | fenicia | única | 126 | `codigo` | centro de custo do pedido | N |
| `tabplan` | fenicia | única | **3** | — | plano de contas | N |
| `familia` | fenicia | única | 85 | `grupo` | alçada por família — **inativa**, ver §4.3 | N |
| `forneced` | fenicia | única | 3.881 | `codigo` | transportadora / fornecedor | U |
| `fornece` | **pedido** | única | 41.519 | `codigo` | vínculo produto × fornecedor | U |
| `cliente` | fenicia | única | 894 | `codigo` | ⚠ tabela **distinta** de `clientes`; aberta no `Load` e sem uso identificado no fluxo de pedido (`NÃO VERIFICADO`) | U |
| `percrat` | **pedido** | única | **0** | — | rateio — vazia nesta base | N |
| `pedhead` | **pedido** | única | 62 | `cod_pedido`, tag `pedhead` | empenho — **inativo**, ver §4.3 | U |
| `pedido` | **pedido** | **única** | 102.104 | `es_mov+tipo_oper+codigo`, tag `pedido7`; `codigo`, tag `pedido` | itens do pedido; lida para detectar número em uso | U |
| `op`, `itemop`, `converte` | custeio | única | 87 · 6 · 0 | — | formulação/custeio; fora do caminho comum | N |

### 3.3 As três leituras caras — dimensionadas

Estas são as que decidem se um endpoint responde em milissegundos ou em minutos.

#### (a) Verificação de crédito — a mais cara, por duas ordens de magnitude

O algoritmo está em `valt2w.prg:249-345` e é, literalmente:

1. `StrCgc = SUBSTR(clientes.cgc,1,10)` — raiz do CNPJ;
2. `SELECT * FROM clientes WHERE SUBSTR(cgc,1,10)=StrCgc AND Posicao="M"` → a matriz;
3. `SELECT * FROM clientes WHERE SUBSTR(cgc,1,10)=StrCgc` → o grupo inteiro;
4. para **cada** cliente do grupo: `dbseek` em `artficha` por `codigo` e
   `DO WHILE codigo = StrCODIGO` percorrendo **todos os títulos daquele cliente**,
   pagos inclusive, filtrando `dt_pag` dentro do laço;
5. por título, se `cod_vencim` estiver preenchido: um `dbseek` em `vencim` **e** um
   em `vencimr`, para testar `ind_credit`;
6. três travas: atraso, `credito = 0`, `saldo + total do pedido > credito`.

`FATO MEDIDO` — Dimensão real dessa varredura em `Fox\WER`:

| Grandeza | Valor |
|---|---:|
| Grupos econômicos (raízes de CNPJ distintas) | 11.597 |
| Raízes que agrupam mais de um cliente | 580 |
| Maior grupo por número de clientes | **105 clientes** |
| Títulos varridos por grupo — média | **12,4** |
| Títulos varridos por grupo — **pior caso** | **10.631** (raiz `21.767.359`, 2 clientes) |
| Títulos totais em `artficha` | 144.287 |
| Títulos **abertos** (`dt_pag` vazio) em toda a tabela | **1.004** |
| Clientes com título aberto | 362 |

Duas observações de projeto saem daí:

- **O laço varre 144× mais linhas do que precisa.** O saldo devedor de todo o
  cliente cabe em 1.004 linhas na base inteira; o código percorre até 10.631 linhas
  para um único pedido porque filtra `dt_pag` depois de ler, não antes.
- **Os dois `SELECT ... WHERE SUBSTR(cgc,1,10)` são varredura completa de
  `clientes`** — nenhum índice serve a uma expressão `SUBSTR`. São 13.086 registros
  por pedido, duas vezes.

`HIPÓTESE` — Em SQL contra a WebAPI, o equivalente correto é uma consulta única
com `WHERE codigo IN (<grupo>) AND EMPTY(dt_pag)`, cujo resultado tem ordem de
dezenas de linhas. O ganho não é de otimização: é o que torna o endpoint viável.
Mas a armadilha nº 2 se aplica — um grupo de 105 clientes gera uma cláusula `IN`
que **estoura os 1.800 caracteres** e volta 404, indistinguível de "cliente sem
débito", que é justamente a resposta que libera o crédito. Este é o caso em que a
armadilha nº 2 **falha para o lado inseguro**, e por isso o lote tem de ser montado
por orçamento de caracteres, como em `portalcomissao\src\erp\conjuntos.ts`.

`FATO MEDIDO` — Dois achados de integridade nos dados que sustentam o crédito, não
descritos na análise antecedente:

1. **19 clientes têm `credito` negativo** (mínimo −1). A trava de `:319` testa
   `credito = 0` e a de `:328` só roda sob `IF credito > 0`. Um limite negativo
   **não satisfaz nenhuma das duas** — passa sem verificação alguma. Distribuição
   completa de `clientes.credito`: 1.159 em branco, 11.250 zero, **19 negativos**,
   658 positivos (máximo R$ 10.000.000,00).
2. **96 clientes têm `cgc` composto só de zeros**, e a raiz `00.000.000` agrupa
   **98 clientes**. Para a verificação de crédito, esses 98 são **um único grupo
   econômico**: o saldo devedor de qualquer um deles é somado contra o limite dos
   outros 97.

#### (b) Consulta de negociação — barata

Um `dbseek` em `negocia` por `cod_cli+grupo+referencia+tipo_prc`, tag `negocia2`,
por item digitado. 11.996 registros, chave exata, três guardas (achou,
`dt_venc >= DATE()`, `cod_valida = 1`). Nada a redesenhar: é um `SELECT` com quatro
igualdades e cabe folgadamente no limite de consulta.

#### (c) Preço de tabela — barata, mas dependente da empresa

`dbseek` em `cadmat` por `grupo+referencia` — **na empresa corrente**. Em SQL isto
obriga o prefixo `fenwin<emp>\cadmat`, e a armadilha M8 do contrato medido é
literal: *"consultar sem o prefixo não dá erro — dá o resultado errado"*.
Confirmado aqui por contagem: `cadmat` tem 31.555 registros em `fenwin02` e 22.499
em `fenwin03`; a cópia da raiz tem 25.264 e **não é nenhuma das duas**.

---

## 4. Mapa de ESCRITA

### 4.1 A ordem obrigatória

A sequência-mestra é `fechapedwer.scx :: wtotpg.KeyPress` (dump 1021-1236), com o
número do pedido já reservado antes, na abertura do formulário. Lida linha a linha:

| # | Passo | Onde | Tabelas escritas | Interativo? |
|---|---|---|---|---|
| **0** | `acumula_documento` — reserva o número | `ped_wer.scx` dump 2205-2274, chamado em **dump 2521** | **`wareas.cont_ped`** (`REPLACE` + `TABLEUPDATE`) | não, mas ocorre **na abertura da tela** |
| **0b** | `totaliza` — **reserva de estoque, a cada item digitado** | `ped_wer.scx` dump 366-381 | **`cadmat.qtd_pedida`** (saída) ou **`cadmat.qtd_fpedid`** + `ipi`, `ipi_fabr`, `val_icm_e` (entrada), com `TABLEUPDATE()` | não, mas ocorre **item por item, durante a digitação** |
| 1 | `delecao` — só se for alteração (`wrespalt="A"`) | `fechapedwer` dump 113-214 | **`DELETE` em `pedido`**, e depois em **`artficha`** (saída) ou **`apagar`** + **`compras`** (entrada) | não |
| 2 | `patualiz` | dump 1041 | recalcula; não grava tabela de pedido | não |
| 3 | `do form totnotaf` | dump 1086 | — | **sim** (despesas) |
| 4 | **`pcondpg2`** | `pcondpg2.prg:150-357` | **`pedido`: `APPEND BLANK` + ~60 `REPLACE` por item**; **`clientes.dt_ult_ven`** (`:172`); `gravalog` (`:368`); `DO ptotnota` (`:382`) | não |
| 5 | `grava_cademp` — só se `empresa_mult` existir | dump 260-294 | `deleta_cademp` → **`DELETE` em `pedemp`**, depois **`INSERT INTO pedemp`** | não |
| 6 | `empenho` | dump 315-330 | **`pedhead`: `APPEND BLANK`**, `do form empenho`, `REPLACE cod_pedido` | **sim** (form no meio da escrita) |
| 7 | `do form totnotap` | dump 1147 | — | **sim** |
| 8 | `dbseek` em `pedido` (tag `pedido7`) | dump 1153-1158 | posiciona | não |
| 9 | **`fecha_nota`** | dump 221-258 | **`pedido`**: `total_icm`, `total_nota`, `total_desc`, `icms_ret`, `val_custo`, `val_finan`, `incide_*`, `incidsubst`, `obs_nf`, `cod_trans`, `transp_nf` | não |
| 10 | `mod_ped` / `mod_com` | dump 1198-1211 | impressão | **sim** |

Fora dessa sequência, o mesmo formulário ainda grava `wareas.cont_ped` em dois
outros pontos: `desmPED` seguido de `replace cont_ped with wcontnot` (dump
1387-1400), e `subtrai_documento` decrementa o contador (`ped_wer.scx` dump
2275-2285, chamado em dump 3705 e 3762). `acertanumped` (dump 2286-2315) o devolve
quando o número foi consumido e não usado.

### 4.2 Efeitos colaterais fora das tabelas do pedido

Seis, e nenhum é opcional:

| Efeito | Tabela | Onde | Natureza |
|---|---|---|---|
| Contador de documento | **`wareas.cont_ped`** | `ped_wer` dump 2246/2264 | registro **único e global**; ver §6 |
| **Reserva de estoque** | **`cadmat.qtd_pedida`** / **`qtd_fpedid`** | `ped_wer` dump 371/378 (`totaliza`), sob `IF tipos.ind_qtd="S"` | tabela **por empresa**; escrita **durante a digitação**, não na gravação. Ver Janela 4 |
| Data da última venda | **`clientes.dt_ult_ven`** | `pcondpg2.prg:170-174` | `REPLACE date()` na tabela **única** de clientes, sob `IF tipos.tipo_es="S"` |
| **Zeragem do limite de crédito** | **`clientes.credito`** | `valt2w.prg:342-345` | `REPLACE credito WITH 0` **no registro da matriz** quando `csll = 0`. É consumo de limite, não leitura |
| Log de auditoria | `fenlog` (248.805 regs) | `pcondpg2.prg:368` | `gravalog` com `wfuncao="PEDIDO"` |
| Multiempresa | `pedemp` | `grava_cademp` | `DELETE` + `INSERT` |

O terceiro é o mais delicado do ponto de vista de contrato de API: **uma operação
de "validar pedido" grava**. Um endpoint `GET /credito` que replicasse `valt2w`
fielmente mutaria o cadastro. A separação entre consulta e consumo do limite não
existe no código atual e **tem de ser criada no desenho web** — é decisão de
negócio, não de tradução.

### 4.3 O que está morto nesta instalação — redução de escopo medida

`FATO MEDIDO` — Quatro ramos do caminho de escrita **não executam** com a
configuração atual do cliente. Isso reduz o escopo real da migração e deve ser
reconferido na instalação viva antes de se apoiar nele:

| Ramo | Gate | Valor medido | Consequência |
|---|---|---|---|
| `empenho` → **`pedhead`** | `tipos.IND_EMPENH = "S"` | **0 de 487 tipos** | `pedhead` **sai do mapa de escrita**. Seus 62 registros são lixo de teste (`PRAZO_VALI = "ASASASASASAS"`, `OBSERVACAO = "DFDFDFDFDFDFDFD"`) |
| Alçada por família → `Familia`, `Pedcomp`, `GrupoAlc` | `wareasb.Limite_Ped > 0` | **em branco** | `pcondpg2.prg:87-146` não roda; `GrupoAlc` e `percrat` têm **0 registros**; `pedido.Posicao` recebe sempre `Tipos.Posicao` |
| Reposicionamento por grupo de formulário | `tipos.IND_GRUPO = "X"` | **0 de 487** | `pcondpg2.prg:187-195` e `:337-343` mortos |
| Prazo por diferença de data | `wareascp.ind_prazo = "S"` | **em branco** | `pedido.prazo` recebe o prazo bruto (`:204`), não a diferença |

Complementarmente: `wareas.ind_ver_fi` está **em branco** (não `"N"`), logo o bloco
de crédito de `valt2w` **roda**; `wareasb.indcredito` está **em branco**, logo a
segunda checagem de `valida6wer:441` **também roda**; e os **5 registros de
`vencimr` têm todos `ind_credit = "S"`**, de modo que **nenhuma condição de
pagamento desliga a verificação de crédito** neste cliente.

### 4.4 Onde falta atomicidade — e o que exatamente se perde

A análise antecedente (§7.3) registra "7 tabelas sem transação". A leitura linha a
linha mostra que o problema não é o número de tabelas: é **onde os intervalos
caem**. Quatro janelas, em ordem de gravidade:

**Janela 1 — a alteração destrói antes de reconstruir. `CONFIRMADO` por leitura.**
`delecao` (passo 1) faz `DELETE` + `TABLEUPDATE()` em laço sobre `pedido` e, em
seguida, sobre `artficha` (ou `apagar` + `compras`). Só depois, no passo 4, o
`pcondpg2` reinsere. Entre os dois passos existem **três formulários
interativos** (`totnotaf` no passo 3 já está dentro do intervalo). Interrupção
nesse intervalo — queda de rede, `QUIT` do `erro_global.PRG`, o operador matando o
processo — deixa **o pedido apagado e o título financeiro apagado, sem nada a
reverter**. Não há `TRY`/`CATCH` em nenhuma das peças (§6.4 R1 da análise: zero em
`ped327wer`, `est326wer`, `pcondpg2` e `valt2w`).

**Janela 2 — o pedido existe com total errado durante intervenção humana.**
O passo 4 grava `pedido.total_nota` com `wtotger2`, o total **antes** das despesas
financeiras, do seguro e do ajuste de ICMS. O valor definitivo só é gravado no
passo 9, por `fecha_nota`. Entre 4 e 9 estão o `empenho` (passo 6) e o `totnotap`
(passo 7) — **dois formulários que esperam uma pessoa**. Nesse intervalo, que dura
o tempo que o operador levar, as linhas do pedido estão **visíveis e consultáveis
por qualquer outra sessão** com um total que não é o total.

**Janela 3 — o número é consumido muito antes de existir registro.**
`acumula_documento` roda na **abertura do formulário** (dump 2519-2532), portanto
antes de o operador digitar o primeiro item. O `REPLACE cont_ped` + `TABLEUPDATE`
é imediato. A primeira linha em `pedido` aparece no passo 4. A janela é o tempo
inteiro de digitação de um pedido. Ver §6.

**Janela 4 — a reserva de estoque acontece antes do pedido existir, e a
compensação depende da sessão sobreviver. `CONFIRMADO` por leitura.**
Este é o efeito colateral mais fora de lugar de todo o caminho, e não aparece na
análise antecedente. `ped_wer.scx :: totaliza` (dump 366-381) faz, a cada item
digitado:

```foxpro
IF tipos->ind_qtd = "S"
    Sele cadmat
    If tipos->tipo_es="S"
        Replace qtd_pedida With qtd_pedida+wquant(wcnvar)
    Else
        Replace qtd_fpedid With qtd_fpedid+wquant(wcnvar)
    Endif
    =Tableupdate()
ENDIF
```

`FATO MEDIDO` — O gate está ligado para o fluxo em questão: o tipo `PED`
(`wareas.TP_PEDIDO`) tem `IND_QTD = "S"` e `TIPO_ES = "S"`. Portanto **todo item
digitado incrementa `cadmat.qtd_pedida` imediatamente, com `TABLEUPDATE()`, na
tabela `cadmat` da empresa corrente — antes de qualquer linha existir em
`pedido`.**

Três consequências:

1. **A reserva não tem vínculo com pedido nenhum.** É um acumulador. Nada em
   `cadmat` diz quem reservou, nem quando.
2. **A compensação é código de tela.** Os decrementos vivem nos métodos `delecao`
   (dump ~663-665, `qtd_pedida - pedido.qtd_itens`) e `altera` (dump ~694-696,
   `- wwqtd`) do **mesmo formulário**. Se a sessão morrer entre a digitação e a
   gravação — e o `erro_global.PRG` responde a erro não previsto com `QUIT` —
   **a reserva fica, e nada a desfaz**.
3. **Existe um mecanismo compensatório, e ele é o CheckUp.** `est106.PRG` e
   `checkup.prg` **zeram `qtd_pedida`/`qtd_fpedid` e os reconstroem** varrendo
   `pedido` (`est106.PRG:471/473/605/615`, `checkup.prg:349/351/475`). Ou seja:
   a inconsistência é conhecida e tratada por reprocessamento em lote, não
   prevenida.

Para o desenho web isso é decisivo: **a reserva de estoque não pode continuar
sendo efeito de digitar.** Numa aplicação web, digitar não é uma transação — o
usuário fecha a aba e não há evento de saída confiável. Se a reserva permanecer
no ato de digitar, cada aba abandonada infla o comprometido de um produto, e a
única correção continua sendo o CheckUp. A alternativa é reservar **no momento da
gravação, dentro da fronteira da §4.5** — o que muda o comportamento visível
(dois vendedores podem comprometer o mesmo saldo enquanto digitam) e é, portanto,
**decisão de negócio, não de implementação**.

### 4.5 A menor fronteira transacional segura

A pergunta admite resposta precisa, e ela é mais estreita do que "o pedido inteiro".

**Do lado da gravação no ERP, a menor fronteira é o conjunto dos passos 1, 4, 5 e
9** — `delecao` + `pcondpg2` + `grava_cademp` + `fecha_nota` — com **todas as
etapas interativas expulsas para fora dela**.

O raciocínio, e é o que sustenta a resposta:

- **Os passos 3, 6, 7 e 10 não podem estar dentro.** São formulários. Uma
  transação que espera decisão humana mantém bloqueio por minutos, e em DBF
  compartilhado isso é indistinguível de travar a tabela para a empresa toda.
  Numa aplicação web eles deixam de existir como interrupção: os dados que hoje
  coletam (despesas, totais, empenho) passam a ser **campos do payload**, decididos
  antes do envio.
- **O passo 9 tem de estar dentro, junto com o 4.** Separá-los é exatamente a
  Janela 2. `total_nota` correto e as linhas do pedido são o mesmo fato; publicar um
  sem o outro é publicar número errado.
- **O passo 1 tem de estar dentro, junto com o 4.** Separá-los é a Janela 1.
  Alteração é `DELETE`+`INSERT`, e as duas metades são uma só operação.
- **O passo 0 fica FORA, e antes.** A reserva de número é uma operação de
  contador, com regime de concorrência próprio (§6). Colocá-la dentro da mesma
  fronteira serializaria todos os lançamentos da instalação pelo tempo da
  transação inteira.
- **A reserva de estoque (`cadmat.qtd_pedida`) tem de ENTRAR na fronteira** — e
  hoje está a minutos de distância dela (Janela 4). Ela é a única escrita do
  conjunto que recai numa tabela **por empresa**, o que significa que a fronteira
  atravessa dois arquivos físicos (`pedido` na raiz, `cadmat` em `fenwin<emp>\`).
  Se ela ficar fora, o comprometido de estoque volta a depender do CheckUp para
  ficar correto.
- **A gravação de `clientes.dt_ult_ven` e o `gravalog` ficam FORA, e depois.** São
  efeitos de auditoria; falhar neles não deve desfazer um pedido válido. Hoje estão
  dentro do laço do `pcondpg2` por acidente de escrita, não por requisito.
- **A zeragem de `clientes.credito` (§4.2) é um caso próprio.** No código atual ela
  acontece na **baixa** (`valt2w`), não no lançamento. Ela deve permanecer na
  fronteira da baixa, e o desenho web do lançamento não deve tocá-la — sob risco de
  consumir limite duas vezes.

Em uma frase: **`{delecao ∪ inserção de itens ∪ pedemp ∪ totais ∪ reserva de
estoque}` é atômico; a reserva de número vem antes; auditoria e data de última
venda vêm depois.**

São **cinco** tabelas dentro da fronteira (`pedido`, `pedemp`, `cadmat`, e
`artficha` **ou** `apagar`+`compras` no caminho de alteração) — não sete. Duas das
sete que a análise antecedente conta (`pedhead` e `clientes.dt_ult_ven`) saem: a
primeira por estar inativa nesta instalação (§4.3), a segunda por ser auditoria.

`HIPÓTESE` — Essa fronteira é implementável em VFP com `BEGIN TRANSACTION` /
`END TRANSACTION`, porque as quatro tabelas envolvidas (`pedido`, `pedemp`,
`artficha`/`apagar`/`compras`) são **tabelas de container `.dbc`**, e o
transacionamento do VFP funciona sobre tabelas de banco de dados, não sobre tabelas
livres. Isto **não foi verificado** — depende de as tabelas estarem realmente
vinculadas ao `PEDIDO.DBC`/`FENICIA.DBC` e não abertas como livres. Verificação:
`USE pedido.dbc` e inspecionar `objecttype='Table'`. Se falhar, a fronteira tem de
ser obtida por reserva-e-confirmação em duas fases, não por transação.

---

## 5. API direta × integrador

### 5.1 Definição dos dois termos, para que a comparação signifique algo

- **API direta**: a aplicação web fala com a WebAPI Fenícia 2 existente
  (`/table/List2`), lê o que precisa, e — se houver escrita — grava por ela.
- **Integrador**: um processo próprio, escrito para este fim, que roda **do lado do
  ERP** (na máquina que já tem a árvore VFP e o acesso ao compartilhamento) e expõe
  operações de negócio à web. A escrita acontece em VFP, pelo mesmo caminho que o
  ERP usa.

### 5.2 A comparação

| Critério | API direta (`/table/List2`) | Integrador do lado do ERP |
|---|---|---|
| **Capacidade de leitura** | Provada em produção. Contrato medido em 11/08/2026, 10 achados. | Igual ou melhor: lê DBF diretamente, sem HTTP no meio |
| **Capacidade de escrita** | **`NÃO VERIFICADO`** (§1). Se existir, é por SQL cru — ver §5.3 | **Certa.** É o único caminho que reusa o código que já sabe gravar pedido |
| **Atomicidade** | **Nenhuma.** Não há como abrir transação por HTTP sem estado; cada requisição é isolada. A fronteira da §4.5 **é inexprimível** | **Possível.** `BEGIN/END TRANSACTION` no processo VFP, ou reserva-e-confirmação (`HIPÓTESE`, §4.5) |
| **Armadilha 1** (chave ausente no JSON) | Atinge em cheio. Todo campo tem de ser lido com tolerância a ausência. `exactOptionalPropertyTypes` no TS existe por isso | Não se aplica na escrita; o integrador define seu próprio contrato |
| **Armadilha 2** (>1.800 caracteres → 404) | **Atinge no ponto mais perigoso**: a consulta de crédito por grupo econômico (105 clientes no maior grupo) estoura o limite, e 404 é indistinguível de "sem débito" — o lado que **libera** o crédito | Não se aplica: o integrador faz o `SCAN` local |
| **Armadilha 3** (reexecuta a consulta a cada página) | Custo O(páginas × consulta). Mitigável com página de 2.000, medido | Não se aplica |
| **Armadilha 4** (OOM; teto cai com o uso) | **É o risco de disponibilidade principal.** Uma tela de pedido que dependa de consulta grande funciona de manhã e falha à tarde. Custo é colunas × linhas | Não se aplica |
| **Desempenho de leitura** | M9 medido: 3 linhas de `cadmov` = **25,5 s**; `artficha` filtrado = 4,7 s. A API **varre a tabela** antes de paginar | `dbseek` por índice — a ordem de grandeza do ERP hoje |
| **Esforço inicial** | **Baixo.** O cliente HTTP, a resiliência, a conversão e a autenticação já existem em produção (§8) | **Alto.** Processo novo, publicação nova, disciplina de erro nova, e código VFP a escrever |
| **Acoplamento ao VFP** | **Baixo em aparência, alto em substância.** Não há dependência de binário, mas cada consulta depende do nome físico da tabela, do prefixo de empresa, dos índices e da semântica dos 6 campos da §7 | **Alto e explícito.** Depende da árvore, do rebuild e da cadeia de procedures — mas o acoplamento fica **num lugar só**, versionado |
| **ERP em manutenção / rebuild** | A leitura **continua funcionando** (a WebAPI lê DBF; não depende do `estoque.exe`). O que quebra é a escrita, se ela existir por lá. O portal atual já tem resposta a isso: motivo `ERP_INDISPONIVEL`, 503 e não 401, e hash local de 30 dias | **Para.** O integrador compartilha a árvore e a cadeia de procedures; durante rebuild ou leva, ele está sujeito ao mesmo estado intermediário que o ERP. Exige janela de manutenção declarada e uma fila que não perca pedido |
| **Reprodutibilidade do build** | Não se aplica | ⚠ Herda o problema já medido: o `pedwer` foi buildado de **três árvores**, duas delas pastas pessoais (`z:\marcio\base`, `admcandido\temp`) — §6.1 da análise |
| **Quem opera o incidente** | Time web. O ERP é caixa fechada | Precisa de quem conheça VFP e a árvore do Z: |

### 5.3 O caminho que a comparação exclui

Se a resposta ao item 1 da §1.3 for "sim, dá para mandar `INSERT` no cabeçalho
`Tabela`", isso **não constitui a opção "API direta com escrita"**. Um `INSERT`
solto pela WebAPI:

- **não passa pelas ~60 regras** que o `pcondpg2` aplica campo a campo, nem pelos
  totais de `fecha_nota`, nem pela alçada, nem pela verificação de crédito;
- **não tem transação**, logo não atende a §4.5;
- **não reserva número**, logo cai direto no problema da §6;
- e transporta SQL arbitrário sobre a mesma credencial que hoje é *"de consulta,
  somente leitura"* (`DOC_NORTEADOR_PROJETOS.md` §3.5).

Registrado explicitamente para que a descoberta de que "é tecnicamente possível"
não seja lida como "é uma opção".

### 5.4 A combinação, que a pergunta não pede mas a medição sugere

`HIPÓTESE` — Os critérios não se movem juntos. Leitura tem contrato provado pela
API direta; escrita não tem contrato nenhum por lá. Uma divisão em que **a leitura
usa a WebAPI e a escrita usa um integrador** herda o esforço baixo de um lado e a
atomicidade do outro, ao custo de manter duas fronteiras. Isso é apresentado como
terceira coluna possível, **não como recomendação** — a decisão depende das
informações listadas na §9.

---

## 6. Concorrência e integridade

### 6.1 Como a numeração funciona hoje, lida linha a linha

`ped_wer.scx :: acumula_documento` (dump 2205-2274):

```
wcontnot = wareas.cont_ped                     && lê o contador global
wchave   = LEFT(alltrim(wareas.comp_seq)+wcontnot,10)
dbseek("codigo", wchave, "M0", "pedido")       && o número já existe?
DO WHILE .NOT. EOF()                           && enquanto existir, avança
    wwcontnota = ltrim(str(val(wwcontnota)+1))
    dbseek("codigo", alltrim(comp_seq)+wwcontnota, "M0","pedido")
ENDDO
SELE wareas
=CURSORSETPROP('Buffering', 5, 'wareas')       && bufferização OTIMISTA de tabela
wdocum = LEFT(alltrim(comp_seq)+wwcontnota,10)
REPLA cont_ped WITH ltrim(str(val(wwcontnota)+1))
=tableupdate()                                 && retorno DESCARTADO
```

Quatro fatos medidos sobre isso:

1. `FATO MEDIDO` — **`wareas` tem 1 registro, na raiz.** O contador é **global a
   todas as empresas**, e `pedido` também é tabela única. Não há espaço de numeração
   por empresa.
2. `FATO MEDIDO` — Valores atuais: `CONT_PED = "39"`, `CONT_COM = "192"`,
   `COMP_SEQ = "  "` (vazio), `IND_PED_DC = "S"` (numeração automática ligada).
3. `FATO MEDIDO` — **O retorno de `TABLEUPDATE()` é descartado.** Sob bufferização
   otimista (modo 5), um conflito de gravação **faz `TABLEUPDATE` devolver `.F.`**,
   e ninguém lê o valor. Uma colisão real é, portanto, **silenciosa**.
4. `FATO MEDIDO` — **Não há `RLOCK()` em nenhum ponto do caminho.** Varredura de
   `ped_wer.scx`, `pcondpg2.prg`, `valt2w.prg` e `fechapedwer.scx`.

### 6.2 A descoberta que muda o desenho: o contador quase não é usado

`FATO MEDIDO` — Varredura dos 102.104 registros de `Fox\WER\pedido.dbf`, campo
`CODIGO C(10)`:

| Grandeza | Valor |
|---|---:|
| `CODIGO` distintos | **35.499** |
| Registros com `CODIGO` puramente numérico | **1** (`"38"`, `tipo_oper="PED"`, `cod_cli="1"`, 11/07/2026) |
| Registros com `CODIGO` alfanumérico | **102.103** |
| `CODIGO` com mais de uma combinação `cliente|empresa|tipo` | **0** |

Os padrões reais são `FB2651A`, `AB17436A`, `AT899A`, `FB9999P` — duas letras,
número, letra final (`A` ou `P`). Os formatos mais frequentes, medidos por
substituição de dígito por `9`:

| Formato | Registros |
|---|---:|
| `FB9999A` | 14.855 |
| `FB99999A` | 10.423 |
| `FT9999A` | 8.984 |
| `AB99999A` | 6.635 |
| `BA99999A` | 6.027 |
| `JD9999A` | 3.508 |

Os prefixos de duas letras observados incluem `FB`, `FT`, `AB`, `BA`, `JD`, `AT`,
`HB`, `FA`, `JM`, `TG`, `TA`, `LT`, `DB` e `CM`; a letra final é `A` ou `P`.

Como `comp_seq` está **vazio**, o número que `acumula_documento` produz é
exatamente `cont_ped` — puramente numérico. Nenhum dos 102.103 registros reais tem
essa forma. O único que tem é o de 11/07/2026, coerente com `cont_ped = 39` = 38+1.

**Conclusão (`HIPÓTESE` forte, sustentada pelos números):** nesta instalação o
número do pedido é **digitado pelo operador**, não gerado. O campo é editável
(`THIS.WCdocument.ENABLED = .T.`, dump 2510) e o auto-número entra apenas como
valor sugerido. A existência de `acertanumped` — que devolve o contador quando o
número sugerido não foi consumido — só faz sentido nesse regime. O registro `"38"`
é o rastro de alguém que abriu a tela e aceitou a sugestão.

**Impacto no desenho web, e é grande:** replicar `cont_ped` reproduziria um
mecanismo que a operação não usa, e produziria números que colidem com o espaço
alfanumérico vigente. O que o desenho precisa saber é **qual é a regra real dos
prefixos** — se é por vendedor, por filial, por tipo de operação ou por talão de
papel. Isso é `NÃO VERIFICADO`, e a resposta está com a operação, não no código.

### 6.3 Dois vendedores web simultâneos — os três cenários

Assumindo, para o pior caso, que o contador **seja** adotado no desenho web:

**Cenário A — abertura simultânea. Colisão estreita e silenciosa.**
Duas sessões leem `cont_ped = N` no mesmo instante, ambas fazem o `dbseek` (N não
existe em `pedido`), ambas adotam N, ambas gravam N+1. A segunda `TABLEUPDATE`
falha ou sobrescreve, e o valor de retorno é descartado. Duas sessões com o mesmo
número. Janela: milissegundos.

**Cenário B — abandono no meio. Colisão larga e reproduzível.**
A abre e recebe N (contador → N+1). B abre e recebe N+1 (contador → N+2). A
abandona; `subtrai_documento` faz `cont_ped = N+1`. C abre, lê N+1, faz `dbseek` em
`pedido` por N+1 — **B ainda não gravou nada**, logo não existe — e C adota N+1.
**C e B saem com o mesmo número.** Janela: o tempo de digitação de um pedido, isto
é, minutos. Este é o cenário que justifica a existência do laço de colisão, e o
laço **não o resolve**, porque o laço consulta `pedido`, e a reserva de B não está
em `pedido`.

**Cenário C — o operador digita o número.** É o regime real (§6.2). Duas sessões
podem digitar o mesmo número sem que nada as impeça: não há chave candidata
conhecida em `pedido.codigo` (`NÃO VERIFICADO`; verificação: `USE pedido.dbc` e
inspecionar as definições de índice, onde chave candidata é a única forma de o VFP
recusar duplicata — tag "unique" apenas oculta repetição, não a impede).

`FATO MEDIDO` — Nos 102.104 registros da base, **nenhum `CODIGO` aparece com mais
de uma combinação `cliente|empresa|tipo`**. Não há evidência de colisão consumada
nesta base. Isso é consistente com números vindos de talões distintos por prefixo, e
**não** prova que o mecanismo seja seguro — prova que o regime atual (poucos
digitadores, prefixos separados) não o exercita.

### 6.4 Bloqueio de registro em DBF compartilhado

Os fatos, sem rodeio:

- **DBF não tem transação multi-tabela.** O `BEGIN TRANSACTION` do VFP existe, mas
  só cobre tabelas de container `.dbc` e só dentro de **um processo VFP**. Não
  atravessa HTTP.
- **O caminho atual não bloqueia nada.** Zero `RLOCK`, zero `FLOCK`, e o único
  mecanismo de detecção — o retorno de `TABLEUPDATE()` sob bufferização otimista —
  tem seu valor **descartado** em todos os pontos lidos.
- **`wareas` é o gargalo estrutural.** Um registro único, global, escrito por todo
  lançamento. Qualquer serialização séria da numeração passa por ele.

### 6.5 Como o desenho web sobrevive a isso

Três decisões, apresentadas como o que a medição sustenta, não como escolha feita:

**(1) A numeração sai do DBF.** O contador não é seguro sob concorrência e, nesta
instalação, não é o mecanismo em uso. A saída que o desenho web tem à mão é uma
**sequência com garantia de unicidade fora do VFP** — o Postgres do portal, que o
`DOC_NORTEADOR_PROJETOS.md` §6 já prevê como opcional, tem `SEQUENCE` e chave
única. O número passa a ser emitido por quem sabe garantir unicidade, e o VFP
recebe um número já decidido. **Isto depende da regra real dos prefixos (§6.2), que
é `NÃO VERIFICADO`.**

**(2) A escrita é idempotente por chave de idempotência do cliente.** A janela do
Cenário B existe porque a reserva não é observável. Se a requisição de gravação
carregar um identificador único gerado pelo navegador, e o integrador recusar a
segunda gravação com a mesma chave, o reenvio por timeout — que numa rede real
acontece — deixa de duplicar pedido. Esta é a proteção que substitui a transação
distribuída que não existe, e é o precedente do portal: em `RL2671` o
`409` foi o que **protegeu** contra duplicidade, e a suspeita de que ele
causava duplicidade foi retratada.

**(3) Nada é gravado até que tudo esteja decidido.** As três janelas da §4.4
existem porque a escrita começa antes de a decisão terminar. Numa aplicação web
isso é evitável de graça: o pedido é montado inteiro no cliente e no servidor web,
validado inteiro, e só então enviado numa operação. Os quatro formulários
interativos do caminho atual (`totnotaf`, `empenho`, `totnotap`, `mod_ped`) deixam
de ser interrupções e passam a ser campos do payload.

Isso resolve as Janelas 1, 2 e 4 de uma vez: se nada é gravado antes de tudo estar
decidido, não existe intervalo em que o pedido esteja apagado, nem em que o total
esteja errado, nem em que a reserva de estoque exista sem pedido. **A Janela 3
permanece**, porque a numeração é um recurso disputado independentemente de quando
se grava — e é o que a decisão (1) endereça.

⚠ **O que nenhuma dessas três resolve:** enquanto o ERP em VFP continuar lançando
pedido pela tela antiga, os dois caminhos disputam o mesmo espaço de numeração e a
mesma tabela `pedido`, e o web não tem visibilidade da reserva feita pelo VFP. Ou a
tela antiga é desativada, ou o VFP passa a pedir número à mesma sequência. Não há
terceira via.

---

## 7. Os seis campos com significado sobreposto — forma do contrato

### 7.1 O problema, em uma frase

`clientes.INSS`, `IRRF`, `ISS`, `PIS`, `COFINS` e `CSLL` — todos `N(10,2)`
(`FATO MEDIDO`) — têm significado **comercial** neste cliente e **fiscal** em
outros, e **não há discriminador no dado** (§4.5 da análise). A única coisa que
distingue as duas leituras é o nome do programa que abriu a tela.

`FATO MEDIDO` — Estado real desses campos em `Fox\WER\clientes.dbf` (13.086
registros), com `InvariantCulture`:

| Campo físico | Significado no WER | Não-zero | Observação medida |
|---|---|---:|---|
| `INSS` | Percentual de Adição (majora o preço) | **105** | |
| `IRRF` | Compensação de ICMS (reduz) | **27** | |
| `ISS` | Desconto Diretoria (reduz) | **70** | |
| `PIS` | Desconto Especial (teto de desconto) | **151** | |
| `COFINS` | Dias de tolerância de atraso | **12.487** | 12.450 valem exatamente `1.00`; 591 em branco; 8 zero; máximo `99.00`. **É `N(10,2)`, e `valt2w.prg:294` o soma a uma data** |
| `CSLL` | Regime de limite: 1=Constante, 0=Não Constante | 577 (=1) | **10.140 = 0; 2.368 EM BRANCO; 1 = `11.00`** |

Dois achados novos aqui, ambos relevantes ao contrato:

- **2.368 clientes (18,1%) têm o regime de crédito EM BRANCO** — nem 0 nem 1. O
  VFP lê numérico em branco como zero, então na prática são "Não Constante"; mas o
  dado não afirma isso. É o caso exato da regra da casa **"`null` não é zero"** —
  "R$ 0,00 é uma afirmação". Um contrato que devolva `regimeLimite: "NAO_CONSTANTE"`
  para esses 2.368 registros **inventa uma afirmação que o cadastro não faz**.
- **1 cliente tem `CSLL = 11.00`**, valor incompatível com um par de radio-buttons e
  compatível com uma alíquota de CSLL. É a ambiguidade se materializando em um
  registro.

### 7.2 A forma do contrato que não propaga a ambiguidade

Quatro regras, e as quatro precisam valer juntas.

**Regra 1 — o campo físico nunca atravessa a fronteira.**
O contrato expõe nome de domínio, e a tradução fica confinada num único módulo de
acesso a dados. Concretamente:

| Contrato de API | Campo físico | Tipo no contrato |
|---|---|---|
| `percentualAdicao` | `clientes.INSS` | `number \| null` |
| `compensacaoIcms` | `clientes.IRRF` | `number \| null` |
| `descontoDiretoria` | `clientes.ISS` | `number \| null` |
| `descontoEspecialTeto` | `clientes.PIS` | `number \| null` |
| `diasToleranciaAtraso` | `clientes.COFINS` | `number \| null` (inteiro; ver Regra 4) |
| `regimeLimite` | `clientes.CSLL` | `'CONSTANTE' \| 'NAO_CONSTANTE' \| null` |

**Regra 2 — o mapeamento é declarado por instalação, e o serviço recusa subir sem
ele.** O mesmo campo físico significa coisas diferentes em instalações diferentes;
portanto o mapeamento **não é constante de código**, é configuração. O padrão da
casa já tem a forma certa: o servidor *"recusa subir com variável faltando,
nomeando todas de uma vez"* (`DOC_NORTEADOR_PROJETOS.md` §15). Um cliente que use
`INSS` como INSS de verdade declara o perfil fiscal, e as leituras comerciais
**não existem** naquele contrato — não vêm com valor errado, não vêm.

**Regra 3 — em branco sai como `null`, sempre, e o consumidor tem de tratar.**
Os 2.368 registros com `CSLL` em branco saem `regimeLimite: null`. A tela mostra um
travessão e a regra de negócio **recusa lançar** até que alguém decida, em vez de
assumir o regime mais permissivo — que é exatamente o que a gravação por omissão de
D14 faz hoje. Isso vale com mais força porque, até 08/04/2026, os radios **não
clicavam** (§5 da análise): o cadastro em branco não é escolha do operador, é
sintoma de defeito de interface.

**Regra 4 — o contrato tipa o que o campo físico não tipa.**
`COFINS` é `N(10,2)` e é somado a uma data (`valt2w.prg:294`). "1,5 dias de
tolerância" não tem significado. O contrato declara `diasToleranciaAtraso` como
inteiro e **rejeita valor fracionário na leitura**, com aviso — em vez de propagar
para dentro do cálculo uma aritmética de data com casas decimais.

### 7.3 O mesmo vale para os campos polivalentes de `pedido`

A análise (§4.2) já registra `qtd_comp` (percentual de comissão **ou** medida
física) e `qtd_larg` (medida física **ou** preço de tabela ajustado, com o guard
`*If wareascp.ind_largco="S"` **comentado**). Ambos estão dormentes por
configuração — `wareascp.IND_LARGCO` medido em branco, `wareas.IND_GRADE = "N"`.

Para o contrato de escrita isso significa: **o payload de item não tem campo
`qtdComp`.** Ele tem `percentualComissao` e, se um dia houver, `medidaLargura`. A
camada de acesso decide em qual campo físico cada um cai. Um schema novo que
herde o campo polivalente herda a colisão junto, e ela deixa de ser dormente no
dia em que a configuração mudar.

---

## 8. O que se reaproveita do Portal de Comissão

O portal está **em produção** desde 14/08/2026, com 513 testes, autenticando contra
o mesmo ERP. É de onde se copia. Inventário do que serve a este projeto:

### 8.1 Reaproveitamento direto — copiar o arquivo

| Peça | Arquivo | O que resolve |
|---|---|---|
| **Cliente da WebAPI** | `portalcomissao\src\erp\cliente.ts` | Envelope `table`, cabeçalho `page`, recusa local acima de 1.800 caracteres, leitura do corpo do 400 pelo **fim** (onde mora a exceção), teto de páginas |
| **Resiliência** | `src\erp\resiliencia.ts` | O que repetir e o que não: 5xx sim; **400 nunca** (é OOM ou consulta grande); 404 não (ambíguo); 401/403 não. Timeout 45 s, 3 tentativas, retentativa **por página** |
| **Conversão de tipos** | `src\erp\conversao.ts` | Vírgula decimal, ponto **recusado**, data DIA/MÊS/ANO, `DTOC()` proibido (inverte dia e mês) |
| **Lotes por orçamento de caracteres** | `src\erp\conjuntos.ts` | Exatamente o que a consulta de crédito por grupo econômico precisa (§3.3a) |
| **Autenticação** | `src\auth\cifra.ts`, `credencial.ts`, `sessao.ts`, `lembranca.ts` | Cifra do `caduser` (CP1252 completo, os dois ramos do `Len`), motivo `ERP_INDISPONIVEL` separado de senha errada, cookie autocontido HMAC de 12 h, hash local de 30 dias para ERP fora do ar |
| **Limitador de tentativas** | `src\api\autenticacao.ts` | 10 tentativas / 5 min / espera 60 s, chaveado por **usuário E origem**; legado fora do ar não conta como tentativa e responde **503** |
| **Guardas de arquitetura** | `tests\guardas-arquitetura.test.ts` | Rota `/api` **nasce protegida** (lista de exceções, não de protegidas); front não importa a cifra; nenhum parâmetro de URL concede sessão |
| **Legado falso para testes** | `tests\ajuda-servidor.ts` | Função `(sql) => Promise<Linha[]>` roteando por regex sobre o próprio SQL. **Reproduz os defeitos da API**, inclusive omitir a chave quando o valor é vazio |
| **Publicação** | `Dockerfile`, `docs\PUBLICAR.md` | Dois builds numa imagem, `USER node`, `npm ci --omit=dev`, e **`tzdata` + `TZ`** (as duas linhas; a segunda sozinha falha em silêncio) |

`FATO MEDIDO` — O código de produção do portal **não tem nada da Versátil
cravado** (`DOC_NORTEADOR_PROJETOS.md` §3.5). Trocar de cliente é trocar
configuração. ⚠ **Uma exceção**: `src\cli\sondar-erp.ts:23` tem a URL da Versátil
como valor **padrão** — rodar a ferramenta sem `.env` bate na produção da
Versátil. Ao clonar, esse padrão tem de ser removido e a ferramenta tem de falhar
sem `ERP_URL`.

### 8.2 Reaproveitamento conceitual — o padrão, não o arquivo

- **Motor de cálculo isolado, testado, sem HTTP e sem banco** (`src\motor\`). É
  para lá que vai a regra de preço/desconto/comissão do WER — e é o mesmo lugar que
  a §7.1 da análise aponta como pré-requisito.
- **Transporte, nunca recálculo.** A tela mostra o número que o motor produziu.
  No portal, recalcular na tela perdeu o sinal de uma devolução e o centavo do maior
  resto (`CONTINUIDADE.md` §5.6, pendência 11).
- **`null` não é zero**, com contaminação na soma (`src\api\ocultar.ts`). Aplica-se
  literalmente aos 2.368 `CSLL` em branco da §7.1.
- **Nenhuma tela busca dado por conta própria** — um provedor, `hooks` derivados.
  Duas telas que mostram números diferentes do mesmo conceito é defeito, e
  `tests\telas-nao-divergem.test.ts` é a guarda.
- **Modo demonstração desde o dia 1**, com tarja, respeitando o mesmo recorte por
  perfil dos dados reais, e **sem conceder acesso**.

### 8.3 O que NÃO copiar

| Item | Motivo |
|---|---|
| **Ausência de CI** | A lacuna mais séria do portal (`DOC_NORTEADOR_PROJETOS.md` §14): `git push` publica e nada garante que a suíte passou. Num sistema que **grava pedido**, isso é inaceitável |
| A carga agendada como desenho | O portal de comissão é de leitura periódica. Lançamento de pedido é interativo e sincrônico; a agenda não se aplica |
| Ausência de teste de front | Tolerável em relatório; num formulário que grava pedido, o roteiro headless da §11.2 do norteador é o mínimo |
| O filtro de competência de `estado.ts:403` | Defeito vivo e medido (junho perde 55,1%). Não é padrão, é pendência nº 13 |

---

## 9. O que falta para a decisão ser tomada com segurança

Cinco informações. As três primeiras **decidem entre API e integrador**; as duas
últimas decidem o desenho, qualquer que seja a escolha.

| # | Pergunta aberta | Quem responde | Por que decide |
|---|---|---|---|
| **1** | **A WebAPI Fenícia 2 tem endpoint de escrita?** | mantenedor da WebAPI (o mesmo destinatário do contrato de `/autenticar`) | Se não tiver, o integrador é **necessidade**, não preferência (§1.2). Pergunta de minutos, alavancagem máxima |
| **2** | **Existe ambiente de homologação da WebAPI e da base?** | operação / infraestrutura | Sem ele, provar escrita exige tocar produção — o que a disciplina desta casa proíbe. Também determina se a §11.3 do norteador ("conferir contra número conhecido") é executável antes de liberar |
| **3** | **`pedido`, `pedemp`, `artficha` estão vinculadas ao `.dbc` ou abertas como tabelas livres?** | medição em VFP: `USE pedido.dbc` + inspeção de `objecttype` | Decide se `BEGIN/END TRANSACTION` cobre a fronteira da §4.5, ou se ela tem de ser obtida por reserva-e-confirmação em duas fases |
| **4** | **Qual é a regra real dos prefixos do número de pedido** (`FB`, `AB`, `AT`, `FT`…)? | operação / vendedores | O contador `cont_ped` está praticamente sem uso (§6.2). Sem essa regra não se projeta a emissão de número no web, e o Cenário C fica sem resposta |
| **5** | **A tela antiga de lançamento continua ativa depois do web?** | decisão de negócio | Se continuar, os dois caminhos disputam `pedido` e o espaço de numeração, e **nenhuma solução de concorrência do lado web basta** (§6.5) |

E uma sexta pergunta, que não decide entre as duas arquiteturas mas define o
comportamento visível ao vendedor:

| # | Pergunta aberta | Quem responde | Por que decide |
|---|---|---|---|
| **6** | **A reserva de estoque continua sendo efeito de digitar, ou passa a valer só na gravação?** | decisão de negócio | Hoje digitar um item compromete saldo imediatamente (Janela 4). Na web, aba abandonada não emite evento de saída, logo a reserva por digitação vaza e só o CheckUp corrige. Mover para a gravação **muda o que dois vendedores veem** enquanto digitam o mesmo produto |

Duas decisões de negócio, já identificadas na análise antecedente, também precedem
qualquer contrato — não são de integração, mas contaminam o contrato se ficarem
abertas:

- **D1** (§4.1 da análise): o preço negociado é preço final ao cliente ou base a
  ser ajustada? As duas leituras são defensáveis e produzem valores diferentes.
- **D7** (§4.2): `totaliza` e `finaliza_item` discordam na faixa de comissão.
  Reconstruir sem decidir qual está certa apenas transporta o defeito para o
  contrato novo.

---

## 10. O que NÃO foi verificado neste documento

Registro explícito, para que nada aqui seja tomado por prova além do que é.

1. **Nenhuma requisição HTTP foi emitida.** Todas as afirmações sobre a WebAPI vêm
   de medições anteriores da casa (11/08 e 06/08/2026), citadas com a fonte.
2. **O VFP não foi executado.** As leituras de DBF foram feitas por parsing
   binário. Semânticas que dependem de execução — comportamento de `TABLEUPDATE`
   sob conflito real, alcance de `BEGIN TRANSACTION`, `RLOCK` implícito — permanecem
   inferidas do código.
3. **A existência de chave candidata em `pedido.codigo` não foi determinada.** O
   `PEDIDO.DBC` (3,3 MB) não foi aberto. A tentativa de ler as tags do
   `pedido.cdx` (11 MB) por parsing produziu saída inconsistente e foi
   **descartada** em vez de reportada.
4. **A regra dos prefixos de número é `HIPÓTESE`**, sustentada por contagem
   (1 registro numérico em 102.104) e por leitura do código, não por confirmação da
   operação.
5. **A tabela `cliente` (894 registros)** é aberta no `Load` de `ped_wer` e não foi
   possível identificar seu uso no fluxo de pedido.
6. **`FENICIA.DBC` tem 15 MB de regras não lidas** — validações, `triggers` e
   valores padrão. A limitação nº 5 da análise antecedente permanece integralmente:
   pode haver regra de negócio que não aparece em nenhum `.prg` e que um contrato de
   escrita precisaria honrar.
7. **A instalação viva não foi medida.** Todos os números vêm do snapshot de
   15/07/2026. Os quatro ramos declarados mortos na §4.3 dependem de configuração
   (`tipos.IND_EMPENH`, `wareasb.Limite_Ped`, `tipos.IND_GRUPO`,
   `wareascp.ind_prazo`) e **têm de ser reconferidos no cliente** antes de se apoiar
   na redução de escopo.
8. **A cópia de `Fox\WER` é laboratório, não produção.** O registro de `pedido`
   com `codigo="38"` e `cod_cli="1"` é rastro de teste, e `pedhead` contém 62
   registros de dado inventado. Contagens que incluam esses registros carregam essa
   sujeira.
9. **Armadilha de medição herdada:** o carimbo de cabeçalho de DBF **não serve**
   como data de último movimento nesta base — a varredura de estrutura do login
   reescreve o cabeçalho de cerca de 200 tabelas. Nenhuma data deste documento vem
   de cabeçalho.
