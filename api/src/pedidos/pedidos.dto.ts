export interface PrecificarBody {
  empresa: string;
  codigoCliente: string;
  grupo: string;
  referencia: string;
  precoDigitado: number;
  quantidade?: number;
  tipoOperacao?: string;
}

export interface CotarItemBody {
  empresa: string;
  codigoCliente: string;
  grupo: string;
  referencia: string;
  tipoOperacao?: string;
}

export interface DadosEntregaBody {
  endereco: string;
  bairro: string;
  cidade: string;
  estado: string;
  referencia: string;
  observacao1: string;
  observacao2: string;
}

export interface ItemPedidoBody {
  grupo: string;
  referencia: string;
  quantidade: number;
  precoDigitado: number;
}

export interface CriarPedidoBody {
  tipoOperacao: string;
  codigoEmpresa: string;
  codigoCliente: string;
  data: string;
  dataEntrega?: string;
  autor: string;
  condicaoPagamentoCodigo?: string;
  vendedorCodigo1: string;
  vendedorCodigo2: string;
  itens: ItemPedidoBody[];
}
