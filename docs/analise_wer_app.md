# Análise técnica do aplicativo WER

**Documento de engenharia reversa — Visual FoxPro 9**
Data da análise: 18/08/2026
Objeto: aplicativo `pedwer` (pasta de fontes `Fox\Pedido`) e base de produção do cliente (`Fox\WER`)
Finalidade: (1) subsidiar a correção de um defeito na lógica de preço negociado; (2) fundamentar a futura migração para aplicação web consumindo a API do ERP.

---

## 0. Metodologia e como ler as citações

Toda afirmação técnica deste documento é seguida de citação de origem. Três convenções:

| Forma da citação | Significado |
|---|---|
| `arquivo.prg:123` | Linha 123 do arquivo, contada com numeração 1-based após normalizar os três tipos de terminador (`CRLF`, `CR` isolado, `LF`). Vários `.prg` desta base têm fim de linha **só-CR**. |
| `arquivo.scx :: Objeto.Metodo` | Método de objeto dentro do formulário. |
| `arquivo.scx:2366 (dump)` | **Linha do despejo textual do formulário**, não do arquivo binário. Os `.scx`/`.sct` são pares DBF+FPT; a linha só existe depois do despejo. Para reproduzir: `Fox\_claude_ferramentas\dumpscx.ps1 -Scx <arquivo> -Out <saída>`. |

Os `.prg` estão em **CP1252**. Foram lidos exclusivamente por PowerShell com `GetEncoding(1252)`; nenhum arquivo do projeto ou da base foi alterado durante a análise.

**Distinção obrigatória:** o documento marca explicitamente `FATO MEDIDO`, `HIPÓTESE` e `NÃO VERIFICADO`. Onde dois analistas divergiram, a seção §9 registra a arbitragem e as retratações. Números sem indicação de origem não existem neste documento.

Relatórios analíticos completos das seis frentes de varredura e das duas auditorias de arbitragem estão em `relatorios_frentes\` (245 KB), com transcrições literais de código.

---

## 1. Resumo executivo

### O que o WER é

O `pedwer` é um **aplicativo satélite de força de vendas** acoplado ao ERP em Visual FoxPro 9. Não é um sistema autônomo: é um `.app` (`pedwer.app`, 22,9 MB, build de 08/04/2026) que roda **dentro da sessão de outro executável do ERP** — presumivelmente o `estoque.exe`. A prova é que o projeto `pedwer.pjx` **não inclui** `fiscal.prg`, `funcoes.prg`, `mainest*.prg`, `config*.prg` nem `erro_global.prg`; funções basilares como `abertura()`, `dbseek()` e `gravalog` resolvem pela cadeia de procedures da sessão hospedeira. Confirmação adicional: a variável `wwempre_pedwer`, lida pelo `menuwer`, só é declarada em `Fox\prg\mainest.PRG:241` (`FATO MEDIDO`).

O app entrega cinco funções ao usuário, todas a partir de um único orquestrador — o formulário `menuwer.scx`, que é o **MAIN do projeto** (`pedwer.pjx`, registro de cabeçalho; `FATO MEDIDO`). É incomum: o MAIN é um formulário, não um programa.

### Propósito de negócio

O WER implementa uma **política comercial própria do cliente**, sobreposta ao ERP padrão, com quatro mecanismos que o ERP nativo não tem nessa forma:

1. **Preço negociado por cliente e produto**, com validade e autorização, em tabela local própria (`negocia`);
2. **Comissão em escada inversa ao desconto** — quanto mais desconto o vendedor concede, menor o percentual de comissão que ele recebe;
3. **Limite de crédito com dois regimes** — "Constante" (recompõe-se) e "Não Constante" (consumo único, zerado na baixa);
4. **Preço de tabela ajustado por fatores do cliente** (adição, compensação de ICMS, desconto de diretoria).

### A descoberta estrutural mais importante

O cliente WER **não usa o mecanismo `especifico`** do ERP — `wareascp.ESPECIFICO` está **em branco** na base de produção (`FATO MEDIDO`, `Fox\WER\wareascp.dbf`). Toda a especificidade está implementada em **programas e formulários de nome próprio** (`menuwer`, `ped_wer`, `ped327wer`, `frmcliwer`, `est326wer`, `ges629wer`, `fechapedwer`, `valt2w`, `valida6wer`, `pcondpg2`). Consequência prática: **buscar `especifico` nos fontes não explica este cliente** — a via de entendimento é a lista de clones nomeados da §3.

Um ponto de acoplamento inverso merece registro: o `vinc5.PRG`, que é **código compartilhado do ERP**, conhece o WER pelo nome e desliga dois bloqueios para ele — `vinc5:1054-1059` (baixa com saldo negativo, barrada exceto se `pcTelaOrigem="PEDWER"`) e `vinc5:1108-1117` (lote insuficiente, idem) (`FATO MEDIDO`). Ou seja: a especificidade do cliente vazou para dentro do núcleo compartilhado.

### Estado de saúde — síntese

A análise localizou **seis defeitos confirmados** e três riscos estruturais. Dois merecem destaque no resumo executivo:

- **O defeito que mais provavelmente é "a questão do preço negociado"** (§4.1, item D1): o preço negociado é gravado **cru**, enquanto o preço de tabela contra o qual ele é comparado passa por três ajustes multiplicativos derivados do cadastro do cliente. O percentual de desconto calculado a partir dessa comparação é, portanto, **aritmeticamente incorreto** — e é justamente ele que define a faixa de comissão. **46% dos itens negociados (966 de 2.099) pertencem a clientes com esses fatores diferentes de zero** (`FATO MEDIDO`).

- **O defeito que erra dinheiro hoje, em pedido comum** (§4.2, item D2): as duas rotinas que consultam a escada de comissão **arredondam o desconto em direções opostas**. Incluir um item paga uma taxa; alterar o mesmo item paga outra, maior. Reproduzido em laboratório com os valores reais da base: desconto de 5% resulta em **3,00% se digitado e 5,00% se alterado** (`FATO MEDIDO`). Não depende de negociação nem de configuração — atinge o fluxo normal.

Sobre a migração web, a conclusão central é que **o app não tem camada de regra de negócio separável**: a política comercial está dentro de métodos de formulário, acoplada à ordem dos eventos de teclado e a variáveis privadas de escopo dinâmico. A §7 detalha por que isso, e não o volume de código, é o custo real do projeto.

---

## 2. Inventário: o que está vivo e o que é sujeira

A pasta `Fox\Pedido` contém 170 arquivos. A separação entre código vivo e resíduo **não foi feita por inspeção de nomes**, e sim pela leitura do projeto `pedwer.pjx` (787 registros; o campo `NAME` é MEMO e exige leitura via FPT). Este é o critério objetivo: **o que não está no `.pjx` não entra no `.app`**.

### 2.1 Vivo — 13 formulários e 12 programas

| Formulário | Função | Alcançável por |
|---|---|---|
| `menuwer.scx` | **MAIN** — orquestrador | ponto de entrada |
| `frmprod.scx` | Negociação de Preço | menu, botão 1 |
| `frmcliwer.scx` | Cadastro de Clientes | menu, botão 2 |
| `frmcliwer_vend.scx` | Definição de 2 vendedores + cond. pagto | `frmcliwer` (botão `DEF_VENDEDORES`) |
| `ped_wer.scx` | Lançamento de Pedido (clone do `ladd`) | menu, botão 3 |
| `fechapedwer.scx` | Fechamento do pedido | `ped_wer` (`cmdok1.Click`) |
| `menucomis.scx` | Submenu de Comissão | menu, botão 5 |
| `FrmComis.SCX` | Comissão de Vendedores (parâmetros) | `menucomis` |
| `FrmComissao.scx` | Consulta de comissão | `menucomis` |
| `frmcomvend.SCX` | Comissão por vendedor | `menucomis` |
| `ComisVdawer.SCX` | Atribuição de vendedores ao pedido | fluxo do pedido |
| `frmsolicit.SCX` | Define `StrTipMerc` (Nacional/Exterior) | `frmprod` |
| `frminformadata.SCX` | Escolhe Data Emissão × Data Saída | `valt2w.prg:255` e `:354` |

| Programa | Função |
|---|---|
| `ped327wer.prg` (2.035 linhas) | Motor de baixa — clone do `ped327` nativo |
| `est326wer.PRG` (101 linhas) | Driver da baixa |
| `valt2w.prg` (416 linhas) | Validação do pedido + **controle de crédito** |
| `valida6wer.prg` | Validação de condição de pagamento + 2ª checagem de crédito |
| `pcondpg2.prg` | Condição de pagamento — **é aqui que o pedido nasce** (`:185-357`) |
| `ges629wer.prg` | Geração de contas a receber |
| `relcomis.PRG` | Relatório de comissão de vendedores |
| `relcomvend.PRG` | Relatório de comissão por vendedor |
| `RelCredito.PRG` | Formulário impresso de autorização de crédito |
| `listcliwer.PRG` | Grid do grupo econômico |
| `LISTNEGOCIA.PRG` | Lista de negociações |
| `valida6W.PRG` | **Órfão** — ver §2.3 |

### 2.2 Sujeira confirmada (fora do `.pjx`, não compilada)

`frmcliwer_01032026.scx`, `frmcliwer_OLD.scx`, `frmprod-old.SCX`, `relcomis2.PRG`, `valida6wer OLD.prg`, `calculacbsibs_OLD.prg`, `prgwer.PRG` (15 bytes), toda a subpasta `23042018\` (cópias de 2017), e os quatro `.app` históricos (`pedwer_08062021`, `pedwer160519`, `pedwernovo`, `pedwerold`).

Também é resíduo, embora não seja código: **`pedwer_ref.DBF`** (628 KB) — apesar do nome sugestivo, **não é tabela de negócio**. É o cache do *Code References* do VFP9 (schema FoxRef, 5.604 registros). Seu valor é arqueológico: registra as últimas buscas do desenvolvedor no projeto (`desonera`, 196 ocorrências; `wcent`, 298; `csll`, 55) e confirma o caminho oficial do projeto, `Z:\DESENV9\WER\PEDIDO\PEDWER.PJX` (`FATO MEDIDO`). **Não deve ser distribuído em leva.**

### 2.3 Três casos que exigem cuidado — sujeira aparente que não é sujeira

Estes três casos são a razão pela qual o critério "está no `.pjx`" não pode ser aplicado mecanicamente:

**a) `relcomis2.PRG` — arquivo morto, algoritmo vivo em produção.**
O `.prg` está fora do projeto e a chamada no menu está comentada (`menucomis.scx` REC 11: `*DO relcomis2`, substituído por `DO relsup.app`). A conclusão natural — "código morto" — **está errada na prática**. O `relsup.app` (excluído do projeto, resolvido no disco do cliente) executa a mesma lógica, e isso foi **provado por impressão digital comportamental**: o `relcomis2.PRG:81/84/92/94` grava o código do vendedor nos campos `artficha.Banco` e `artficha.Agencia`, e é o **único código de toda a árvore** que faz isso. Na base de produção há **21.965 registros marcados** desse modo (6.316 em `BANCO`, 17.216 em `AGENCIA`), com códigos que são de supervisores (`SUP - EDSON DE FARIAS`, `SV MARCIO SILVA` com 17.783 marcas) e **última data de pagamento marcada em 29/05/2026** (`FATO MEDIDO`).
Nota metodológica relevante para esta casa: a verificação por *scan* de strings no `.app` foi tentada e **reprovou em controle positivo** — o `pedwer.app` comprovadamente embute o `relcomis.prg`, e nenhum literal dele aparece na varredura. O `relsup.app` é cifrado. Portanto: **não conclua o conteúdo de um `.app` por busca de texto.**

**b) `frmcomis2.scx` — modernizado e nunca publicado.**
Fora do projeto, mas **não é cópia** do `frmcomis`: é "Comissão de Supervisor" contra "Comissão de Vendedores", e pertence ao `relsup.app`. O achado é outro: foi **modernizado para o tema visual iuven em 21/06/2025**, enquanto o `relsup.app` que o hospedaria é de **19/04/2021** — quatro anos antes. No mesmo dia 21/06/2025 o desenvolvedor tocou `FrmComissao`, `frmprod` e `fechapedwer`, que estão no projeto e entraram no app. **Só o `frmcomis2` ficou parado, aguardando um rebuild do `relsup` que não aconteceu** (`FATO MEDIDO`).

**c) `valida6W.PRG` — o ancestral órfão, com risco latente.**
Ambos `valida6W.PRG` e `valida6wer.prg` estão no `.pjx` (registros 705 e 706), mas só `valida6wer` é chamado (`ped327wer.prg:174` → `pformat2.PRG:83`). Varredura byte a byte de toda a pasta encontrou uma única string `valida6w*`. O `valida6W.PRG` é o **ancestral**: seu bloco de saldo e atraso migrou literalmente para `valt2w.prg:258-317`, comentários mortos inclusive.
**Risco latente:** `pformat2.PRG:19` contém `chamada="VALIDA6W"` e usa `=` para comparar. Sob `SET EXACT OFF` (o default do VFP), `"VALIDA6WER" = "VALIDA6W"` é **verdadeiro por prefixo** — e a linha 19 vem **antes** da 83. O despacho hoje só acerta porque `Fox\prg\mainest.PRG:155` executa `set exact on`. **A correção funciona por estado global estabelecido em outro módulo** — se a ordem de inicialização mudar, o programa errado passa a ser chamado (`FATO MEDIDO`; consequência é `HIPÓTESE` não exercitada).

---

## 3. Mapeamento de arquitetura e fluxo

### 3.1 O orquestrador `menuwer.scx`

Formulário de cinco botões, versão exibida `"Vs 4.03"` (`menuwer.scx :: menuwer.Init`, com histórico de cinco linhas de `Caption` sobrescritas em sequência, todas marcadas `#BT210 - Cândido` entre 03/03/2026 e 12/03/2026).

