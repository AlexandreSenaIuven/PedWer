# Requisitos funcionais — Lançamento de Pedido (app WER)

**Especificação para reimplementação web**
Data: 18/08/2026
Objeto: fluxo de lançamento de pedido do aplicativo `pedwer` (Visual FoxPro 9), fontes em `C:\Users\Lana\Desktop\Sistemas\Fox\Pedido`, base de produção em `C:\Users\Lana\Desktop\Sistemas\Fox\WER`.
Documento complementar a `analise_wer_app.md` (mesma pasta), que descreve **como o código faz**. Este documento especifica **o que o sistema pr  ecisa garantir**, de forma independente de tecnologia.

---

## 0. Como ler este documento

### 0.1 Escopo

Coberto: lançamento de pedido de venda — cabeçalho, ciclo de item, fechamento, gravação, numeração, autorizações, alteração e exclusão, e a máquina de estados do pedido resultante.

Fora de escopo (referenciado apenas quando o lançamento depende): baixa de pedido (`est326wer`/`ped327wer`), geração de financeiro (`ges629wer`), cálculo de imposto por item (`totitem2`/`totitemp`), negociação de preço como cadastro (`frmprod`), cadastro de cliente (`frmcliwer`).

### 0.2 Convenções de citação

| Forma | Significado |
|---|---|
| `arquivo.prg:123` | Linha 123 do arquivo, numeração 1-based após normalizar `CRLF`, `CR` isolado e `LF`. |
| `arquivo.scx:1234 (dump)` | Linha do **despejo textual** do formulário, não do binário. Reproduzir com `Fox\_claude_ferramentas\dumpscx.ps1 -Scx <arquivo> -Out <saída>`. |
| `arquivo.scx :: Objeto.Metodo` | Método de objeto, quando a linha exata não é relevante. |

Os `.prg` foram lidos em CP1252. Nenhum arquivo do projeto ou da base foi alterado.

### 0.3 Marcação de evidência

| Marca | Significado |
|---|---|
| `FATO MEDIDO` | Verificado no fonte ou na base por leitura direta / parsing binário. |
| `HIPÓTESE` | Deduzido por leitura de código, sem execução. |
| `NÃO VERIFICADO` | Registrado como lacuna; não sustentar decisão sem medir. |
| `⚠ DIVERGE DO CÓDIGO ATUAL` | O requisito especifica o comportamento **correto desejado**, e o código atual não o cumpre. A migração não deve replicar o defeito. |

### 0.4 Criticidade

| Nível | Critério |
|---|---|
| **essencial** | Sem isto o pedido sai com dado errado, com valor errado, ou não sai. |
| **importante** | Sem isto há retrabalho, inconsistência recuperável, ou perda de controle gerencial. |
| **desejável** | Conveniência operacional; a ausência não corrompe dado. |

### 0.5 Advertência sobre parâmetros

Muitas regras do código são condicionadas a *flags* de configuração (`wareas`, `wareasb`, `wareascp`, `tipos`). A §14 mede o valor real desses *flags* na base do cliente e classifica cada regra em **ATIVA** ou **INERTE**. Um requisito marcado `INERTE` descreve regra que existe no código e **não é exercitada por esta instalação** — deve ser reimplementada apenas se houver decisão explícita de mantê-la.

---

## 1. Contexto operacional

### 1.1 Modelo de dados de destino

`FATO MEDIDO` — `Fox\WER\PEDIDO.DBF`: 148 campos, 102.104 registros vivos, **35.499 códigos de pedido distintos**.

O pedido **não tem tabela de cabeçalho**. Cada item é um registro de `pedido`, e os dados de cabeçalho são **repetidos em todos os itens do mesmo pedido** (`pcondpg2.prg:185-351` grava cabeçalho e item no mesmo `APPEND BLANK`). A identidade do pedido é a tripla:

```
pedido.ES_MOV (C1) + pedido.TIPO_OPER (C3) + pedido.CODIGO (C10)
```
(índice `pedido7`, usado em `pcondpg2.prg:374`, `fechapedwer.scx:1155-1157 (dump)`, `fechapedwer.scx:116 (dump)`).

`RF-001` | **O modelo web deve separar cabeçalho de item.** | Uma entidade `Pedido` (identidade = empresa + tipo de operação + código) e uma entidade `ItemPedido` (N por pedido). Os ~40 campos de cabeçalho hoje replicados em cada item passam a existir uma única vez. | `pedido.DBF` (148 campos); `pcondpg2.prg:185-351` | **essencial**

`RF-002` | **A duplicação atual não é redundância inofensiva: é fonte de divergência.** | `fechapedwer.fecha_nota` (`fechapedwer.scx:221-254 (dump)`) percorre todos os itens do pedido reescrevendo `total_nota`, `total_icm`, `total_desc`, `icms_ret`, `val_custo`, `val_finan`, `incide_*`, `cod_trans`, `obs_nf`, `qtditenspe` — um segundo passe sobre a mesma gravação. Se esse passe falhar no meio, metade dos itens fica com totais de capa antigos. O modelo novo deve tornar isso impossível por construção (total de capa existe uma vez). | `fechapedwer.scx:221-254 (dump)` | **essencial**

`RF-003` | **`pedido` é compartilhada entre empresas; `cadmat` não é.** | `FATO MEDIDO`: `pedido.COD_EMPR` tem 4 valores na mesma tabela (`02`=75.124, `03`=26.972, `01`=5, vazio=3). Não existe `fenwin02\pedido.dbf`. Já o `cadmat` é por empresa e é **reaberto** quando a empresa muda (`ped_wer.scx:5405-5421 (dump)`, `:3712-3721 (dump)`, `:3974-3983 (dump)`). O modelo novo deve manter a empresa como **atributo** do pedido e como **chave de resolução** do produto e do seu preço. | `Fox\WER\PEDIDO.DBF`; `ped_wer.scx:5405-5421 (dump)` | **essencial**

### 1.2 Atores

| Ator | Identificação no código | Papel |
|---|---|---|
| Operador de vendas | `widentific` (usuário logado), `caduser` | Digita o pedido |
| Supervisor | `caduser.ind_caixa = "S"` | Libera desconto acima do teto e troca de condição de pagamento e de vendedor |
| Gerente | `caduser.ind_caixa = "G"` | Libera alteração/exclusão de pedido gravado e digitação de preço quando a tabela é amarrada |
| Usuário `SUPORTE` | `widentific = "SUPORTE"` | Isento da senha de troca de vendedor (`ComisVdawer.SCX:208 (dump)`) |

`RF-004` | **A identidade do operador deve acompanhar toda gravação.** | Hoje só `widentific` existe, e a auditoria depende de `gravalog`. O modelo novo deve registrar autor e momento em cabeçalho e em cada alteração. | `pcondpg2.prg:368`; `fechapedwer.scx:1170 (dump)` | **essencial**

---

## 2. Máquina de estados do pedido

### 2.1 Estados observáveis

`FATO MEDIDO` — medição campo a campo dos 102.104 itens de `Fox\WER\PEDIDO.DBF`:

| Campo candidato a marcador de estado | Valores distintos medidos | Serve como estado? |
|---|---|---|
| `QTD_LIBER` N(10,3) | 0 em 452 itens; `= QTD_ITENS` em 101.652 | **Sim** — é o marcador real |
| `QT_PARCIAL` N(10,3) | 0 em 102.104 | Sim, mas nunca armado nesta base |
| `NOTAFIS` C(10) | preenchido em 101.643 | **Sim** — nº da NF da baixa |
| `POSICAO` C(10) | **vazio em 102.104** | **Não** (ver RF-012) |
| `MOV_FEITO` C(1) | **vazio em 102.104** | Não |
| `STATUS` C(1) | **vazio em 102.104** | Não |
| `INTEGRADO` C(1) | vazio em 102.097; `0` em 7 | Não |

Estados que o sistema efetivamente distingue:

```
                (não existe registro)
                        │
        [E1] EM DIGITAÇÃO — só no cursor PEDTEMP, em memória
             ⚠ já consumiu número de documento e já reservou estoque
                        │  gravação (pcondpg2)
                        ▼
        [E2] GRAVADO / ABERTO
             QTD_LIBER = 0 ; QT_PARCIAL = 0 ; NOTAFIS vazio
                 │                    │                        │
   arma parcial  │                    │ baixa integral         │ exclusão
                 ▼                    ▼                        ▼
        [E3] PARCIAL ARMADO   [E5] BAIXADO TOTAL          (registro apagado
             QT_PARCIAL > 0        QTD_LIBER = QTD_ITENS    fisicamente)
                 │                 NOTAFIS preenchido
                 ▼
        [E4] BAIXADO PARCIAL
             0 < QTD_LIBER < QTD_ITENS ; QT_PARCIAL zerado
                 │  (novo ciclo de baixa)
                 └──────────► [E5]
```

`RF-005` | **Estado do item = par (quantidade pedida, quantidade liberada).** | `qtd_liber = 0` → aberto; `0 < qtd_liber < qtd_itens` → parcialmente baixado; `qtd_liber >= qtd_itens` → totalmente baixado. O motor de baixa pula o item já liberado: `ped327wer.prg:294` `IF wbaixa_P<>"S" AND qtd_itens =< qtd_liber → SKIP/LOOP`. | `ped327wer.prg:294`, `:1240` | **essencial**

`RF-006` | **Estado do pedido = agregação dos estados dos itens.** | Não existe campo de estado no pedido. A tela deriva por varredura: `ped_wer.scx:3677-3686 (dump)` percorre os itens e produz `wSt_baixa = "p"` (algum item parcialmente baixado) ou `"t"` (algum item totalmente baixado). O modelo novo deve manter esse estado **derivado**, não denormalizado, ou garantir sua consistência transacionalmente. | `ped_wer.scx:3677-3686 (dump)` | **essencial**

`RF-007` | **`QT_PARCIAL` é intenção, não histórico.** | É preenchido antes da baixa parcial para dizer *quanto* baixar, e **zerado** pelo motor após consumir: `ped327wer.prg:1237` `REPLACE qt_parcial WITH 0`. `ped327wer.prg:290` pula o item cujo `qt_parcial = 0` quando a baixa é parcial. O modelo novo deve tratar isso como comando/fila, não como atributo do item. | `ped327wer.prg:290`, `:1237` | **importante**

`RF-008` | **A reserva é campo próprio e é consumida na baixa.** | `pedido.QT_RESERVA` é gravada no lançamento (`pcondpg2.prg:232`) e decrementada na baixa: `ped327wer.prg:1236` `REPLACE qt_reserva WITH IIF(qt_parcial <= qt_reserva, qt_reserva - qt_parcial, 0)`. | `pcondpg2.prg:232`; `ped327wer.prg:1236` | **importante**

`RF-009` | **A exclusão é física e não deixa rastro no dado.** | `ped_wer.scx:691 (dump)` e `fechapedwer.scx:120 (dump)` fazem `DELETE` + `=tableupdate()`. O único vestígio é o `gravalog` (`ped_wer.scx:665 (dump)`, com `wrespalt` = `"E"`). ⚠ **O modelo novo deve usar exclusão logística (estado CANCELADO + autor + momento + motivo), não `DELETE`.** Sem isso não é possível auditar pedido cancelado nem explicar buraco na numeração. | `ped_wer.scx:658-698 (dump)`; `fechapedwer.scx:113-123 (dump)` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-010` | **Alteração de pedido gravado é apagar-e-regravar, não atualizar.** | `fechapedwer.scx:1036-1038 (dump)`: `IF wrespalt="A" AND wmais_pg=0 → thisform.delecao`, e `delecao` (`:113-123`) apaga **todos** os itens do pedido; em seguida `pcondpg2` (`:1117`) regrava do zero. Consequência: qualquer campo que o `pcondpg2` não regrave é **perdido**, e o `RECNO` muda. O modelo novo deve fazer atualização diferencial com identidade estável de item. | `fechapedwer.scx:113-123 (dump)`, `:1036-1038 (dump)`, `:1117 (dump)` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-011` | **Não existe estado "orçamento" nem "aprovação" neste fluxo.** | `wareas.IND_ORCAM = "N"` (`FATO MEDIDO`); `wrequisicao` é forçado a `"N"` pelo menu (`analise_wer_app.md` §3.1). O pedido nasce firme. | `Fox\WER\WAREAS.DBF`; `menuwer.scx :: Command8.Click` | **importante**

### 2.2 O estado "bloqueado" existe no código e está morto no dado

`RF-012` | **O bloqueio de faturamento por `posicao` deve existir e hoje é inalcançável.** | A tabela `Fox\WER\posicao.dbf` tem **2 registros** (`FATO MEDIDO`): `98` = "BLOQUEIO DE FATURAMENTO" (`BLOQUEIO="S"`, `OBS="DIRETORIA"`) e `99` = "DESBLOQUEIO DE FATURAMENTO" (`BLOQUEIO="N"`). O gravador é `pcondpg2.prg:346-350`: `REPLACE Posicao WITH WAreasB.Bloq_Pedid` se houve estouro de alçada, senão `REPLACE Posicao WITH Tipos.Posicao`. **`tipos.POSICAO` está vazio nos 487 tipos** e `wareasb.BLOQ_PEDID` está vazio — logo `pedido.POSICAO` sai vazio em 100% dos casos, e o teste de bloqueio da baixa (`valt2w.prg:47-56`) nunca barra nada. | `pcondpg2.prg:346-350`; `valt2w.prg:47-56`; `Fox\WER\posicao.dbf`, `tipos.dbf`, `wareasb.dbf` | **importante** |

`RF-013` | **O teste de bloqueio da baixa deve falhar de forma segura quando o estado é desconhecido.** | `valt2w.prg:49` faz `=dbseek("Cod_Pos", pedido.Posicao, "M0", "posicao")` com chave **vazia** e **não testa o retorno**; a linha 50 lê `Posicao.Bloqueio` com o ponteiro possivelmente fora de faixa. É o mesmo padrão do defeito RL2676 já conhecido nesta base. O comportamento correto: chave vazia = "sem posição" = não bloqueia, decidido **explicitamente**; chave preenchida e não encontrada = **erro**, e a baixa para. | `valt2w.prg:47-56` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

### 2.3 Transições permitidas

| De | Para | Gatilho | Guarda |
|---|---|---|---|
| — | E1 | Abrir a tela de pedido | `vefuncao("Pedido Wer")` + estoque principal |
| E1 | (nada) | ESC com zero itens → confirma saída | devolve o número (`acertanumped`) |
| E1 | E2 | ENTER em `wtotpg` no fechamento | passa por `pcondpg2` |
| E2 | E1 | Digitar o código existente e responder `A` | senha de GERENTE se `tipos.ind_senha="S"` |
| E2 | apagado | Digitar o código existente e responder `E` + confirmar | senha de GERENTE se `tipos.ind_senha="S"` |
| E2 | E3 | Armar `qt_parcial` (fora deste fluxo) | — |
| E2/E3/E4 | E4/E5 | Baixa (`est326wer`→`ped327wer`) | `valt2w` + `valida6wer` |
| E5 | E1 | **permitido pelo código, com aviso** | ver RF-014 |

`RF-014` | **Reabrir para alteração um pedido já baixado deve ser proibido, ou exigir alçada explícita.** | Hoje `ped_wer.scx:3688-3699 (dump)` apenas **avisa**: `"PEDIDO COM ITENS PARCIALMENTE|TOTALMENTE BAIXADOS." + "DESEJA CONTINUAR O PROCESSO ?"` (tipo 4 = Sim/Não), e "Sim" segue para o menu `A/E/T`. Em seguida `contnota` (`:492-657`) pode devolver `werro=1` e aí sim barra com `"PEDIDO JÁ BAIXADO PARCIALMENTE OU TOTALMENTE !"` (`:3730`) — mas a proteção fica no recarregamento, não na decisão. Como a alteração é apagar-e-regravar (RF-010), aceitar aqui destrói o vínculo com a movimentação já gerada. | `ped_wer.scx:3688-3699 (dump)`, `:3728-3734 (dump)` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

---

## 3. Sequência obrigatória de operações

A ordem abaixo não é preferência de interface: cada passo **produz** o estado que o passo seguinte **consome**. Reimplementar fora de ordem produz valor errado sem mensagem de erro.

### 3.1 Cabeçalho

```
(1) Tipo de operação  → posiciona o registro de `tipos`
(2) Empresa           → decide QUAL cadmat será lido (preço, saldo, IPI)
(3) Número/código     → decide inclusão × alteração × exclusão
(4) Data              → base de wdata_mov
(5) Cliente           → posiciona `clientes`; valida CFOP×UF; congela o cabeçalho
```

`RF-020` | **O tipo de operação é o primeiro campo e governa o resto.** | `ped_wer.scx:2515 (dump)` põe o foco nele. O registro de `tipos` posicionado é consultado depois em pelo menos 30 decisões: rótulo cliente/fornecedor (`:3510-3513`), CFOP (`tipos.natureza`, `:5577-5612`), preço amarrado (`tipos.ind_valb`, `:3552-3562`), permissão de tipo por cliente (`tipos.tip_client`, `:5617-5638`), empenho (`tipos.ind_empenho`), multiempresa (`tipos.tp_ped_mt`), reserva de estoque (`tipos.ind_qtd`), sentido (`tipos.tipo_es`), status C/F (`tipos.status`), comissão (`tipos.ind_comiss`), senha de alteração (`tipos.ind_senha`), posição (`tipos.posicao`). **A API deve receber o tipo de operação como primeiro parâmetro e resolver `tipos` uma única vez, expondo as decisões derivadas como atributos nomeados.** | `ped_wer.scx:3493-3578 (dump)` | **essencial**

`RF-021` | **A empresa precisa ser conhecida antes do produto.** | `ped_wer.scx:5386-5430 (dump)` valida a empresa contra `tabplan` e, se válida, **fecha e reabre o `cadmat`** apontando para a pasta da empresa (`:5410-5415`). Se o produto for lido antes, o preço, o saldo e o IPI vêm da empresa errada. | `ped_wer.scx:5386-5430 (dump)` | **essencial**

`RF-022` | **O número do documento precisa ser resolvido antes do cliente.** | O ramo `A` (alteração) chama `contnota` (`ped_wer.scx:492-657 (dump)`), que **sobrescreve** o cliente, a condição de pagamento, o centro de custo, os vendedores, a data e o desconto de capa a partir do pedido existente (`:496-517`). Se o operador informar o cliente antes, o valor é descartado. | `ped_wer.scx:492-517 (dump)`, `:3702-3759 (dump)` | **essencial**

`RF-023` | **O cliente congela o cabeçalho.** | `ped_wer.scx:5738-5745 (dump)` e `:5764-5771 (dump)`: aceito o cliente, o sistema desabilita **tipo de operação, empresa, data, número e o próprio cliente**, e move o foco para o produto. **O cabeçalho passa a ser imutável até o fechamento.** É uma regra de negócio, não um detalhe de tela: mudar a empresa ou o tipo depois de itens lançados invalidaria preço, imposto e reserva já gravados. A API deve materializar isso (ex.: cabeçalho selado quando existe o primeiro item). | `ped_wer.scx:5738-5745 (dump)`, `:5764-5771 (dump)` | **essencial**

### 3.2 Ciclo de item

```
(6)  Produto            → posiciona `cadmat`; trava de "negócio" do 1º item
(7)  PREÇO DE TABELA    → depende de cliente E produto  [Text1.LostFocus]
(8)  Quantidade         → depende do produto (unidade de medida)
(9)  NEGOCIAÇÃO         → depende de cliente E produto  [txtAddText.LostFocus]
(10) DESCONTO           → depende do preço de tabela (7), de cadmat.Gradecol
                          e de clientes.PIS               [Text2.KeyPress]
(11) finaliza_item → totaliza → grava linha em PEDTEMP,
                                reserva estoque, FIXA A COMISSÃO
```

`RF-024` | **O preço de tabela só pode ser formado depois de cliente e produto conhecidos.** | `ped_wer.scx:4244-4292 (dump)`: `ValPrcWer = Cadmat.Prc_Venda`, depois três ajustes que vêm do **cadastro do cliente**. Sem os dois lados o valor é indefinido. | `ped_wer.scx:4244-4292 (dump)` | **essencial**

`RF-025` | **A consulta de negociação só pode ocorrer depois de cliente, produto e quantidade.** | A chave é `Cod_Cli + Grupo + Referencia + Tipo_Prc` (`ped_wer.scx:3220-3224 (dump)`), e o gatilho é a **saída do campo quantidade** (`txtAddText.LostFocus`). A quantidade não entra na chave, mas a ordem dos eventos torna a negociação dependente dela. Na versão web a consulta deve ser função explícita de `(cliente, produto, tipo de preço, data)`. | `ped_wer.scx:3214-3240 (dump)` | **essencial**

`RF-026` | **O desconto só pode ser validado depois do preço de tabela.** | `ped_wer.scx:6060` calcula o teto em valor como `ROUND((ValPrcWer * ValPrcWer2)/100, 2)` e `:6064` o desconto concedido como `ValPrcWer - preço digitado`. Sem `ValPrcWer` o teto é zero e todo desconto é recusado (ou aceito indevidamente, conforme o sinal). | `ped_wer.scx:6029-6118 (dump)` | **essencial**

`RF-027` | **A comissão é fixada no momento em que o item é confirmado, e nunca mais recalculada.** | `totaliza` (`ped_wer.scx:258 (dump)`) grava `REPLACE qtd_comp WITH curComis.Comis`; daí segue para `pedido.qtd_comp` (`pcondpg2.prg:230`) e depois para `cadmov.qtd_comp` na baixa. Alterar a escada `tabcomis` **não** reescreve o passado. | `ped_wer.scx:258 (dump)`; `pcondpg2.prg:230` | **essencial**

`RF-028` | **A reserva de estoque acontece na confirmação do item, antes de o pedido existir.** | `ped_wer.scx:366-381 (dump)`, sob `IF tipos.ind_qtd="S"`: saída → `REPLACE cadmat.qtd_pedida WITH qtd_pedida + wquant(...)`; entrada → `qtd_fpedid`; e `=Tableupdate()` **item a item**. Se o operador abandonar a tela sem passar pelo cancelamento, a reserva fica órfã. Ver RF-160. | `ped_wer.scx:366-381 (dump)` | **essencial**

