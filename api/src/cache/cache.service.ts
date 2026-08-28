import { Injectable, Logger } from '@nestjs/common';
import {
  CadicmFisico,
  CadsubFisico,
  ClienteFisico,
  CondicaoPagamentoFisico,
  EmpresaFisica,
  NegociacaoFisica,
  ProdutoFisico,
  TabcolFisico,
  TipoOperacaoFisico,
  TituloAbertoFisico,
  UsuarioFisico,
  VendedorFisico,
} from './cache.types';

/**
 * Cópia local dos dados de referência, alimentada pelo console via
 * POST /sincronizacao/* — em memória por ora (some ao reiniciar; um
 * banco real — Postgres, como o plano original previa — é o próximo passo
 * antes disto rodar em produção). Ler daqui é o que permite a API responder
 * rápido sem depender do console estar de pé no instante da consulta; o
 * preço é a defasagem do intervalo de sincronização (hoje 30s).
 */
@Injectable()
export class CacheService {
  private readonly logger = new Logger(CacheService.name);

  private clientes = new Map<string, ClienteFisico>();
  private produtosPorEmpresa = new Map<string, Map<string, ProdutoFisico>>();
  private tabcolPorEmpresa = new Map<string, Map<string, TabcolFisico>>();
  private vendedores = new Map<string, VendedorFisico>();
  private usuarios = new Map<string, UsuarioFisico>();
  private tiposOperacao = new Map<string, TipoOperacaoFisico>();
  private condicoesPagamento = new Map<string, CondicaoPagamentoFisico>();
  private titulosAbertos: TituloAbertoFisico[] = [];
  private negociacoes = new Map<string, NegociacaoFisica>();
  private cadsubPorEmpresa = new Map<string, CadsubFisico[]>();
  private cadicmPorEmpresa = new Map<string, CadicmFisico[]>();
  private empresas = new Map<string, EmpresaFisica>();
  private ultimaSincronizacao: Date | null = null;

  substituirClientes(lista: ClienteFisico[]) {
    this.clientes = new Map(lista.map((c) => [c.codigo, c]));
    this.registrarSync();
  }

  substituirProdutos(empresa: string, lista: ProdutoFisico[]) {
    this.produtosPorEmpresa.set(empresa, new Map(lista.map((p) => [`${p.grupo}|${p.referencia}`, p])));
    this.registrarSync();
  }

  substituirTabcol(empresa: string, lista: TabcolFisico[]) {
    this.tabcolPorEmpresa.set(empresa, new Map(lista.map((t) => [t.codigo, t])));
    this.registrarSync();
  }

  substituirVendedores(lista: VendedorFisico[]) {
    this.vendedores = new Map(lista.map((v) => [v.codVend, v]));
    this.registrarSync();
  }

  substituirUsuarios(lista: UsuarioFisico[]) {
    this.usuarios = new Map(lista.map((u) => [u.identific, u]));
    this.registrarSync();
  }

  substituirTitulosAbertos(lista: TituloAbertoFisico[]) {
    this.titulosAbertos = lista;
    this.registrarSync();
  }

  substituirTiposOperacao(lista: TipoOperacaoFisico[]) {
    this.tiposOperacao = new Map(lista.map((t) => [t.tipo, t]));
    this.registrarSync();
  }

  substituirCondicoesPagamento(lista: CondicaoPagamentoFisico[]) {
    this.condicoesPagamento = new Map(lista.map((c) => [c.codigo, c]));
    this.registrarSync();
  }

  substituirNegociacoes(lista: NegociacaoFisica[]) {
    // chave real da tabela: cod_cli+grupo+referencia+tipo_prc (RF-075)
    this.negociacoes = new Map(lista.map((n) => [`${n.codCli}|${n.grupo}|${n.referencia}|${n.tipoPrc}`, n]));
    this.registrarSync();
  }

