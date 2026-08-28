# Dominio

Modelo de dados novo do lançamento de pedido (Fase 2 do plano) — agregado
`Pedido`/`ItemPedido` em C#, com a contraparte SQL em `../modelo-dados/schema.sql`.
Sem I/O: este projeto não conhece VFPOLEDB, Postgres nem HTTP — só as
invariantes do negócio. Referencia `MotorRegras` para os cálculos
(`ComissaoService`, `OrigemPreco`) em vez de duplicá-los.

## O que o agregado resolve, e por quê

| Decisão | RF | Como |
|---|---|---|
| Cabeçalho e item são entidades separadas | RF-001/002 | `Pedido` tem os campos únicos; `ItemPedido` é lista, não repetição |
| Item tem identidade estável | RF-174 | `Numero` é fixado na criação, nunca recalculado por posição na lista |
| Estado do item é explícito | RF-162 | `EstadoItemPedido.Ativo/Excluido` — substitui os três sentinelas de hoje |
| Sem campos polivalentes | RF-054 | `PercentualComissao` e `MedidaLargura` são campos próprios, nunca reaproveitam `qtd_comp`/`qtd_larg` |
| Linha de negócio única por pedido | RF-142 | Primeiro item trava `LinhaNegocioGrupo`; item de outro grupo é rejeitado |
| Comissão resolvida no fechamento, com o vendedor do pedido | RF-089-097, decisão L2 | `PercentualComissao` só é preenchido dentro de `Fechar(...)`, nunca em `AdicionarItem` |
| Cancelamento é lógico | RF-009 | `Cancelar(autor, motivo)` muda estado; não existe método de exclusão física |
| Quantidade e existência de item validadas no próprio construtor | RF-088 | `ItemPedido` rejeita quantidade ≤ 0 antes de existir |

## O que NÃO está modelado ainda — de propósito

Este é um recorte deliberado, não uma omissão por esquecimento — RF-050 é
explícito que a tela real do item vivo tem poucos campos, não os ~60 do
cursor `PEDTEMP` original, e vários existem só por configuração inerte:

- **Campos fiscais do item** (`cfop`, `cst`, `csosn`, `redbasest`, etc. —
  RF-058/059/060). Ficam para quando a Fase 4 (front + API) precisar deles
  de verdade; a Fase 2 não decide sozinha como resolver a precedência de 3
  fontes do RF-058.
- **Segunda condição de pagamento** (RF-154-158) — hoje é um sentinela
  `"XX"` num campo C(2); a recomendação (RF-155) é modelar como coleção, mas
  isso só faz sentido junto com o desenho de fechamento da Fase 5.
- **Reserva de estoque** (RF-160/163) e **avaliação de crédito** (RF-190-203)
  não são responsabilidade do agregado — dependem de dado externo (saldo em
  `cadmat`, títulos em `artficha`) que só o console lê. Ficam para a
  Fase 5, como uma camada de aplicação que orquestra
  `Pedido` + `AvaliacaoCreditoService` (em MotorRegras) + os repositórios do
  console.
- **Alteração diferencial de pedido fechado** (RF-010) — a Fase 2 cobre o
  ciclo de rascunho→fechamento→cancelamento; reabrir um pedido fechado para
  editar é Fase 6, e a decisão L7 (mantida) já diz que pedido baixado com
  aviso continua editável, então essa reabertura precisa conviver com itens
  já baixados — desenho próprio, não uma extensão trivial deste agregado.
- **Persistência real** — `schema.sql` ainda não foi executado (não há
  Postgres nesta máquina). Revisar tipo a tipo antes da primeira migração.
