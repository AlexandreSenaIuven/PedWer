// Espelha os DTOs físicos do console (Integrador/Leitura/Dtos.cs) — é o
// formato que chega via POST /sincronizacao/*. Nomes de negócio só existem
// depois da tradução nos controllers voltados ao front (decisão L10).

export interface ClienteFisico {
  codigo: string;
  razaoSoc: string;
  cgc: string;
  posicao: string;
  codVendor: string;
  codVend2: string;
  credito: number;
  csll: number;
  inss: number;
  irrf: number;
  iss: number;
  pis: number;
  cofins: number;
  condPag: string;
  codEmpr: string;
  estado: string;
  cgcCpf: string;
  enquadra: string;
  suframa: string;
  incideSuf: number;
  incidePis: number;
  desonera: string;
  endereco: string;
  bairro: string;
  cidade: string;
  cep: string;
  telefone1: string;
  inscEsta: string;
  tipoCli: string;
  comprador: string;
}

export interface ProdutoFisico {
  grupo: string;
  referencia: string;
  descricao: string;
  prcVenda: number;
  gradecol: string;
  gradegrp: string;
  qtdPedida: number;
  qtdFpedid: number;
  ipi: number;
  indIpi: string;
  codProc: string;
  ncm: string;
  valIcm: number;
  valIcmSu: number;
  despSubst: number;
  permIcm: string;
  legislacao: string;
  beneficio: string;
  unidEmb: string;
  pesoUnit: number;
  volume: number;
  cstPis: string;
  aliqPis: number;
  cstCof: string;
  aliqCof: number;
  /** Complemento da descrição (cor/tamanho/variante) — sempre mostrado junto com `descricao` no VFP original. */
  caracter: string;
}

export interface CadsubFisico {
  grupo: string;
  referencia: string;
  uf: string;
  enquadra: string;
  ncm: string;
  valIcm: number;
  valIcmSu: number;
  icmproprio: number;
  reducbase: number;
  icmReduc: number;
  prcmedio: number;
  fcp: number;
  subsTrib: number;
  cstIcms: string;
  cfop: string;
  cfopsubst: string;
}

export interface CadicmFisico {
  estado: string;
  enquadra: string;
  valIcm: number;
  valIcmE: number;
  valIcmSu: number;
  subsTrib: number;
  aliqInter: number;
  aliqFcp: number;
  incideFcp: string;
}

export interface NegociacaoFisica {
  codCli: string;
  grupo: string;
  referencia: string;
  tipoPrc: string;
  dtVenc: string | null;
  preco: number;
  codValida: number;
}

export interface EmpresaFisica {
  codEmpr: string;
  nomeEmpr: string;
  ufEmpr: string;
  icmEstado: number;
  cgc: string;
  inscempr: string;
  enderE: string;
  bairro: string;
  cidadeE: string;
  cepE: string;
  telefone: string;
  fax: string;
  email: string;
}

export interface VendedorFisico {
  codVend: string;
  nome: string;
  tipoVend: string;
}

/** Login web (25/08/2026) — espelha `caduser`. `senha` chega cifrada como no VFP. */
export interface UsuarioFisico {
  identific: string;
  senha: string;
  nome: string;
  codVend: string;
  inativo: boolean;
}

export interface TabcolFisico {
  codigo: string;
  nome: string;
}

export interface TipoOperacaoFisico {
  tipo: string;
  descricao: string;
  tipoEs: string;
  indQtd: string;
  natureza: string;
  indValb: string;
  posicao: string;
  indComiss: string;
  indEmpenho: string;
  indSenha: string;
  codVencim: string;
  operacao: number;
  pisCofins: string;
  nfcE: string;
  codContro: number;
}

export interface CondicaoPagamentoFisico {
  codigo: string;
  descOcor: string;
  resumido: string;
  limVenda: number;
  indDiaPf: string;
  codOper: number;
  diaPref: number;
}

export interface TituloAbertoFisico {
  codigo: string;
  valor: number;
  dtVencim: string | null;
  codVencim: string;
}