Abertura de tabelas no `Load` (`FATO MEDIDO`):
```foxpro
=abertura("fenicia","cadmat","U")
=abertura("fenicia","clientes","N")
=abertura("negocia","negocia","U")
```
A terceira linha é a assinatura do app: `negocia` é um **container DBC local do próprio aplicativo**, não do ERP.

Mapa de despacho — todo botão é guardado por `vefuncao()`, a função de permissão do ERP:

| # | Botão | Guarda | Destino |
|---|---|---|---|
| 1 | Negociação de Preço | `vefuncao("Negociação Wer")` **e** `wfinanceiro="S"` | `DO FORM FrmProd` |
| 2 | Cadastro de Clientes | `vefuncao("Clientes Wer")` | `DO FORM frmcliwer` |
| 3 | Lançamento de Pedido | `vefuncao("Pedido Wer")` **e** estoque principal | `DO FORM ped_wer` |
| 4 | Baixa de Pedido | `vefuncao("Baixa Pedido Wer")` **e** `wpedido="S"` | `DO est326wer` |
| 5 | Comissão | `vefuncao("Comissao")` **e** `wpedido="S"` | `DO FORM MenuComis` |

O botão 3 tem lógica adicional relevante: se `wwempre<>"  " and empty(caduser.cod_empr)`, executa `close databases all` + `do opwareas` e reabre `caduser`, com `set exact on` explícito antes do `dbseek`. Em seguida, se `!empty(alltrim(wwempre)) and empty(alltrim(wwempre_user))`, **recusa a operação** com a mensagem "ESTA FUNÇÃO SÓ É PERMITIDA PARA O ESTOQUE PRINCIPAL". Antes de chamar o formulário, fixa `wrequisicao="N"` e `wped_vend="S"`.

### 3.2 Fluxo A — Lançamento de pedido

```
menuwer (botão 3; wrequisicao="N", wped_vend="S")
  └─> ped_wer.scx  (formulário LADD — clone do ladd nativo)
        Load    : CLOSE DATA ALL + 18 chamadas abertura() + OpwAreaB
        Activate: cria cursores PEDTEMP / empresa_mult / PEDTEMPg / GradePed
        ── ciclo de digitação do item, dirigido por eventos de teclado ──
        Text1.KeyPress      (código do produto)
        Text1.LostFocus     (PREÇO — aplica os 3 fatores do cliente)
        txtAddText          (quantidade)
        txtAddText.LostFocus(consulta a NEGOCIAÇÃO)   ← ponto crítico
        Text2.KeyPress      (DESCONTO — verifica alçada)
        finaliza_item  ->  totaliza                    ← grava linha no PEDTEMP
        ── fim do ciclo ──
        cmdok1.Click : verifica alçada geral -> DO FORM fechapedwer
  └─> fechapedwer.scx   wtotpg.KeyPress (ENTER):
        delecao (se alteração) -> patualiz -> totnotaf
        -> pcondpg2   ← APPEND BLANK + ~60 REPLACE: O PEDIDO NASCE AQUI (:185-357)
        -> grava_cademp -> empenho -> totnotap -> fecha_nota -> mod_ped
```

Tabelas gravadas no fluxo A: `pedido`, `pedemp`, `pedhead`, `cadmat` (`qtd_pedida`, `qtd_fpedid`), `clientes.dt_ult_ven`, `wareas.cont_ped`, e o log (`FATO MEDIDO`).

### 3.3 Fluxo B — Baixa de pedido

```
menuwer (botão 4; pcTelaOrigem="PEDWER", wbaixa_p="N", wped_vend="S", wind_serv="N")
  └─> est326wer.PRG   (driver: abre 10 tabelas, oppedemp, open21)
        └─> ped327wer.prg  (2.035 linhas; interface em MODO TEXTO via formata2)
              despacho por TITC:
                [2]  -> valt2w      (validação + CRÉDITO)
                [4]  -> vinc5       (motor fiscal compartilhado)
                [6]  -> (sem validação)
                [8]  -> valida6wer  (condição de pagamento + 2ª checagem de crédito)
                [11] -> valida1
              valt2w -> DO FORM frminformadata  (define strdtaux)
              laço por item:
                cubagem (tabcor) -> cadmov (APPEND + ~70 REPLACE) -> cplmov
                -> formula -> calcula_impostos -> cadmat -> pedido
              ajustenf -> gravalog -> lotes -> ges629wer('PED327WER') -> boleta
```

Tabelas gravadas no fluxo B: `cadmov`, `cplmov`, `cplmovi`, `pedido`, `cadmat`, `clientes`, `artficha` + `compras` (via `ges629wer`), `apagar` (via `ges829`), `toponf`/`itensnf` (via `boleta`), `loteitem`/`lotes`, `fornece` (`FATO MEDIDO`).

### 3.4 Variáveis de controle

O app é governado por variáveis privadas de escopo dinâmico — atravessam `DO` por serem `PRIVATE`/`PUBLIC`, nunca `LOCAL`. Isso é central para a migração (§7.2).