### 3.3 Fechamento

```
(12) ESC              → se há itens: cmdok1.Click → abre fechapedwer
(13) Vendedores       → ComisVdawer (2 vendedores obrigatórios)
(14) Desconto de capa → percentual / valor / abatimento
(15) Condição de pagamento
(16) Centro de custo
(17) ENTER em "Total pago" → GRAVA
```

`RF-029` | **A ordem interna da gravação é obrigatória e cada etapa consome a anterior.** | `fechapedwer.scx:1021-1234 (dump)`, na ordem literal: `delecao` (só se alteração, `:1036-1038`) → `patualiz` (`:1041`, totaliza itens e impostos) → `totnotaf` (`:1086`, frete/custo/seguro) → soma dos incidentes em `wtotger2` (`:1100-1108`) → **`pcondpg2`** (`:1117`, cria os registros) → `grava_cademp` (`:1128`, `pedemp`) → `empenho` (`:1130`, `pedhead`) → `totnotap` (`:1147`) → `fecha_nota` (`:1181`, reescreve totais de capa em todos os itens) → `mod_ped` (`:1198`, impressão) → `sair1.Click` (`:1233`). | `fechapedwer.scx:1021-1234 (dump)` | **essencial**

`RF-030` | **O fechamento pode recusar e voltar ao início do fechamento.** | `fechapedwer.scx:1119-1125 (dump)`: se `pcondpg2` retornar com `wexit=0` (o operador respondeu que **não** quer segunda condição de pagamento, ou o valor pago não fecha), a tela zera o valor pago, devolve o foco à condição de pagamento e mostra `WTOTGER4`. Nada foi gravado. | `fechapedwer.scx:1119-1125 (dump)`; `pcondpg2.prg:12-64` | **importante**

`RF-031` | **O acionamento do fechamento é a tecla ESC, não um botão.** | `ped_wer.scx:2813-2837 (dump)`: ESC desabilita 13 controles e então, `IF ws>0 → cmdok1.click()`, senão `sair2.Click()`. O botão `cmdok1` tem `Visible = .F.`. A versão web deve ter um comando explícito de "Fechar pedido" e um de "Cancelar", distintos e rotulados. | `ped_wer.scx:2813-2837 (dump)`, `:5925-5972 (dump)` | **importante**

---

## 4. Inventário de campos — CABEÇALHO

Nenhum controle de cabeçalho grava direto em tabela. Todos alimentam variáveis de memória, e o `pcondpg2` as transporta para `pedido`, repetindo em cada item.

| # | Campo na tela | Objeto (`ped_wer.scx`) | Tipo/tam. na tela | Origem | Obrigatório | Destino em `pedido` | Onde grava |
|---|---|---|---|---|---|---|---|
| C1 | Tipo | `wtipo_oper` | `XXX`, `Format="!"` | default `wareas.tp_pedido`; digitado; F5 = `listtipo` | **sim**, tem de existir em `tipos` | `TIPO_OPER` C(3) via `wtpmov` | `pcondpg2.prg:255` |
| C2 | Empresa | `wc_empr` | `XX`, `Format="!"` | default `"  "`; forçado por `tipos.cod_empre` ou `wwempre_user`; F5 = `listEMP` | não (vazio = empresa base) | `COD_EMPR` C(2) | `pcondpg2.prg:260` |
| C3 | Documento | `WCDOCUMENT` | `XXXXXXXXXX`, `Format="!"` | pré-preenchido por `acumula_documento`; **sobrescritível**; vazio+ENTER = `lista_pedido()` | **sim** | `CODIGO` C(10) via `wdocum` | `pcondpg2.prg:256` |
| C4 | Data | `Text11` | data, `Value=(DATE())` | default hoje; digitável | **sim** | `DATA_PED` D via `wdata_mov`/`PEDTEMP.data_ped` | `ped_wer.scx:307 (dump)`; `pcondpg2.prg:268` |
| C5 | Cliente | `wcli_for` | `XXXXXXX` (7) | digitado; `+` abre cadastro; vazio+ENTER = `pesqcli` | **sim**, tem de existir em `clientes` | `COD_CLI` C(7) via `wclie` | `pcondpg2.prg:289` |
| C6 | (nome do cliente) | `Text5` | exibição | `clientes.razao_soc` | — | — | — |
| C7 | (empresa corrente) | `Text6` | exibição, `Enabled=.F.` | `wareas.nome_empr` | — | — | — |
| C8 | (% desconto do cliente) | `Text10` | exibição | `clientes.perc_desc` | — | — | — |
| C9 | Itens / Total | `qwtotal` / `nwtotal` | `999,999,999.99`, `ReadOnly` | acumulado | — | — | — |

Campos de cabeçalho que **não** têm controle na tela e são gravados por default:

| Destino | Valor | Onde |
|---|---|---|
| `ES_MOV` C(1) | `"S"` se `tipos.tipo_es="S"`, senão `"E"` | `pcondpg2.prg:330-335` |
| `COD_EMP_OR` C(2) | `wcod_empre_ori` (empresa de origem, transferência) | `pcondpg2.prg:245` |
| `PERC_VEND` N(10,2) | `wareas.cons_min` — **vazio nesta base**, grava 0 | `pcondpg2.prg:259` |
| `COMPRADOR` C(20) | `WIDENTIFIC` (o usuário logado) | `pcondpg2.prg:257` |
| `MATERIAL03` C(3) | número sequencial do item no pedido | `pcondpg2.prg:356` |
| `QTDITENSPE` N(6) | total de itens do pedido | `pcondpg2.prg:274`; reescrito em `fechapedwer.scx:224 (dump)` |
| `COD_PROJ` C(7) | `WCOD_PROJ` | `pcondpg2.prg:290` |
| `POSICAO` C(10) | `tipos.posicao` (vazio) ou `wareasb.bloq_pedid` | `pcondpg2.prg:346-350` |

`RF-040` | **Todo campo de cabeçalho hoje só exibido deve ser exposto como leitura pela API.** | `Text5`, `Text6`, `Text10`, `qwtotal`, `nwtotal` não são persistidos; são derivados. O contrato da API deve devolvê-los calculados, e não exigir que o cliente web os recomponha. | `ped_wer.scx` REC 15, 30, 38, 52 (dump) | **desejável**

`RF-041` | **`COMPRADOR` está sendo usado como campo de auditoria.** | `pcondpg2.prg:257` grava `WIDENTIFIC` (usuário logado) num campo cujo nome indica "comprador do cliente". `FATO MEDIDO`: é o único registro do autor dentro da própria tabela `pedido`. O modelo novo deve ter campo de autor próprio e liberar `COMPRADOR` para o seu significado. | `pcondpg2.prg:257` | **importante**

`RF-042` | **`PERC_VEND` grava zero por configuração vazia e não deve ser herdado.** | `wareas.CONS_MIN` está vazio (`FATO MEDIDO`) e `pcondpg2.prg:259` grava esse vazio em `pedido.perc_vend`. Nenhum consumidor de comissão lê esse campo. | `pcondpg2.prg:259`; `Fox\WER\WAREAS.DBF` | **desejável**

---

## 5. Inventário de campos — ITEM

### 5.1 Controles de item na tela

| # | Campo | Objeto | Tipo/tam. | Visível? | Origem | Obrigatório |
|---|---|---|---|---|---|---|
| I1 | Código do Produto | `Text1` | `XXXXXXXXXXXXXXXXXXXX` (20), `Format="!"` | sim | digitado; `+` abre `cadmat`; vazio+ENTER = pesquisa | **sim** |
| I2 | (descrição) | `Text8` | exibição | sim | `cadmat.descricao + cadmat.caracter` | — |
| I3 | Quantidade | `txtAddText` | `999999999.999` | sim | digitada, **ou calculada** para M2/ML/M3 | **sim**, ≠ 0 |
| I4 | Valor Unitário | `Text2` | `9999999.99` | sim | semeado por `Text1.LostFocus`; sobrescrito por negociação; digitável sob alçada | **sim**, ≠ 0 |
| I5 | Tabela de preço | `Text3` | `X`, valores 1..6 | **`Visible=.F.`** | fixado em `"1"` em `Activate` | — |
| I6 | Reserva | `Text4` | `9999999.999` | **`Visible=.F.`** | `wreserva` | — |
| I7 | Prazo de entrega | `Text7` | numérico ou data | **`Visible=.F.`** | `wprazo` | — |
| I8 | Desconto (%) | `wdesc_ind` | `99999.99` | **`Visible=.F.`** | `wdesc_ind` | — |
| I9 | Observação do item | `Edit1` | memo | só se `wareas.ind_obs="S"` (**"N"**) | `cadmat.obs` ou descrição | — |
| I10 | Item (empenho) | `Text9` | — | só se `tipos.ind_empenho="S"` (**vazio em 487**) | — | — |

`RF-050` | **A tela real de item tem quatro campos, não dez.** | `FATO MEDIDO` — `Text3`, `Text4`, `Text7`, `wdesc_ind` têm `Visible = .F.` no projeto (`ped_wer.scx:4633, 4744, 4862, 4936 (dump)`) e nada no fluxo de venda os torna visíveis; `Edit1` depende de `wareas.ind_obs="S"`, medido `"N"`; `Text9` depende de `tipos.ind_empenho="S"`, medido vazio nos 487 tipos. **O ciclo de item vivo é: produto → quantidade → (preço) → confirma.** A versão web não deve reconstruir os seis campos ocultos sem decisão explícita. | `ped_wer.scx` REC 18,19,23,25,21,28 (dump); `Fox\WER\WAREAS.DBF`, `tipos.dbf` | **essencial**

`RF-051` | **O desconto por percentual de item (`wdesc_ind`) está oculto e sua lógica é incoerente; não deve ser portado como está.** | Está invisível (`:4936`), mas o caminho existe: `:4956-4970` pede senha de SUPERVISOR acima de `wareas.lim_desc`; `totaliza:332` aplica `text2.Value - ROUND(text2.Value*wdesc_ind/100, 5)`; `pcondpg2.prg:233` grava em `pedido.desc_ind`. Problema estrutural: **o desconto por percentual não alimenta a escolha da faixa de comissão** (§7). Se a versão web reintroduzir o campo, tem de alimentar a mesma variável de desconto que a escada consome. | `ped_wer.scx:4918-4970 (dump)`, `:332 (dump)`; `pcondpg2.prg:233` | **importante** `⚠ DIVERGE DO CÓDIGO ATUAL` |

### 5.2 Esquema do item em digitação (cursor `PEDTEMP`)

`FATO MEDIDO` — `ped_wer.scx:2415-2422 (dump)`, 62 campos. É o esquema real do "item em digitação" e a base para o modelo de rascunho da versão web.

```
BARRA C(16), PRODUTO M, WVITEM C(10), WVMARCA C(10), data_ped D, custo_merc N(10,3),
recno_ped N(10), iss N(7,2), ipi N(10,5), icm N(7,2), perc_icm N(7,2), icmreduc N(7,2),
wvunidade C(5), wwaltemp C(1), qtd_liber N(14,3), qtd_emb N(8,3), grupo C(4),
referencia C(10), ind_venda C(1), qtd_larg N(7,3), qtd_comp N(7,3), qtd_acres N(7,3),
qtd_med N(7,3), NUM_PED C(10), obs_nf C(10), transp_nf C(4), cod_trans C(4),
codigo_cli C(13), cod_vend C(4), cod_vend1 C(4), cfop C(4), cst C(3), csosn C(1),
vl_pis N(12,2), vl_cofins N(12,2), Hora_Ini C(8), Hora_Fim C(8), qtd_infor N(10),
desc_icm N(12,2), cstpis C(2), aliqpis N(5,2), cstcof C(2), aliqcof N(5,2),
cstipi C(2), desonera C(1), REG_ITEM C(15), IBPT N(5,2), IBPTEST N(5,2),
IBPTMUN N(5,2), PERC_IMP N(5,2), STATUS C(1), INTEGRADO C(1), NUM_ITEM C(10),
qtd_unit N(10,3), vl_unit N(15,5), cenqipi C(3), temdifa L, qtd_conf N(12,3),
qtd_orig N(10,3), qt_reserva N(10,3), valordifa N(12,2), separador C(4),
qt_volumes C(12), redbasest N(5,2), alpis_iss N(6,2), alcof_iss N(6,2),
cbenef C(8), extipi C(3)
```

`RF-052` | **O rascunho de pedido precisa ser persistente e por usuário.** | Hoje o `PEDTEMP` é cursor em memória, criado no `Activate` e destruído com a tela (`ped_wer.scx:2405-2430 (dump)`, com `ZAP` na reentrada). Se a estação cair, o pedido em digitação é perdido — mas o número já foi consumido (RF-060) e a reserva de estoque já foi gravada (RF-028). Na versão web o rascunho deve ser persistido com identidade própria, dono, e política de expiração que **estorne a reserva**. | `ped_wer.scx:2405-2430 (dump)` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-053` | **Três campos do `PEDTEMP` têm largura menor que o destino e truncam.** | `FATO MEDIDO`: `PEDTEMP.qtd_larg N(7,3)` contra `pedido.QTD_LARG N(12,3)`; `PEDTEMP.qtd_comp N(7,3)` contra `pedido.QTD_COMP N(12,3)`; `PEDTEMP.codigo_cli C(13)` contra `pedido.CODIGO_CLI C(16)`; `PEDTEMP.cbenef C(8)` contra `pedido.CBENEF C(10)`. Como `qtd_larg` carrega **preço** (RF-054), um preço de tabela ≥ 10.000,00 não cabe em N(7,3). | `ped_wer.scx:2418-2422 (dump)`; `Fox\WER\PEDIDO.DBF` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

### 5.3 Campos de medida usados para outra finalidade

`RF-054` | **`qtd_larg` e `qtd_comp` não devem carregar preço e percentual de comissão.** | `FATO MEDIDO` — `ped_wer.scx:256-258 (dump)`, com o guard `*If wareascp.ind_largco="S"` **comentado**: `REPLACE qtd_larg WITH ValPrcWer` (preço de tabela ajustado) e `REPLACE qtd_comp WITH curComis.Comis` (percentual de comissão). Ambos são campos de **medida física** no esquema (ao lado de `QTD_ACRES`, `QTD_MED`). No mesmo formulário, `txtAddText.GotFocus` (`:3180-3183`) lê `pedtemp.qtd_larg`/`qtd_comp` como **largura e comprimento** para produtos M2/ML/M3, e `contnota` (`:585`, `:595`) os recarrega de `pedido`. O modelo novo deve ter `precoTabelaAjustado` e `percentualComissao` como campos próprios. | `ped_wer.scx:255-260 (dump)`, `:3180-3183 (dump)`, `:585-595 (dump)`; `pcondpg2.prg:229-230` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-055` | **A colisão está dormente por configuração, não por correção.** | `FATO MEDIDO`: `wareascp.IND_LARGCO` vazio; `wareas.IND_GRADE="N"`; `pedido.QTD_MED = 0` em todos os 102.104 itens; zero itens de produto M2/M³/ML/MT. Basta um produto por metro quadrado entrar em uso para preço e largura colidirem no mesmo campo. | `Fox\WER\wareascp.dbf`, `WAREAS.DBF`, `PEDIDO.DBF` | **importante**

`RF-056` | **A quantidade de produto por área/comprimento/volume é calculada, não digitada.** | `ped_wer.scx:3148-3204 (dump)`: para `cadmat.unid_mat` em {`M2`,`ML`,`M3`} a tela abre um diálogo de medidas e calcula — `M2`: `((larg+2·acr) × (comp+2·acr)) × qtd`; `ML`: `((larg+acr)·2 + (comp+acr)·2) × qtd`; `M3`: `(larg × comp × acr) × qtd`. Zeros são forçados a 1 (`:3165-3172`, `:3190-3201`). `INERTE` nesta base (zero itens dessas unidades), mas é regra de negócio real do ERP. | `ped_wer.scx:3148-3204 (dump)` | **importante**

`RF-057` | **Produto sem decimais deve recusar quantidade fracionária.** | `ped_wer.scx:3065-3071 (dump)`: `If cadmat.ind_decim="N" And This.Value <> Int(This.Value)` → mensagem `"PRODUTO NÃO OPERA COM DECIMAIS !"`, zera a quantidade, prende o foco (`RETURN 0`). | `ped_wer.scx:3065-3071 (dump)` | **importante**

### 5.4 Campos fiscais do item

`RF-058` | **Os campos fiscais do item vêm de três origens e a precedência importa.** | `totaliza` grava em `PEDTEMP` (`ped_wer.scx:269-301 (dump)`) e `pcondpg2.prg:214-224` transporta para `pedido`. Origens: (a) `DO veicm3` (`:86`) resolve ICMS/IPI; (b) `strcfop`/`strcst`/`strorigem` (`:103-118`) vêm de variáveis "aux" preenchidas por telas auxiliares, com *fallback* em `tipos.natureza` e `cadmat.cod_proc`; (c) `DO FORM frmipiicm` (`:120`, sob `wareas.ind_pre="S"` — **medido "S"**) permite o operador **editar** CST/alíquotas de PIS/COFINS/IPI item a item. | `ped_wer.scx:86-124 (dump)`, `:269-301 (dump)`; `pcondpg2.prg:214-224` | **essencial**

