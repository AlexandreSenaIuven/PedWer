// Cliente HTTP para a API real (Node/NestJS). Substitui dadosDemonstracao.ts
// no fluxo principal — dados reais, vindos do VFP através do console C#.

// Build de produção: a API serve o front publicado, na mesma origem — mas o
// front pode estar num subcaminho (ex.: proxy atrás de outro site, num
// endereço tipo http://<ip>/pedwer/) em vez da raiz do domínio. Resolver
// contra `document.baseURI` (não `/caminho` fixo) funciona nos dois casos.
// Dev (`npm run dev`): front na 5173, API separada, sempre na 3001 e na raiz.
function urlApi(caminho: string): string {
  const base = import.meta.env.PROD ? document.baseURI : 'http://localhost:3001/'
  return new URL(caminho, base).toString()
}

export interface ClienteResumo {
  codigo: string
  nome: string
  descontoEspecialTeto: number
  regimeLimite: 'CONSTANTE' | 'NAO_CONSTANTE'
  credito: number
  condicaoPagamentoCodigo: string
  vendedorCodigo1: string
  vendedorCodigo2: string
  cnpj: string
  inscricaoEstadual: string
  endereco: string
  bairro: string
  cidade: string
  estado: string
  cep: string
  telefone: string
  tipoCliente: string
  comprador: string
}

export interface UsuarioLogado {
  codigo: string
  nome: string
  vendedorCodigo: string
  vendedorNome: string
}

export interface EmpresaCompleta {
  codigo: string
  nome: string
  uf: string
  cnpj: string
  inscricaoEstadual: string
  endereco: string
  bairro: string
  cidade: string
  cep: string
  telefone: string
  fax: string
  email: string
}

export interface ProdutoResumo {
  grupo: string
  referencia: string
  descricao: string
  precoTabela: number
  gradecol: string
  gradegrp: string
}

export type FiscalItem =
  | { valorIpi: number; valorIcm: number; valorIcmSt: number; totalItemComImpostos: number; aliquotaIpi: number; aliquotaIcm: number }
  | { naoCalculado: string }

export interface ResultadoPrecificacao {
  produtoDescricao: string
  produtoGrupo: string
  produtoReferencia: string
  gradegrp: string
  unidade: string
  pesoUnitario: number
  volumeUnitario: number
  precoTabelaAjustado: number
  precoFinal: number
  percentualDesconto: number
  origemPreco: 'TabelaSemDesconto' | 'TabelaComDesconto' | 'Negociado'
  fiscal: FiscalItem
}

export interface ReferenciaSimples {
  codigo: string
  descricao: string
}

export interface ResultadoCredito {
  matrizCodigo: string
  matrizNome: string
  limite: number
  saldoDevedorGrupo: number
  status: 'Aprovado' | 'BloqueadoAtraso' | 'BloqueadoLimiteZerado' | 'BloqueadoLimiteExcedido'
}

export interface CotacaoItem {
  precoTabelaAjustado: number
  precoSugerido: number
  percentualDesconto: number
  origem: 'TabelaSemDesconto' | 'TabelaComDesconto' | 'Negociado'
  negociacao: {
    situacao: 'nenhuma' | 'vigente' | 'vencida' | 'nao_autorizada'
    preco: number | null
    dataValidade: string | null
  }
}

async function tratar<T>(resposta: Response): Promise<T> {
  if (!resposta.ok) {
    const corpo = await resposta.json().catch(() => ({}))
    throw new Error(corpo.erro ?? corpo.message ?? `Erro ${resposta.status}`)
  }
  return resposta.json()
}

// Quantos resultados a tela de busca pede por vez — quando a resposta bate
// neste teto, o modal avisa "refine a busca" em vez de fingir que acabou.
export const LIMITE_BUSCA = 50