| Variável | Origem | Papel |
|---|---|---|
| `wped_vend` | `menuwer` (botões 3 e 4) | marca operação de força de vendas |
| `wrequisicao` | `menuwer` (botão 3) | `"N"` = pedido, não requisição |
| `wbaixa_p`, `wind_serv` | `menuwer` (botão 4) | modo da baixa |
| `pcTelaOrigem` | `menuwer` (botão 4) = `"PEDWER"` | **desliga 2 bloqueios do `vinc5`**; zerado em `ped327wer:1726` |
| `wwempre`, `wwempre_pedwer` | `mainest.PRG:241` (hospedeira) | empresa corrente |
| `strdtaux` | `frminformadata` | data escolhida; consumida em `ped327wer:1614-1616` |
| `StrTipMerc` | `frmsolicit` | Nacional/Exterior |
| `ValPrcWer` | `ped_wer.scx:4244 (dump)` | preço de tabela ajustado — ver §4.1 |
| `ValDescDado` | `ped_wer.scx` | percentual de desconto que seleciona a faixa de comissão |

**`NÃO VERIFICADO` (e importa):** a variável `wwempre_pedwer` é lida duas vezes pelo `menuwer` (`Activate` e o botão 4) mas **não recebe atribuição em nenhum ponto de toda a pasta `Sistemas`** — só existe como `PUBLIC` em `mainest.PRG:241`. Se ela nasce vazia no cliente, `wwempre = wwempre_pedwer` zera a empresa corrente ao voltar da baixa. Isso precisa ser medido na instalação real antes de qualquer conclusão.

---

## 4. Dicionário de regras de negócio

### 4.1 Preço negociado

**Tabela.** `negocia.DBF`, container próprio `negocia.dbc`, 8 campos:

| Campo | Tipo | Papel |
|---|---|---|
| `COD_CLI` | C(7) | cliente — casa com `clientes.CODIGO` C(7) |
| `GRUPO` | C(4) | grupo do produto — casa com `cadmat.GRUPO` C(4) |
| `REFERENCIA` | C(10) | referência — casa com `cadmat.REFERENCIA` C(10) |
| `DT_NEGOCIA` | D | data da negociação |
| `DT_VENC` | D | **validade** |
| `PRECO` | N(15,2) | o preço negociado |
| `TIPO_PRC` | C(1) | tipo de preço |
| `COD_VALIDA` | N | **autorização** (1 = autorizada) |

**Chave:** `COD_CLI + GRUPO + REFERENCIA + TIPO_PRC` (tag `negocia2`). Ou seja: **cliente + produto + tipo de preço**. Não há vendedor, não há empresa, não há condição de pagamento na chave. A validade é campo, não chave (`FATO MEDIDO`).

As três larguras conferem byte a byte com as tabelas de destino — **os 11.996 registros casam** (`FATO MEDIDO`). A suspeita inicial de SEEK desalinhado (armadilha conhecida desta base, em que campo mais largo que o valor nunca casa) foi **investigada e refutada**.

**Volume real:** 11.996 registros na tabela viva (`Fox\WER\NEGOCIA.DBF`), com `DT_NEGOCIA` até 09/06/2026 e **94,5% vencendo em 2026**. A tabela é **reciclada, não acumulada** — existem 28 cópias datadas na raiz, a maior com 140.426 registros em 2016. A cópia em `Fox\Pedido` tem 13 registros de 2008 e é apenas o molde de desenvolvimento (`FATO MEDIDO`).

**Quem grava.** Um único ponto: `frmprod.scx :: Form1.operacao` — o formulário de Negociação de Preço, alcançável só pelo botão 1 do menu, sob dupla guarda (`vefuncao("Negociação Wer")` **e** `wfinanceiro="S"`).

**Quem lê e onde o preço é substituído.** `ped_wer.scx :: txtAddText.LostFocus` (dump 3217-3239) executa o SEEK e testa **três guardas**: registro encontrado, `Dt_Venc >= DATE()`, e `cod_valida = 1`. Passando as três, o método `LADD.negocia` (dump 2355) faz:
```foxpro
thisform.text2.Value = negocia.preco
```
e chama `finaliza_item` em seguida — **a negociação encerra a digitação do item ali mesmo**, pulando a etapa de desconto manual.

Que este é o único caminho vivo foi provado por medição, não por leitura: mediu-se `tipos.IND_VALB` para os 8 valores de `TIPO_OPER` presentes nos 102.104 itens de pedido — **todos "0"**, de modo que o caminho alternativo pelo `KeyPress` da quantidade sempre retorna antes de finalizar (`FATO MEDIDO`).

**Consumo pós-pedido: nenhum.** `ped327wer`, `valt2w` e `valida6wer` **não contêm a palavra `negocia`**. Depois do lançamento, o preço fica congelado em `pedido.prc_venda` e a negociação não é reconsultada (`FATO MEDIDO`). Isso é uma boa notícia para a migração: a negociação é um evento de digitação, não uma regra recorrente do faturamento.

**Cadeia de precedência (a resposta à pergunta central):**

```
cadmat.prc_venda
   │  ×(1 + clientes.INSS)      ["Percentual de Adição"]
   │  ×(1 − clientes.IRRF)      ["Compensação de ICMS"]
   │  ×(1 − clientes.ISS)       ["Desconto Diretoria"]
   ▼
ValPrcWer  ──────────────────────────────► grava em pedido.qtd_larg
   │                                        (preço de tabela ajustado)
   │
   ├─► negocia.preco  SOBRESCREVE TUDO — valor CRU, SEM alçada  ◄── ganha de todos
   │
   └─► (sem negociação) preço digitado à mão, COM alçada:
           teto = clientes.PIS + VAL(tabcol.Nome)
                  ["Desconto Especial"]  + teto por coluna de grade

wdesc_ind (% de desconto do item) é o único que ainda incide depois.
```

O `vepreco` nativo do ERP está **desativado** — a chamada está comentada em `ped_wer.scx:4478 (dump)` e há um `RETURN` em `:4483`. Consequência: tabelas de preço 2/3/4, `ind_promo` e `prc_medio` são **código morto neste app** (`FATO MEDIDO`).

#### Defeitos confirmados no preço negociado

**D1 — O preço negociado é cru; o preço de tabela é ajustado. `CONFIRMADO`.**
A comparação que gera o percentual de desconto — e portanto a faixa de comissão — coloca lado a lado um valor cru e um valor que passou por três multiplicações. Exemplo real: cliente com `INSS` de 21,5% faz o sistema calcular **25,8% de desconto onde o desconto real é 9,8%**, jogando o item numa faixa de comissão errada. **966 dos 2.099 itens negociados (46%) pertencem a clientes com INSS/IRRF/ISS diferentes de zero** (`FATO MEDIDO`).
Este é o candidato mais forte a ser "a questão do preço negociado" que motivou a análise. **Requer decisão de negócio antes de correção técnica:** o preço negociado deve ser interpretado como preço final ao cliente (e então o comparativo deve usar o preço cru dos dois lados) ou como preço-base a ser ajustado (e então ele também deve receber os três fatores)? As duas leituras são defensáveis e produzem valores diferentes.

**D2 — `negocia` é global; `cadmat` é por empresa. `CONFIRMADO`.**
A tabela de negociação não tem coluna de empresa, mas o preço de tabela contra o qual ela é comparada vem do `cadmat` **da empresa corrente**. **3.977 negociações vigentes têm preço de tabela diferente entre `fenwin02` e `fenwin03`** — mesmo preço negociado, percentual de desconto diferente, comissão diferente, conforme a empresa em que o pedido é lançado (`FATO MEDIDO`).

**D3 — Divisão por zero sem guarda. `CONFIRMADO`, com gravidade REBAIXADA.**
`ped_wer.scx:2366 (dump)` calcula `((ValPrcWer − preço)/ValPrcWer)*100` sem verificar o denominador. O código gêmeo em `Text2.KeyPress:6081` **tem** o guard (`IF ValPrcWer > 0`, com comentário datado de 29/04/2025): **corrigiram um dos dois caminhos**.
Existem **400 negociações vigentes e autorizadas apontando para produto com `cadmat.prc_venda = 0`** — 2 clientes (`0043873`, `0043811`) × 200 produtos, todas com `DT_VENC = 31/12/2026`, todas na empresa `fenwin03`, nenhuma na `fenwin02`. O guard existente na linha 2357 protege 0 dos 400 casos, porque testa o numerador, não o denominador (`FATO MEDIDO`).
**A gravidade inicial foi corrigida na arbitragem.** A hipótese de que isso fecharia o ERP (via `erro_global.PRG`, que responde a erro não previsto com `QUIT`) foi **REFUTADA por medição em VFP9** (`09.00.0000.5815`, `ENGINEBEHAVIOR 90`): `((0-62)/0)*100` **não dispara `ON ERROR` nem é capturado por `TRY/CATCH`**; devolve um numérico estourado que compara como menor que zero. O erro 39 só ocorreria num `REPLACE` em campo de DBF, e `ValDescDado` **nunca é gravado** — só comparado.
**Efeito real:** o item sai com **3% (via `totaliza`) ou 5% (via `finaliza_item`) de comissão, sem nenhuma mensagem ao usuário.** É corrupção silenciosa de comissão, não queda do sistema. Menos alarmante, mais insidioso.

**D4 — A negociação contorna a alçada inteira. `CONFIRMADO`.**
O caminho da negociação não passa pela verificação de teto de desconto. Agravante medido: **18,5% dos produtos têm `Gradecol` vazio**, o que torna a parcela `VAL(tabcol.Nome)` do teto igual a zero — alçada efetiva de 0%. Existe negociação vigente de **R$ 0,20** na base (`FATO MEDIDO`).

**D5 — `ValPrcWer` obsoleto na alteração de item. `HIPÓTESE` (não medido).**
Na alteração, `Text1` é desabilitado, seu `LostFocus` não roda, e `finaliza_item:1968 (dump)` grava `qtd_larg` com o valor do **produto anterior**. Não foi possível separar este efeito de mudanças legítimas de preço ao longo de 2020-2026; **exige laboratório**.

