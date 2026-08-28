# Integrador

Console C# (.NET 9, compilado como x86) que fala diretamente com as tabelas
VFP via VFPOLEDB. É o único componente da arquitetura que toca DBF/DBC — a
API e o front nunca acessam a base do VFP diretamente (ver
`C:\Users\asena\.claude\plans\dreamy-growing-bear.md`).

## Arquitetura de rede (revisada em 24/08/2026)

**O console só faz chamadas de SAÍDA — nunca recebe conexão.** Isto é
deliberado: a máquina do cliente deve ter o menor footprint possível
(nenhuma porta escutando, nenhum firewall a abrir). Rodar com:

```
Integrador.exe servico <urlApiCentral>   # ex.: http://localhost:3001
```

Isso inicia dois laços contínuos (`Servico/`), sempre iniciados pelo console:

1. **`LacoSincronizacao`** — a cada 30s, empurra (`POST`) uma cópia dos
   dados de referência (clientes, produtos por empresa, tabcol, vendedores,
   títulos abertos) para a API central. A API central passa a responder
   consultas com essa cópia — não depende do console estar de pé no
   instante do clique. Preço: a tela pode ficar até 30s desatualizada em
   relação ao VFP (troca deliberada).
2. **`LacoComandos`** — a cada 2s, busca (`GET /comandos/pendentes`) pedidos
   já decididos pela API central (preço/desconto/comissão já calculados
   pelo núcleo de regras — ver `nucleo/Nucleo.Api`), reconstrói o mesmo
   `Dominio.Pedido` testado, grava via `MapeadorPedidoParaVfp` +
   `PedidoDbfRepositorio` (sem alteração), e reporta o resultado
   (`POST /comandos/{id}/resultado`).

O antigo modo "console como servidor HTTP" (`Integrador.Api`, ouvindo
`/clientes`, `/precificar` etc.) foi **removido** — ele exigia uma porta
aberta na máquina do cliente e não escalava para "vários clientes, um
servidor central". As contas de cálculo que ele fazia (preço/comissão/
crédito) mudaram de casa para `nucleo/Nucleo.Api` — roda ao lado da API
central, sem VFPOLEDB, sem restrição de x86.

## Por que x86

`VFPOLEDB.1` só está registrado no hive de 32 bits do Windows nesta máquina
(confirmado em 20/08/2026 via registro). O projeto força
`PlatformTarget=x86` / `RuntimeIdentifiers=win-x86` por isso — compilar como
x64 falha ao abrir a conexão.

## Caminhos

- Fontes VFP (nunca tocar): `Z:\Desenv9\WER\PedidoWER`
- Base de produção (autorizada para leitura em 20/08/2026): `Z:\BASES_CLIENTES\WER`

`Vfp/VfpConexao.cs` é o único lugar que monta a connection string.

## O que está fechado (Fase 3 do plano — leitura)

Repositórios em `Leitura/`, todos SOMENTE LEITURA, com nomes físicos do VFP
(decisão L10 — a tradução para nomes de negócio acontece na API, nunca aqui):

| Repositório | Tabela(s) | Observação |
|---|---|---|
| `ClienteRepositorio` | `clientes` | busca por código; busca de grupo econômico por raiz de CGC |
| `CadmatRepositorio` | `cadmat` (por empresa) | exige código da empresa — nunca ler sem prefixo |
| `NegociaRepositorio` | `negocia` | container próprio, global |
| `TabcomisRepositorio` | `tabcomis` | schema real é NORMALIZADO (negocio/desc/comis por linha) — descoberto ao vivo, diferente da tabela "larga" que a prosa dos documentos de análise sugeria |
| `VendedorRepositorio` | `vendedor` | |
| `TiposRepositorio` | `tipos` | |
| `ParametrosRepositorio` | `wareas`/`wareascp`/`wareasb` | cada uma é 1 registro único e global |
| `VencimRepositorio` | `vencim`/`vencimr` | |
| `CreditoRepositorio` | `artficha` | filtra `dt_pag IS NULL` DENTRO da consulta SQL — é a otimização que o doc de integração (§3.3a) aponta como necessária para o endpoint ser viável (hoje o VFP filtra depois de ler, varrendo até 10.631 linhas por pedido) |
| `TabcolRepositorio` | `tabcol` (por empresa) | `Nome` é char mas guarda número — `VAL()` explícito |
| `CadsubRepositorio` | `cadsub` (por empresa) | ST/MVA/pauta |
| `CadicmRepositorio` | `cadicm` (por empresa) | alíquota por UF |
| `CcustoRepositorio` | `ccusto` | |

`Vfp/EsquemaTabela.cs` é uma ferramenta de diagnóstico (só schema, nunca
linhas) — útil para conferir nomes de campo reais antes de escrever qualquer
consulta nova, em vez de confiar na grafia da prosa dos documentos.

### Defesa contra o defeito RF-105/D6

Toda busca por chave faz `WHERE campo = ?` e **confere a igualdade exata em
C# depois** (`LeitorHelpers.ChaveExata`). Isso existe porque, sob
`SET EXACT OFF` (default do VFP), `"AB25" = "AB2557A"` é verdadeiro por
prefixo — o mesmo defeito documentado em `ped_wer.scx`. Não confiamos em que
o provider OLE DB resolva isso corretamente sozinho.

