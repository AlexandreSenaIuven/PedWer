// Nomes de negócio (decisão L10) — espelha os tipos de MotorRegras/Dominio (C#).
// Quando a API real existir, estes tipos devem vir gerados/sincronizados a
// partir dela, não mantidos à mão em dois lugares.

export type OrigemPreco = 'TabelaSemDesconto' | 'TabelaComDesconto' | 'Negociado'
export type EstadoPedido = 'Rascunho' | 'Processando' | 'Fechado'

export interface ItemPedido {
  numero: number
  produtoGrupo: string
  produtoReferencia: string
  produtoDescricao: string
  quantidade: number
  precoTabelaAjustado: number
  precoFinal: number
  percentualDesconto: number
  origemPreco: OrigemPreco
  percentualComissao?: number
  unidade: string
  pesoUnitario: number
  volumeUnitario: number
  valorIpi: number | null // null = motor fiscal não calculou (cenário excluído — mostrar aviso, nunca zero silencioso)
  valorIcmSt: number | null
  aliquotaIpi: number | null
  aliquotaIcm: number | null
  fiscalNaoCalculado?: string
}

export interface CabecalhoPedido {
  tipoOperacao: string
  codigoEmpresa: string
  codigoCliente: string
  data: string
}