**D6 — Comparação de caractere com `=` em SQL. `HIPÓTESE`, não confirmada.**
O `frmprod` usa `=` em `SELECT`/`UPDATE`/`DELETE` sobre campos caractere. Sob `SET EXACT OFF`, `=` casa por prefixo, e um `DELETE` poderia atingir registro de outro cliente. Hoje é mitigado por `This.Value = clientes->codigo` (valor completo), mas não foi possível confirmar sem executar o VFP. Teste barato, consequência caríssima.

**Refutado por medição** (registrado para não ser reinvestigado): a tese de que o item negociado seria gravado duas vezes. Foram verificados os 2.099 grupos — nenhum com linha duplicada nem com linha irmã ao preço de tabela; apenas 17 itens com quantidade zero na base, nenhum deles em produto negociado.

### 4.2 Comissão

**A comissão não vem do vendedor, nem do produto, nem do cliente.** Vem de uma **escada desconto → comissão**, na tabela `tabcomis` (que reside no container `negocia.dbc`, não no `fenicia`).

**Estrutura real** (tabela viva, `Fox\WER\tabcomis.dbf`, 11/07/2026 — 5 campos, 10 registros):

| `NEGOCIO` | 0% desc. | 10% | 20% | 30% | 99,99% |
|---|---|---|---|---|---|
| **1** | 5 | 3 | 2 | 2 | 2 |
| **2** | 10 | 7 | 5 | 3 | 3 |

A escolha da escada: `ped_wer.scx:188 (dump)` testa `IF vendedor.tipo_vend = "V"` e usa `WHERE negocio = 1` (`:192`); caso contrário, `negocio = 2`. Idem em `:1894`/`:1898`.

**Distribuição real dos vendedores** (`Fox\WER\vendedor.DBF`, 193 registros): `R` = 118, `I` = 62, **`V` = 10**, branco = 3. Ou seja: **183 vendedores (94,8%) caem na escada `negocio=2`, que paga o dobro** (`FATO MEDIDO`).
Detalhe que merece atenção do negócio: o código comentado (`:209`, `:216`, `:1915`, `:1922`) testava `"R"` **explicitamente**; o código vivo usa um `ELSE` pelado. Consequência: **65 vendedores de tipo `I` ou em branco recebem o dobro sem que exista regra escrita dizendo isso** — herdam o `ELSE` por omissão, não por decisão.

**Base de cálculo:** `quantidade × preço unitário × qtd_comp ÷ 100`. Valor bruto de mercadoria — **sem IPI, sem ST, sem frete, sem seguro e sem desconto**. Existe em `ped327wer.prg:462` um `&& wval_com_ipi` comentado, alternativa não adotada (`FATO MEDIDO`).

**Atribuição:** grava-se **percentual, nunca valor**, e ele é fixado **no lançamento do pedido**:
```
pedtemp.qtd_comp  ->  pedido.qtd_comp  (pcondpg2.prg:230)  ->  cadmov.qtd_comp  (ped327wer.prg:1023)
```
**Nenhum valor de comissão em reais existe em disco.** Os três consumidores recalculam na leitura — o que significa que alterar a `tabcomis` não reescreve o passado, mas **corrigir um `qtd_comp` em `cadmov` reescreve comissão já paga, sem deixar rastro**.

#### Defeitos confirmados na comissão

**D7 — As duas buscas de faixa arredondam em direções opostas. `CONFIRMADO` por dois analistas independentes. É o defeito que erra dinheiro hoje.**
`ped_wer.scx:241-251 (dump)` (método `totaliza`, item **incluído**) arredonda o desconto para a faixa **seguinte**. `ped_wer.scx:1951-1964 (dump)` (método `finaliza_item`, item **alterado**) arredonda para a faixa **anterior**.
Reproduzido em laboratório com os valores reais da escada `negocio=1`:

| Desconto | Item incluído | Item alterado |
|---|---|---|
| 2,5% | 3,00% | **5,00%** |
| 5,0% | 3,00% | **5,00%** |
| 15,0% | 2,00% | **3,00%** |

Coincidem apenas nos degraus exatos (0, 10, 20, 30). **Atinge pedido normal — sem negociação, sem configuração especial, sem divisão por zero.** Incluir um item paga uma taxa; alterar o mesmo item paga outra, maior (`FATO MEDIDO`).
**Não quantificado:** a incidência por caminho. Nenhum campo registra qual método gravou a linha; medir exige reconstituir o desconto de cada item a partir de preço e valor.

**D8 — O desconto por percentual escapa da escada dos dois lados. `CONFIRMADO`.**
`wdesc_ind` chama `finaliza_item` **sem alimentar `ValDescDado`** — a busca de faixa volta ao topo da escada (10%). E o `prc_venda` é gravado **cheio**, com o desconto em campo separado (`desc_ind`). Resultado: o desconto por percentual **reduz a nota, mas não reduz nem o percentual de comissão nem a base de cálculo** (`FATO MEDIDO`).

**D9 — Vendedor 2 é obrigatório e recebe zero. `CONFIRMADO`.**
`ComisVdawer` exige os dois vendedores, exige senha de supervisor para trocar o primeiro, e grava `cod_vend1` em quatro tabelas — e **nenhum dos três consumidores de comissão o lê**. Não há rateio. Os campos nativos `artficha.PERCVEND1`/`PERCVEND2` existem no ERP e este app os ignora (`FATO MEDIDO`).

**D10 — `relcomis` grava `1,00` num campo monetário. `CONFIRMADO` no código; dano fiscal `REFUTADO`; risco latente `CONFIRMADO`.**
Este item passou por auditoria adversarial dedicada e o resultado é mais interessante que a tese original.
O código faz exatamente o que se suspeitava: `relcomis.PRG:136` é `REPLACE IR WITH 1`, **incondicional** (não há `IF` entre as linhas 134 e 138) e **dentro do laço** 87-154, gravando o numérico `1.00` num campo que a estrutura confirma ser `N 10.2`. O filtro em `:76` é `AND IIF(StrReImpre = "S",.T.,A.IR = 0)` — ou seja, o campo é usado como flag de "comissão paga". A refutação mais plausível foi testada e **não se sustentou**: `ges629wer.prg:547` grava `cplmovi.val_irrf` **nesse mesmo campo `ir`** (alias fixado na linha 531; segunda ocorrência na 589). E existe um campo `RETEM_IR C(1)` — **o flag correto existe e não foi usado**; está em branco nos 144.287 registros.
Na base: **121.553 registros com `IR` valendo exatamente 1,00 (84,2% de 144.287)**, distribuídos de 03/2010 a 07/2026 — regime permanente, não incidente. **Zero valores fora do conjunto {0; 1,00} em 16 anos** (`FATO MEDIDO`):

| Empresa | Total | `IR`=0 | `IR`=1,00 | outros |
|---|---|---|---|---|
| 02 | 116.939 | 16.932 | **99.959** | 0 |
| 03 | 27.345 | 5.722 | **21.594** | 0 |

**Mas o dano fiscal não se materializou:** nunca houve IRRF em `artficha.IR` para destruir. `tipos.IND_ISS` está em branco nos **487** tipos, de modo que o ramo da linha 547 nunca dispara; e `wir` (`relcomis.PRG:384`) é `PUBLIC` zerado em `mainest.PRG:387` e nunca recebe outro valor em toda a árvore. O IRRF existe apenas no `cplmovi` (57 registros de 387.976 em `fenwin02`) e nunca foi transportado.
**Duas consequências permanecem reais:**
- *Exposição contábil condicionada:* `ges802.PRG:216` soma esse campo como dinheiro (`valtotart = valor + csst + cofins + pis + ir`), o que produziria **R$ 121.553 fabricados** — condicionado a esse relatório ser usado por este cliente, o que **não foi confirmado**.
- *Risco latente:* se `ind_iss="S"` for algum dia ligado, a linha 547 passa a gravar IRRF real e a reimpressão (liberada por `vefuncao2("Reemissao")`) anula o filtro protetor. A partir daí a destruição de imposto vale integralmente.

**D11 — `dbseek` falhando em silêncio dentro do laço do relatório. `CONFIRMADO`.**
`relcomis.PRG:135` usa `dbseek` com `"M0"`, que **falha em silêncio** (contrato em `fenodbc.PRG:1092-1108` e `:1155`), e o `REPLACE` seguinte roda **sem guarda de retorno** — padrão idêntico ao defeito RL2676 já conhecido nesta base.
A evidência disso está no log de produção e explica uma anomalia que parecia inexplicável. `gravalog` está **dentro do laço** (`relcomis.PRG:150`) e é o único gravador de `"REL COMIS"` na árvore, de modo que os eventos são proxy 1:1 das gravações. Decompondo o pico de 02/03/2026: **274 eventos com documento e observação preenchidos, 21.448 com os dois em branco, zero mistos**, começando às 14:42:39. Dois campos que vêm de leituras distintas do mesmo alias, ambos vazios 21.448 vezes, é assinatura de **ponteiro fora de faixa** — `dbseek` falhado, com o `REPLACE` rodando antes (`FATO MEDIDO`).
**Consequência operacional, mais séria que a contábil:** o `APPEND` no cursor `TmpComis` ocorre **antes** do `dbseek`. Nas 21.448 iterações, **a comissão foi impressa sem ser marcada como paga** — o que abre risco de **repagamento**.