### Demo de ponta a ponta (CLI)

```
Integrador.exe schema <tabela> [codigoEmpresa]
Integrador.exe amostra
Integrador.exe credito <codigoCliente>
Integrador.exe preco <empresa> <cliente> <grupo> <referencia> <tipoPrc> <qtd> [precoDigitado]
```

`credito` e `preco` já foram executados contra a base de produção real
(somente leitura) em 20/08/2026 e produziram resultado correto, incluindo o
caminho de rejeição por teto de desconto (`DESCONTO_ACIMA_DO_TETO`) com valor
calculado a partir de dado real (`clientes.pis` + `tabcol.nome` do produto).

## Mapeamento de escrita (metade de ida da Fase 5)

`Escrita/MapeadorPedidoParaVfp.cs` traduz um `Dominio.Pedido` (Fechado) para
as linhas físicas que `pedido.dbf` espera — cabeçalho repetido por item,
nomes físicos, incluindo o mapeamento deliberado de volta aos campos
polivalentes que os relatórios de comissão existentes ainda leem
(`PercentualComissao` → `qtd_comp`, `PrecoTabelaAjustado` → `qtd_larg`).
**Função pura — não abre conexão, não grava nada.** Falha explicitamente
(não inventa valor) se o pedido não estiver fechado ou não tiver
`ReferenciaExterna` (a regra real dos prefixos do código ainda não foi
respondida pelo negócio — §9-Q4 dos documentos). Campos fiscais, reserva de
estoque, posição de bloqueio e número de nota ficam `null` de propósito —
ver `dominio/README.md`. 7 testes em `Integrador.Tests/`.

`Escrita/PedidoDbfRepositorio.cs` grava de fato — `INSERT` dentro de uma
`OleDbTransaction` (RF-163), com rollback em qualquer falha. **Testado com
sucesso em 20/08/2026** contra a cópia de testes em
`Z:\BASES_CLIENTES\WER` (autorizado explicitamente pelo usuário — essa base
não é produção viva, é uma cópia criada para isso):

1. `testar-transacao` — confirma que `OleDbConnection.BeginTransaction()`
   funciona de verdade contra VFPOLEDB: INSERT+ROLLBACK faz o registro
   desaparecer, INSERT+COMMIT faz o registro sobreviver, ambos confirmados
   por leitura numa conexão nova. Resolve a incerteza que o doc de
   integração deixava em aberto sobre transação real via VFPOLEDB.
2. `gravar-pedido-teste <empresa> <cliente> <grupo> <referencia> <qtd>` —
   fluxo completo: lê cliente/produto reais, calcula preço, monta e fecha um
   `Pedido`, mapeia para linha física, grava, e confirma por leitura. Todos
   os valores conferidos manualmente batem (preço, comissão, total).

**Achado técnico que mudou o desenho:** via VFPOLEDB, um `INSERT` com lista
PARCIAL de colunas falha — praticamente todo campo de `pedido.dbf` está
marcado `NOT NULL` no schema, e colunas omitidas não recebem o zero-valor do
tipo (como um `APPEND BLANK` nativo do VFP faria); o provider tenta gravar
`NULL` nelas e o VFP rejeita. A solução, em `Escrita/GravadorGenerico.cs`, é
montar o `INSERT` com **todas** as colunas da tabela, preenchendo com o
zero-valor do tipo (0 / "" / a data do próprio pedido, como placeholder,
para campos de data não modelados — ver nota no código) o que não temos, e
sobrepondo com o que sabemos de verdade. Isso é exatamente o que
`APPEND BLANK` + `REPLACE` faz no `pcondpg2.prg` original — só que expresso
via schema, não uma lista de ~130 nomes de campo escritos à mão.

Cuidado registrado: campos char do VFP truncam em silêncio (`codigo` é
`C(10)`) — usar `ChaveExata`/tamanhos corretos ao gerar `ReferenciaExterna`
e qualquer outro valor que vá para um campo de largura fixa.

## O que NÃO está fechado — de propósito

- **Fila de comandos persistente.** Hoje `ComandosService`/`CacheService`
  (lado Node) guardam tudo em memória — um restart da API central perde
  comandos em andamento e a cópia sincronizada. Antes de valer para
  produção, isso precisa de um banco real (Postgres, como o plano original
  já previa).
- **Transação cobrindo várias tabelas** (`pedido`+`pedemp`+`artficha`). O
  spike da Fase 0 confirmou que as três são membros de `.dbc` (favorece
  transação real), e `testar-transacao` comprova `BEGIN/END TRANSACTION`
  funcionando para uma tabela — mas a fronteira maior ainda não foi testada.
- **Negociação de preço (RF-075).** Ainda não entra no laço de
  sincronização nem no cálculo de `/precificar` — gap registrado ao
  reorganizar a arquitetura em 24/08/2026.
- **Regra real de numeração do pedido** (§9-Q4 dos documentos) — a API
  central ainda gera uma referência placeholder (`WB<timestamp>`).