`RF-059` | **O bloco de gravação fiscal está duplicado no código e o segundo sobrescreve o primeiro.** | `FATO MEDIDO`: `ped_wer.scx:270-288 (dump)` grava CFOP e CST/CSOSN decidindo por `pbIndsimples`; `:290-301 (dump)` **repete literalmente** os mesmos `REPLACE` decidindo por `wareasb.indsimples`. Vence o segundo. `wareasb.INDSIMPLES = "F"` nesta base. A versão web deve ter uma única decisão, com a fonte do indicador de regime explicitada. | `ped_wer.scx:270-301 (dump)`; `Fox\WER\wareasb.dbf` | **importante** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-060` | **`redbasest` nunca é preenchido no lançamento, e a baixa depende dele.** | `PEDTEMP` tem `redbasest N(5,2)` (`ped_wer.scx:2422 (dump)`) e `pedido` tem `REDBASEST N(5,2)`, mas **nenhum fonte WER escreve o campo**, e `pcondpg2` não o transporta. Consequência medida na análise (DF-R2, §4.6 de `analise_wer_app.md`): o ST é calculado sobre base cheia. **O modelo novo deve resolver a redução de base a partir do cadastro (`cadsub.icm_reduc`) no momento do item, e persistir o percentual efetivamente usado.** | `analise_wer_app.md` §4.6 DF-R2; `pcondpg2.prg:214-224` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

---

## 6. Formação de preço, desconto e comissão

A análise (`analise_wer_app.md` §4.1 e §4.2) descreve a cadeia. Esta seção especifica o contrato que a versão web tem de cumprir e corrige o que está errado.

### 6.1 Preço de tabela ajustado

`RF-070` | **O preço de tabela é o preço do produto na empresa corrente, ajustado por três fatores do cliente, com arredondamento a cada etapa.** | `ped_wer.scx:4244-4292 (dump)`, literal: <br>`ValPrcWer = Cadmat.Prc_Venda` <br>`IF !EMPTY(Clientes.Inss) → ValPrcWer = ROUND(ValPrcWer * ((100 + Clientes.INSS)/100), 2)` <br>`IF !EMPTY(Clientes.IRRF) → ValPrcWer = ROUND(ValPrcWer * ((100 - Clientes.IRRF)/100), 2)` <br>`IF !EMPTY(Clientes.ISS) → ValPrcWer = ROUND(ValPrcWer * ((100 - Clientes.ISS)/100), 2)` <br>`thisform.text2.value = ValPrcWer` <br>A ordem é significativa: são multiplicações com `ROUND(...,2)` intercalado, logo não comutam. | `ped_wer.scx:4244-4292 (dump)` | **essencial**

`RF-071` | **Os três fatores são majoração e reduções, com nomes de domínio distintos dos nomes físicos.** | `FATO MEDIDO` (§4.5 de `analise_wer_app.md`, confirmado por coordenada de rótulo em `frmcliwer.scx`): `clientes.INSS` = **Percentual de Adição** (majora); `clientes.IRRF` = **Compensação de ICMS** (reduz); `clientes.ISS` = **Desconto Diretoria / Reciclado** (reduz). A API deve expor `percentualAdicao`, `compensacaoIcms`, `descontoDiretoria`. | `ped_wer.scx:4252-4289 (dump)` (comentários literais do fonte) | **essencial**

`RF-072` | **Incidência real dos fatores.** | `FATO MEDIDO` — `Fox\WER\clientes.dbf`, 13.086 clientes vivos: `INSS ≠ 0` em **105**; `IRRF ≠ 0` em **27**; `ISS ≠ 0` em **70**. Não é caso de exceção raro o bastante para ser ignorado, nem regra geral. | `Fox\WER\clientes.dbf` | informativo

`RF-073` | **O seletor de tabela de preço (1..6) existe, está invisível e é fixado em "1".** | `ped_wer.scx:2574 (dump)`: `THISFORM.text3.VALUE="1"` no `Activate`; o objeto tem `Visible=.F.` (`:4633`). A lógica em `:4651-4694` mapeia 1→`prc_venda`, 2→`prc_venda2`, 3→`prc_venda3`, 4→`prc_venda4`, 5→`pu_mat`, 6→`prc_medio`, somando `ipi_fabr` quando `cadmat.ind_ipi="V"`. `FATO MEDIDO`: `pedido.IND_VENDA = "1"` em 102.100 de 102.104 itens. **Conclusão: a única tabela em uso é `prc_venda`.** O `vepreco` nativo está desativado (`ped_wer.scx:4478-4483 (dump)`). | `ped_wer.scx:2574 (dump)`, `:4651-4694 (dump)`; `Fox\WER\PEDIDO.DBF` | **importante**

`RF-074` | **Quando o tipo de operação amarra a tabela de preço, o campo de valor é bloqueado — e o bloqueio é aplicado duas vezes com regras diferentes.** | `ped_wer.scx:3552-3562 (dump)`: se `tipos.ind_valb` ∈ {1..6}, consulta `caduser` e faz `text2.enabled=.f.` **apenas se `caduser.ind_caixa <> "G"`**. Mas `ped_wer.scx:4576-4580 (dump)` (`Text1.GotFocus`) repete o bloqueio **sem consultar `ind_caixa`**, e roda depois. Efeito líquido: nem o gerente consegue digitar o preço a partir do segundo item. `FATO MEDIDO`: `tipos.IND_VALB` = `"0"` em 453 dos 487 tipos (`"1"`=26, `"5"`=6, `"4"`=1, `"2"`=1) — para os tipos de venda em uso a regra é inerte. **A versão web deve ter uma única regra: preço editável se e somente se o perfil autoriza.** | `ped_wer.scx:3552-3562 (dump)`, `:4576-4580 (dump)`; `Fox\WER\tipos.dbf` | **importante** `⚠ DIVERGE DO CÓDIGO ATUAL` |

### 6.2 Preço negociado

`RF-075` | **A negociação, quando vigente e autorizada, substitui o preço e encerra o item.** | `ped_wer.scx:3214-3240 (dump)`: `dbseek("Cod_Cli+Grupo+Referencia+Tipo_Prc", Clientes.Codigo + Cadmat.Grupo + Cadmat.Referencia + <"E" se SUBSTR(Tipos.Natureza,1,1)="7", senão "N">, "M0", "Negocia2")`; se encontrou **e** `Dt_Venc >= DATE()` **e** `cod_valida = 1` → `thisform.negocia`, que faz `thisform.text2.Value = negocia.preco` (`:2355`) e chama `finaliza_item` (`:2367`). | `ped_wer.scx:3214-3240 (dump)`, `:2345-2368 (dump)` | **essencial**

`RF-076` | **As três guardas devem produzir mensagens distintas, e uma delas hoje é silenciosa.** | `FATO MEDIDO`: existe mensagem para negociação **fora de validade** (`:3237` `"ESSE PRODUTO NEGOCIADO ESTÁ FORA DA DATA DE VALIDADE !"`) e para **não autorizada** (`:3233` `"ESSE PRODUTO NÃO FOI AUTORIZADO PARA NEGOCIAÇÃO !"`). Não há mensagem quando **não existe negociação** — o `IF !EOF()` (`:3227`) simplesmente não entra, e o preço de tabela permanece. Isso é correto e deve ser preservado; o que deve mudar é que os dois casos de falha hoje **avisam e seguem** com o preço de tabela, sem exigir confirmação. | `ped_wer.scx:3227-3239 (dump)` | **importante**

`RF-077` | **O tipo de preço da negociação é derivado do CFOP do tipo de operação.** | `"E"` (exportação) se `SUBSTR(Tipos.Natureza,1,1) = "7"`, senão `"N"` (`ped_wer.scx:3220-3224 (dump)`). É a única discriminação de `TIPO_PRC` no fluxo. | `ped_wer.scx:3220-3224 (dump)` | **importante**

`RF-078` | **O preço negociado deve ter alçada, e hoje não tem.** | O caminho da negociação vai direto de `:2355` para `finaliza_item` em `:2367`, **sem passar pelo `Text2.KeyPress`**, que é onde o teto de desconto é verificado. `FATO MEDIDO` (§4.1 D4 de `analise_wer_app.md`): existe negociação vigente de R$ 0,20 na base, e 18,5% dos produtos têm `Gradecol` vazio (teto efetivo 0%). A versão web deve submeter o preço negociado ao mesmo teto, ou exigir que a **autorização da negociação** (`cod_valida`) seja concedida por perfil com alçada para o desconto que ela implica. | `ped_wer.scx:2345-2368 (dump)`; `analise_wer_app.md` §4.1 D4 | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-079` | **A comparação que gera o percentual de desconto da negociação deve usar bases homogêneas.** | `ped_wer.scx:2366 (dump)`: `ValDescDado = ((ValPrcWer - thisform.text2.Value) / ValPrcWer) * 100`, onde `ValPrcWer` é o preço **ajustado pelos três fatores** e `text2.Value` é `negocia.preco` **cru**. `FATO MEDIDO` (§4.1 D1): 966 de 2.099 itens negociados pertencem a clientes com algum dos três fatores ≠ 0; com `INSS` de 21,5% o sistema calcula 25,8% de desconto onde o desconto real é 9,8%. **A versão web deve decidir e documentar a semântica do preço negociado — preço final ao cliente (comparar cru contra cru) ou preço-base (aplicar os três fatores também nele) — e usar a mesma base nos dois lados da subtração.** Ver §15, lacuna L1. | `ped_wer.scx:2366 (dump)`, `:4244-4292 (dump)`; `analise_wer_app.md` §4.1 D1 | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-080` | **A divisão pelo preço de tabela precisa de guarda.** | `ped_wer.scx:2366 (dump)` divide por `ValPrcWer` sem verificar o denominador; o código gêmeo em `:6081` **tem** o guard (`IF ValPrcWer > 0`, datado 29/04/2025). `FATO MEDIDO` (§4.1 D3): existem 400 negociações vigentes e autorizadas apontando para produto com `cadmat.prc_venda = 0`. O efeito não é queda do sistema (medido em VFP9), é percentual de desconto estourado e **faixa de comissão errada em silêncio**. | `ped_wer.scx:2366 (dump)` × `:6081 (dump)` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-081` | **A negociação é global e o preço de tabela é por empresa; a chave precisa da empresa.** | `FATO MEDIDO` (§4.1 D2): `negocia` tem 8 campos e nenhum é empresa; 3.977 negociações vigentes têm preço de tabela diferente entre `fenwin02` e `fenwin03`. Mesmo preço negociado → percentual de desconto diferente → comissão diferente, conforme a empresa de lançamento. **A chave de negociação na versão web deve incluir a empresa, ou a regra deve declarar explicitamente que o percentual é calculado contra uma tabela de referência única.** | `Fox\WER\NEGOCIA.DBF`; `analise_wer_app.md` §4.1 D2 | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-082` | **Depois do lançamento a negociação não é reconsultada.** | `FATO MEDIDO`: `ped327wer`, `valt2w` e `valida6wer` não contêm a palavra `negocia`. O preço congela em `pedido.prc_venda`. Isto **facilita** a migração: a negociação é serviço de consulta no momento do item, não regra recorrente do faturamento. | `analise_wer_app.md` §4.1 | **importante**

### 6.3 Teto de desconto por item

`RF-083` | **O teto de desconto do item é aditivo: percentual do cliente + percentual da coluna do produto.** | `ped_wer.scx:6034-6056 (dump)`: `=abertura("fenicia","tabcol","N")` e `dbseek("Codigo", Cadmat.Gradecol, "M0", "Tabcol")`; então <br>`IF !EMPTY(Clientes.PIS) → ValPrcWer2 = Clientes.PIS + VAL(TabCol.Nome)` <br>`ELSE → ValPrcWer2 = VAL(TabCol.Nome)` <br>`clientes.PIS` = **Desconto Especial** do cliente; `TabCol.Nome` é campo **caractere** convertido com `VAL()` e representa o teto da coluna de desconto do produto (`cadmat.Gradecol`). | `ped_wer.scx:6034-6056 (dump)` | **essencial**

`RF-084` | **A comparação do teto é feita em valor, não em percentual.** | `ped_wer.scx:6060-6091 (dump)`: teto em valor `ValDesc = ROUND((ValPrcWer * ValPrcWer2)/100, 2)`; desconto concedido em valor `ValDesDadoN = ValPrcWer - preço digitado` (zerado se negativo, `:6067-6071`); percentual derivado `Perc_Desc = (ValDescDado/ValPrcWer)*100` (com guard, `:6081-6086`); e a decisão é `IF ValDescDado <= ValDescNovo`. | `ped_wer.scx:6060-6091 (dump)` | **essencial**

`RF-085` | **Dentro do teto: o percentual é limitado ao teto e o item é confirmado.** | `ped_wer.scx:6095-6104 (dump)`: `IF Perc_Desc > ValPrcWer2 → REPLACE Perc_Desc WITH ValPrcWer2`; mensagem `"DESCONTO MÁXIMO PERMITIDO = " + TRANSFORM(ValPrcWer2,"@R 99.99") + CHR(13) + "DESCONTO CONCEDIDO = " + TRANSFORM(Perc_Desc,"@R 99.99")`; então `finaliza_item`. **Observação: a mensagem é exibida mesmo no caso de sucesso**, o que a torna ruído. Na versão web deve ser informação de tela, não caixa modal. | `ped_wer.scx:6091-6105 (dump)` | **importante**

`RF-086` | **Acima do teto: recusa e restaura o preço de tabela.** | `ped_wer.scx:6106-6117 (dump)`: mesma mensagem, então `THISFORM.OkToLeave = .F.` e **`thisform.text2.Value = ValPrcWer`** — o preço volta ao de tabela e o foco fica preso. O item **não** é confirmado. | `ped_wer.scx:6106-6117 (dump)` | **essencial**

`RF-087` | **Valor unitário zero deve ser recusado com mensagem, e hoje há um caminho que o aceita em silêncio.** | Recusa correta: `ped_wer.scx:6037-6043 (dump)` → `"VALOR INVÁLIDO !"`, prende o foco. Caminho silencioso: `txtAddText.KeyPress:3051-3055 (dump)` — se o preço estiver 0 ao confirmar a quantidade, chama `finaliza_item` **mesmo assim**; `finaliza_item:1859 (dump)` (`IF !EMPTY(THISFORM.text2.VALUE)`) não entra, e o bloco de reset das linhas `2028-2038` está **fora** desse `IF` (balanceamento verificado: profundidade 0 na linha 2028). Resultado: campos limpos, foco de volta ao produto, **nenhum item gravado e nenhuma mensagem**. | `ped_wer.scx:3051-3055 (dump)`, `:1858-1859 (dump)`, `:2026-2038 (dump)` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-088` | **Quantidade zero deve ser recusada.** | `ped_wer.scx:2979-2988 (dump)`: `IF THIS.VALUE=0` → `"QUANTIDADE INVALIDA !"`, `OkToLeave=.F.`, `RETURN`. | `ped_wer.scx:2979-2988 (dump)` | **essencial**

### 6.4 Escada de comissão

`RF-089` | **A comissão do item é um percentual escolhido numa escada desconto→comissão, gravado no item.** | Tabela `tabcomis` (container `negocia.dbc`). `FATO MEDIDO` — `Fox\WER\tabcomis.dbf`, 5 campos (`NEGOCIO N(2)`, `DESC N(15,2)`, `COMIS N(15,2)`, `FIGURA G`, `FIGURA2 C(30)`), 10 registros: <br><br>`NEGOCIO = 1`: desc 0,00→5,00 · 10,00→3,00 · 20,00→2,00 · 30,00→2,00 · 99,99→2,00 <br>`NEGOCIO = 2`: desc 0,00→10,00 · 10,00→7,00 · 20,00→5,00 · 30,00→3,00 · 99,99→3,00 | `Fox\WER\tabcomis.dbf` | **essencial**

`RF-090` | **A escada é escolhida pelo tipo do vendedor do CADASTRO DO CLIENTE, não pelo vendedor do pedido.** | `ped_wer.scx:183-200 (dump)`: `dbseek("cod_vend", clientes.cod_vendor, "M0", "vendedor")` e então `IF vendedor.tipo_vend = "V" → WHERE negocio = 1`, `ELSE → WHERE negocio = 2`. `totaliza` roda **durante a digitação do item**, e o vendedor do pedido só é decidido depois, no fechamento (`ComisVdawer`, §8.4). Portanto: se o operador trocar o vendedor no fechamento, **a faixa de comissão continua a do vendedor default do cliente**. `HIPÓTESE` quanto ao impacto financeiro (não medido); `FATO MEDIDO` quanto ao código. **A versão web deve decidir e documentar qual vendedor determina a escada, e resolver a escada depois de o vendedor estar definido.** Ver §15, lacuna L2. | `ped_wer.scx:183-200 (dump)`; `ComisVdawer.SCX:118-119 (dump)`; `pcondpg2.prg:246-247` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-091` | **O `ELSE` da escolha da escada é implícito e paga o dobro para dois terços do cadastro.** | `FATO MEDIDO` — `Fox\WER\vendedor.DBF`, 193 registros: `R`=118, `I`=62, **`V`=10**, vazio=3. Só os 10 do tipo `V` caem em `negocio=1`; os outros **183 (94,8%)** caem no `ELSE` → `negocio=2`, que paga o dobro. O código comentado em `:209`, `:216` testava `"R"` explicitamente. **A versão web deve exigir mapeamento explícito de cada tipo de vendedor para uma escada, e recusar tipo não mapeado.** | `ped_wer.scx:188-200 (dump)`, `:209-216 (dump)`; `Fox\WER\vendedor.DBF` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-092` | **O algoritmo de busca da faixa seleciona o degrau IMEDIATAMENTE SUPERIOR ao desconto.** | `ped_wer.scx:241-251 (dump)`, literal: <br>`GO TOP` / `DO WHILE !EOF()` / `IF lcValorDesc = Desc → EXIT` / `ELSE SKIP` / `IF lcValorDesc <= Desc → EXIT` <br>Sobre a escada `negocio=1` (índice `STR(negocio,2,0)+STR(desc,15,2)`, `:202`): desconto 0% → 5,00%; 5% → **3,00%**; 15% → **2,00%**; 10% → 3,00% (degrau exato). **A versão web deve especificar a regra em uma frase e testá-la nos degraus e entre eles.** | `ped_wer.scx:202 (dump)`, `:241-251 (dump)`; `Fox\WER\tabcomis.dbf` | **essencial**

`RF-093` | **A variável que alimenta a escada tem duas unidades diferentes conforme o caminho, e a seleção entre elas depende de estado residual.** | `ped_wer.scx:235-239 (dump)`: `IF bolSeek = .T. → lcValorDesc = curtemp.Perc_Desc  ELSE  lcValorDesc = ValDescDado`. <br>• `curtemp.Perc_Desc` é **percentual** (`:6082`). <br>• `ValDescDado` é **percentual** quando vem de `negocia:2366`, e o campo homônimo do cursor `curtemp.ValDescDado` é **valor absoluto** (`:6068`) — nomes iguais, unidades diferentes. <br>• `bolSeek` é `PUBLIC` (`:2626`), **não é inicializado no `Load`**, vale `.T.` só entre `:6032` e `:6105`/`:6117`, e é deixado `.F.` em todas as saídas do `Text2.KeyPress`. <br>Consequência: no caminho padrão de dois ENTER (produto → quantidade, sem visitar o campo de valor — `:3058`), `bolSeek` é `.F.` e a escada recebe `ValDescDado`, que **retém o percentual do item anterior** quando esse item passou por negociação (`:2364-2366` grava e nada zera depois). `HIPÓTESE` (leitura de código; não reproduzido em laboratório). **A versão web deve calcular o desconto do item como um valor único, em uma única unidade (percentual), a partir do preço de tabela e do preço praticado, sem estado entre itens.** | `ped_wer.scx:235-239 (dump)`, `:2364-2366 (dump)`, `:2626 (dump)`, `:3058 (dump)`, `:6032-6117 (dump)` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-094` | **O desconto por percentual de item não entra na escada nem na base de comissão.** | `FATO MEDIDO` (§4.2 D8 de `analise_wer_app.md`, confirmado): `wdesc_ind.KeyPress` (`ped_wer.scx:4956-4970 (dump)`) não escreve em `ValDescDado`; a escada já foi resolvida em `totaliza:241-251` com o desconto derivado do preço; e `pcondpg2.prg:272` grava `prc_venda` **cheio**, com o desconto em campo separado (`desc_ind`, `:233`). Resultado: o desconto por percentual reduz a nota e **não** reduz o percentual de comissão nem a base. | `ped_wer.scx:4956-4970 (dump)`, `:241-251 (dump)`; `pcondpg2.prg:233`, `:272` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-095` | **A divergência de arredondamento entre inclusão e alteração de item (D7 da análise) é INALCANÇÁVEL no código atual.** | `FATO MEDIDO` por verificação de balanceamento de `IF/ENDIF` de `ped_wer.scx:1858` a `:2040 (dump)`: o segundo algoritmo de busca de faixa (`:1945-1969`, que faz `SKIP -1` e seleciona o degrau **anterior**) está em **profundidade 2**, dentro de `IF THISFORM.pwaltera` (`:1860`), cujo `ELSE` está em `:1972`. E `pwaltera` nunca é `.T.` (ver RF-170). **Logo o par 3,00% × 5,00% não pode ser produzido por ação do operador.** A análise registra D7 (§4.2) e R2 (§6.4) como achados independentes; eles são mutuamente excludentes, e R2 prevalece por dupla prova. **O requisito permanece:** a versão web deve ter **um único** algoritmo de faixa. O que muda é o diagnóstico — o defeito que produz faixa errada em operação é RF-093, não RF-095. | `ped_wer.scx:1858-2040 (dump)`; `analise_wer_app.md` §4.2 D7 e §6.4 R2 | **essencial** |

`RF-096` | **A base de cálculo da comissão é mercadoria pura.** | `FATO MEDIDO` (§4.2 de `analise_wer_app.md`): `quantidade × preço unitário × qtd_comp ÷ 100`, sem IPI, sem ST, sem frete, sem seguro e sem desconto. Existe alternativa comentada em `ped327wer.prg:462` (`&& wval_com_ipi`), não adotada. | `analise_wer_app.md` §4.2 | **importante**

`RF-097` | **Nenhum valor de comissão em reais é persistido.** | Grava-se **percentual** (`pedido.qtd_comp` → `cadmov.qtd_comp`); os consumidores recalculam na leitura. Consequência para a versão web: corrigir um percentual gravado **reescreve comissão já paga sem deixar rastro**. Toda alteração de `qtd_comp` de item já baixado deve ser auditada. | `ped_wer.scx:258 (dump)`; `pcondpg2.prg:230`; `ped327wer.prg:1023` | **essencial**

---

## 7. Numeração do pedido

### 7.1 Como o número é obtido hoje

`RF-100` | **O número é composto por prefixo de empresa + contador, truncado em 10 caracteres, com duas séries.** | `acumula_documento` (`ped_wer.scx:2205-2274 (dump)`): <br>`wcontnot = wareas.cont_ped` (venda) ou `wareas.cont_COM` (compra) <br>`wchave = LEFT(ALLTRIM(wareas.comp_seq) + wcontnot, 10)` <br>então um laço `DO WHILE .not.eof()` sobre `pedido` (`:2224-2234`) incrementa até achar número livre <br>e finalmente `wdocum = LEFT(ALLTRIM(wareas.comp_seq) + <contador>, 10)` + `REPLA cont_ped WITH LTRIM(STR(VAL(<contador>)+1))` + `=tableupdate()`. | `ped_wer.scx:2205-2274 (dump)` | **essencial**

`RF-101` | **O número é reservado quando a TELA ABRE, antes de qualquer dado.** | `ped_wer.scx:2519-2533 (dump)`, no `Activate`: se `wareas.ind_ped_dc <> "N"` → `THISFORM.acumula_documento` e o campo é pré-preenchido. `FATO MEDIDO`: `wareas.IND_PED_DC = "S"`. Consequência: abrir e fechar a tela consome contador. | `ped_wer.scx:2519-2533 (dump)`; `Fox\WER\WAREAS.DBF` | **essencial**

`RF-102` | **O contador automático está praticamente sem uso: o operador digita o código.** | `FATO MEDIDO`: `WCDOCUMENT` tem `InputMask = "XXXXXXXXXX"` e `Format = "!"` (`ped_wer.scx:3620-3623 (dump)`) — dez caracteres livres, maiúsculas. `wareas.CONT_PED = 39` e `wareas.COMP_SEQ` **vazio**, contra **35.499 códigos distintos** em `pedido`. Amostra real de `pedido.CODIGO`: `MR518A`, `JP880AD`, `AB2557A`, `ZRC0612`, `011PACTUAL`, `ZEEURICO`, `XANDE`, `WWWBIG1`. **O código do pedido é, na prática, uma referência escolhida pelo operador (aparentemente iniciais + sequência), não um número de sistema.** | `ped_wer.scx:3620-3623 (dump)`; `Fox\WER\WAREAS.DBF`, `PEDIDO.DBF` | **essencial**

### 7.2 Riscos de concorrência e integridade

`RF-103` | **Não há travamento nem transação na obtenção do número.** | `FATO MEDIDO`: `acumula_documento` usa `=CURSORSETPROP('Buffering', 5, 'wareas')` + `=tableupdate()` (`ped_wer.scx:2241-2270 (dump)`) — *row buffering* otimista, **não** é *lock*. Não há `RLOCK`, `FLOCK` nem `BEGIN TRANSACTION` em nenhum ponto do fluxo de lançamento. O laço `:2224-2234` protege contra número **já gravado**, não contra duas estações que leem o mesmo contador no mesmo instante. | `ped_wer.scx:2205-2274 (dump)` | **essencial**

`RF-104` | **A unicidade do código não é garantida por estrutura.** | Não existe índice único. A detecção de duplicidade é **interativa**: `ped_wer.scx:3656-3702 (dump)` faz `dbseek` com `SET EXACT OFF`, e se achou oferece `A/E/T`. Se o operador responder `T` (trocar), o campo é apenas limpo (`:3795-3796`) — **nada** impede que ele digite outro código já existente com outro tipo de operação. Note que a chave real inclui `tipo_oper`, logo o mesmo `CODIGO` sob tipos diferentes é **legítimo**. | `ped_wer.scx:3656-3702 (dump)`, `:3794-3797 (dump)` | **essencial**