**D12 — Duas comissões, duas bases. `CONFIRMADO`, com base medida.**
A comissão de **vendedor** (`relcomis.PRG:100`) calcula sobre `qtd_mov × valor_mov` = mercadoria pura, **sem ST**. A comissão de **supervisor** (`relcomis2.PRG:65`, viva dentro do `relsup.app` — ver §2.3a) calcula sobre `artficha.Valor` = a duplicata, **com ST**.
Medido em `fenwin02` (63.951 notas): Σ`artficha.Valor` = R$ 363.727.384,62 contra Σ`qtd_mov × valor_mov` = R$ 323.454.563,58 — **razão de 1,1245**. A base do supervisor é **12,45% maior**; 58.478 notas maiores, 393 menores (`FATO MEDIDO`).
`RESSALVA`: provou-se que a rotina está viva e qual é a fórmula no `.prg` de origem; **não** se provou que a fórmula esteja intacta dentro do `relsup.app` de 2021. Isso exigiria `z:\desenv9\wer\relatorio_supervisor\relsup.pjx`, ausente no C: e no D:.

**D13 — Divergência entre consulta e relatório. `CONFIRMADO`.**
`FrmComissao` lê `pedido.qtd_itens` (quantidade **pedida**); `relcomis` lê `cadmov.qtd_mov` (quantidade **baixada**). Em baixa parcial, **a consulta superestima**. Além disso `relcomis` é regime de **caixa** (`dt_pag`, filtra empresa) e `relcomvend` é **competência** (`data_mov`, não filtra empresa): **não fecham por construção**, e ambos gravam log com o mesmo `wcod_sis = "RELCOMIS"` (`FATO MEDIDO`).

**Colisão de campo — investigada e considerada teórica neste cliente.**
O campo `qtd_comp` guarda o percentual de comissão (`:258`/`:1969`), mas o mesmo campo é gravado com medida física em `:827` (`REPLACE qtd_comp WITH wmedida2`) para produtos vendidos por M2/ML/M3 — e o schema confirma que o sentido **original** é físico (`QTD_COMP N(12,3)`, ao lado de `QTD_LARG`/`QTD_MED`/`QTD_PES`). A colisão é real no código; **o estrago, medido, é zero neste cliente**: `PEDIDO.DBF` (102.104 registros) tem **0 valores fora da escada**; `QTD_MED` = 0 em todos; **0 itens** de produto M2/M³/ML/MT; `wareascp.IND_LARGCO` está em branco e `wareas.IND_GRADE` = `"N"`. Em `cadmov` (915.759 registros) há 2.754 valores fora da escada (0,30%), com apenas 4 valores distintos, **nenhum posterior a 2017**, concentrados em unidades UN e CX — são degraus de comissão antigos, não comprimentos (`FATO MEDIDO`).
`ATENÇÃO PARA A MIGRAÇÃO`: a colisão é dormente porque a configuração do cliente a mantém dormente. Um schema novo não deve herdar o campo polivalente.
Um **terceiro campo invadido** foi identificado: `:256` faz `REPLACE qtd_larg WITH ValPrcWer` — `qtd_larg` tem **62.933 valores entre 100 e 1.000, que são preços**, e o guard `*If wareascp.ind_largco="S"` da linha 255 está **comentado** (`FATO MEDIDO`).

### 4.3 Limite e crédito — "constante e não constante"

**O termo é do código, não do usuário.** Está literal como legenda de dois radio-buttons em `frmcliwer.scx :: Pageframe1.page3.Optiongroup1` — `Option1.Caption="Constante"`, `Option2.Caption="Não Constante"`. Persistido em **`clientes.CSLL`** (N 10,2): **1 = Constante, 0 = Não Constante** (`FATO MEDIDO`).

**A mecânica dos dois regimes.** O consumo está em `valt2w.prg:342-345`, executado **depois** de o pedido passar por todas as travas:
```foxpro
IF clientes.csll = 0
    REPLACE credito WITH 0
    =TABLEUPDATE(.T.)
```
- **Não Constante** = limite de **uso único**: é **zerado no ato da baixa**, no registro **da matriz**. O cliente precisa de nova liberação para comprar outra vez.
- **Constante** = permanece e se recompõe conforme os títulos são pagos.

**A verificação** (`valt2w.prg:249-345`): o limite é o **da matriz** (`Posicao="M"`), mas o saldo devedor é **do grupo econômico** — agrupado por `SUBSTR(cgc,1,10)`, isto é, matriz e filiais do mesmo CNPJ-raiz. Três travas duras:

| Trava | Linha | Critério |
|---|---|---|
| Atraso | `:306` | `dt_vencim + clientes.cofins < DATE()` — tolerância em dias vem de `cofins` |
| Limite zerado | `:319` | limite = 0 barra |
| Estouro | `:328` | saldo + pedido > limite |

Todo o bloco é **pulado** se `wareas.ind_ver_fi = "N"` (`:252-257`). Há uma segunda checagem em `valida6wer.prg:441-455`, com três chaves de desligamento (`vencimr.ind_credit` = "N" ou "R", e `wareasb.indcredito`).

**Não existe liberação por alçada.** A `PROCEDURE senha_gerente6` existe completa em `valida6wer.prg:459-549` e **a chamada está comentada** (`:446-450`). A senha (SUPERVISOR/GERENTE) protege apenas a *troca de condição de pagamento* — não o crédito (`FATO MEDIDO`).

**Não confundir com o vale-crédito.** Existe na base um `credito.dbf` que é **vale-crédito de devolução**, acionado pela condição de pagamento `"CR"`. Não tem relação com o limite. É uma armadilha de nomenclatura para a migração.

#### Defeito e risco no crédito

**D14 — O `IIF` de gravação não trata o estado inicial. `CONFIRMADO` no código; efeito na base `NÃO MEDIDO`.**
A gravação é `REPLACE csll WITH IIF(optiongroup1.value == 2, 0, 1)`. O `OptionGroup` **nasce com `value = 0`**, e o ramo "senão" grava **1 = Constante**. Ou seja: cliente cadastrado sem que ninguém toque no par de radios sai **Constante por omissão** — o regime mais permissivo.
Isso se combina com um defeito de interface que existiu até 08/04/2026 (ver §5): **os radios apareciam e não clicavam**. Recomendação antes de qualquer conclusão sobre a intenção do cadastro: rodar `SELECT csll, COUNT(*) FROM clientes GROUP BY csll` na base do cliente.

### 4.4 Desconto por cliente

**Não existe desconto por grupo/família nem escalonado por quantidade.** Existem dois mecanismos, de naturezas diferentes:

1. **Ajuste do preço de tabela** — `ped_wer.scx :: LADD.Text1.LostFocus` (dump 4244-4292): três fatores **multiplicativos, com arredondamento a cada etapa** — `+INSS`, `−IRRF`, `−ISS`. Não é "desconto" no sentido comercial; é formação de preço por cliente.
2. **Teto de desconto do vendedor** — `Text2.KeyPress` (dump 6048-6118): **aditivo**, `clientes.PIS + VAL(TabCol.Nome)`, sendo a segunda parcela um teto por coluna de grade do produto (via `cadmat.gradecol`). Excedido o teto, o sistema **rejeita e restaura o preço anterior**.

`clientes.perc_desc` — `HIPÓTESE: morto`. A varredura só encontrou `forneced.perc_desc`.

### 4.5 Os seis campos sequestrados — ponto crítico para a API

Comparando os `ControlSource` de `frmcliwer.scx` (115) com o cadastro nativo `Fox\form\clientes.scx` (124), o WER é um **subconjunto estrito**: **zero campos novos**. O que o cliente controla "a mais" não está em campos novos — está em **significado sobreposto a campos fiscais existentes**. Provado por correspondência de coordenada `Top` entre label e campo (`FATO MEDIDO`):

| Campo físico | Significado no ERP | Significado no WER |
|---|---|---|
| `clientes.INSS` | INSS | **Percentual de Adição** (majora o preço) |
| `clientes.IRRF` | IRRF | **Compensação de ICMS** (reduz o preço) |
| `clientes.ISS` | ISS | **Desconto Diretoria** (reduz o preço) |
| `clientes.PIS` | PIS | **Desconto Especial** (teto de desconto) |
| `clientes.COFINS` | COFINS | **Dias de tolerância de atraso** |
| `clientes.CSLL` | CSLL | **Flag Constante / Não Constante** |

**Não há discriminador na tabela.** O mesmo schema serve clientes que usam esses campos com o significado fiscal original e este cliente, que os usa com significado comercial. Nada no dado diz qual leitura vale — a distinção existe apenas no fato de o programa que abre a tela chamar-se `frmcliwer`. Isto é, provavelmente, **a maior armadilha de toda a migração** (§7.4).

### 4.6 Impostos e Substituição Tributária

**O ST não é calculado por código WER.** É calculado no `totitem2.PRG`, programa compartilhado do ERP, embutido no app. Fórmula literal, na ordem em que ocorre (`FATO MEDIDO`):

```
mercadoria  ← mf_totproduto
            ← menos desconto (truncando em 3 casas)
wba_calc1 = ROUND((wmerc_tot + wripi) * wdesp_subst/100, 2)      ← MVA sobre mercadoria + IPI
wba_calc2 = wmerc_tot + wba_calc1 + wba_calc3 + wripi − lntotalpis
wba_calc2 = wba_calc2 − wba_calc2 * pnredbasesubst/100           ← redução de base
wic_ret1  = wba_calc2 * WVAL_ICM_SU/100
wic_ret2  = ROUND(wic_ret1 − wwicm2, 2)                          ← menos o ICMS próprio
```
Gravação em `est077.prg:3003-3005` (rotina `gravacp`).

