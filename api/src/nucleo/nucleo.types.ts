export interface PrecificarPayload {
  precoTabelaBase: number;
  percentualAdicao: number;
  compensacaoIcms: number;
  descontoDiretoria: number;
  negociacao?: { precoNegociado: number; dataValidade: string; autorizada: boolean } | null;
  precoDigitado?: number | null;
  percentualTetoDesconto: number;
  dataReferencia: string;
}

export interface ResultadoPrecificacao {
  precoTabelaAjustado: number;
  precoFinal: number;
  percentualDesconto: number;
  origem: 'TabelaSemDesconto' | 'TabelaComDesconto' | 'Negociado';
}

export interface CreditoPayload {
  titulosAbertos: { valor: number; dataVencimento: string }[];
  limiteMatriz: number;
  diasToleranciaAtraso: number;
  valorPedidoAtual: number;
  dataReferencia: string;
}

export interface ResultadoCreditoNucleo {
  status: 'Aprovado' | 'BloqueadoAtraso' | 'BloqueadoLimiteZerado' | 'BloqueadoLimiteExcedido';
  saldoDevedorGrupo: number;
  limiteMatriz: number;
}

export interface FiscalPayload {
  quantidade: number;
  precoUnitario: number;
  percentualDesconto: number;
  aliquotaIpi: number;
  indIpi: string;
  clienteEstado: string;
  clienteCgcCpf: string;
  clienteEnquadra: string;
  clienteSuframa: string;
  clienteIncideSuf: number;
  clienteIncidePis: number;
  clienteDesonera: string;
  produtoCodProc: string;
  produtoNcm: string;
  produtoValIcm: number;
  produtoValIcmE: number;
  produtoValIcmSu: number;
  produtoDespSubst: number;
  produtoIcmReduc: number;
  produtoPermIcm: string;
  produtoLegislacao: string;
  produtoTemBeneficio: boolean;
  tipoNatureza: string;
  tipoPisCofins: string;
  tipoNfcE: string;
  tipoCodContro: number;
  empresaEstado: string;
  empresaIcmEstado: number;
  cadicm: {
    valIcm: number;
    valIcmE: number;
    valIcmSu: number;
    subsTrib: number;
    aliqInter: number;
    aliqFcp: number;
    incideFcp: string;
  } | null;
  cadsub: {
    valIcm: number;
    valIcmSu: number;
    icmProprio: number;
    reducBase: number;
    prcmedio: number;
    fcp: number;
    subsTrib: number;
  } | null;
}

export interface ResultadoFiscal {
  valorMercadoria: number;
  valorDesconto: number;
  aliquotaIcmResolvida: number;
  baseIpi: number;
  valorIpi: number;
  baseIcm: number;
  valorIcm: number;
  baseIcmSt: number;
  aliquotaIcmSt: number;
  mvaAplicado: number;
  usouPautaFiscal: boolean;
  valorIcmSt: number;
  totalItemComImpostos: number;
}
