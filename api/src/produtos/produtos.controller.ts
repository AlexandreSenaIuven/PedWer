import { Controller, Get, Param, Query } from '@nestjs/common';
import { CacheService } from '../cache/cache.service';
import { traduzirProduto } from './produtos.dto';

@Controller('produtos')
export class ProdutosController {
  constructor(private readonly cache: CacheService) {}

  @Get(':empresa')
  listar(@Param('empresa') empresa: string, @Query('q') q?: string, @Query('limite') limite?: string) {
    return this.cache.listarProdutos(empresa, q, limite ? Number(limite) : undefined).map(traduzirProduto);
  }
}
