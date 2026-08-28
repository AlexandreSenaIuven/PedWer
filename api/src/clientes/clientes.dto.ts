import { ClienteFisico } from '../cache/cache.types';

// Nomes de negócio (decisão L10, 20/08/2026) — nunca expor inss/irrf/iss/
// pis/cofins/csll direto: são campos fiscais reaproveitados com sentido
// comercial só para este cliente, e não há discriminador no dado (§7.2 do
// doc de integração). Tradução confinada aqui, num único lugar.
export interface ClienteResumo {
  codigo: string;
  nome: string;
  percentualAdicao: number;
  compensacaoIcms: number;
  descontoDiretoria: number;
  descontoEspecialTeto: number;
  diasToleranciaAtraso: number;
  regimeLimite: 'CONSTANTE' | 'NAO_CONSTANTE';
  credito: number;
  condicaoPagamentoCodigo: string;
  vendedorCodigo1: string;
  vendedorCodigo2: string;
  // Dados cadastrais para a impressão do pedido (sem ambiguidade — passam direto)
  cnpj: string;
  inscricaoEstadual: string;
  endereco: string;
  bairro: string;
  cidade: string;
  estado: string;
  cep: string;
  telefone: string;
  tipoCliente: string;
  comprador: string;
}

export function traduzirCliente(c: ClienteFisico): ClienteResumo {
  return {
    codigo: c.codigo,
    nome: c.razaoSoc,
    percentualAdicao: c.inss,
    compensacaoIcms: c.irrf,
    descontoDiretoria: c.iss,
    descontoEspecialTeto: c.pis,
    diasToleranciaAtraso: c.cofins,
    // Nota (gap conhecido): o console hoje não distingui campo em branco de
    // zero real (RF-197/L10) — csll=0 e csll-em-branco chegam aqui iguais.
    // Corrigir exigiria csll nullable de ponta a ponta; não feito ainda.
    regimeLimite: c.csll === 1 ? 'CONSTANTE' : 'NAO_CONSTANTE',
    credito: c.credito,
    condicaoPagamentoCodigo: c.condPag,
    vendedorCodigo1: c.codVendor,
    vendedorCodigo2: c.codVend2,
    cnpj: c.cgc,
    inscricaoEstadual: c.inscEsta,
    endereco: c.endereco,
    bairro: c.bairro,
    cidade: c.cidade,
    estado: c.estado,
    cep: c.cep,
    telefone: c.telefone1,
    tipoCliente: c.tipoCli,
    comprador: c.comprador,
  };
}