**Pauta fiscal existe:** se `cadsub.prcmedio > 0` e o preço praticado for menor que 90% da pauta, a base passa a ser `prcmedio × quantidade` e **o MVA é ignorado** (`totitem2:1149-1158`).

**Origem da alíquota e do MVA**, em cascata de precedência: `cadsub` (`val_icm_su` / `subs_trib`) → `cadicm` → `cadmat.desp_subst` → e, em último recurso, **três MVAs de São Paulo escritos no fonte: 165,55 / 71,60 / 38,90** (`totitem2:1086`, `:1090`, `:1093`). A chave do `cadsub` é grupo + referência + UF + enquadramento, com fallback por **NCM + UF + enquadramento** (`pesquisacadsub:42-44`).

**O ST entra no total, no financeiro e nos boletos** — rastreado ponta a ponta: `vinc5:1563`/`:1613` → `ped327wer:995`/`:1172` → `wsoma` → `wvalor` → `ges629wer` (`FATO MEDIDO`). Na comissão, ver D12: as duas comissões discordam justamente por causa do ST.

**DIFAL:** `calculadifa` é chamado com `.F.` em todo item, e a chamada com `.T.` é **código morto** porque `cadmov.temdifa` nunca é gravado (ver D-fiscal-3 abaixo).

#### Divergências deliberadas contra o nativo (`ped327wer` × `ped327`)

| # | Divergência | Efeito |
|---|---|---|
| DF1 | `gcajusta` nunca vira `"BAIXAPED"` | o motor roda em **modo NOTA** durante a baixa |
| DF2 | falta `pnredbasesubst = pedido.redbasest` antes do `calcula_impostos` | redução de base não chega ao cálculo |
| DF3 | não grava `cadmov.redbasest` nem `cadmov.temdifa` | torna `calculadifa(.T.)` inalcançável |
| DF4 | chama `calculadifa(.F.)` em todo item | sem a guarda de combustível que o nativo tem |
| DF5 | `pcondpg2` perdeu o bloco que lê `cadsub.icm_reduc` | redução do cadastro não é aplicada |

Três suspeitas foram **retratadas** por se provarem idênticas ao nativo: `perc_subst`, `icmret_m`/`baseicm_rt` e `pmvast`.

#### Riscos fiscais estruturais

**DF-R1 — O app carrega o motor fiscal pré-fusão. `CONFIRMADO`, com uma incerteza declarada.**
`calcimp:74` executa `DO totitemp`, e toda a cadeia (`fechapedwer` → `patualiz` → `calcimp`) está **dentro do `.app` de 08/04/2026**. O `totitemp` atual é um shim de **18/07/2026**, cujo cabeçalho declara que divergências de ICMS-ST entre `totitemp` e `totitem2` foram resolvidas a favor do `totitem2`. Medida a diferença contra o `totitemp.BAK`: divergem na **base do MVA** (um termo `-lntotalpis` a mais) e na **dedução do ICMS próprio** (`wvend_int` contra `wwicm2`).
**Consequência prática: rebuild do `estoque.exe` NÃO corrige isto. Só o rebuild do `pedwer.pjx` corrige.**
`INCERTEZA DECLARADA`: a precedência de módulo do VFP entre `.app` e `.exe` no `DO totitem2` feito pelo `funcoes` não foi medida. É a única variável capaz de inverter parte desta conclusão, e deve ser verificada antes de agir.

**DF-R2 — Redução de base do ST inalcançável no WER. `CONFIRMADO`.**
Os três *setters* de `pnredbasesubst` presentes no projeto leem `redbasest` do pedido/pedtemp — campo que **nenhum fonte WER preenche**. Os que leem do cadastro (`condpg`, `fiscalped`, `ajustafiscal`) **não estão no app**. Resultado: **ST calculado sobre base cheia**, sem redução (`FATO MEDIDO`).

**DF-R3 — Motor fiscal vindo de pasta pessoal de desenvolvedor. `CONFIRMADO`.**
O `pedwer.PJT` grava o caminho absoluto de origem de três itens: `z:\marcio\base\totitem.prg`, `z:\marcio\base\calculadifa.prg` e `..\..\..\marcio\base\nfsenacional.app`. Todo o restante (228 programas, 245 formulários) vem de `z:\fendesv9\`. **O `totitem` é o motor fiscal por item do ERP** — e este app foi buildado com uma cópia mantida fora da árvore oficial.
**Nenhum dos dois arquivos existe nesta máquina** (de `calculadifa` só há um `calculadifa2.PRG` de 2018), portanto **a divergência não pôde ser medida**. O fato verificável é a referência no projeto. Requer acesso ao Z:.

Registro complementar: `calculacbsibs_OLD.prg` está fora do projeto e é morto; corresponde à versão **sem o guard do `fiscal.prg`** (a assinatura conhecida do erro "linha 92"). A versão viva é a nativa, chamada por `boleta.prg:292`. As alíquotas da reforma tributária (`ALIQCBS`, `ALIQIBSUF`, `ALIQIBSMUN`) estão **vazias** no `wareascp` deste cliente (`FATO MEDIDO`).

---

## 5. O que mudou entre março e abril de 2026

O build vigente é de 08/04/2026 e a ficha responsável é `#BT210 - Cândido`. Comparando `frmcliwer.scx` (08/04) com a cópia `frmcliwer_01032026.scx` (01/03), as entregas foram (`FATO MEDIDO`):

1. Botão `DEF_VENDEDORES` → `frmcliwer_vend`, com **bloqueio da gravação sem 2 vendedores e sem condição de pagamento**;
2. A aba 3 deixa de nascer desabilitada;
3. **Correção no par Constante / Não Constante** — `CREDITO1.KeyPress` ganhou `option1.Enabled = .T.` e `option2.Enabled = .T.`. Na versão de março, apenas o contêiner era habilitado: **os radios apareciam e não clicavam**;
4. Suporte a **CNPJ alfanumérico** (`valida_cgc_alfa`, máscara `!!.!!!.!!!/!!!!-99`);
5. Obrigatórios qualificados por alias (`empty(cgc)` → `empty(clientes.CGC)`).

O item 3 é relevante para D14: durante um período indeterminado até 08/04/2026, **o operador não conseguia escolher o regime de crédito**, e a gravação por omissão registra "Constante". Isso reforça a recomendação de auditar a distribuição de `clientes.csll` antes de tomar o cadastro como intencional.

Sintoma de interface a considerar em qualquer correção futura em formulário desta casa: a biblioteca `wizstyle.vcx` força `Enabled = EditMode` em todo CheckBox e TextBox via `SetAllProp`, de modo que controle de ação nasce morto — aparece, não clica, e **não emite mensagem**. O defeito corrigido no item 3 é dessa família.

---

## 6. Análise de dependências

### 6.1 O que o WER puxa do sistema nativo

Dos 771 itens do projeto, **apenas 25 (13 formulários + 12 programas) são do próprio app**. O restante:

| Origem | Itens | Natureza |
|---|---|---|
| `..\..\..\fendesv9\form` | 245 | formulários do ERP |
| `..\..\..\fendesv9\prg` | 228 | programas do ERP |
| `..\..\..\fendesv9\frx` | 61 | relatórios |
| `..\..\..\fendesv9\sipro` | 15 | módulo CRM/atendimento |
| `..\..\..\fendesv9c\producao` | 4 | módulo de produção |
| **`..\..\..\admcandido\temp`** | **4** | `on_error.prg`, `vesenha_candido.prg`, `cadmat_oca.scx`, `frmcategoria_candido.scx` |
| **`..\..\..\marcio\base`** | **2** | `totitem.prg`, `calculadifa.prg` — **o motor fiscal** |
| `..\..\vgk`, `..\..\vsx`, `..\..\hrs_comercio` | 4 | fontes de **outros clientes** |
| `..\..\..\fendesv9c\fiscal` | 1 | `comenta.prg` |

**Três árvores de fonte distintas coexistem num mesmo build**, sendo duas delas pastas pessoais de desenvolvedores (`admcandido\temp` e `marcio\base`). O `on_error.prg` — tratamento global de erro — vem de uma pasta chamada `temp`. Este é um risco de configuração, independente de qualquer defeito de lógica: **o build não é reproduzível a partir da árvore oficial**.

### 6.2 Dependências funcionais principais

| O que o WER usa | De onde | Para quê |
|---|---|---|
| `vefuncao()`, `vefuncao2()` | ERP | permissão por função nomeada |
| `abertura()`, `dbseek()`, `opwareas` | ERP (`fenodbc`, `funcoes`) | acesso a dados |
| `gravalog` | ERP | auditoria |
| `vinc5.PRG` | ERP | motor fiscal da baixa — **e conhece o WER pelo nome** |
| `totitem2` / `totitemp` | ERP | cálculo de imposto por item, ST |
| `mf_totproduto` | `fiscal.prg` | totalização de mercadoria |
| `boleta.prg` | ERP | `toponf`/`itensnf`, emissão |
| `ges829` | ERP | contas a pagar |
| `formata2`, `pformat2` | ERP | interface em modo texto da baixa |
| `iuven_messagebox` | ERP | caixas de diálogo |
| `clientes`, `cadmat`, `caduser`, `vendedor` | ERP | cadastros |
| `wareas`, `wareascp`, `wareasb` | ERP | parâmetros do cliente |

**Observação de risco:** `ped327wer` e `est326wer` referenciam `wareasb` (12 vezes) e `wareascp` (9 vezes) **sem nunca abrir nenhuma das duas** — dependem de a sessão hospedeira as ter aberto (`FATO MEDIDO`).

### 6.3 Comparação com os formulários nativos de referência