`RF-105` | **A busca de duplicidade usa comparação por prefixo.** | `ped_wer.scx:3659-3661 (dump)`: `SET EXACT OFF` antes do `dbseek("codigo", THIS.VALUE, "M0", "pedido")`, `SET EXACT ON` depois. Com `EXACT OFF`, digitar `"AB25"` casa com `"AB2557A"`. Isso faz a tela oferecer `A/E/T` sobre um pedido **diferente** do que o operador quis. Idêntico padrão em `delecao` (`:661-663`) e `valt2w.prg:12-14`. | `ped_wer.scx:3659-3661 (dump)`, `:661-663 (dump)`; `valt2w.prg:12-14` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-106` | **A devolução do número não funciona quando o código tem prefixo alfanumérico.** | `fechapedwer.scx:1379-1404 (dump)`: `if ltrim(wareas->cont_ped) = ltrim(str(val(wdocum) + 1))` → só então devolve. `VAL()` sobre `"MR518A"` devolve **0**, a igualdade nunca se verifica e **o número é queimado a cada cancelamento**. O mesmo padrão em `acertanumped` (`ped_wer.scx:2286-2313 (dump)`), que ao menos extrai a parte numérica com `SUBSTR` a partir do tamanho de `comp_seq` — mas `comp_seq` está vazio, então extrai o código inteiro. | `fechapedwer.scx:1379-1404 (dump)`; `ped_wer.scx:2286-2313 (dump)` | **importante** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-107` | **O contador é decrementado em dois pontos que podem ambos executar.** | `subtrai_documento` (`ped_wer.scx:2275-2285 (dump)`) é chamada nos ramos `A` (`:3705`) e `E` (`:3762`) e decrementa **sem verificação**; `acertanumped` (`:2286-2315`) é chamada em `Sair2.Click` em `:3284` (quando o operador responde que **não** quer sair) e em `:3297` (quando sai). Reabrir um pedido existente e desistir pode decrementar o contador de um pedido novo. | `ped_wer.scx:2275-2315 (dump)`, `:3284-3297 (dump)`, `:3705 (dump)`, `:3762 (dump)` | **importante** `⚠ DIVERGE DO CÓDIGO ATUAL` |

### 7.3 O que a versão web deve garantir

`RF-108` | **A numeração deve ser transacional e o número só deve ser consumido na GRAVAÇÃO.** | Requisito derivado: sequência do banco (ou `SELECT ... FOR UPDATE` sobre a linha do contador) obtida dentro da mesma transação que cria o pedido. Enquanto o pedido está em digitação, o rascunho tem identidade própria (RF-052) e **nenhum** número de pedido. Isso elimina simultaneamente RF-101, RF-103, RF-106 e RF-107. | derivado de RF-100 a RF-107 | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-109` | **Devem coexistir dois identificadores: o número interno e a referência do operador.** | O dado medido (RF-102) mostra que o campo `CODIGO` cumpre hoje o papel de **referência comercial livre**. Suprimir essa liberdade quebra a operação; mantê-la como chave primária impede unicidade e ordenação. Solução: identidade interna sequencial e imutável + campo `referenciaExterna` C(10..20) livre, indexado, com unicidade opcional por (empresa, tipo de operação). Ver §15, lacuna L3. | `Fox\WER\PEDIDO.DBF`; `ped_wer.scx:3620-3623 (dump)` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-110` | **O número da nota fiscal da baixa é outro contador, com o mesmo problema.** | `ped327wer.prg:118-121`: `wnt_fisc = wareas.prox_nota` / `wcont_nota = VAL(wareas.prox_nota) + 1` / `wnt_fisc = LTRIM(STR(wcont_nota)) + SPACE(wnf)` — o valor efetivamente usado é **`prox_nota + 1`**, alinhado à esquerda com espaços à direita. Só ocorre se `wareas.ind_nota = "S"` e `wped_vend = "S"`. `FATO MEDIDO`: `wareas.IND_NOTA = "S"`, `wareas.PROX_NOTA = 77779`. Fora deste escopo, mas registrado porque compartilha a fronteira transacional. | `ped327wer.prg:118-126`; `Fox\WER\WAREAS.DBF` | **importante**

---

## 8. Autorizações e alçadas

### 8.1 Porta de entrada

`RF-120` | **A única verificação de permissão por função nomeada está no menu, não na tela.** | `FATO MEDIDO`: varredura do despejo de `ped_wer.scx` e de `fechapedwer.scx` — **zero** ocorrências de `vefuncao` e `vefuncao2`. A guarda é `vefuncao("Pedido Wer")` em `menuwer.scx :: Command8.Click`, acompanhada de duas condições: se `wwempre <> "  "` e `EMPTY(caduser.cod_empr)`, reabre bases; e se `!EMPTY(wwempre)` e `EMPTY(wwempre_user)`, recusa com `"ESTA FUNÇÃO SÓ É PERMITIDA PARA O ESTOQUE PRINCIPAL"`. | `menuwer.scx :: Command8.Click`; `analise_wer_app.md` §3.1 | **essencial**

`RF-121` | **Toda operação da API deve reverificar a permissão; hoje a autorização é só de abertura de tela.** | Requisito derivado de RF-120: em HTTP não existe "tela aberta". Cada endpoint (criar rascunho, adicionar item, excluir item, fechar, alterar, cancelar) precisa de verificação própria. | derivado | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

### 8.2 Senha de GERENTE — alterar e excluir pedido gravado

`RF-122` | **Alterar ou excluir pedido gravado exige senha de GERENTE quando o tipo de operação o exige.** | `ped_wer.scx:3735-3743 (dump)` (ramo `A`) e `:3777-3784 (dump)` (ramo `E`): `IF ind_senha="S" → THISFORM.vesenha`; `vesenha` (`:1160-1166`) faz `WIDENT="GERENTE"` e `thisform.senha_gerente.click()`. Recusa (`werro=3`) → volta ao `Activate`, recomeçando a tela. | `ped_wer.scx:1160-1166 (dump)`, `:3735-3743 (dump)`, `:3777-3784 (dump)` | **essencial**

`RF-123` | **Na prática essa proteção não existe: só 1 dos 487 tipos a exige.** | `FATO MEDIDO` — `Fox\WER\tipos.dbf`: `IND_SENHA = "S"` em **1** registro, vazio em 486. Como o tipo de venda em uso (`wareas.tp_pedido = "PED"`) não está entre eles, **alterar e excluir pedido gravado é hoje operação sem senha**. **A versão web deve exigir alçada para alterar e para cancelar pedido gravado, independentemente de configuração por tipo.** | `Fox\WER\tipos.dbf`, `WAREAS.DBF` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

### 8.3 Senha de SUPERVISOR

| Ponto | Condição | Efeito da recusa | Origem | Estado |
|---|---|---|---|---|
| Desconto de item acima do teto global | `wareas.lim_desc <> 0 AND wped_vend="S" AND wareas.lim_desc < wdesc_ind` | zera o desconto, prende o foco | `ped_wer.scx:4956-4970 (dump)` | **INERTE** — `lim_desc = 99,99` |
| Desconto de capa, modo percentual | `wareas.lim_desc <> 0 AND wpv1="P" AND wareas.lim_desc < wdesconto` | zera o desconto | `fechapedwer.scx:649-659 (dump)` | **INERTE** — idem |
| Desconto de capa, modo valor/abatimento | `wwnwtotal * wareas.lim_desc/100 < wdesconto` | zera o desconto | `fechapedwer.scx:660-672 (dump)` | **INERTE** — idem |
| Troca de condição de pagamento (digitada) | `This.Value <> OLD_COND` | restaura `OLD_COND`, limpa `ALTER_COND_PAG`/`ALTER_GERENTE` | `fechapedwer.scx:758-776 (dump)` | **ATIVA** |
| Troca de condição de pagamento (por lista) | `vencim.CODIGO <> OLD_COND` | restaura código e descrição anteriores | `fechapedwer.scx:825-847 (dump)` | **ATIVA** |
| Troca do vendedor 1 | `THIS.VALUE <> wcod_vdpd AND widentific <> "SUPORTE" AND !EMPTY(this.Value)` | restaura `wcod_vdpd` | `ComisVdawer.SCX:207-226 (dump)` | **ATIVA** |
| Condição de pagamento com valor abaixo do mínimo | `Vencim.lim_venda <> 0 AND wtotger2 < Vencim.lim_venda` | limpa a condição, devolve o foco | `fechapedwer.scx:1059-1074 (dump)` | **INERTE** — `lim_venda = 0` nos 99 registros |

`RF-124` | **O teto global de desconto está configurado em 99,99% e neutraliza três alçadas.** | `FATO MEDIDO`: `wareas.LIM_DESC = 99.99`. Como as três verificações comparam `lim_desc < desconto`, só um desconto acima de 99,99% dispararia a senha. **A alçada real de desconto neste cliente é apenas a de item, por coluna de produto + cliente (RF-083), que não pede senha — apenas recusa.** | `Fox\WER\WAREAS.DBF`; `ped_wer.scx:4959 (dump)`; `fechapedwer.scx:649-672 (dump)` | **essencial**

`RF-125` | **A troca de condição de pagamento é a única alçada efetivamente exercida no fechamento, e ela é auditada.** | `fechapedwer.scx:1162-1178 (dump)`: se `wrespalt == "A"` e `ALTER_COND_PAG` não está vazio, grava `DO GRAVALOG WITH "ALT CND PE","A",ALLTRIM(wdocum),xLOG` com `xLOG = "Condição " + <antiga> + " alterada para " + <nova>`, temporariamente substituindo `wident_alt` pelo nome do gerente. **O log só é gravado quando `wrespalt == "A"` (alteração de pedido existente); numa inclusão nova a troca passa pela senha e NÃO gera log.** | `fechapedwer.scx:1162-1178 (dump)`, `:758-776 (dump)` | **importante** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-126` | **O usuário `SUPORTE` é isento da senha de troca de vendedor.** | `ComisVdawer.SCX:208 (dump)`: `IF THIS.VALUE <> wcod_vdpd AND widentific <> "SUPORTE" AND !EMPTY(this.Value)`. É uma exceção por nome de usuário embutida no código. **A versão web deve tratar isenções por perfil/permissão, nunca por comparação com um literal.** | `ComisVdawer.SCX:208 (dump)` | **importante** `⚠ DIVERGE DO CÓDIGO ATUAL` |

### 8.4 Vendedores do pedido

`RF-127` | **O pedido de venda exige dois vendedores, e a tela não fecha sem ambos.** | `ComisVdawer.SCX:337-349 (dump)`: `IF EMPTY(wcod_VEND.Value) → msgcomenta("Favor Escolher um Vendedor 1 !")` + foco; `IF EMPTY(WCOD_VEND1.Value) → msgcomenta("Favor Escolher um Vendedor 2 !")` + foco; só então `Release thisform`. | `ComisVdawer.SCX:337-349 (dump)` | **essencial**

`RF-128` | **Os dois vendedores nascem do cadastro do cliente e são apenas confirmados.** | `ComisVdawer.SCX:116-135 (dump)`: `wcod_vend = pedtemp.cod_vend`, `wcod_vend1 = pedtemp.cod_vend1`; se `wcod_vend` estiver vazio → se `clientes.COD_VENDOR` vazio, usa `wareas.cod_vend` (`FATO MEDIDO`: `"VEN"`) e `clientes.COD_VEND2`; senão usa `clientes.COD_VENDOR` e `clientes.COD_VEND2`. | `ComisVdawer.SCX:116-135 (dump)`; `Fox\WER\WAREAS.DBF` | **essencial**

`RF-129` | **A tela de vendedores NÃO grava em tabela: apenas alimenta duas variáveis.** | `FATO MEDIDO` — o despejo de `ComisVdawer.SCX` não contém nenhum `REPLACE`, `INSERT` ou `APPEND`. Os `ControlSource` são as memvars `wcod_vend` (`:180`) e `wcod_vend1` (`:383`). O consumidor é `pcondpg2.prg:244-247`: sob `IF wped_vend="S"`, `REPLACE cod_vend WITH wcod_vend` e `REPLACE cod_vend1 WITH wcod_vend1` — sobrescrevendo o que veio do `PEDTEMP` em `:240-241`. **Isto refuta a afirmação de que a tela grava em quatro tabelas** (§4.2 D9 de `analise_wer_app.md`). | `ComisVdawer.SCX` (dump completo); `pcondpg2.prg:240-247` | **importante** |

`RF-130` | **Não existe rateio de percentual entre os dois vendedores.** | `FATO MEDIDO`: `PERCVEND1`/`PERCVEND2` não aparecem em nenhum ponto do fluxo; `pedido.PERC_VEND` recebe `wareas.cons_min` (vazio) e não é lido por consumidor de comissão. **A versão web deve decidir se o segundo vendedor é informativo ou remunerado.** Ver §15, lacuna L4. | `pcondpg2.prg:259`; `analise_wer_app.md` §4.2 D9 | **importante**

`RF-131` | **A lista de vendedores exclui o tipo `I`, mas a validação por código não.** | `ComisVdawer.SCX:234 (dump)` e `:270`, `:406`, `:422`: `set filter to tipo_vend<>"I  "` antes de abrir a lista (`listvend`) e antes do `dbseek`. `FATO MEDIDO`: 62 dos 193 vendedores são tipo `I`. Note que o filtro compara com `"I  "` (com dois espaços) — se `tipo_vend` for C(1), a comparação é sobre valor mais largo que o campo. `HIPÓTESE` de que o filtro funciona por conversão implícita; não verificado em execução. | `ComisVdawer.SCX:234 (dump)`; `Fox\WER\vendedor.DBF` | **importante** |

`RF-132` | **ESC na tela de vendedores arma o cancelamento do pedido sem fechar a tela.** | `ComisVdawer.SCX:138-148 (dump)`: ESC → `MESSAGEBOX("Deseja Cancelar esta comissão ?", 4+32+256, "Artsoft Sistemas")`; se confirmado, `wcnvar = 1` e `thisform.sair1.click()`. Mas `Sair1.Click` **recusa fechar** se falta vendedor (RF-127). Resultado: `wcnvar = 1` permanece armado, e `wcnvar <> 0` é justamente o gatilho de `do desmPED` (cancelamento do pedido) em `fechapedwer.scx:1379-1380 (dump)`. Estado inconsistente possível. | `ComisVdawer.SCX:138-148 (dump)`, `:337-349 (dump)`; `fechapedwer.scx:1379-1380 (dump)` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-133` | **A tela de vendedores só é alcançável sob quatro condições simultâneas.** | `fechapedwer.scx:449-458 (dump)` e `:416-418 (dump)`: o botão `Vendedor` é visível se `wped_vend="S"` **e** `wareas.ind_aut_v="S"` **e** `tipos.tipo_es="S"` **e** `tipos.IND_COMISS="S"`. `FATO MEDIDO`: `wareas.IND_AUT_V = "S"`; `tipos.IND_COMISS = "S"` em **132** dos 487 tipos. Ou seja: para 353 tipos com `IND_COMISS="N"` o pedido é gravado **sem passar pela escolha de vendedor**, usando o que vier do `PEDTEMP`. | `fechapedwer.scx:416-418 (dump)`, `:449-458 (dump)`; `Fox\WER\WAREAS.DBF`, `tipos.dbf` | **importante**

### 8.5 Alçada por valor e por família

`RF-134` | **Existem duas alçadas por valor, ambas restritas a compras e ambas inertes.** | (a) **Por pedido**: `ped_wer.scx:5942-5966 (dump)` — `If WAreasB.Limite_Ped > 0 And wped_vend = "N"`, e se `nwtotal > WAreasB.Limite_Ped` exibe `"ESTE PEDIDO FICARÁ BLOQUEADO PORQUE ULTRAPASSOU O LIMITE DE ALÇADA! CONFIRMA ?"` (tipo 4); "Não" religa nove controles e retorna. (b) **Por família**: `pcondpg2.prg:87-146` — soma pendências por grupo em `Pedcomp`, compara com `GrupoAlc.Alcada`, e monta a mensagem `"Limite de alçada de familia ultrapassado para a(s) seguinte(s) familia(s): ..."` + `"Este pedido ficará bloqueado aguardando liberação !"`, marcando `bolAlcadaFam = .T.`. <br>`FATO MEDIDO`: `wareasb.LIMITE_PED` **vazio**; `Fox\WER\grupoalc.dbf` tem **0 registros**; e as duas exigem `wped_vend <> "S"`. **Ambas são inertes para pedido de venda.** | `ped_wer.scx:5942-5966 (dump)`; `pcondpg2.prg:87-146`; `Fox\WER\wareasb.dbf`, `grupoalc.dbf` | **importante**

`RF-135` | **O resultado da alçada é a única coisa que escreveria `pedido.POSICAO`.** | `pcondpg2.prg:346-350`: `IF bolAlcadaFam OR bolAlcadaPed → REPLACE Posicao WITH WAreasB.Bloq_Pedid`. Como as duas alçadas são inertes e `Bloq_Pedid` está vazio, fecha o círculo do RF-012: **o estado "bloqueado" nunca é atingido.** Se a versão web quiser bloqueio por alçada, precisa dos três elementos: limite configurado, tabela de alçada povoada, e código de posição de bloqueio. | `pcondpg2.prg:346-350` | **importante**

`RF-136` | **Não existe liberação de crédito por alçada.** | `FATO MEDIDO`: a `PROCEDURE senha_gerente6` existe completa em `valida6wer.prg:459-549` e **a chamada está comentada** (`:446-450`). Quando o limite é excedido, a operação é simplesmente barrada. | `valida6wer.prg:441-455`, `:459-549` | **importante**

`RF-137` | **A permissão "Alteração de Documento" é verificada por cadastro de função, num único ponto.** | `valida6wer.prg:284-321`: após a senha de GERENTE, consulta `Cadfunc` pelo `Caduser.Cod_Func` e filtra `funcao = "Alteração de Documento"`; se o registro existe e `Ind_Aceita` está vazio → `werro = 3` → `"Função não Autorizada..."` e retorna com `Erro=1`. Note a semântica invertida: **função não cadastrada = autorizado** (`:293-294` e `:301-302` fazem `werro = 0`). | `valida6wer.prg:284-321` | **importante** `⚠ DIVERGE DO CÓDIGO ATUAL` |

---

## 9. Catálogo de validações

Uma linha por validação, com o texto **literal** da mensagem exibida. Legenda de consequência:

| Código | Significado |
|---|---|
| **B** | Bloqueia: não avança, mantém o foco no campo |
| **BL** | Bloqueia e **limpa** o campo |
| **BR** | Bloqueia e **restaura** valor anterior (indicado) |
| **A** | Apenas avisa e segue |
| **P** | Pergunta (Sim/Não) e obedece |
| **S** | Exige senha; a recusa causa o efeito indicado |
| **T** | Fecha / reinicia a tela |

### 9.1 Cabeçalho — `ped_wer.scx`

| # | Objeto.Método | Condição exata | Mensagem literal | Cons. | Linha (dump) |
|---|---|---|---|---|---|
| V01 | `wtipo_oper.KeyPress` | `dbseek("tipo",this.Value,...)` e `eof()` | `Tipo de Movimentacao nao Encontrado->` + código *(WAIT WINDOW, sem acento no fonte)* | **BL** | 3504-3507 |
| V02 | `wtipo_oper.KeyPress` | `tipos.ind_transf="S" AND tipo_es="S"` e o tipo de entrada da transferência não existe | `Tipo de entrada nao existe para esta transferencia ` + código *(WAIT WINDOW)* | **A** | 3529-3531 |
| V03 | `wtipo_oper.Valid` | `wareas.ind_fix_pe="S" AND wped_vend="S" AND this.value <> wareas.tp_pedido` | *(nenhuma)* | **BR** → `wareas.tp_pedido` | 3479-3484 |
| V04 | `wc_empr.KeyPress` | `this.value <> "  "` e `dbseek("cod_empr",...,"tabplan")` → `eof()` | `EMPRESA NÃO ENCONTRADA !` | **BL** | 5396-5401 |
| V05 | `WCDOCUMENT.KeyPress` | código existe e `pedido.tipo_oper <> wtipo_oper` | `DOCUMENTO JÁ EXISTE COM OUTRO TIPO DE MOVIMENTAÇÃO: ` + tipo | **BL** | 3663-3669 |
| V06 | `WCDOCUMENT.KeyPress` | código existe e algum item já baixado (`wSt_baixa` ≠ vazio) | `PEDIDO COM ITENS PARCIALMENTE\|TOTALMENTE BAIXADOS.` + CR + `DESEJA CONTINUAR O PROCESSO ?` (tipo 4) | **P** — "Não" limpa | 3688-3699 |
| V07 | `WCDOCUMENT.KeyPress` | código existe (após V06) | `DOCUMENTO JÁ EXISTE. ALTERA, EXCLUI OU TROCA NUM. DOCUMENTO <A/E/T>?` | ramifica | 3702 |
| V08 | `WCDOCUMENT.KeyPress` | ramo `A`, e `contnota` devolve `werro=1` | `PEDIDO JÁ BAIXADO PARCIALMENTE OU TOTALMENTE !` | **B** | 3728-3734 |
| V09 | `WCDOCUMENT.KeyPress` | ramo `A`/`E` e `tipos.ind_senha="S"` | *(tela `autoriz`)* | **S** → `T` (volta ao `Activate`) | 3735-3743, 3777-3784 |
| V10 | `WCDOCUMENT.KeyPress` | ramo `E`, antes de apagar | `CONFIRMA EXCLUSAO ?` (tipo 4) *(sem acento no fonte)* | **P** | 3775-3793 |
| V11 | `WCDOCUMENT.KeyPress` | código existe e `wind_caixa = "S"` | `DOCUMENTO JÁ EXISTE !` | **BL** | 3800-3803 |
| V12 | `Text11.KeyPress` (data) | `this.Value < DATE()` | `ATENÇÃO - DATA INFERIOR À DATA DO DIA !` | **A** | 5301-5306 |
| V13 | `wcli_for.KeyPress` | compras e `forneced.posicao = "I"` | `FORNECEDOR INATIVO !` | **B** | 5556-5562 |
| V14 | `wcli_for.KeyPress` | `dbseek` no cliente/fornecedor → `eof()` | `CLIENTE NÃO ENCONTRADO EM PESSOAS JURÍDICAS !` ou `FORNECEDOR NÃO ENCONTRADO EM PESSOAS JURÍDICAS !` | **BL** | 5564-5575 |
| V15 | `wcli_for.KeyPress` | `LEN(ALLTRIM(tipos.natureza))=4` e `clientes.estado="EX"` e 1º dígito do CFOP ∉ {3,7} | `CFOP DO TIPO DE MOVIMENTAÇÃO INVÁLIDO PARA EXPORTAÇÃO OU IMPORTAÇÃO !` | **B** | 5585-5593 |
| V16 | `wcli_for.KeyPress` | UF do cliente = UF da empresa e 1º dígito ∉ {1,5} | `CFOP DO TIPO DE MOVIMENTAÇÃO INVÁLIDO PARA VENDAS OU COMPRAS DENTRO DO ESTADO !` | **B** | 5594-5602 |
| V17 | `wcli_for.KeyPress` | outra UF e 1º dígito ∉ {2,6} | `CFOP DO TIPO DE MOVIMENTAÇÃO INVÁLIDO PARA VENDAS OU COMPRAS INTERESTADUAIS !` | **B** | 5603-5611 |
| V18 | `wcli_for.KeyPress` | `tipos.status="C"` e `tipos.tip_client <> " "` e `tipo_oper <> tipos.tip_client` | `TIPO DE MOVIMENTAÇÃO NÃO PERMITIDO PARA ESTE CLIENTE !` | **BL** | 5620-5628 |
| V19 | `wcli_for.KeyPress` | `tipos.status="C"` e `tipos.tip_client = " "` | `ATENÇÃO: TIPO DE MOVIMENTAÇÃO COM CAMPO (TIPO P/VINCULAR CLIENTE:) NÃO DEFINIDO PARA ESTE CLIENTE !` | **BL** | 5629-5636 |
| V20 | `wcli_for.KeyPress` → `vecliente` → `credito_clienteW` | cliente ≠ `wareas.cod_cli`, `tipos.status="C"`, `tipos.tipo_es="S"`, e `!wstat_cli` ou `!wstat_cli2` | *(a mensagem vem da classe `credito_clientew`)* | **BL** | 5646-5661 |
| V21 | `vecredito` | `wstat_cli2` e `TmpCliCgc2.CREDITO > 0` e `wsaldo + nwtotal > TmpCliCgc2.CREDITO` | `Crédito do Cliente Excedeu ` + valor | **A** — ⚠ faz `wstat_cli = .T.` (não `.F.`) | 1145-1157 |
| V22 | `wcli_for.KeyPress` | compras, `wareascp.Tip_Dv_For` preenchido e há devolução pendente | `EXISTEM PENDÊNCIAS DE DEVOLUÇÃO PARA ESSE FORNECEDOR.` + CR + `DESEJA VISUALIZÁ-LAS ?` (tipo 4) | **P** | 5730-5736 |
| V23 | `wcli_for.KeyPress` | venda, `wareascp.Tip_Dv_Cli` preenchido e há devolução pendente | `EXISTEM PENDÊNCIAS DE DEVOLUÇÃO PARA ESSE CLIENTE.` + CR + `DESEJA VISUALIZÁ-LAS?` (tipo 4) | **P** | 5756-5762 |
| V24 | `LADD.KeyPress` | `nkeycode=13 AND ind_senha="S" AND wind_caixa="S" AND wind_abert<>"S"` | `CAIXA NÃO ABERTO !` | **B** + `T` | 2805-2811 |
| V25 | `LADD.Activate` | `wdemo="S"` e `RECCOUNT() > 100` | `Versão demo...excedeu limite de movimentações` *(WAIT WINDOW)* | **T** (libera a tela) | 2372-2378 |

