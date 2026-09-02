export interface FiscalComando {
  aliquotaIpi: number;
  aliquotaIcm: number;
  valorIpi: number;
  valorIcm: number;
  valorIcmSt: number;
  baseIcm: number;
  baseIcmSt: number;
  valorMercadoria: number;
  cst: string;
  cfop: string;
  cstPis: string;
  aliqPis: number;
  cstCof: string;
  aliqCof: number;
  unidade: string;
}

export interface ItemComando {
  grupo: string;
  referencia: string;
  gradegrp: string;
  quantidade: number;
  precoTabelaAjustado: number;
  precoFinal: number;
  percentualDesconto: number;
  origem: 'TabelaSemDesconto' | 'TabelaComDesconto' | 'Negociado';
  fiscal: FiscalComando | null;
}

/**
 * Dados da tela "Dados Para Entrega" (form ped_ph do ERP). No VFP vão para
 * `cligeral.dbf`, chaveada só por `codigo` do pedido: obs/obs2 (observações)
 * e `cligeral.cond` → coluna `condpag` (a "Referência"). Endereço/bairro/
 * cidade/UF são memvars só de impressão lá; aqui gravamos também em
 * ender_ent/bairro_ent/cidade_ent/uf_ent, que existem na tabela.
 */
export interface DadosEntregaComando {
  endereco: string;
  bairro: string;
  cidade: string;
  estado: string;
  referencia: string;
  observacao1: string;
  observacao2: string;
}

/**
 * Uma linha de "Últimas Compras" (botão do ped_wer.scx) — vem de `cadmov`
 * (venda faturada), não de `pedido.dbf`. Consulta sob demanda pelo console,
 * nunca sincronizada por inteiro (900k+ linhas por empresa).
 */
export interface ItemCompra {
  dataMov: string;
  notaFiscal: string;
  grupo: string;
  referencia: string;
  quantidade: number;
  valorUnitario: number;
  /** Preenchidos na leitura (GET /comandos/:id), a partir do catálogo já sincronizado — não vêm do console. */
  produtoDescricao?: string;
  produtoCaracter?: string;
}

export interface ComandoCriarPedido {
  id: string;
  /**
   * Sem tipo = 'CriarPedido' (compatibilidade). 'GravarEntrega' grava só o
   * cligeral do pedido já criado. 'ConsultarUltimasCompras' não grava nada
   * — só lê `cadmov` e devolve em `ultimasCompras`.
   */
  tipo?: 'CriarPedido' | 'GravarEntrega' | 'ConsultarUltimasCompras';
  entrega?: DadosEntregaComando;
  ultimasCompras?: ItemCompra[];
  tipoOperacao: string;
  codigoEmpresa: string;
  codigoCliente: string;
  data: string;
  dataEntrega?: string;
  autor: string;
  referenciaExterna: string;
  condicaoPagamentoCodigo?: string;
  vendedorCodigo1: string;
  vendedorCodigo2: string;
  tipoVendedorParaComissao: string;
  itens: ItemComando[];
  status: 'Pendente' | 'Processando' | 'Gravado' | 'Erro';
  erro?: string;
  criadoEm: Date;
}