export const api = {
  // vendedorCodigo restringe à carteira do vendedor logado (cod_vendor OU
  // cod_vend2 do cliente) — omitido, traz todos (ex.: checagem de conexão).
  buscarClientes: (termo?: string, vendedorCodigo?: string) =>
    fetch(
      urlApi(
        `clientes?limite=${LIMITE_BUSCA}${termo ? `&q=${encodeURIComponent(termo)}` : ''}${vendedorCodigo ? `&vendedor=${encodeURIComponent(vendedorCodigo)}` : ''}`,
      ),
    ).then((r) => tratar<ClienteResumo[]>(r)),

  login: (usuario: string, senha: string) =>
    fetch(urlApi('auth/login'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ usuario, senha }),
    }).then((r) => tratar<UsuarioLogado>(r)),

  buscarProdutos: (empresa: string, termo?: string) =>
    fetch(urlApi(`produtos/${empresa}?limite=${LIMITE_BUSCA}${termo ? `&q=${encodeURIComponent(termo)}` : ''}`)).then((r) =>
      tratar<ProdutoResumo[]>(r),
    ),

  precificar: (body: {
    empresa: string
    codigoCliente: string
    grupo: string
    referencia: string
    precoDigitado: number
    quantidade: number
    tipoOperacao?: string
  }) =>
    fetch(urlApi('precificar'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    }).then((r) => tratar<ResultadoPrecificacao>(r)),

  consultarCredito: (codigo: string) => fetch(urlApi(`credito/${codigo}`)).then((r) => tratar<ResultadoCredito>(r)),

  // Cotação na escolha do produto: preço de tabela ajustado ao cliente e a
  // negociação (vigente / vencida / não autorizada / nenhuma) — para o campo
  // de preço já vir certo e a tela avisar/bloquear antes de "Adicionar".
  cotarItem: (body: { empresa: string; codigoCliente: string; grupo: string; referencia: string; tipoOperacao?: string }) =>
    fetch(urlApi('cotar-item'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    }).then((r) => tratar<CotacaoItem>(r)),

  buscarVendedores: (termo?: string) =>
    fetch(urlApi(`vendedores?limite=${LIMITE_BUSCA}${termo ? `&q=${encodeURIComponent(termo)}` : ''}`)).then((r) =>
      tratar<ReferenciaSimples[]>(r),
    ),

  buscarTiposOperacao: (termo?: string) =>
    fetch(urlApi(`tipos-operacao?limite=${LIMITE_BUSCA}${termo ? `&q=${encodeURIComponent(termo)}` : ''}`)).then((r) =>
      tratar<ReferenciaSimples[]>(r),
    ),

  buscarCondicoesPagamento: (termo?: string) =>
    fetch(urlApi(`condicoes-pagamento?limite=${LIMITE_BUSCA}${termo ? `&q=${encodeURIComponent(termo)}` : ''}`)).then((r) =>
      tratar<ReferenciaSimples[]>(r),
    ),

  listarEmpresas: () => fetch(urlApi('empresas')).then((r) => tratar<EmpresaCompleta[]>(r)),

  buscarVendedor: (codigo: string) =>
    fetch(urlApi(`vendedores/${encodeURIComponent(codigo)}`)).then((r) => tratar<{ codigo: string; nome: string }>(r)),

  // A gravação é assíncrona: o console só busca comandos periodicamente
  // (nunca recebe conexão) — por isso isto devolve um comandoId para
  // acompanhar, não uma confirmação imediata.
  criarPedido: (body: {
    tipoOperacao: string
    codigoEmpresa: string
    codigoCliente: string
    data: string
    dataEntrega?: string
    autor: string
    condicaoPagamentoCodigo?: string
    vendedorCodigo1: string
    vendedorCodigo2: string
    itens: { grupo: string; referencia: string; quantidade: number; precoDigitado: number }[]
  }) =>
    fetch(urlApi('pedidos'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    }).then((r) => tratar<{ comandoId: string; status: string; referenciaExterna: string }>(r)),

  // "Dados Para Entrega" → cligeral.dbf (obs, obs2, referência, endereço de
  // entrega), como o ERP faz antes de imprimir o PED_WE. Segundo comando
  // para o console; não bloqueia a impressão.
  gravarEntrega: (
    comandoId: string,
    dados: { endereco: string; bairro: string; cidade: string; estado: string; referencia: string; observacao1: string; observacao2: string },
  ) =>
    fetch(urlApi(`pedidos/${comandoId}/entrega`), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(dados),
    }).then((r) => tratar<{ comandoId: string; status: string }>(r)),

  statusPedido: (comandoId: string) =>
    fetch(urlApi(`pedidos/${comandoId}/status`)).then((r) =>
      tratar<{ status: 'Pendente' | 'Processando' | 'Gravado' | 'Erro'; referenciaExterna: string; erro?: string }>(r),
    ),

  // Botão "Últimas Compras" do ped_wer.scx original — vem de `cadmov`
  // (venda já faturada), consultado sob demanda pelo console (não é um
  // dado sincronizado). Fila igual à de criar pedido: dispara o comando,
  // depois faz poll em statusUltimasCompras até ele voltar.
  consultarUltimasCompras: (empresa: string, codigoCliente: string) =>
    fetch(urlApi(`clientes/${codigoCliente}/ultimas-compras`), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ empresa }),
    }).then((r) => tratar<{ comandoId: string; status: string }>(r)),

  statusUltimasCompras: (comandoId: string) =>
    fetch(urlApi(`comandos/${comandoId}`)).then((r) =>
      tratar<{ status: 'Pendente' | 'Processando' | 'Gravado' | 'Erro'; erro?: string; ultimasCompras?: ItemCompra[] }>(r),
    ),

  // Botão "Giro" — mesmo padrão de "Últimas Compras", filtrado por produto.
  consultarGiro: (empresa: string, grupo: string, referencia: string) =>
    fetch(urlApi(`produtos/${grupo}/${referencia}/giro`), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ empresa }),
    }).then((r) => tratar<{ comandoId: string; status: string }>(r)),

  statusGiro: (comandoId: string) =>
    fetch(urlApi(`comandos/${comandoId}`)).then((r) =>
      tratar<{ status: 'Pendente' | 'Processando' | 'Gravado' | 'Erro'; erro?: string; giro?: ItemGiro[] }>(r),
    ),

  // Botão "Saldo Geral" — saldo do produto em cada empresa (`empresas` são
  // os códigos já carregados na sessão, sem a "PRINCIPAL" sintética).
  consultarSaldoGeral: (empresa: string, grupo: string, referencia: string, empresas: string[]) =>
    fetch(urlApi(`produtos/${grupo}/${referencia}/saldo-geral`), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ empresa, empresas }),
    }).then((r) => tratar<{ comandoId: string; status: string }>(r)),

  statusSaldoGeral: (comandoId: string) =>
    fetch(urlApi(`comandos/${comandoId}`)).then((r) =>
      tratar<{ status: 'Pendente' | 'Processando' | 'Gravado' | 'Erro'; erro?: string; saldoGeral?: SaldoEmpresa[] }>(r),
    ),
}

export interface ItemCompra {
  dataMov: string
  notaFiscal: string
  grupo: string
  referencia: string
  quantidade: number
  valorUnitario: number
  produtoDescricao?: string
  produtoCaracter?: string
}

export interface ItemGiro {
  dataMov: string
  notaFiscal: string
  cliFor: string
  quantidade: number
  valorUnitario: number
  clienteNome?: string
}

export interface SaldoEmpresa {
  codigoEmpresa: string
  qtdReal: number
  qtReserva: number
}