`RF-140` | **A validação de crédito na entrada do cliente está com o sinal invertido e apenas avisa.** | `ped_wer.scx:1145-1157 (dump)`: quando o crédito é excedido, o código exibe a mensagem e executa `wstat_cli = .T.` — o mesmo valor que significa "aprovado" para o teste em `:5649` (`If !wstat_cli`). O único bloqueio efetivo de crédito no lançamento é o que vier de dentro da classe `credito_clientew` via `wstat_cli2`. `NÃO VERIFICADO`: o conteúdo dessa classe não foi lido (vive em VCX). **A versão web deve barrar no lançamento, não só na baixa.** Ver §13. | `ped_wer.scx:1145-1157 (dump)`, `:5646-5661 (dump)` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-141` | **A validação de CFOP contra UF compara o CFOP do TIPO, não o do item.** | `ped_wer.scx:5577-5614 (dump)` usa `tipos.natureza`, e só quando ele tem exatamente 4 caracteres. O CFOP efetivo do item pode ser sobrescrito depois (`totaliza:270-282 (dump)`, via `strcfopaux` ou `pedido.cfop`) **sem revalidação contra a UF**. | `ped_wer.scx:5577-5614 (dump)`, `:270-282 (dump)` | **importante** `⚠ DIVERGE DO CÓDIGO ATUAL` |

### 9.2 Item — `ped_wer.scx`

| # | Objeto.Método | Condição exata | Mensagem literal | Cons. | Linha (dump) |
|---|---|---|---|---|---|
| V30 | `Text1.KeyPress` | `wareas.ind_linha =< ws AND wareas.ind_linha <> 0 AND tipos.tipo_es="S"` | `LIMITE DE ITENS POR DOCUMENTO !` | **B** | 4356-4362 |
| V31 | `Text1.KeyPress` | produto não achado em nenhum dos 4 índices (`barra`→`grupo+referencia`→`referencia`→`barra_emb`) nem em `fornece` (entrada) | `PRODUTO NÃO ENCONTRADO !` | **BL** | 4371-4400 |
| V32 | `Text1.KeyPress` | `StrGrp <> Cadmat.Gradegrp` (o "negócio" do 1º item do pedido) | `PRODUTO COM NEGÓCIO DIFERENTE !` | **BL** | 4405-4417 |
| V33 | `Text1.KeyPress` | `cadmat.ind_tab = "N"` | `PRODUTO NÃO PODE SER VENDIDO/COMPRADO !` | **BL** | 4419-4426 |
| V34 | `Text1.KeyPress` | produto já no `PEDTEMP` e `tipos.tp_ped_mt="N"` (multiempresa) | `PRODUTO JÁ FOI DIGITADO NESTE PEDIDO E NÃO É PERMITIDO PARA PEDIDO MULTIEMPRESA !` + CR + `FAVOR ALTERAR ITEM ANTERIOR.` | **BL** | 4432-4439 |
| V35 | `Text1.KeyPress` | produto já no `PEDTEMP` (caso geral) | `PRODUTO JÁ FOI DIGITADO NESTE PEDIDO. CONTINUA ?` (tipo 4) | **P** — "Não" limpa | 4441-4447 |
| V36 | `txtAddText.KeyPress` | grade ligada e `THIS.Value <> wTotQtd` | *(restaura sem mensagem)* | **BR** → `wTotQtd` | 2899-2909 |
| V37 | `txtAddText.KeyPress` | `bxest_ped="S"` e saldo `< THIS.VALUE` | *(mensagem de saldo insuficiente, montada em `:2965`)* | **BL** + foco ao produto | 2939-2978 |
| V38 | `txtAddText.KeyPress` | `THIS.VALUE = 0` | `QUANTIDADE INVALIDA !` *(sem acento no fonte)* | **B** | 2979-2988 |
| V39 | `txtAddText.Valid` | `cadmat.ind_decim="N"` e `This.Value <> Int(This.Value)` | `PRODUTO NÃO OPERA COM DECIMAIS !` | **BL** (`RETURN 0`) | 3065-3071 |
| V40 | `txtAddText.GotFocus` / `When` | rateio multiempresa e `wgtotal = 0` após o diálogo | `QUANTIDADES NÃO DIGITADAS !` | **B** (laço) | 3138-3146, 3100-3106 |
| V41 | `txtAddText.LostFocus` | negociação achada, `Dt_Venc >= DATE()`, `cod_valida <> 1` | `ESSE PRODUTO NÃO FOI AUTORIZADO PARA NEGOCIAÇÃO !` | **A** — segue com preço de tabela | 3231-3234 |
| V42 | `txtAddText.LostFocus` | negociação achada e `Dt_Venc < DATE()` | `ESSE PRODUTO NEGOCIADO ESTÁ FORA DA DATA DE VALIDADE !` | **A** — idem | 3235-3238 |
| V43 | `negocia` | `negocia.preco = 0` | `VALOR INVÁLIDO !` | **B** | 2357-2363 |
| V44 | `Text2.KeyPress` | `THIS.VALUE = 0` | `VALOR INVÁLIDO !` | **B** | 6037-6043 |
| V45 | `Text2.KeyPress` | `ValDescDado <= ValDescNovo` (dentro do teto) | `DESCONTO MÁXIMO PERMITIDO = <teto> ` + CR + `DESCONTO CONCEDIDO = <concedido>` | **A** + confirma item | 6091-6105 |
| V46 | `Text2.KeyPress` | `ValDescDado > ValDescNovo` (acima do teto) | *(mesma mensagem de V45)* | **BR** → `ValPrcWer` | 6106-6117 |
| V47 | `Text3.KeyPress` | valor ∉ {1,2,3,4,5,6} | `PREÇO DIFERENTE DE 1,2,3,4,5,6 !` | **B** | 4651-4656 |
| V48 | `Text7.KeyPress` | `wareascp.ind_prazo="S"` e prazo `< Text11.Value` | `DATA PREVISTA DE ENTREGA MENOR QUE O PEDIDO !` | **B** | 4876-4883 |
| V49 | `wdesc_ind.KeyPress` | `wareas.lim_desc <> 0 AND wped_vend="S" AND wareas.lim_desc < THIS.VALUE` | *(tela `autoriz`)* | **S** → zera o desconto e prende o foco | 4956-4970 |
| V50 | `lstAdd.DblClick` | duplo clique numa linha de item, `pwaltera = .F.`, grade desligada | `CONFIRMA EXCLUSÃO DO ITEM ?` (tipo 4) | **P** | 4042-4047 |
| V51 | `Sair2.Click` | ESC com `ws > 0` *(via botão, não via ESC do form)* | `JÁ EXISTE MOVIMENTAÇÃO. SE QUISER CANCELAR, PRESSIONE ESC 2 VEZES !` | **B** | 3270-3276 |
| V52 | `Sair2.Click` | saída com `ws = 0` | `DESEJA REALMENTE SAIR DA MOVIMENTAÇÃO ?` (tipo 4) | **P** | 3281-3288 |
| V53 | `cmdok1.Click` | compras, `WAreasB.Limite_Ped > 0`, `nwtotal > Limite_Ped`, `bolAlcadaPed = .F.` | `ESTE PEDIDO FICARÁ BLOQUEADO PORQUE ULTRAPASSOU O LIMITE DE ALÇADA! CONFIRMA ?` (tipo 4) | **P** — "Não" religa 9 controles e retorna | 5942-5966 |
| V54 | `LADD.Error` | qualquer erro não previsto no formulário | `ERRO ENCONTRADO: ` + nº + CR + `MESSAGE()` + CR + método + linha *(tipo 3)* | **A** | 2840-2844 |

`RF-142` | **A trava de "negócio" único por pedido é regra de negócio e precisa ser explicitada.** | `ped_wer.scx:4405-4417 (dump)`: o **primeiro** item do pedido fixa `StrGrp = Cadmat.Gradegrp` e `bolGrp = .T.`; todo item seguinte com `Gradegrp` diferente é recusado com `"PRODUTO COM NEGÓCIO DIFERENTE !"`. `StrGrp`/`bolGrp` são zerados em `LADD.KeyPress:2831-2832 (dump)` e em `Sair2.Click:3302 (dump)`. **Um pedido não pode misturar linhas de negócio.** | `ped_wer.scx:4405-4417 (dump)`, `:2831-2832 (dump)` | **essencial**

`RF-143` | **Existe limite de itens por pedido e ele está configurado em 70.** | `FATO MEDIDO`: `wareas.IND_LINHA = 70`. A condição é `wareas.ind_linha =< ws` — bloqueia quando o número de itens já lançados **alcança** o limite. `HIPÓTESE de inconsistência`: existe na base um pedido com **163 itens** (`MR518A`) e outro com 88 (`JP880AD`), acima do limite atual — provavelmente porque `IND_LINHA` mudou de valor ao longo do tempo, ou porque `ws` não conta itens excluídos. `NÃO VERIFICADO`. | `ped_wer.scx:4356-4362 (dump)`; `Fox\WER\WAREAS.DBF`, `PEDIDO.DBF` | **importante** |

`RF-144` | **A validação de saldo em estoque no lançamento está DESLIGADA nesta instalação.** | `ped_wer.scx:2939-2978 (dump)` só roda sob `bxest_ped="S"`. `FATO MEDIDO`: `wareas.BXEST_PED = "N"`. **Consequência: durante a digitação o sistema NÃO verifica disponibilidade.** A primeira verificação de saldo ocorre na **baixa**, em `valt2w.prg:229-238`, quando o pedido já existe e o cliente já foi atendido comercialmente. A fórmula, quando ativa, é `wsaldo = (qtd_fpedid - qtd_fatend) + qtdreal - (qtd_pedida - qtd_atendi)` (`:2963`) — saldo projetado, não físico. **A versão web deve decidir se valida no lançamento; se sim, com essa fórmula e com o produto substituto (`cadmat.refer_que`, `:2956-2962`).** Ver §15, lacuna L5. | `ped_wer.scx:2939-2978 (dump)`; `valt2w.prg:229-238`; `Fox\WER\WAREAS.DBF` | **essencial** |

`RF-145` | **O `Error` do formulário absorve exceções e permite continuar em estado indefinido.** | `ped_wer.scx:2840-2844 (dump)`: o método `Error` mostra número, mensagem, método e linha, e **retorna** — a execução continua. Isso protege o ERP do `QUIT` do `erro_global.PRG`, mas deixa o pedido em digitação num estado que ninguém validou. **A versão web deve tratar exceção no ciclo de item como aborto do item, não como aviso.** | `ped_wer.scx:2840-2844 (dump)` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-146` | **Não existe `TRY/CATCH` em nenhum ponto do caminho de gravação.** | `FATO MEDIDO` (§6.4 R1 de `analise_wer_app.md`): zero `TRY`/`CATCH` em `pcondpg2`, `valt2w`, `est326wer` e `ped327wer`. Combinado com a ausência de transação (§11), qualquer falha no meio da gravação deixa o pedido pela metade. | `analise_wer_app.md` §6.4 R1 | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

### 9.3 Fechamento — `fechapedwer.scx`

| # | Objeto.Método | Condição exata | Mensagem literal | Cons. | Linha (dump) |
|---|---|---|---|---|---|
| V60 | `wcdesconto.KeyPress` | `wpv1="P"` e `wareas.lim_desc < wdesconto` | *(tela `autoriz`)* | **S** → zera o desconto | 649-659 |
| V61 | `wcdesconto.KeyPress` | `wpv1` ∈ {`V`,`A`} e `wwnwtotal*lim_desc/100 < wdesconto` | *(tela `autoriz`)* | **S** → zera o desconto | 660-672 |
| V62 | `wcdesconto.KeyPress` | `wpv1="A"` e `this.value > nwtotal` | `ABATIMENTO MAIOR QUE O VALOR DE VENDA !` | **BL** (zera) | 675-683 |
| V63 | `WPV1.KeyPress` | valor ∉ {`P`,`V`,`A`} | `OPÇÃO DIFERENTE DE <P>ERCENTUAL, <V>ALOR E <A>BATIMENTO !` | **B** | 1476-1483 |
| V64 | `wnCond_pag.KeyPress` | `This.Value <> OLD_COND` | *(tela `autoriz`)* | **S** → restaura `OLD_COND` | 758-776 |
| V65 | `wnCond_pag.KeyPress` | `dbseek("codigo",...,"vencim")` e `.not. found()` | `Condição de Pagamento não encontrada -> ` + código *(WAIT WINDOW)* | **BL** | 786-789 |
| V66 | `wnCond_pag.Click` | escolha por lista ≠ `OLD_COND` | *(tela `autoriz`)* | **S** → restaura código e descrição | 825-847 |
| V67 | `wccusto.KeyPress` | `dbseek("c_custo",...,"ccusto")` e `.not.found()` | `Centro de Custo não encontrada->` + código *(WAIT WINDOW)* | **BL** | 1601-1606 |
| V68 | `wtotpg.KeyPress` | `Vencim.lim_venda <> 0 AND wcondpg <> "  " AND wped_vend="S" AND wtotger2 < Vencim.lim_venda` | `VALOR MENOR PARA ESTA CONDIÇÃO DE PAGAMENTO !` | **S** → limpa a condição e devolve o foco | 1059-1074 |
| V69 | `FECHA.KeyPress` | ESC com `wind_esc` verdadeiro | `Deseja Cancelar esta venda ?` (venda) / `Deseja Cancelar esta compra ?` (compra) — tipo 292 | **P** → `wcnvar=1` + `sair1.click()` | 335-355 |
| V70 | `FECHA.KeyPress` | ESC com `wind_esc` falso | *(nenhuma)* | fecha | 357-359 |
| V71 | `FECHA.Load` | `dbseek("tipo",wtpmov,...)` e `EOF()` | *(WAIT WINDOWS com o próprio `wtpmov`)* | **A** | 396-399 |

### 9.4 Vendedores e data

| # | Objeto.Método | Condição exata | Mensagem literal | Cons. | Linha (dump) |
|---|---|---|---|---|---|
| V80 | `ComisVdawer :: Sair1.Click` | `EMPTY(wcod_VEND.Value)` | `Favor Escolher um Vendedor 1 !` | **B** | 338-342 |
| V81 | `ComisVdawer :: Sair1.Click` | `EMPTY(WCOD_VEND1.Value)` | `Favor Escolher um Vendedor 2 !` | **B** | 344-348 |
| V82 | `ComisVdawer :: wcod_vend.Valid` | `!EMPTY(wcod_vdpd)` e `THIS.VALUE <> wcod_vdpd` e `widentific <> "SUPORTE"` e `!EMPTY(this.Value)` | `Confirma alteração do vendedor padrão ?` (4+32) | **P** → se sim, **S**; recusa restaura `wcod_vdpd` | 207-226 |
| V83 | `ComisVdawer :: wcod_vend.KeyPress` | `dbseek("cod_vend",...)` → `EOF()` | `Vendedor nao Encontrado->` + código *(WAIT WINDOW)* | **BL** | 241-244 |
| V84 | `ComisVdawer :: wcod_vend1.KeyPress` | idem para o vendedor 2 | `Vendedor nao Encontrado->` + código | **BL** | 429-432 |
| V85 | `ComisVdawer :: KeyPress` | ESC e `wind_esc` | `Deseja Cancelar esta comissão ?` (4+32+256) | **P** → `wcnvar=1` (ver RF-132) | 141-146 |
| V86 | `frminformadata :: cmdFechar.Click` | `option2` (Data de Saída) marcada e `Txtdata.Value <= DATE()` | `Data inválida para a opção por data de entrega!` (48) | **B** (`SetFocus` + `RETURN`) | 86-92 |
| V87 | `frminformadata :: cmdFechar.Click` | `option1` (Data de Emissão) marcada e `Txtdata.Value <> DATE()` | `Para a opção por data de Emissão a Data deve ser do dia!` (48) | **B** | 93-99 |

`RF-147` | **A tela de escolha da data não tem cancelamento e não registra a opção escolhida.** | `frminformadata.SCX` — `Init:79-81 (dump)`: default `Option1` (Data de Emissão) e `Txtdata.Value = DATE()`. Único destino: `Txtdata.ControlSource = "strdtaux"` (`:165`); **a opção marcada não é gravada em variável alguma**. O chamador distingue as duas opções pelo próprio valor da data (emissão = hoje; saída = maior que hoje), porque as validações V86/V87 garantem essa correspondência. `HIPÓTESE` — é a única leitura possível, mas não há confirmação explícita no código. ESC é redirecionado para o botão Confirmar (`:73-75`), que reexecuta a mesma validação: **não há caminho de cancelamento**. | `frminformadata.SCX:73-101 (dump)`, `:165 (dump)` | **importante** |

`RF-148` | **Há incoerência entre o rótulo e a mensagem da tela de data.** | A opção chama-se `"Data de Saida"` (`frminformadata.SCX:122 (dump)`) e a mensagem de erro fala de `"data de entrega"` (`:88`). A versão web deve usar um único termo. | `frminformadata.SCX:88 (dump)`, `:122 (dump)` | **desejável**

`RF-149` | **A data escolhida na tela de data vira a data-base do financeiro da baixa.** | `valt2w.prg:252-257` (quando `wareas.ind_ver_fi="N"`) e `:353-354` (ao final da verificação de crédito) chamam `DO FORM frminformadata`; o consumidor é `ped327wer.prg:1611-1619`: se `wareascp.ind_odbc = "  "`, então `IF !EMPTY(strdtaux) → wdata_nfisc = strdtaux` antes de `DO ges629wer with 'PED327WER'`. `FATO MEDIDO`: `wareascp.IND_ODBC` **vazio** → o caminho que consome `strdtaux` **é** o ativo. Limpeza em `ped327wer.prg:1726-1727`. | `valt2w.prg:252-257`, `:353-354`; `ped327wer.prg:1611-1619`, `:1726-1727` | **importante**

---

## 10. Fechamento, desconto de capa e condição de pagamento

### 10.1 Campos do fechamento

