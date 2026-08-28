import { Body, Controller, Get, Param, Post } from '@nestjs/common';
import { CacheService } from '../cache/cache.service';
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
} from '../cache/cache.types';

/**
 * Endpoints que o CONSOLE chama (nunca o contrário) para empurrar uma cópia
 * fresca dos dados de referência. Não fazem sentido para o front — não são
 * expostos com nomes de negócio porque quem fala com eles é o console, não
 * o navegador.
 */
@Controller('sincronizacao')
export class SincronizacaoController {
  constructor(private readonly cache: CacheService) {}

  @Post('clientes')
  clientes(@Body() lista: ClienteFisico[]) {
    this.cache.substituirClientes(lista);
    return { recebidos: lista.length };
  }

  @Post('produtos/:empresa')
  produtos(@Param('empresa') empresa: string, @Body() lista: ProdutoFisico[]) {
    this.cache.substituirProdutos(empresa, lista);
    return { recebidos: lista.length };
  }

  @Post('tabcol/:empresa')
  tabcol(@Param('empresa') empresa: string, @Body() lista: TabcolFisico[]) {
    this.cache.substituirTabcol(empresa, lista);
    return { recebidos: lista.length };
  }

  @Post('vendedores')
  vendedores(@Body() lista: VendedorFisico[]) {
    this.cache.substituirVendedores(lista);
    return { recebidos: lista.length };
  }

  @Post('usuarios')
  usuarios(@Body() lista: UsuarioFisico[]) {
    this.cache.substituirUsuarios(lista);
    return { recebidos: lista.length };
  }

  @Post('titulos')
  titulos(@Body() lista: TituloAbertoFisico[]) {
    this.cache.substituirTitulosAbertos(lista);
    return { recebidos: lista.length };
  }

  @Post('tipos-operacao')
  tiposOperacao(@Body() lista: TipoOperacaoFisico[]) {
    this.cache.substituirTiposOperacao(lista);
    return { recebidos: lista.length };
  }

  @Post('condicoes-pagamento')
  condicoesPagamento(@Body() lista: CondicaoPagamentoFisico[]) {
    this.cache.substituirCondicoesPagamento(lista);
    return { recebidos: lista.length };
  }

  @Post('cadsub/:empresa')
  cadsub(@Param('empresa') empresa: string, @Body() lista: CadsubFisico[]) {
    this.cache.substituirCadsub(empresa, lista);
    return { recebidos: lista.length };
  }

  @Post('cadicm/:empresa')
  cadicm(@Param('empresa') empresa: string, @Body() lista: CadicmFisico[]) {
    this.cache.substituirCadicm(empresa, lista);
    return { recebidos: lista.length };
  }

  @Post('empresas')
  empresas(@Body() lista: EmpresaFisica[]) {
    this.cache.substituirEmpresas(lista);
    return { recebidos: lista.length };
  }

  @Post('negociacoes')
  negociacoes(@Body() lista: NegociacaoFisica[]) {
    this.cache.substituirNegociacoes(lista);
    return { recebidos: lista.length };
  }

  @Get('status')
  status() {
    return this.cache.status();
  }
}
