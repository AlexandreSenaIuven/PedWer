import { Controller, Get, NotFoundException, Param, Query } from '@nestjs/common';
import { CacheService } from '../cache/cache.service';
import { traduzirCliente } from './clientes.dto';

@Controller('clientes')
export class ClientesController {
  constructor(private readonly cache: CacheService) {}

  @Get()
  listar(@Query('q') q?: string, @Query('limite') limite?: string, @Query('vendedor') vendedor?: string) {
    return this.cache.listarClientes(q, limite ? Number(limite) : undefined, vendedor).map(traduzirCliente);
  }

  @Get(':codigo')
  buscar(@Param('codigo') codigo: string) {
    const cliente = this.cache.buscarCliente(codigo);
    if (!cliente) throw new NotFoundException(`Cliente ${codigo} não encontrado (ou ainda não sincronizado).`);
    return traduzirCliente(cliente);
  }
}