| # | Campo | Objeto (`fechapedwer.scx`) | Tipo/tam. | Visível? | Origem / default | Destino |
|---|---|---|---|---|---|---|
| F1 | Modo do desconto | `WPV1` | `X`, `Format="!"`, `Value="P"` | **`Visible=.F.`** | `"P"` em inclusão; `wval_per` em alteração (`:460-476`) | `pedido.TIPO_DESC` C(1) via `wval_per` |
| F2 | Desconto de capa | `wcdesconto` | `99999.99` | `Visible=.F.` no projeto; ligado por `wareas.ind_desc="S"` e `bolDesc` | `clientes.perc_desc` em inclusão; `wdesc1` em alteração | `pedido.DESCONTO` N(10,2) via `wdesc1` |
| F3 | Condição de pagamento | `wnCond_pag` | `XX`, `Format="!"` | sim (oculto se `tipos.ind_finan="N"`) | `clientes.cond_pag`, senão `tipos.cod_vencim` | `pedido.COND_PAG` C(2) via `wcondpg` |
| F4 | (descrição da condição) | `wnome_vencim` | exibição | sim | `vencim.desc_ocor` | — |
| F5 | Centro de custo | `wccusto` | `XXXX`, `Format="!"` | sim | `wareas.ccusto` ou `tabplan.ccusto` | `pedido.C_CUSTO` C(4) via `WCCUSTO` |
| F6 | (nome do centro de custo) | `wnome_ccusto` | exibição | sim | `ccusto.descricao` | — |
| F7 | Total | `nwtotal` | numérico | sim | acumulado, recalculado a cada desconto | `pedido.TOTAL_NOTA` via `wtotger2` |
| F8 | Total pago | `wtotpg` | `99999.99` | sim | 0 | — (dispara a gravação) |
| F9 | Troco | `nwtroco` | `999,999.99` | **`Visible=.F.`, `Top=559` fora da área útil (`Height` do form)** | `wtroco` | — |
| F10 | Documento | `wcdocument` | — | oculto salvo `tipos.ind_transf="z"` | `wcod_empre_e` | — |
| F11 | Vendedores | botão `Vendedor` | — | 4 condições (RF-133) | abre `ComisVdawer` | `pedido.COD_VEND`, `COD_VEND1` |

`RF-150` | **O modo do desconto de capa (P/V/A) é inalcançável e o dado confirma.** | `WPV1` tem `Visible=.F.` (`fechapedwer.scx:1451 (dump)`) e é fixado em `"P"` em inclusão (`:461`). `FATO MEDIDO`: `pedido.TIPO_DESC = "P"` em **102.104 de 102.104** itens. O desconto de capa é sempre percentual. Note em `pcondpg2.prg:284-288` a lógica curiosa: `IF desconto = 0 → tipo_desc = "P"` senão `tipo_desc = wval_per` — ou seja, mesmo com modo valor, desconto zero grava `"P"`. | `fechapedwer.scx:1435-1483 (dump)`; `pcondpg2.prg:284-288`; `Fox\WER\PEDIDO.DBF` | **importante**

`RF-151` | **O desconto de capa altera o total exibido antes da gravação.** | `fechapedwer.scx:687-692 (dump)`: se modo `"P"` → `NWTOTAL = wwnwtotal - wwnwtotal*wdesconto/100`; senão → `NWTOTAL = wwnwtotal - wdesconto`; e `wtotger2 = NWTOTAL`. É esse `wtotger2` que `pcondpg2.prg:278` grava em `total_nota` e `:280` em `base_icm`. | `fechapedwer.scx:687-692 (dump)`; `pcondpg2.prg:278-281` | **essencial**

`RF-152` | **O desconto de item acumulado é transportado para o desconto de capa na abertura do fechamento.** | `fechapedwer.scx:434-438 (dump)`: `if wwtotdesc <> 0 → wcdesconto.value = wwtotdesc ; Wpv1.value = "V" ; nwtotal.value = nwtotal.value + wwtotdesc`. `wwtotdesc` é acumulado em `ped_wer.scx:344-345 (dump)` a partir de `wdesc_ind`. Como `wdesc_ind` está oculto (RF-051), na prática `wwtotdesc = 0`. | `fechapedwer.scx:434-438 (dump)`; `ped_wer.scx:344-345 (dump)` | **importante**

`RF-153` | **O troco existe no código e é inalcançável na tela.** | `fechapedwer.scx:1290-1316 (dump)`: `nwtroco` com `Visible=.F.` e `Top=559`, fora da altura útil do formulário. O valor é calculado em `pcondpg2.prg:72-82` (`wtroco = ROUND(wwtotpg - wtotger2, 2)` ou contra `wtotger4` quando há segunda condição) e exibido em `fechapedwer.scx:1185 (dump)`. Não é persistido. | `fechapedwer.scx:1290-1316 (dump)`, `:1185 (dump)`; `pcondpg2.prg:72-82` | **desejável**

### 10.2 Segunda condição de pagamento

`RF-154` | **O sistema suporta duas condições de pagamento num pedido, e marca isso com um valor sentinela.** | `pcondpg2.prg:18-37`: se `wwtotpg < wtot_geral` e `wwtotpg <> 0` e `wareas.ind_orcam <> "S"` e `wipi_tot = 0`, pergunta `"Mais de uma condição de pagamento ? <S/N>"`. Resposta `"S"` → `wmais_pg = 1`, `wtotger4 = wtotger4 - wwtotpg`, `wtotger3 = wwtotpg`, `wwtotpg = 0`. Resposta `"N"` → `wtotger3 = wtotger2`, `wexit = 1`. E `pcondpg2.prg:306-310`: `IF wmais_pg = 1 → REPLACE cond_pag WITH "XX"` senão `REPLACE cond_pag WITH wcondpg`. | `pcondpg2.prg:12-64`, `:306-310` | **essencial**

`RF-155` | **`cond_pag = "XX"` é um valor sentinela que altera o comportamento a jusante.** | `FATO MEDIDO`: `pedido.COND_PAG` tem 64 valores distintos, entre eles `"XX"`. `valida6wer.prg:330` trata `"XX"` diferente de qualquer outra condição (`If LINHA<>"  " .And. LINHA<>"XX"`), e `fechapedwer.delecao` usa `wcondpg <> "XX"` como guarda para não apagar títulos (`fechapedwer.scx:135 (dump)`, `:176`, `:189`). **A versão web deve modelar parcelas/formas de pagamento como coleção, não como valor especial num campo de 2 caracteres.** | `pcondpg2.prg:306-310`; `valida6wer.prg:330`; `fechapedwer.scx:135-189 (dump)` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-156` | **`wareas.IND_TOT_N = "N"` mantém o caminho da segunda condição ativo.** | `pcondpg2.prg:12`: `IF wareas.ind_tot_n <> "S"` → entra no bloco de duas condições. `FATO MEDIDO`: `wareas.IND_TOT_N = "N"`. O caminho **está ativo**; a pergunta aparece sempre que o valor pago é menor que o total e diferente de zero. | `pcondpg2.prg:12`; `Fox\WER\WAREAS.DBF` | **importante**

`RF-157` | **A condição de pagamento tem efeitos colaterais de interface a jusante que a versão web deve mapear.** | `valida6wer.prg`: `"CR"` + existência de `credito.dbf` abre `controle_credito` e barra se `VL_CREDITO_CLIENTE < pedido.total_nota` com `"Valor do Crédito menor que o valor do pedido"` (`:338-351`); `vencim.ind_dia_pf="S"` abre a tela `diapref` (`:353-361`); `vencimr.ind_card="S"` ou (`wareas.ind_credit<>"S"` e `vencim.ind_crt_cr="S"` e `wareas.Ind_TEF` vazio) abre `conta_credito` (`:370-375`). `FATO MEDIDO`: `vencim.IND_DIA_PF` e `IND_CRT_CR` **vazios nos 99 registros** → as duas últimas são inertes. | `valida6wer.prg:338-375`; `Fox\WER\vencim.dbf` | **importante**

`RF-158` | **A operação contábil do pedido é resolvida por precedência de três níveis.** | `valida6wer.prg:421-428`: `Case !Empty(vencim.cod_oper) → WpnOperacao = PADR(vencim.cod_oper,3)`; `Case !Empty(tipos.operacao) → PADR(tipos.operacao,3)`; `Otherwise → PADR(wareas.cod_oper,3)`. | `valida6wer.prg:421-428` | **importante**

---

## 11. Efeitos colaterais da gravação e fronteira transacional

Esta é a seção mais importante para o desenho da API: cada efeito abaixo é hoje uma gravação independente, sem transação. Numa API cada um é uma decisão de atomicidade.

### 11.1 Efeitos ANTES de o pedido existir

| # | Alvo | Operação | Momento | Origem |
|---|---|---|---|---|
| S1 | `wareas.CONT_PED` / `CONT_COM` | incremento | **abertura da tela** | `ped_wer.scx:2246`, `:2264-2268 (dump)` |
| S2 | `cadmat.QTD_PEDIDA` (saída) ou `QTD_FPEDID` (entrada) | soma da quantidade do item | **confirmação de cada item** | `ped_wer.scx:366-381 (dump)` |
| S3 | `cadmat.IPI`, `IPI_FABR`, `VAL_ICM_E` | atualização (só entrada) | confirmação de cada item | `ped_wer.scx:373-379 (dump)` |
| S4 | `cadmat.QTD_PEDIDA` / `QTD_FPEDID` | **subtração** | exclusão de item por duplo clique | `ped_wer.scx:4086-4115 (dump)` |
| S5 | `empresa_mult` | `DELETE ALL FOR qtd_itens = windex` | exclusão de item, multiempresa | `ped_wer.scx:4117-4122 (dump)` |
| S6 | `wareas.CONT_PED` / `CONT_COM` | **decremento** | ramos `A`/`E`, e abandono | `ped_wer.scx:2277-2283 (dump)`, `:2296-2310 (dump)` |

`RF-160` | **A reserva de estoque não pode ser gravada antes de o pedido existir.** | `ped_wer.scx:366-381 (dump)` grava em `cadmat` com `=Tableupdate()` **por item**, dentro de `totaliza`, quando o pedido ainda é um cursor em memória. Consequência: queda de estação, falta de energia ou abandono anormal deixa a reserva inflada, sem nenhum pedido correspondente. O desfazer depende de o operador passar por `Sair2.Click` → `do desmped` (`:3294-3296`), que só roda `if wcnvar<>0`. `NÃO VERIFICADO`: o conteúdo de `desmped` (programa externo, fora do escopo lido). **A versão web deve reservar dentro da mesma transação que grava o pedido, ou usar reserva com expiração explícita atrelada ao rascunho.** | `ped_wer.scx:366-381 (dump)`, `:3294-3296 (dump)` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-161` | **A reserva é condicionada a `tipos.ind_qtd = "S"`.** | `ped_wer.scx:366 (dump)`. `FATO MEDIDO`: `IND_QTD = "S"` em 418 dos 487 tipos, `"N"` em 69. Para os 69, o pedido não reserva nada. | `ped_wer.scx:366 (dump)`; `Fox\WER\tipos.dbf` | **importante**

`RF-162` | **A exclusão de item usa marcação lógica no vetor de controle, não exclusão no cursor.** | `ped_wer.scx:4061 (dump)`: `vwbarra(windex) = SPACE(16)`; a linha da lista vira `"****..."` (`:4062`, `:4133`); o registro do `PEDTEMP` **permanece**. Quem descarta é `pcondpg2.prg:162`: `IF wquant(wnvar1)=0 .or. pedtemp.grupo+pedtemp.referencia=" " .or. vwbarra(nvar)=space(16)` → `witens=witens+1`, `N=n+1`, `LOOP`. **Três sentinelas diferentes significam "item descartado".** A versão web deve ter estado de item explícito. | `ped_wer.scx:4057-4062 (dump)`, `:4133 (dump)`; `pcondpg2.prg:162-168` | **importante** `⚠ DIVERGE DO CÓDIGO ATUAL` |

### 11.2 Efeitos DURANTE a gravação (`pcondpg2` e `fechapedwer`)

| # | Alvo | Operação | Origem | Observação |
|---|---|---|---|---|
| S10 | `clientes.DT_ULT_VEN` | `REPLACE dt_ult_ven WITH date()` + `=tableupdate()` | `pcondpg2.prg:170-174` | **dentro do laço de itens** — grava N vezes o mesmo valor; só se `tipos.tipo_es="S"` |
| S11 | `pedido` | `APPEND BLANK` + ~60 `REPLACE` + `=tableupdate()` por item | `pcondpg2.prg:185-351` | sem transação |
| S12 | log | `do gravalog with wfuncao, wrespalt, pedido.codigo` — `wfuncao` = `"PEDIDO"` (venda) ou `"COMPRAS"` | `pcondpg2.prg:359-368` | `wrespalt` = `" "`→`"I"` se vazio |
| S13 | `pedido` (2ª passagem) | `DO ptotnota` recalcula o total da nota | `pcondpg2.prg:374-383` | reposiciona por `pedido7` |
| S14 | `pedemp` | `deleta_cademp` (DELETE por `num_ped`) e então `INSERT INTO pedemp FROM MEMVAR` por linha de `empresa_mult` com `qtd_empr <> 0` | `fechapedwer.scx:260-312 (dump)` | apaga-e-reinsere |
| S15 | `pedhead` | `seek wdocum`; se não achou `APPEND BLANK`; abre `DO form empenho`; `repla cod_pedido WITH wdocum` | `fechapedwer.scx:315-330 (dump)` | só se `tipos.ind_empenho="S"` |
| S16 | `pedido` (3ª passagem) | `fecha_nota` reescreve `qtditenspe`, `total_icm`, `val_custo`, `val_finan`, `incide_*`, `total_nota`, `total_desc`, `icms_ret`, `obs_nf`, `cod_trans`, `transp_nf`, `incidsubst` em **todos** os itens | `fechapedwer.scx:221-254 (dump)` | terceira escrita no mesmo registro |
| S17 | log | `DO GRAVALOG WITH "ALT CND PE","A",ALLTRIM(wdocum),xLOG` | `fechapedwer.scx:1162-1178 (dump)` | só quando `wrespalt == "A"` |
| S18 | impressão | `do mod_ped` / `do mod_com` | `fechapedwer.scx:1194-1214 (dump)` | `wareas.IND_IMPPED="S"`, `MOD_PEDIDO="PE"` |

`RF-163` | **A gravação do pedido deve ser uma transação única.** | `FATO MEDIDO`: são hoje **no mínimo 3 passagens de escrita** no mesmo registro de `pedido` (S11, S13, S16), mais 4 tabelas satélites (S10, S12, S14, S15), sem `BEGIN TRANSACTION` em nenhum ponto. **Fronteira transacional exigida: cabeçalho + itens + reserva de estoque + `dt_ult_ven` + log, tudo ou nada.** A impressão (S18) fica **fora** da transação. | `pcondpg2.prg:150-388`; `fechapedwer.scx:221-330 (dump)`, `:1021-1234 (dump)` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-164` | **`clientes.dt_ult_ven` é gravado uma vez por item e deve ser gravado uma vez por pedido.** | `pcondpg2.prg:170-174`, dentro do `DO while witens < wcontador`. Um pedido de 163 itens grava 163 vezes o mesmo `DATE()`. Além do custo, `clientes` fica aberto para escrita durante todo o laço. | `pcondpg2.prg:170-174` | **importante** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-165` | **O log de pedido é gravado uma vez, com a natureza da operação.** | `pcondpg2.prg:359-368`: `wfuncao = "PEDIDO"` se `wped_vend="S"`, senão `"COMPRAS"`; `if wrespalt=" " → wrespalt="I"`; e `do gravalog with wfuncao, wrespalt, pedido.codigo`. Valores de `wrespalt`: `"I"` (inclusão), `"A"` (alteração), `"E"` (exclusão, gravado em `ped_wer.scx:665 (dump)`). **Este é o único registro de auditoria da operação.** | `pcondpg2.prg:359-368`; `ped_wer.scx:665 (dump)` | **essencial**

`RF-166` | **O log não registra o conteúdo, apenas o código do pedido e o tipo de operação.** | Não há *before/after* de campo. A única exceção é a troca de condição de pagamento (S17), que grava a frase `"Condição <antiga> alterada para <nova>"`. **A versão web deve registrar diff de campos em alteração de pedido gravado.** | `pcondpg2.prg:368`; `fechapedwer.scx:1169-1170 (dump)` | **importante** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-167` | **`pedhead` e `pedemp` estão praticamente sem uso e não devem ser herdados sem revisão.** | `FATO MEDIDO`: `Fox\WER\PEDHEAD.DBF` tem **62 registros** (18 campos: licitação, processo, empenho, endereço, validade); `Fox\WER\pedemp.DBF` tem **1 registro**; `tipos.IND_EMPENH` está **vazio nos 487 tipos**, logo `empenho` (S15) nunca dispara. Contra 35.499 pedidos. | `Fox\WER\PEDHEAD.DBF`, `pedemp.DBF`, `tipos.dbf` | **importante**

`RF-168` | **A gravação em `pedido` depende de uma variável de controle que pode não estar posicionada.** | `pcondpg2.prg:176-182`: antes do `APPEND BLANK`, faz `SET exact off` + `dbseek("grupo+referencia", pedtemp.grupo+pedtemp.referencia, "M0","cadmat")` e, se `eof()`, executa apenas `WAIT windows ("condpg"+...)` — **avisa e continua**. As linhas seguintes leem `cadmat->PAIS`, `cadmat->cod_client`, `cadmat->REG_OFICIA` (`:293-295`) com o ponteiro fora de faixa. Também usa `SET exact off`, o que permite casar produto por prefixo. | `pcondpg2.prg:176-182`, `:291-304` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-169` | **A gravação sai sem erro visível quando o pedido não é encontrado na releitura.** | `pcondpg2.prg:374-378`: após o laço, `dbseek("es_mov+tipo_oper+codigo", trim(tipos->tipo_es+wtpmov+wdocum), "M0","pedido7")` e `IF eof() → WAIT windows (...) ; RETURN`. Sai da rotina **sem recalcular o total da nota** (`ptotnota`) e sem sinalizar erro ao chamador — o `fechapedwer` prossegue para `grava_cademp`, `empenho`, `fecha_nota`. Note também o `trim()` aplicado à concatenação inteira, que remove espaços à direita do código e pode desalinhar a chave. | `pcondpg2.prg:374-383` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

---

## 12. Alteração e exclusão

### 12.1 Alteração de ITEM já lançado — veredito

`RF-170` | **A alteração de item já lançado é CÓDIGO MORTO, por decisão de negócio documentada em 2009. `CONFIRMADO`.** | Três provas independentes: <br>**(1) `RETURN 0` incondicional.** `ped_wer.scx:3861-3876 (dump)`: no `lstAdd.KeyPress`, o `IF fquest("Confirma Alteração do Item?<S/N>","SN")="N"` da linha 3871 **está comentado** e o `Endif` da linha 3876 **também**, deixando `Thisform.text1.SetFocus` / `This.ListIndex = 0` / `RETURN 0` (`:3872-3874`) **sem condição**. As linhas 3877-4027 (150 linhas) são inalcançáveis. <br>**(2) Comentário do autor, com data e ficha.** `ped_wer.scx:3867-3869 (dump)`, literal: `**** Comentado pelo Desenvolvedor Mario, Para não permitir alteração do ítem.` / `**** Dia 27/10/2009` / `**** Ficha : 4600JM`. **Não é acidente: é requisito.** <br>**(3) `pwaltera` nunca vale `.T.`.** Varredura completa do despejo: as duas únicas atribuições `.T.` estão em `:4005` e `:4025`, ambas **depois** do `RETURN 0` de `:3874`; a única atribuição `.F.` está em `:890`, dentro do método morto `altera` (`:699-892`). Logo a propriedade permanece no seu valor de projeto. Esse valor é `.F.`, **por contradição com o dado**: se fosse `.T.`, `finaliza_item:1860 (dump)` desviaria sempre para o ramo de alteração e nenhum item seria inserido — contra 102.104 itens gravados. | `ped_wer.scx:3861-3876 (dump)`, `:699-892 (dump)`, `:4005 (dump)`, `:4025 (dump)`, `:1858-2026 (dump)` | **essencial** |

`RF-171` | **Oito pontos do formulário testam `pwaltera` e todos tomam o ramo falso.** | `ped_wer.scx` (dump): `:1860` (ramo de alteração de `finaliza_item`), `:2985`, `:3010`, `:3085`, `:3123`, `:3149`, `:4039` (guarda da exclusão de item), `:4474`, `:4647`. Consequências úteis: a exclusão de item **nunca** é bloqueada (`:4039`), e o segundo algoritmo de faixa de comissão **nunca** roda (RF-095). Consequência a observar: qualquer reimplementação que "reative" a alteração de item liga simultaneamente esses oito comportamentos. | `ped_wer.scx` (grep `pwaltera` no dump) | **essencial** |

`RF-172` | **A versão web deve manter a proibição de editar item lançado, ou reintroduzi-la como decisão nova e explícita.** | O requisito de 2009 é: item lançado não se altera; **exclui-se e lança-se de novo**. Isso tem efeito colateral desejável — força o recálculo de preço, desconto e faixa de comissão pelo caminho normal, evitando exatamente a classe de divergência do RF-093/RF-095. Se a decisão mudar, a edição precisa reexecutar **todo** o pipeline do item (RF-024 a RF-028), não apenas trocar o valor. Ver §15, lacuna L6. | derivado de RF-170 | **essencial** |

### 12.2 Exclusão de ITEM já lançado — VIVA

`RF-173` | **A exclusão de item é acionada por duplo clique na lista, com confirmação. `CONFIRMADO VIVA`.** | `ped_wer.scx:4034-4136 (dump)`: `IF THIS.LISTINDEX > 0`; retorna se grade ligada (`:4036-4038`) ou se `pwaltera` (`:4039-4041`, nunca); pergunta `"CONFIRMA EXCLUSÃO DO ITEM ?"` (tipo 4); calcula o índice a partir do par de linhas da lista (`:4048-4054`); retorna se já excluído (`IF vwbarra(windex)=" "`, `:4057-4059`); marca `vwbarra(windex)=SPACE(16)`; substitui a linha por asteriscos; subtrai de `wwwtotal`, `qwtotal` e `nwtotal`; **estorna a reserva em `cadmat`** (`:4086-4115`, sob `tipos.ind_qtd="S"`); e limpa `empresa_mult` se multiempresa. | `ped_wer.scx:4034-4136 (dump)` | **essencial** |