  /**
   * RF-075: negociação vigente e autorizada para cliente+produto+tipo de
   * preço. Vigência (dt_venc >= hoje) e autorização (cod_valida=1) são as
   * três guardas do VFP (ped_wer txtAddText.LostFocus).
   */
  buscarNegociacaoVigente(codCli: string, grupo: string, referencia: string, tipoPrc: string): NegociacaoFisica | null {
    const n = this.negociacoes.get(`${codCli}|${grupo}|${referencia}|${tipoPrc}`);
    if (!n || n.codValida !== 1 || !n.dtVenc) return null;
    const hoje = new Date().toISOString().slice(0, 10);
    return n.dtVenc.slice(0, 10) >= hoje ? n : null;
  }

  /** O registro bruto, vigente ou não — para a tela avisar/bloquear negociação vencida (decisão do usuário, 25/08/2026). */
  buscarNegociacaoRegistro(codCli: string, grupo: string, referencia: string, tipoPrc: string): NegociacaoFisica | null {
    return this.negociacoes.get(`${codCli}|${grupo}|${referencia}|${tipoPrc}`) ?? null;
  }

  substituirCadsub(empresa: string, lista: CadsubFisico[]) {
    this.cadsubPorEmpresa.set(empresa, lista);
    this.registrarSync();
  }

  substituirCadicm(empresa: string, lista: CadicmFisico[]) {
    this.cadicmPorEmpresa.set(empresa, lista);
    this.registrarSync();
  }

  substituirEmpresas(lista: EmpresaFisica[]) {
    this.empresas = new Map(lista.map((e) => [e.codEmpr, e]));
    this.registrarSync();
  }

  /**
   * `vendedorCodigo`: quando informado, restringe aos clientes em que o
   * usuário logado é vendedor 1 ou 2 (`cod_vendor`/`cod_vend2`) — carteira
   * de vendedor (25/08/2026).
   */
  listarClientes(termo?: string, limite = 20, vendedorCodigo?: string): ClienteFisico[] {
    const todos = [...this.clientes.values()];
    const daCarteira = vendedorCodigo
      ? todos.filter((c) => c.codVendor === vendedorCodigo || c.codVend2 === vendedorCodigo)
      : todos;
    const filtrados = termo
      ? daCarteira.filter((c) => this.contemTermo(c.codigo, termo) || this.contemTermo(c.razaoSoc, termo))
      : daCarteira;
    return filtrados.slice(0, limite);
  }

  buscarCliente(codigo: string): ClienteFisico | null {
    return this.clientes.get(codigo) ?? null;
  }

  buscarUsuario(identific: string): UsuarioFisico | null {
    return this.usuarios.get(identific) ?? null;
  }

  /** Grupo econômico pela raiz do CNPJ (RF-190) — mesma regra do console, aplicada sobre a cópia local. */
  buscarGrupoEconomico(raizCgc10: string): ClienteFisico[] {
    return [...this.clientes.values()].filter((c) => c.cgc.length >= 10 && c.cgc.slice(0, 10) === raizCgc10);
  }

  listarProdutos(empresa: string, termo?: string, limite = 20): ProdutoFisico[] {
    const todos = [...(this.produtosPorEmpresa.get(empresa)?.values() ?? [])];
    const filtrados = termo
      ? todos.filter((p) => this.contemTermo(p.grupo, termo) || this.contemTermo(p.referencia, termo) || this.contemTermo(p.descricao, termo))
      : todos;
    return filtrados.slice(0, limite);
  }

  buscarProduto(empresa: string, grupo: string, referencia: string): ProdutoFisico | null {
    return this.produtosPorEmpresa.get(empresa)?.get(`${grupo}|${referencia}`) ?? null;
  }

  buscarVendedor(codVend: string): VendedorFisico | null {
    return this.vendedores.get(codVend) ?? null;
  }

  listarVendedores(termo?: string, limite = 20): VendedorFisico[] {
    const todos = [...this.vendedores.values()];
    const filtrados = termo ? todos.filter((v) => this.contemTermo(v.codVend, termo) || this.contemTermo(v.nome, termo)) : todos;
    return filtrados.slice(0, limite);
  }