- **`ped_wer.scx` × `ladd`**: o WER é um clone do formulário de lançamento nativo, com o `vepreco` desativado e a formação de preço substituída pelos fatores do cliente (§4.1).
- **`frmcliwer.scx` × `clientes.scx`**: subconjunto estrito de campos (115 contra 124), com seis campos fiscais reinterpretados (§4.5).
- **`ped327wer.prg` × `ped327.PRG`**: as divergências relevantes:

| Aspecto | Nativo | WER |
|---|---|---|
| nº de pedido / cond. pagto / série | `valt2b` / `valida6` / `validaserie` | `valt2w` / `valida6wer` / **sem validação de série** |
| cabeçalho da baixa | formulário `ped327_form.scx` (13 referências a `iuv_USA_FORM_BAIXA`) | **só tela texto** (0 referências) |
| NF centralizada | `lcnfcentral`, 8 referências | 0 referências; `est326wer:6` força `.F.` |
| `calculacbsibs` | 1 referência, comentada | 0 referências |
| **`TRY`/`CATCH`** | 4 / 2 | **0 / 0** |
| refresh multiempresa pós-cabeçalho | `wareascp` + `opwareas` + reseek `tabplan` (`:429-440`) | **ausente** |
| financeiro | `ges629('PED327')` | `ges629wer('PED327WER')`, com `wdata_nfisc = strdtaux` |
| ESC na baixa | zera `pedido.qt_parcial` (`est326:205-214`) | **não zera** |
| cubagem | ausente | `pncubagem` via `tabcor` → `boleta.prg:2288` |

### 6.4 Riscos de robustez identificados

| # | Risco | Evidência |
|---|---|---|
| R1 | **Zero `TRY`/`CATCH`** em `ped327wer`, `est326wer`, `pcondpg2` e `valt2w` — caminho crítico inteiro exposto ao handler global, que responde a erro não previsto com `QUIT` | contagem direta |
| R2 | Alteração de item é **código morto**: `lstAdd.KeyPress` (dump 3871-3876) tem `RETURN 0` fora do `IF` comentado; ~150 linhas nunca executam | leitura |
| R3 | `ped327wer:357` faz `int(pedido.qtd_itens/cadmat.qtd_emb)` sem guarda; que `qtd_emb=0` existe está provado no próprio arquivo (`:427`). Medido em VFP9: `INT(10/0)` **não erra** — devolve overflow em silêncio, que vai parar num `REPLACE cubagem` | medição |
| R4 | `ped327wer:346` chama `dbseek("codigo", cadmat.gradecor, "TabCor")` com 3 argumentos; pelo contrato real (`fenodbc.PRG:943`) o 3º argumento cai em `wpmsg`, o índice fica vazio (**sem `SET ORDER`**) e a função **retorna `.T.` mesmo sem encontrar** | contrato + leitura |
| R5 | `ped327wer:1594` divide por `VAL_MOEDA` sem guarda na saída, enquanto a entrada (`:1656`) protege | leitura |
| R6 | `ped327wer:1726` zera `pcTelaOrigem` **dentro** do laço — numa segunda volta, as exceções que o `vinc5` concede ao PEDWER deixam de valer | leitura |
| R7 | `pformat2.PRG:19` compara com `=` e casaria `"VALIDA6WER"` com `"VALIDA6W"` por prefixo; só não acontece por causa do `set exact on` de `mainest.PRG:155` | leitura |

---

## 7. Considerações para a migração web

A migração pretende transformar este app em aplicação web consumindo a API do ERP. Os desafios reais **não são de volume de código** — são de natureza da arquitetura. Ordenados por dificuldade decrescente.

### 7.1 Desafio nº 1 — A regra de negócio mora nos eventos de teclado

A política comercial do WER não está em funções chamáveis. Está distribuída pela **ordem de disparo dos eventos** de um formulário:

```
Text1.KeyPress  → Text1.LostFocus  → txtAddText  → txtAddText.LostFocus  → Text2.KeyPress  → finaliza_item → totaliza
   (produto)         (PREÇO)          (qtd)          (NEGOCIAÇÃO)            (DESCONTO)
```

Sair do campo de quantidade **é** o gatilho da consulta de negociação; entrar no campo de desconto **é** o gatilho da verificação de alçada. Não existe uma função `calcularPreco(cliente, produto, quantidade)` para expor como endpoint — ela precisa ser **extraída e reconstruída**, e as duas rotinas que hoje fazem trabalho equivalente (`totaliza` e `finaliza_item`) **discordam entre si** (D7). Reconstruir sem antes decidir qual das duas está certa apenas transporta o defeito.

**Recomendação:** a extração da regra de preço/desconto/comissão para uma função pura, testável e única — feita **no VFP, antes da migração** — é pré-requisito, não etapa opcional. Ela é também a correção de D7.

### 7.2 Desafio nº 2 — Estado global implícito

O comportamento do app depende de ~15 variáveis privadas de escopo dinâmico (§3.4) criadas por **outro programa** (`mainest.PRG`) e visíveis por herança de escopo. Além disso:

- `pcTelaOrigem = "PEDWER"` altera o comportamento de **código compartilhado** (`vinc5`), desligando duas validações;
- `SET EXACT ON` estabelecido em `mainest.PRG:155` é o que faz um despacho funcionar corretamente (§2.3c);
- `ped327wer` usa tabelas que **não abre**, contando com a sessão hospedeira.

Em HTTP não há sessão VFP nem escopo dinâmico. Cada uma dessas dependências precisa se tornar **parâmetro explícito de chamada**. O inventário da §3.4 é o ponto de partida, e deve ser tratado como incompleto até que uma varredura dedicada a variáveis não declaradas seja feita.

### 7.3 Desafio nº 3 — Não existe camada de dados

O app manipula DBF diretamente: `APPEND BLANK` seguido de ~60 `REPLACE` em `pcondpg2.prg:185-357` para criar um pedido; ~70 `REPLACE` por item em `cadmov`. Não há transação, não há validação centralizada, não há *unit of work*. A criação de um pedido toca 7 tabelas em sequência sem atomicidade.

Consequência para a API: **um endpoint `POST /pedido` não tem contrapartida no código atual**. O que existe é uma sequência de mutações intercaladas com decisões de interface. Definir a fronteira transacional é trabalho de projeto, não de tradução.

### 7.4 Desafio nº 4 — Campos com significado sobreposto

Os seis campos da §4.5 (`INSS`, `IRRF`, `ISS`, `PIS`, `COFINS`, `CSLL` na tabela `clientes`) têm significado comercial neste cliente e significado fiscal em outros, **sem discriminador no dado**. Um contrato de API que exponha `cliente.inss` estará mentindo para metade dos consumidores.

**Recomendação:** o contrato da API deve expor **nomes de domínio** (`percentualAdicao`, `compensacaoIcms`, `descontoDiretoria`, `descontoEspecial`, `diasToleranciaAtraso`, `regimeLimite`), com a tradução para o campo físico confinada na camada de acesso. E o mapeamento precisa ser **por cliente**, porque o mesmo campo físico significa coisas diferentes em instalações diferentes.

O mesmo vale para `qtd_comp` (percentual de comissão **ou** medida física) e `qtd_larg` (medida física **ou** preço de tabela ajustado) — ver §4.2. Um schema novo não deve herdar campos polivalentes.

### 7.5 Desafio nº 5 — Interface em modo texto no fluxo de baixa

O `ped327wer.prg` desenha sua interface via `formata2`/`pformat2` — **tela em modo texto**, com despacho por código numérico (`TITC`). O nativo já evoluiu para formulário (`ped327_form.scx`, com 13 referências a `iuv_USA_FORM_BAIXA`); o WER tem **zero**. Não há o que reaproveitar de camada de apresentação nesse fluxo: a baixa terá de ser redesenhada do zero, e a lógica precisará ser separada do desenho de tela linha por linha.

### 7.6 O que facilita

Nem tudo é adverso, e três características reduzem o escopo:

1. **A negociação não é reconsultada após o lançamento** (§4.1). O preço congela em `pedido.prc_venda`. Isso torna a negociação um serviço de consulta simples, sem necessidade de replicar a regra no faturamento.
2. **A comissão é armazenada como percentual, calculada na leitura.** Não há valores monetários de comissão persistidos a migrar — apenas a escada `tabcomis`, que tem **10 registros**.
3. **A tabela `negocia` tem 8 campos e chave clara.** É uma entidade bem definida, com 11.996 registros ativos: candidata natural a primeiro serviço a ser migrado, e boa escolha para provar a arquitetura antes de atacar o pedido.

### 7.7 Sequência sugerida

| Fase | Escopo | Justificativa |
|---|---|---|
| 0 | **Corrigir D7 no VFP** (unificar as duas buscas de faixa) e decidir a semântica de D1 | erra dinheiro hoje; e migrar antes de decidir transporta o defeito |
| 1 | Extrair a regra de preço/desconto/comissão para função pura no VFP, com testes | pré-requisito de qualquer endpoint |
| 2 | Migrar **consulta** de negociação (leitura) para a API | entidade limpa, risco baixo, prova a arquitetura |
| 3 | Migrar **cadastro** de negociação (escrita), com a alçada que hoje não existe (D4) | corrige defeito ao migrar |
| 4 | Migrar o lançamento de pedido | depende de 1, 2 e 3 |
| 5 | Baixa e financeiro | maior acoplamento; depende de decisão sobre o motor fiscal (DF-R1) |

---

## 8. Achados priorizados

Ordenação por **dano medido**, não por severidade teórica.