`RF-174` | **A aritmética de índice da exclusão depende de a lista ter exatamente duas linhas por item.** | `ped_wer.scx:4048-4054 (dump)`: `IF INT((THIS.LISTINDEX/2))*2 = THIS.LISTINDEX → wlistindex = LISTINDEX-1 ELSE wlistindex = LISTINDEX`; e `windex = (LISTCOUNT)/2 - ((wlistindex+1)/2) + 1`. Cada item gera duas `AddItem` inseridas na **posição 1** (`ped_wer.scx:414 (dump)` e bloco `:415-448`), ou seja, em ordem inversa. **Uma grade de uma linha por item na versão web elimina esta classe inteira de defeito de índice.** | `ped_wer.scx:414-448 (dump)`, `:4048-4054 (dump)` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-175` | **O total exibido após exclusão usa preço × quantidade sem desconto.** | `ped_wer.scx:4064-4067 (dump)`: `wtotal = wquant(windex)*wpreco(windex)`, e subtrai esse valor de `wwwtotal` e de `nwtotal`. Mas na inclusão o total foi somado como `lntotaux - ((lntotaux*wdesc_ind)/100)` (`:407`) ou `lntotaux - wwwdesconto` (`:402`). **Se o item tinha desconto, a exclusão subtrai mais do que somou.** `HIPÓTESE` — não reproduzido; e como `wdesc_ind` está oculto (RF-051), hoje `wdesc_ind = 0` e os dois coincidem. | `ped_wer.scx:402-408 (dump)`, `:4064-4067 (dump)` | **importante** |

### 12.3 Alteração de PEDIDO gravado — VIVA

`RF-176` | **Digitar um código já existente abre um menu de três opções.** | `ped_wer.scx:3702 (dump)`: `DO FORM iuven_fquest WITH "DOCUMENTO JÁ EXISTE. ALTERA, EXCLUI OU TROCA NUM. DOCUMENTO <A/E/T>?","AET",600,.F. TO wrespalt`. <br>`A` → recarrega para edição (`:3703-3759`); `E` → recarrega e oferece exclusão (`:3761-3793`); `T` (ou qualquer outra) → limpa o campo (`:3794-3797`). | `ped_wer.scx:3702-3797 (dump)` | **essencial** |

`RF-177` | **O ramo de alteração recarrega o pedido inteiro no cursor de digitação.** | `contnota` (`ped_wer.scx:492-657 (dump)`): copia para variáveis o tipo, a data, o desconto, o modo de desconto, o centro de custo, a condição de pagamento, o projeto, a empresa e os dois vendedores (`:496-517`); reposiciona `clientes`/`forneced` (`:522-531`); percorre os itens do pedido (`:536-650`) preenchendo os vetores `vwbarra`, `wquant`, `wpreco`, `vwreserva`, `vwprazo`, `wvdesc_ind` e fazendo `APPEND BLANK` no `PEDTEMP` com ~40 `REPLACE` (`:580-629`); marca `REPLA wwaltemp WITH "S"` (`:625`); recompõe as duas linhas da lista por item (`:633-646`); e acumula os totais (`:647-652`). Ao final, `ws = nvar` (`:654`). | `ped_wer.scx:492-657 (dump)` | **essencial** |

`RF-178` | **Na alteração, o preço e a comissão dos itens preexistentes NÃO são recalculados.** | `contnota` copia `wpreco(nvar) = prc_venda` (`:564`) e `REPLACE qtd_comp WITH pedido.qtd_comp` (`:595`) — ou seja, o percentual de comissão gravado originalmente é **preservado**. Isso é coerente com RF-027 (comissão fixada no lançamento) e é o comportamento correto; a versão web deve preservá-lo explicitamente e não "recalcular por precaução". | `ped_wer.scx:564 (dump)`, `:595 (dump)` | **essencial** |

`RF-179` | **Na alteração, o campo de produto é desabilitado; o operador só pode excluir itens e incluir novos.** | `ped_wer.scx:4008 (dump)` (`Thisform.text1.Enabled=.F.`) está no bloco morto, mas o efeito prático é o mesmo por outro caminho: não existe caminho vivo de edição de item (RF-170). Portanto **a "alteração de pedido" é, operacionalmente: trocar dados de cabeçalho editáveis + excluir itens + incluir itens.** | derivado de RF-170, RF-173, RF-177 | **essencial** |

`RF-180` | **A gravação da alteração apaga todos os itens antigos antes de regravar.** | `fechapedwer.scx:1036-1038 (dump)`: `if wrespalt="A" .AND. wmais_pg=0 → thisform.delecao`. E `fechapedwer.delecao` (`:113-123`) percorre `es_mov+tipo_oper+codigo` fazendo `Delete` + `=tableupdate()`. **Nota importante: esse `delecao` NÃO estorna `cadmat`** — diferente do `delecao` do `ped_wer` (`:674-688`), que estorna. O comportamento é coerente (os itens preservados mantêm a reserva original; os excluídos foram estornados pelo duplo clique; os novos reservam via `totaliza`), mas o equilíbrio é acidental e depende de três caminhos distintos acertarem. `HIPÓTESE` — não verificado em laboratório. | `fechapedwer.scx:113-123 (dump)`, `:1036-1038 (dump)`; `ped_wer.scx:674-688 (dump)` | **essencial** |

`RF-181` | **O `delecao` do fechamento tem um bloco de limpeza de financeiro que é inalcançável nesta instalação.** | `fechapedwer.scx:124-212 (dump)`: sob `IF WAREAS->IND_ped_E="S" .and. tipos->tipo_es="S"`, apaga `artficha` (venda) ou `apagar` + `compras` (compra), filtrando por `tipo_operf` e `ccusto`. `FATO MEDIDO`: `wareas.IND_PED_E = "N"` → **todo o bloco é inerte**. Consequência para a versão web: se o pedido passar a gerar financeiro no lançamento, a alteração terá de desfazê-lo, e a regra de filtro (`tipo_operf` + centro de custo) precisa ser respeitada. | `fechapedwer.scx:124-212 (dump)`; `Fox\WER\WAREAS.DBF` | **importante** |

### 12.4 Exclusão de PEDIDO gravado — VIVA

`RF-182` | **A exclusão de pedido gravado estorna a reserva de estoque e apaga os registros.** | `ped_wer.scx:658-698 (dump)`: `seek wdocum` com `SET EXACT OFF`; `do gravalog with "PEDIDO", wrespalt, pedido.codigo` (`:665`); laço `do while codigo=wdocum` que, sob `tipos.ind_qtd="S"`, faz `replace cadmat.qtd_pedida WITH qtd_pedida - pedido.qtd_itens` (saída) ou `qtd_fpedid` (entrada) + `=tableupdate()`, e então `delete` no `pedido` + `=tableupdate()`; ao final, se `wareas.ind_ped_e="S"` e `tipos.tipo_es="S"`, chama `deleta_financeiro` (inerte, RF-181). | `ped_wer.scx:658-698 (dump)` | **essencial** |

`RF-183` | **O laço de exclusão não filtra por tipo de operação e usa comparação por prefixo.** | `ped_wer.scx:661-670 (dump)`: `SET EXACT OFF` + `seek wdocum`, e `do while codigo=wdocum`. Como a identidade real do pedido é `es_mov+tipo_oper+codigo` (RF-001) e este laço só compara `codigo` **por prefixo**, a exclusão pode alcançar registros de **outro tipo de operação** com o mesmo código, ou de um código mais longo com o mesmo prefixo. Compare com `fechapedwer.delecao` (`:116-119`), que **usa** a chave completa. | `ped_wer.scx:661-670 (dump)`; `fechapedwer.scx:116-119 (dump)` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-184` | **A exclusão de pedido baixado deve ser proibida.** | Não há verificação de `qtd_liber` dentro de `delecao`. A única barreira é o aviso V06 (`ped_wer.scx:3688-3699 (dump)`), que o operador pode aceitar, e o `werro=1` do `contnota` no ramo `A` — que **não** protege o ramo `E` da mesma forma (`:3761-3773` chama `contnota` mas não testa `werro`). Apagar o pedido de uma nota já emitida deixa a movimentação (`cadmov`), o financeiro (`artficha`) e a nota (`toponf`/`itensnf`) sem origem. | `ped_wer.scx:3761-3773 (dump)`, `:658-698 (dump)` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

### 12.5 Abandono

`RF-185` | **Abandonar com itens lançados exige dois ESC e não é claramente reversível.** | `ped_wer.scx:3270-3276 (dump)`: o botão de saída, com `ws > 0`, recusa com `"JÁ EXISTE MOVIMENTAÇÃO. SE QUISER CANCELAR, PRESSIONE ESC 2 VEZES !"`. Mas o ESC do formulário, com `ws > 0`, **não cancela — ele fecha o pedido** (`:2830-2833` chama `cmdok1.click()`). O segundo ESC atua já dentro do `fechapedwer` (`:335-355`), onde a pergunta é `"Deseja Cancelar esta venda ?"` e a confirmação arma `wcnvar=1` + `sair1.click()` → `do desmPED`. **A versão web deve ter comandos distintos e rotulados para "fechar" e "cancelar".** | `ped_wer.scx:2830-2836 (dump)`, `:3270-3276 (dump)`; `fechapedwer.scx:335-355 (dump)` | **importante** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-186` | **O cancelamento na tela de fechamento também exige senha quando o tipo o exige.** | `fechapedwer.scx:345-350 (dump)`: `if ind_senha="S" → thisform.vesenha ; IF WERRO=3 RETURN`. Inerte por RF-123. | `fechapedwer.scx:345-350 (dump)` | **importante** |

---

## 13. Crédito e limite

A análise (`analise_wer_app.md` §4.3) descreve os dois regimes. Esta seção acrescenta a medição que faltava e especifica o contrato.

`RF-190` | **O limite de crédito é do CNPJ-raiz e o saldo devedor é do grupo econômico.** | `valt2w.prg:259-265`: `StrCgc = SUBSTR(Clientes.cgc,1,10)`; então dois cursores — `TmpCliCgc2` = clientes do mesmo CNPJ-raiz **com `Posicao = "M"`** (matriz), de onde sai o **limite**; `TmpCliCgc` = **todos** os clientes do mesmo CNPJ-raiz, sobre os quais o laço `:270-304` acumula `wsaldo` a partir de `artficha` com `dt_pag` vazio. | `valt2w.prg:259-304` | **essencial**

`RF-191` | **Três travas duras, na ordem, e nenhuma delas tem liberação por alçada.** | `valt2w.prg`: <br>**(T1) Atraso** — `:294` `If Empty(dt_pag) .And. dt_vencim + Clientes.cofins < Date() → watrazado = .T.`; e `:306-317` barra com `"Pedido Não Pode Ser Baixado. Cliente Em Atraso."` (`msgcomenta`), `ERRO = 1`. <br>**(T2) Limite zerado** — `:319-323` `IF TmpCliCgc2.credito = 0` → `MESSAGEBOX("Pedido não pode ser baixado. Limite zerado!", 0+48+256, wFENICIA)`, `ERRO = 1`. <br>**(T3) Estouro** — `:325-336` `If wsaldo + Pedido.Total_Nota > TmpCliCgc2.CREDITO` → `Messagebox(" Crédito do Cliente excedeu   " + Transf(wsaldo+Pedido.Total_Nota,"99,999,999.99") + CHR(13) + " O Pedido não pode ser baixado.", 0+48+256, wFENICIA)`, `ERRO = 1`. | `valt2w.prg:294`, `:306-336` | **essencial**

`RF-192` | **A tolerância de atraso é o campo `clientes.COFINS`, em dias.** | `valt2w.prg:294`: `dt_vencim + Clientes.cofins < Date()`. Note a linha anterior comentada (`:293`) que fazia `VAL(SUBSTR(Clientes.cofins,1,2))` — indício de que o campo já foi caractere. `FATO MEDIDO`: `clientes.COFINS ≠ 0` em **12.487 de 13.086** clientes (95,4%) — a tolerância é amplamente configurada, não exceção. A API deve expor `diasToleranciaAtraso`. | `valt2w.prg:293-294`; `Fox\WER\clientes.dbf` | **essencial**

`RF-193` | **Títulos podem ser excluídos do cálculo do saldo por tipo de vencimento.** | `valt2w.prg:277-290`: para cada título, se `artficha.cod_vencim <> "  "`, busca `vencim` e depois `vencimr` por `vencim.resumido`; se `vencimr.ind_credit = "N"`, **pula o título** (não soma no saldo). | `valt2w.prg:277-290` | **essencial**

`RF-194` | **Todo o bloco de crédito é pulado quando a verificação financeira está desligada.** | `valt2w.prg:252-257`: `If wareas.ind_ver_fi = "N" → wsaldo = 0 ; pctelaorigem = "PEDWER" ; DO FORM frminformadata ; Return`. `FATO MEDIDO`: `wareas.IND_VER_FI` está **vazio**, não `"N"` — portanto **a verificação de crédito ESTÁ ATIVA** nesta instalação. | `valt2w.prg:252-257`; `Fox\WER\WAREAS.DBF` | **essencial**

`RF-195` | **Existe uma segunda verificação de crédito na condição de pagamento, com três chaves de desligamento.** | `valida6wer.prg:430-455`: sob `wped_vend = "S" AND tipos.status = "C"`, e se `wcondpg <> "  "` **e** `wareas.cod_cli <> wclie` **e** `vencimr.ind_credit <> "N"` **e** `wareasb.indcredito <> "N"` **e** `vencimr.ind_credit <> "R"` **e** `clientes.CREDITO <> 0`, então `IF WSALDO + wsubtot > clientes.CREDITO` → mensagem detalhada com limite, acumulado, venda e valor permitido, e `ERRO = 1`. `FATO MEDIDO`: `wareas.COD_CLI = "1"`, `wareasb.INDCREDITO` **vazio** (≠ "N", logo não desliga). **Esta segunda checagem usa `clientes.CREDITO` do registro corrente, não o da matriz — divergindo de RF-190.** | `valida6wer.prg:430-455`; `Fox\WER\WAREAS.DBF`, `wareasb.dbf` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

### 13.1 Os dois regimes — medição inédita

`RF-196` | **O regime do limite está em `clientes.CSLL`: 1 = Constante, 0 = Não Constante.** | `FATO MEDIDO` (§4.3 de `analise_wer_app.md`): rótulos literais em `frmcliwer.scx :: Pageframe1.page3.Optiongroup1` (`Option1.Caption="Constante"`, `Option2.Caption="Não Constante"`). Consumo em `valt2w.prg:342-345`: `IF clientes.csll = 0 → REPLACE credito WITH 0 ; =TABLEUPDATE(.T.)` — executado **no registro da matriz** (o ponteiro foi posicionado em `:340` por `dbseek("codigo", TmpCliCgc2.codigo, ...)`), **depois** de o pedido passar por todas as travas. | `valt2w.prg:337-348` | **essencial**

`RF-197` | **A distribuição real do regime na base responde à pergunta aberta da análise (D14).** | `FATO MEDIDO` — `Fox\WER\clientes.dbf`, 13.086 clientes vivos: <br><br>| `CSLL` | clientes | leitura | <br>|---|---|---| <br>| `0.00` | **10.140** (77,5%) | Não Constante — gravado explicitamente | <br>| *(vazio)* | **2.368** (18,1%) | nunca tocado pela tela; VFP lê numérico vazio como 0 → **Não Constante** | <br>| `1.00` | **577** (4,4%) | Constante | <br>| `11.00` | **1** | resíduo de alíquota fiscal real de CSLL | <br><br>Ou seja: **12.508 clientes (95,6%) operam em regime de consumo único**, e o limite é zerado a cada baixa. O registro com `11.00` é a prova de que o campo já foi usado com o seu significado fiscal — e cai no `ELSE` do `IF clientes.csll = 0`, sendo tratado como **Constante**. | `Fox\WER\clientes.dbf`; `valt2w.prg:342` | **essencial** |

`RF-198` | **A leitura por `= 0` não distingue "Não Constante" de "não informado" nem rejeita valor inválido.** | O teste é `IF clientes.csll = 0`, e não há domínio fechado. Consequência: os 2.368 clientes em branco recebem o regime mais **restritivo** por omissão, e o valor `11.00` recebe o mais **permissivo**. A gravação, do outro lado, é `REPLACE csll WITH IIF(optiongroup1.value == 2, 0, 1)` com o `OptionGroup` nascendo em `value = 0` — logo cliente cadastrado sem tocar nos radios sai **Constante** (§4.3 D14). **A versão web deve ter um enumerado obrigatório (`CONSTANTE` / `NAO_CONSTANTE`), sem valor default silencioso e sem estado nulo.** | `valt2w.prg:342`; `analise_wer_app.md` §4.3 D14 | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-199` | **A medição do crédito disponível confirma o regime de consumo único.** | `FATO MEDIDO`: `clientes.CREDITO ≠ 0` em apenas **677 de 13.086** clientes (5,2%). Combinado com T2 (RF-191, "Limite zerado!"), significa que **12.409 clientes estão, neste instante, impedidos de ter pedido baixado até nova liberação de crédito**. Isso é consistente com o desenho "Não Constante", e não é anomalia. | `Fox\WER\clientes.dbf`; `valt2w.prg:319-323` | **essencial** |

`RF-200` | **A versão web deve modelar a liberação de crédito como evento auditado, não como edição de campo.** | Hoje o ciclo é: alguém edita `clientes.credito` no cadastro → a baixa consome → `valt2w.prg:343` zera. Não há registro de quem liberou, quanto, quando, para qual pedido. Com 95,6% do cadastro em consumo único, essa liberação é uma operação **diária e crítica**, e é invisível. | `valt2w.prg:342-345`; `Fox\WER\clientes.dbf` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

`RF-201` | **O cliente sem matriz cadastrada é tratado como limite zero.** | `FATO MEDIDO`: `clientes.POSICAO` = `M` em 11.644, `F` em 1.346, **vazio em 96**. Se nenhum cliente do CNPJ-raiz tiver `Posicao = "M"`, o cursor `TmpCliCgc2` sai **vazio**, e `valt2w.prg:319` lê `TmpCliCgc2.credito` de um cursor sem registros → 0 → barra com `"Limite zerado!"`. É comportamento defensável, mas a mensagem não explica a causa real. | `valt2w.prg:263`, `:319-323`; `Fox\WER\clientes.dbf` | **importante** |

`RF-202` | **O "vale-crédito" de devolução é entidade distinta do limite e não deve ser confundido.** | `FATO MEDIDO` (§4.3 de `analise_wer_app.md`, confirmado): existe `credito.dbf` acionado pela condição de pagamento `"CR"` (`valida6wer.prg:338-351`), sem relação com `clientes.credito`. **Armadilha de nomenclatura para a migração.** | `valida6wer.prg:338-351` | **importante** |

`RF-203` | **A verificação de crédito no lançamento e a da baixa devem ser a mesma regra.** | Hoje são três implementações diferentes: `vecredito` no lançamento (`ped_wer.scx:1145-1157 (dump)`, que apenas avisa e com sinal invertido — RF-140), `valt2w.prg:249-345` na baixa (limite da matriz, saldo do grupo, três travas) e `valida6wer.prg:430-455` na condição de pagamento (limite do registro corrente). **A versão web deve expor uma única função de avaliação de crédito, chamada nos três momentos, devolvendo o mesmo veredito.** | `ped_wer.scx:1145-1157 (dump)`; `valt2w.prg:249-345`; `valida6wer.prg:430-455` | **essencial** `⚠ DIVERGE DO CÓDIGO ATUAL` |

---

## 14. Parâmetros medidos: o que está ligado e o que está desligado

`FATO MEDIDO` — leitura binária de `Fox\WER\WAREAS.DBF` (1 registro), `wareasb.dbf` (1), `wareascp.dbf` (1), `tipos.dbf` (487), `vencim.dbf` (99), `vendedor.DBF` (193), `posicao.dbf` (2), `grupoalc.dbf` (0). Snapshot de 15/07/2026.

### 14.1 `wareas`

| Campo | Valor | Efeito no lançamento | Estado |
|---|---|---|---|
| `NOME_EMPR` | `WER` | — | — |
| `ESTADO` | `RJ` | UF de referência da validação de CFOP quando `wc_empr` vazio | ATIVO |
| `TP_PEDIDO` | `PED` | tipo default de venda | ATIVO |
| `TP_PED_COM` | `PEF` | tipo default de compra | ATIVO |
| `COD_CLI` | `1` | cliente "consumidor" — dispensa `vecliente` e a 2ª checagem de crédito | ATIVO |
| `COD_VEND` | `VEN` | vendedor default quando o cliente não tem | ATIVO |
| `CONT_PED` | `39` | contador de venda | ATIVO, praticamente sem uso (RF-102) |
| `CONT_COM` | `192` | contador de compra | ATIVO |
| `COMP_SEQ` | *(vazio)* | prefixo do número | sem prefixo |
| `IND_PED_DC` / `IND_COM_DC` | `S` / `S` | numeração automática ligada | ATIVO |
| `PROX_NOTA` | `77779` | nº da NF na baixa | ATIVO |
| `IND_NOTA` | `S` | gera nº de NF na baixa | ATIVO |
| **`LIM_DESC`** | **`99.99`** | teto global de desconto | **INERTE** — neutraliza 3 alçadas (RF-124) |
| **`IND_VER_FI`** | *(vazio)* | ≠ `"N"` → verificação de crédito **ativa** | **ATIVO** |
| `IND_NEGAT` | `N` | não permite estoque negativo na baixa | ATIVO |
| **`IND_LINHA`** | **`70`** | limite de itens por pedido; ≠ 0 desliga a trava de "pedido já liberado" em `valt2w.prg:117` | ATIVO (RF-143) |
| `IND_GRADE` | `N` | grade desligada | INERTE — toda a lógica de grade |
| `IND_TOT_N` | `N` | ≠ `"S"` → segunda condição de pagamento **ativa** | ATIVO (RF-156) |
| `IND_ORCAM` | `N` | não é orçamento | ATIVO |
| **`IND_PRE`** | **`S`** | abre `frmipiicm` em **todo item** — operador edita CST/alíquotas | **ATIVO** (RF-058) |
| **`BXEST_PED`** | **`N`** | validação de saldo no lançamento | **INERTE** (RF-144) |
| `IND_OBS` | `N` | observação de item | INERTE |
| `IND_FORMUL` | `N` | formulação/custeio | INERTE |
| `IND_PED_E` | `N` | financeiro no lançamento | INERTE (RF-181) |
| `IND_DESC` | `S` | desconto de capa visível | ATIVO |
| `IND_AUT_V` | `S` | botão de vendedores visível | ATIVO (RF-133) |
| `IND_CREDIT` | `N` | abre `conta_credito` conforme `vencim.ind_crt_cr` | INERTE (RF-157) |
| `LIM_PREMIO` | `9999999.99` | tela `clipref` no fechamento | INERTE |
| `IND_IMPPED` / `MOD_PEDIDO` / `IND_IMP` | `S` / `PE` / `N` | imprime pelo modelo `PE` | ATIVO |
| `IND_CUSTOP` | `N` | tela `nfcusto` | INERTE |
| `CONS_MIN` | *(vazio)* | grava 0 em `pedido.perc_vend` | INERTE (RF-042) |
| `IND_FIX_PE` | *(vazio)* | não força o tipo de operação | INERTE |
| `IND_WEBB` | *(vazio)* | integração WEBB | INERTE |
| `NUM_SEQ_RE` | `4` | nº de caracteres da referência para agrupar grade | INERTE (grade off) |
| `IND_SICON` | `S` | — | — |

