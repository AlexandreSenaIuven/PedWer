import { Controller, Get, NotFoundException, Param, Query } from '@nestjs/common';
import { CacheService } from '../cache/cache.service';

/** Tabelas de apoio pequenas (tipo de operação, condição de pagamento) — sem campo ambíguo, sem tradução L10 necessária. */
@Controller()
export class ReferenciasController {
  constructor(private readonly cache: CacheService) {}

  @Get('tipos-operacao')
  tiposOperacao(@Query('q') q?: string, @Query('limite') limite?: string) {
    return this.cache.listarTiposOperacao(q, limite ? Number(limite) : undefined).map((t) => ({
      codigo: t.tipo,
      descricao: t.descricao,
    }));
  }

  @Get('condicoes-pagamento')
  condicoesPagamento(@Query('q') q?: string, @Query('limite') limite?: string) {
    return this.cache.listarCondicoesPagamento(q, limite ? Number(limite) : undefined).map((c) => ({
      codigo: c.codigo,
      descricao: c.descOcor,
    }));
  }

  @Get('empresas')
  empresas() {
    return this.cache.listarEmpresas().map((e) => ({
      codigo: e.codEmpr,
      nome: e.nomeEmpr,
      uf: e.ufEmpr,
      cnpj: e.cgc,
      inscricaoEstadual: e.inscempr,
      endereco: e.enderE,
      bairro: e.bairro,
      cidade: e.cidadeE,
      cep: e.cepE,
      telefone: e.telefone,
      fax: e.fax,
      email: e.email,
    }));
  }

  @Get('vendedores')
  vendedores(@Query('q') q?: string, @Query('limite') limite?: string) {
    return this.cache.listarVendedores(q, limite ? Number(limite) : undefined).map((v) => ({
      codigo: v.codVend,
      descricao: v.nome,
    }));
  }

  @Get('vendedores/:codigo')
  vendedor(@Param('codigo') codigo: string) {
    const v = this.cache.buscarVendedor(codigo);
    if (!v) throw new NotFoundException(`Vendedor ${codigo} não encontrado.`);
    return { codigo: v.codVend, nome: v.nome };
  }
}