  /** RF-083: `TabCol.Nome` é char mas guarda um número — VAL() explícito, com fallback 0 se não numérico/não encontrado. */
  buscarTetoColuna(empresa: string, codigoGradecol: string): number {
    const tabcol = this.tabcolPorEmpresa.get(empresa)?.get(codigoGradecol);
    if (!tabcol) return 0;
    const valor = Number(tabcol.nome);
    return Number.isFinite(valor) ? valor : 0;
  }

  titulosAbertosDoGrupo(codigosClientes: string[]): TituloAbertoFisico[] {
    const codigos = new Set(codigosClientes);
    return this.titulosAbertos.filter((t) => codigos.has(t.codigo));
  }

  listarTiposOperacao(termo?: string, limite = 20): TipoOperacaoFisico[] {
    const todos = [...this.tiposOperacao.values()];
    const filtrados = termo ? todos.filter((t) => this.contemTermo(t.tipo, termo) || this.contemTermo(t.descricao, termo)) : todos;
    return filtrados.slice(0, limite);
  }

  buscarTipoOperacao(tipo: string): TipoOperacaoFisico | null {
    return this.tiposOperacao.get(tipo) ?? null;
  }

  listarCondicoesPagamento(termo?: string, limite = 20): CondicaoPagamentoFisico[] {
    const todos = [...this.condicoesPagamento.values()];
    const filtrados = termo
      ? todos.filter((c) => this.contemTermo(c.codigo, termo) || this.contemTermo(c.descOcor, termo))
      : todos;
    return filtrados.slice(0, limite);
  }

  buscarCondicaoPagamento(codigo: string): CondicaoPagamentoFisico | null {
    return this.condicoesPagamento.get(codigo) ?? null;
  }

  /**
   * Porta de `pesquisacadsub.PRG:42-44`: chave grupo+referencia+uf+enquadra,
   * com fallback por NCM+uf+enquadra. Enquadramento "S" do cliente vira " "
   * (pesquisacadsub:37-41).
   */
  buscarCadsub(empresa: string, grupo: string, referencia: string, uf: string, enquadraCliente: string, ncm: string): CadsubFisico | null {
    const lista = this.cadsubPorEmpresa.get(empresa) ?? [];
    const enquadra = enquadraCliente === 'S' ? '' : enquadraCliente;

    const porChave = lista.find(
      (c) => c.grupo === grupo && c.referencia === referencia && c.uf === uf && c.enquadra === enquadra,
    );
    if (porChave) return porChave;

    if (ncm) {
      return lista.find((c) => c.ncm === ncm && c.uf === uf && c.enquadra === enquadra) ?? null;
    }
    return null;
  }

  /** Porta de veicm3:150 — cadicm por estado+enquadra (enquadra "S" vira " "). */
  buscarCadicm(empresa: string, estado: string, enquadraCliente: string): CadicmFisico | null {
    const lista = this.cadicmPorEmpresa.get(empresa) ?? [];
    const enquadra = enquadraCliente === 'S' ? '' : enquadraCliente;
    return lista.find((c) => c.estado === estado && c.enquadra === enquadra) ?? lista.find((c) => c.estado === estado) ?? null;
  }

  buscarEmpresa(codEmpr: string): EmpresaFisica | null {
    return this.empresas.get(codEmpr) ?? null;
  }

  listarEmpresas(): EmpresaFisica[] {
    return [...this.empresas.values()];
  }

  status() {
    return {
      ultimaSincronizacao: this.ultimaSincronizacao,
      totalClientes: this.clientes.size,
      totalVendedores: this.vendedores.size,
      totalUsuarios: this.usuarios.size,
      totalTitulosAbertos: this.titulosAbertos.length,
      totalTiposOperacao: this.tiposOperacao.size,
      totalCondicoesPagamento: this.condicoesPagamento.size,
      empresasComProdutos: [...this.produtosPorEmpresa.keys()],
    };
  }

  private contemTermo(campo: string, termo: string) {
    return campo.toUpperCase().includes(termo.toUpperCase());
  }

  private registrarSync() {
    this.ultimaSincronizacao = new Date();
    this.logger.log(`Sincronização recebida do console em ${this.ultimaSincronizacao.toISOString()}`);
  }
}