### 14.2 `wareasb`, `wareascp`

| Campo | Valor | Efeito | Estado |
|---|---|---|---|
| `wareasb.LIMITE_PED` | *(vazio)* | alçada por valor de pedido | **INERTE** (RF-134) |
| `wareasb.BLOQ_PEDID` | *(vazio)* | código de posição de bloqueio | **INERTE** (RF-012, RF-135) |
| `wareasb.INDCREDITO` | *(vazio)* | ≠ `"N"` → 2ª checagem de crédito **ativa** | ATIVO |
| `wareasb.INDSIMPLES` | `F` | decide CST × CSOSN | ATIVO — não é Simples |
| `wareascp.IND_ARREND` | **`S`** | arredonda (`ROUND`) em vez de truncar | ATIVO |
| `wareascp.IND_PRAZO` | *(vazio)* | prazo é nº de dias, não data | ATIVO |
| `wareascp.IND_LARGCO` | *(vazio)* | mantém dormente a colisão de `qtd_larg`/`qtd_comp` | (RF-055) |
| `wareascp.IND_ODBC` | *(vazio)* | ≠ `"SQ"` e ≠ preenchido → consome `strdtaux` no financeiro | ATIVO (RF-149) |
| `wareascp.IND_PED_PR` | *(vazio)* | tela `cadmatpr` na entrada | INERTE |
| `wareascp.ESPECIFICO` | *(vazio)* | mecanismo `especifico` do ERP | **NÃO USADO** |

`RF-210` | **O arredondamento do total do item é configurável e a fórmula não é a trivial.** | `ped_wer.scx:395-400 (dump)`: `IF wareascp.ind_arrend="S" → lntotaux = ROUND(int(((qtd*preco)*100)*10)/1000, 2)` senão `lntotaux = (int((qtd*preco)*100)*10)/1000`. A mesma fórmula aparece em `valt2w.prg:209-213`. `FATO MEDIDO`: `IND_ARREND = "S"`. **A versão web deve reproduzir a expressão literalmente, não uma equivalência algébrica — a ordem de `int()` e `/1000` define o resultado no centavo.** | `ped_wer.scx:395-400 (dump)`; `valt2w.prg:209-213`; `Fox\WER\wareascp.dbf` | **essencial** |

### 14.3 `tipos` (487 registros) e cadastros auxiliares

| Campo | Distribuição medida | Consequência |
|---|---|---|
| `TIPO_ES` | `E`=301, `S`=186 | 186 tipos de saída |
| `STATUS` | `F`=283, `C`=204 | `C` = opera com cliente |
| `IND_QTD` | `S`=418, `N`=69 | 69 tipos não reservam estoque (RF-161) |
| **`IND_VALB`** | `0`=453, `1`=26, `5`=6, `4`=1, `2`=1 | trava de preço inerte para os tipos em uso (RF-074) |
| **`IND_SENHA`** | `S`=**1**, vazio=486 | senha de alteração/exclusão inerte (RF-123) |
| `IND_COMISS` | `S`=132, `N`=353, vazio=2 | 353 tipos não passam pela escolha de vendedor (RF-133) |
| **`IND_EMPENH`** | **vazio em 487** | `pedhead` nunca é gravado (RF-167) |
| **`POSICAO`** | **vazio em 487** | `pedido.POSICAO` sempre vazio (RF-012) |
| `POSICAOMOV` | vazio em 487 | — |
| `TIP_BAIXA` | vazio=485, `PEA`=1, `331`=1 | baixa usa `wareas.tp_pedido` |
| `vencim` (99) — `IND_DIA_PF` | vazio em 99 | tela `diapref` inerte |
| `vencim` (99) — `IND_CRT_CR` | vazio em 99 | tela `conta_credito` inerte |
| `vencim` (99) — `LIM_VENDA` | `0.00` em 99 | V68 inerte |
| `vendedor` (193) — `TIPO_VEND` | `R`=118, `I`=62, `V`=10, vazio=3 | 183 caem no `ELSE` da escada (RF-091) |
| `posicao` (2) | `98` bloqueia, `99` desbloqueia | nunca referenciado (RF-012) |
| `grupoalc` | **0 registros** | alçada por família inerte (RF-134) |
| `tabcomis` (10) | 2 escadas × 5 degraus | RF-089 |
| `negocia` (11.996) | 8 campos | RF-075 a RF-082 |

`RF-211` | **Aproximadamente 45% das regras condicionais lidas estão inertes por configuração.** | Consequência de projeto: **a versão web não deve portar código inerte "por segurança".** Cada regra desligada precisa de uma decisão — implementar, descartar, ou registrar como pendência. Portar tudo reproduz 15 anos de acúmulo e multiplica a superfície de teste sem benefício mensurável. Portar nada perde regra de negócio real do ERP (ex.: unidades M2/ML/M3, alçada por família, bloqueio por posição). | esta seção | **essencial** |

`RF-212` | **A configuração precisa ser explícita e versionada, não um registro único sem histórico.** | `wareas`, `wareasb` e `wareascp` têm **1 registro cada**, sem data, sem autor, sem histórico. Mudar `wareas.LIM_DESC` de 99,99 para 10 altera silenciosamente o comportamento de três alçadas, sem rastro. | `Fox\WER\WAREAS.DBF`, `wareasb.dbf`, `wareascp.dbf` | **importante** `⚠ DIVERGE DO CÓDIGO ATUAL` |

---

## 15. Lacunas que exigem decisão

Cada item abaixo é uma bifurcação de negócio, não uma dúvida técnica. Nenhuma pode ser resolvida por leitura de código.

| # | Lacuna | Por que não dá para decidir sozinho | Requisitos afetados |
|---|---|---|---|
| **L1** | **Semântica do preço negociado.** É preço **final ao cliente** (e então o percentual de desconto deve comparar cru contra cru) ou **preço-base** (e então os três fatores do cliente devem incidir sobre ele também)? | As duas leituras são defensáveis e produzem valores diferentes; 966 de 2.099 itens negociados estão expostos. A escolha muda o percentual de desconto e, por consequência, a **faixa de comissão paga**. | RF-079, RF-070 |
| **L2** | **Qual vendedor determina a escada de comissão** — o vendedor default do cliente (comportamento atual) ou o vendedor efetivamente registrado no pedido? | Muda o percentual pago. Se for o do pedido, a escada tem de ser resolvida **depois** do fechamento, não durante a digitação do item, o que reordena o pipeline. | RF-090, RF-027 |
| **L3** | **O código do pedido continua sendo texto livre digitado pelo operador?** | O dado mostra que é uma referência comercial (`MR518A`, `XANDE`). Suprimir quebra a operação; manter como chave impede unicidade e ordenação. Proposta: identidade interna + `referenciaExterna`. Precisa de confirmação de quem opera. | RF-102, RF-109, RF-104 |
| **L4** | **O segundo vendedor é remunerado ou informativo?** | Hoje é obrigatório e recebe zero. Se for remunerado, falta a regra de rateio (percentual fixo? metade? por tabela?). | RF-130, RF-127 |
| **L5** | **Validar saldo de estoque no lançamento?** | Está desligado (`BXEST_PED="N"`) e a primeira verificação é na baixa. Ligar muda a experiência de venda e pode barrar pedido legítimo de produto em produção. | RF-144 |
| **L6** | **Reabilitar a edição de item lançado?** | Proibida desde 27/10/2009 por decisão explícita (ficha 4600JM). Reabilitar exige reexecutar todo o pipeline do item e reintroduz oito comportamentos hoje desligados. | RF-170, RF-172, RF-171 |
| **L7** | **Pedido já baixado pode ser alterado ou cancelado?** | Hoje o sistema **avisa e permite continuar**. Como a alteração é apagar-e-regravar, isso desvincula a movimentação, o financeiro e a nota já emitidos. | RF-014, RF-184, RF-010 |
| **L8** | **O bloqueio de faturamento por posição volta a existir?** | A tabela `posicao` tem os dois códigos prontos (98/99, "DIRETORIA") e nada os usa. Se a diretoria quer bloqueio, faltam três peças: limite configurado, `grupoalc` povoado e `wareasb.Bloq_Pedid` preenchido. | RF-012, RF-135, RF-134 |
| **L9** | **A alçada real de desconto é só a de item?** | `LIM_DESC = 99,99` neutraliza as três alçadas com senha. Ou o teto global deve voltar a valer, ou ele é intencionalmente irrelevante e o controle é a coluna do produto + `clientes.PIS`. | RF-124, RF-083 |
| **L10** | **Os seis campos fiscais reinterpretados são renomeados no schema novo?** | `INSS`, `IRRF`, `ISS`, `PIS`, `COFINS`, `CSLL` em `clientes` carregam significado comercial neste cliente e fiscal em outros, **sem discriminador no dado** — e a base tem um `CSLL = 11,00` que é alíquota fiscal real. Um contrato de API que exponha `cliente.inss` mente para metade dos consumidores. | RF-071, RF-197, RF-192 |
| **L11** | **Qual é o comportamento correto para desconto por percentual de item?** | O campo está oculto. Se voltar, tem de alimentar a escada e a base de comissão — hoje não alimenta nenhuma das duas. | RF-051, RF-094 |
| **L12** | **A negociação passa a ser por empresa?** | 3.977 negociações vigentes têm preço de tabela diferente entre `fenwin02` e `fenwin03`. Sem empresa na chave, a comissão depende da empresa de lançamento. | RF-081 |

`NÃO VERIFICADO` — itens que exigem medição ou execução antes de fundamentar decisão:

| # | O que falta medir | Por que importa |
|---|---|---|
| N1 | Conteúdo das classes `credito_clientew`, `senha_supervisor`, `senha_gerente`, `valida_credito`, `pos_saldo_estoque` (vivem em VCX, não lidas) | são a implementação real de três alçadas e da validação de crédito no lançamento (RF-140) |
| N2 | Conteúdo de `desmped`, `patualiz`, `totnotaf`, `totnotap`, `ptotnota`, `mod_ped`, `veicm3`, `frmipiicm` (programas/formulários externos) | `desmped` é o desfazer da reserva (RF-160); `frmipiicm` é onde o operador edita imposto (RF-058) |
| N3 | Valor de projeto de `pwaltera` e `OkToLeave` no `.SCT` | RF-170 sustenta `.F.` por contradição com o dado, não por leitura direta |
| N4 | Comportamento do filtro `set filter to tipo_vend<>"I  "` sobre campo mais estreito | RF-131 |
| N5 | Se um pedido de 163 itens é possível hoje com `IND_LINHA = 70` | RF-143 |
| N6 | Regras, *triggers* e *defaults* dentro de `FENICIA.DBC` (15 MB, nunca aberto) | pode conter validação de negócio que não aparece em nenhum `.prg` |
| N7 | Leitura de numérico vazio em VFP quando a tabela tem `_NullFlags` | RF-197 assume 0 para os 2.368 clientes com `CSLL` vazio |

---

## 16. Rastreabilidade

### 16.1 Contagem por seção

| Seção | Faixa | Qtd. | essencial | importante | desejável |
|---|---|---|---|---|---|
| 1. Contexto e modelo | RF-001 a RF-004 | 4 | 4 | 0 | 0 |
| 2. Máquina de estados | RF-005 a RF-014 | 10 | 6 | 4 | 0 |
| 3. Sequência obrigatória | RF-020 a RF-031 | 12 | 10 | 2 | 0 |
| 4. Campos de cabeçalho | RF-040 a RF-042 | 3 | 0 | 1 | 2 |
| 5. Campos de item | RF-050 a RF-060 | 11 | 6 | 5 | 0 |
| 6. Preço, desconto, comissão | RF-070 a RF-097 | 28 | 20 | 7 | 0 (+1 informativo: RF-072) |
| 7. Numeração | RF-100 a RF-110 | 11 | 8 | 3 | 0 |
| 8. Autorizações e alçadas | RF-120 a RF-137 | 18 | 8 | 10 | 0 |
| 9. Validações (requisitos) | RF-140 a RF-149 | 10 | 5 | 4 | 1 |
| 10. Fechamento e cond. pagto | RF-150 a RF-158 | 9 | 3 | 5 | 1 |
| 11. Efeitos colaterais | RF-160 a RF-169 | 10 | 5 | 5 | 0 |
| 12. Alteração e exclusão | RF-170 a RF-186 | 17 | 13 | 4 | 0 |
| 13. Crédito | RF-190 a RF-203 | 14 | 12 | 2 | 0 |
| 14. Parâmetros | RF-210 a RF-212 | 3 | 2 | 1 | 0 |
| **Total** | | **160** | **102** | **53** | **4** |

Validações catalogadas na §9: **70** — V01-V25 (cabeçalho, 25), V30-V54 (item, 25), V60-V71 (fechamento, 12), V80-V87 (vendedores e data, 8) — todas com o texto literal da mensagem exibida ao operador.

### 16.2 Requisitos marcados `⚠ DIVERGE DO CÓDIGO ATUAL`

São **52** dos 160 (32,5%). O código atual não os cumpre; a migração não deve replicar o defeito.

RF-009, RF-010, RF-013, RF-014, RF-051, RF-052, RF-053, RF-054, RF-059, RF-060, RF-074, RF-078, RF-079, RF-080, RF-081, RF-087, RF-090, RF-091, RF-093, RF-094, RF-105, RF-106, RF-107, RF-108, RF-109, RF-121, RF-123, RF-125, RF-126, RF-132, RF-137, RF-140, RF-141, RF-145, RF-146, RF-155, RF-160, RF-162, RF-163, RF-164, RF-166, RF-168, RF-169, RF-174, RF-183, RF-184, RF-185, RF-195, RF-198, RF-200, RF-203, RF-212.

### 16.3 Confirmações e refutações contra a análise anterior

| Achado de `analise_wer_app.md` | Este documento | Prova |
|---|---|---|
| §6.4 **R2** — "alteração de item é código morto" | **CONFIRMADO e reforçado** — três provas independentes, e a decisão é **documentada com data e ficha** (27/10/2009, 4600JM): é requisito, não acidente | RF-170 |
| §4.2 **D7** — "as duas buscas de faixa arredondam em direções opostas; erra dinheiro hoje" | **A divergência é INALCANÇÁVEL.** O segundo algoritmo (`:1945-1969`) está dentro de `IF pwaltera`, verificado por balanceamento de `IF/ENDIF`. D7 e R2 são mutuamente excludentes e R2 prevalece. O requisito de faixa única permanece; o defeito **operante** é outro (RF-093) | RF-095 |
| §4.2 **D9** — "`ComisVdawer` grava `cod_vend1` em quatro tabelas" | **REFUTADO.** O formulário não contém nenhum `REPLACE`/`INSERT`/`APPEND`; alimenta duas memvars, e o gravador é `pcondpg2.prg:246-247` | RF-129 |
| §4.2 **D9** — "não há rateio" | **CONFIRMADO** | RF-130 |
| §4.2 **D9** — "exige senha de supervisor para trocar o primeiro vendedor" | **CONFIRMADO**, com a ressalva de que `widentific = "SUPORTE"` é isento | RF-126 |
| §4.3 **D14** — "efeito na base NÃO MEDIDO; rodar `SELECT csll, COUNT(*)`" | **MEDIDO**: 10.140 zeros, 2.368 vazios, 577 uns, 1 com `11,00`. **95,6% em consumo único** | RF-197 |
| §4.4 — "`clientes.perc_desc`: HIPÓTESE morto" | **CONFIRMADO MORTO por medição**: `PERC_DESC = 0` nos 13.086 clientes. Note que o código o **lê** em dois pontos (`ped_wer.scx:5667 (dump)` exibe; `fechapedwer.scx:463 (dump)` semeia o desconto de capa) | RF-150, tabela §4 |
| §4.2 — "colisão de `qtd_comp` dormente por configuração" | **CONFIRMADO**, e localizada a terceira colisão: `txtAddText.GotFocus:3180-3183 (dump)` lê `qtd_larg`/`qtd_comp` como largura e comprimento | RF-054, RF-055 |
| §4.1 — "o `vepreco` nativo está desativado" | **CONFIRMADO e precisado**: o mecanismo vivo é `Text3` (`wind_venda`), que está `Visible=.F.` e fixado em `"1"`; `pedido.IND_VENDA="1"` em 102.100 de 102.104 | RF-073 |
| §3.2 — "tabelas gravadas no fluxo A" | **CONFIRMADO e ampliado**: falta `pedemp` via `deleta_cademp` (DELETE antes do INSERT) e a **terceira** passagem de escrita em `pedido` (`fecha_nota`) | RF-163, §11.2 |
| §4.3 — "o limite é da matriz e o saldo é do grupo" | **CONFIRMADO**, e localizada uma **segunda** verificação que usa o limite do **registro corrente** (`valida6wer.prg:443`), divergindo | RF-195 |

### 16.4 Fontes lidas

| Arquivo | Como |
|---|---|
| `C:\Users\Lana\Desktop\Sistemas\Fox\Pedido\ped_wer.scx` | despejo (6.501 linhas, 59 objetos) |
| `C:\Users\Lana\Desktop\Sistemas\Fox\Pedido\fechapedwer.scx` | despejo (1.782 linhas, 29 objetos) |
| `C:\Users\Lana\Desktop\Sistemas\Fox\Pedido\ComisVdawer.SCX` | despejo (597 linhas, 16 objetos) |
| `C:\Users\Lana\Desktop\Sistemas\Fox\Pedido\frminformadata.SCX` | despejo (8 objetos) |
| `C:\Users\Lana\Desktop\Sistemas\Fox\Pedido\pcondpg2.prg` | 389 linhas, CP1252 |
| `C:\Users\Lana\Desktop\Sistemas\Fox\Pedido\valt2w.prg` | 416 linhas, CP1252 |
| `C:\Users\Lana\Desktop\Sistemas\Fox\Pedido\valida6wer.prg` | 550 linhas, CP1252 |
| `C:\Users\Lana\Desktop\Sistemas\Fox\Pedido\est326wer.PRG` | 101 linhas, CP1252 |
| `C:\Users\Lana\Desktop\Sistemas\Fox\Pedido\ped327wer.prg` | 2.035 linhas, CP1252 (leitura parcial) |
| `C:\Users\Lana\Desktop\Sistemas\Fox\Pedido\ped_wer.SCT` | leitura binária (verificação de propriedade customizada) |
| `C:\Users\Lana\Desktop\Sistemas\Fox\WER\*.dbf` | parsing binário somente-leitura: `PEDIDO`, `PEDHEAD`, `pedemp`, `posicao`, `tipos`, `clientes`, `vendedor`, `vencim`, `tabcomis`, `negocia`, `grupoalc`, `WAREAS`, `wareasb`, `wareascp` |
| `C:\Users\Lana\Desktop\Sistemas\Fox\DOC_ANALISE_WER_20260818\analise_wer_app.md` | referência |
| `...\relatorios_frentes\rel_B_pedido.md` | referência |

Nenhum arquivo foi criado ou alterado nas pastas `Fox\Pedido` e `Fox\WER`.

---

## 17. Ordem de ataque sugerida

Complementa a §7.7 de `analise_wer_app.md`, agora com os requisitos nomeados.

| Fase | Escopo | Requisitos que fecha | Pré-requisito |
|---|---|---|---|
| **0** | Decidir L1, L2, L3, L6, L7 | RF-079, RF-090, RF-109, RF-172, RF-014 | nenhum — são decisões de negócio |
| **1** | Extrair no VFP uma função pura de precificação: `(cliente, produto, empresa, quantidade, tipo) → {precoTabela, precoNegociado, tetoDesconto, descontoPercentual, faixaComissao}`, com testes nos degraus da escada | RF-070, RF-075, RF-083, RF-092, RF-093, RF-095 | Fase 0 (L1, L2) |
| **2** | Modelo de dados novo: cabeçalho × item, campos com nome de domínio, estado explícito, sem campo polivalente | RF-001, RF-002, RF-054, RF-155, RF-162, RF-198 | Fase 0 (L3, L10) |
| **3** | Numeração transacional + rascunho persistente + reserva com expiração | RF-052, RF-108, RF-160, RF-163 | Fase 2 |
| **4** | Função única de avaliação de crédito, chamada no lançamento, no fechamento e na baixa | RF-190, RF-191, RF-200, RF-203 | Fase 2 |
| **5** | Catálogo de validações (§9) como regras nomeadas e testáveis, com mensagens revisadas | V01-V87, RF-013, RF-087, RF-105, RF-145 | Fases 1 e 2 |
| **6** | Autorizações por perfil, com reverificação por operação e log de diff | RF-121, RF-123, RF-126, RF-166 | Fase 2 |
| **7** | Alteração e cancelamento com identidade estável de item e estado CANCELADO | RF-009, RF-010, RF-174, RF-183, RF-184 | Fases 2, 3 e 6 |