| # | Achado | Onde | Estado |
|---|---|---|---|
| **1** | **Escada de comissão arredonda em direções opostas** — incluir e alterar o mesmo item pagam taxas diferentes; atinge pedido comum | `ped_wer.scx:241-251` × `:1951-1964` (dump) | `CONFIRMADO`, medido em laboratório |
| **2** | **Preço negociado cru comparado com preço de tabela ajustado** — percentual de desconto aritmeticamente errado; define a faixa de comissão. 966 de 2.099 itens expostos | `ped_wer.scx:2355`/`:4244` (dump) | `CONFIRMADO`; **exige decisão de negócio** |
| **3** | **`relcomis` grava `1,00` em campo monetário e o `dbseek` falha em silêncio** — 121.553 registros afetados; 21.448 comissões impressas sem marcação (risco de repagamento) | `relcomis.PRG:135-136`, `:150` | código `CONFIRMADO`; dano fiscal `REFUTADO`; risco latente `CONFIRMADO` |
| **4** | **Duas comissões, duas bases** — supervisor calcula sobre base 12,45% maior (com ST) que o vendedor (sem ST) | `relcomis.PRG:100` × `relcomis2.PRG:65` (vivo no `relsup.app`) | `CONFIRMADO`; fórmula no `.app` não verificada |
| **5** | **App carrega motor fiscal de ST pré-fusão** — rebuild do `estoque.exe` não corrige; só rebuild do `pedwer.pjx` | `calcimp:74`, `.app` de 08/04/2026 | `CONFIRMADO` com uma incerteza declarada |
| **6** | **Redução de base do ST inalcançável** — `pnredbasesubst` nunca alimentado; ST sobre base cheia | DF2, DF5 | `CONFIRMADO` |
| **7** | **Desconto por percentual escapa da escada** — reduz a nota, não reduz comissão nem base | `wdesc_ind` → `finaliza_item` | `CONFIRMADO` |
| **8** | **`negocia` global × `cadmat` por empresa** — 3.977 negociações com comissão dependente da empresa | §4.1 D2 | `CONFIRMADO` |
| **9** | **Divisão por zero na negociação** — 400 casos armados na `fenwin03`; efeito é comissão de 3% ou 5% em silêncio, **não** queda do sistema | `ped_wer.scx:2366` (dump) | `CONFIRMADO`; gravidade **rebaixada** por medição |
| **10** | **Negociação contorna a alçada**; 18,5% dos produtos com alçada efetiva 0%; existe negociação de R$ 0,20 | §4.1 D4 | `CONFIRMADO` |
| **11** | **Regime de crédito grava "Constante" por omissão**, e os radios não clicavam até 08/04/2026 | `frmcliwer.scx`, `CREDITO1.KeyPress` | código `CONFIRMADO`; efeito na base **a medir** |
| **12** | **Zero `TRY`/`CATCH`** em todo o caminho crítico da baixa | R1 | `CONFIRMADO` |
| **13** | **65 vendedores recebem o dobro por herdarem um `ELSE` pelado** | `ped_wer.scx:188-192` (dump) | `CONFIRMADO` |
| **14** | **Motor fiscal buildado de pasta pessoal** (`z:\marcio\base`); build não reproduzível da árvore oficial | `pedwer.PJT` | `CONFIRMADO`; divergência **não medida** |
| **15** | **`frmcomis2` modernizado em 2025 e nunca publicado** — aguarda rebuild do `relsup.app` (2021) | §2.3b | `CONFIRMADO` |

---

## 9. Arbitragens e retratações

Seis analistas varreram o app em paralelo e produziram achados divergentes em quatro pontos. Duas auditorias adversariais dedicadas arbitraram cada um. O registro é parte do documento porque **três conclusões intuitivas se provaram erradas**, e repetir a investigação custaria o mesmo trabalho de novo.

| Ponto | Divergência | Veredito |
|---|---|---|
| `relcomis2` morto? | "fora do `.pjx`, logo morto" × "usei sua linha 65 como prova" | **Os dois certos sobre a própria metade**: arquivo morto, algoritmo vivo no `relsup.app`. Provado por impressão digital comportamental (21.965 marcas em `banco`/`agencia`, última em 29/05/2026), não por scan de string — que **reprovou em controle positivo** |
| `tabcomis` tem 2 ou 5 campos? | cópia local de 2008 (2 campos) × tabela viva (5 campos) | **Tabela viva prevalece.** E `tipo_vend="V"` cai em `negocio=1`, a escada que paga **metade** — com apenas 10 dos 193 vendedores nela |
| Divisão por zero derruba o ERP? | "sintoma seria o ERP fechando" | **REFUTADO por medição em VFP9.** Divisão por zero numérica não dispara `ON ERROR` nem `TRY/CATCH`; sem `REPLACE` em campo de DBF não há erro 39. O efeito é comissão errada em silêncio |
| `qtd_comp` está corrompendo comissão? | colisão de significado no código | **Colisão real, estrago zero neste cliente** (0 itens M2/ML/M3, `IND_LARGCO` em branco, 0 valores fora da escada em 102.104 registros). Dormente por configuração — não deve ser herdada no schema novo |

**Retratações registradas** (erros dos próprios analistas, corrigidos na auditoria):

- A empresa morta é a **`fenwin01`** (0 registros), **não** a `fenwin03` — que tem 2.287 movimentações e 1.215 pedidos em 2026, contra 4.136 pedidos da `fenwin02`.
- Um analista afirmou "match exato" entre os 21.594 eventos de log e os 21.594 registros da empresa 03. É **coincidência**: a hora de pico tem 21.509 eventos.
- Os números 21.866 / 21.594 / 269 do pico de log **não reproduziram** na segunda medição (obteve-se 37.627 / 21.509 / 9.765). O **fenômeno** se confirma; a **contagem exata**, não. Quem for tratar o pico deve remedir.
- Uma suspeita de colisão de chave de `artficha` entre empresas foi levantada e **refutada**: 0 chaves compartilhadas.
- Suspeitas fiscais de divergência em `perc_subst`, `icmret_m`/`baseicm_rt` e `pmvast` foram **retratadas** — são idênticas ao nativo.

---

## 10. Limitações desta análise

Registro explícito do que **não** foi verificado, para que nada aqui seja tomado por prova além do que é.

1. **O VFP não foi executado contra a base do cliente.** Todas as medições em DBF foram feitas por parsing binário, somente leitura. Semânticas que dependem de execução (comportamento de `REPLACE` em EOF, por exemplo) permanecem não confirmadas.
2. **Nada foi testado em laboratório funcional.** As reproduções de laboratório citadas (arredondamento da escada, divisão por zero em VFP9) foram feitas isoladamente, não no fluxo completo do app.
3. **`z:\marcio\base\totitem.prg` e `calculadifa.prg` não existem nesta máquina.** A divergência do motor fiscal contra o nativo **não pôde ser medida** (§4.6, DF-R3).
4. **`relsup.pjx` não foi encontrado** no C: nem no D:. A fórmula viva dentro do `relsup.app` é inferida do `.prg` de origem, não lida do binário (§2.3a).
5. **Nenhum `.dbc` foi aberto.** O `FENICIA.DBC` tem 15 MB de regras (validações, triggers, valores default) **não lidas** — pode conter regra de negócio relevante que não aparece em nenhum `.prg`.
6. **A precedência de módulo do VFP entre `.app` e `.exe`** no `DO totitem2` não foi medida. É a única variável capaz de inverter parte de DF-R1.
7. **`wwempre_pedwer` não recebe atribuição em nenhum ponto da árvore local.** Se isso também vale na instalação do cliente, há efeito colateral não mapeado (§3.4).
8. **Snapshot da base:** cópia de **15/07/2026**, com movimento denso terminando em **15/06/2026**. Julho deve ser tratado como mês truncado. A base tem 7,18 GB em 1.756 DBFs; `fenwin02` é a empresa viva (2,42 GB; `cadmov` com 915.759 registros até 11/07/2026).
9. **Armadilha de medição documentada:** o carimbo de cabeçalho de DBF **não serve** como data de último movimento nesta base — a varredura de estrutura do login reescreve o cabeçalho de ~200 tabelas. `WER\cplmov.DBF` tem cabeçalho de 11/07/2026 e dados que param em 2012. Todas as datas deste documento foram medidas **campo a campo**, registro a registro.
10. **Sujeira de data na base**, relevante para qualquer relatório por período: `PEDIDO.DATA_ENT` varia de 0206 a 2202; `apagar.DT_VENCIM` chega a 8203; `cadmat.DT_CADASTR` tem 7 registros em 2119.

---

## Anexos

`relatorios_frentes\` contém os relatórios analíticos completos, com transcrições literais de código:

| Arquivo | Conteúdo |
|---|---|
| `rel_A_preco_negociado.md` | Preço negociado — chave, gravação, consumo, precedência, 7 suspeitas |
| `rel_B_pedido.md` | Fluxo transacional completo, variáveis de controle, 15 riscos, comparação com o nativo |
| `rel_C_comissao.md` | Escada, base de cálculo, atribuição, 9 achados |
| `rel_D_cliente_credito.md` | Limite constante/não constante, desconto, campos sequestrados, diff março→abril |
| `rel_E_impostos_st.md` | ST, MVA, pauta, DIFAL, 6 divergências vs. nativo, 2 riscos estruturais |
| `rel_F_base_dados.md` | Mapa de uso real, topografia, `wareas`/`wareascp`, evidência de vida por `fenlog` |
| `ver_V1_artficha_ir.md` | Auditoria adversarial do `artficha.IR` e do pico de log |
| `ver_V2_arbitragem.md` | Arbitragem das quatro divergências entre analistas |
| `inventario_pedwer_pjx.txt` | Despejo integral do `pedwer.pjx` — 771 itens classificados |
