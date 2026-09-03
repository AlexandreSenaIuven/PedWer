import { Body, Controller, Get, Param, Post } from '@nestjs/common';
import { CacheService } from '../cache/cache.service';
import { ComandosService } from './comandos.service';
import type { ItemCompra, ItemGiro, SaldoEmpresa } from './comandos.types';

interface ResultadoComandoBody {
  sucesso: boolean;
  referenciaExterna?: string;
  erro?: string;
  ultimasCompras?: ItemCompra[];
  giro?: ItemGiro[];
  saldoGeral?: SaldoEmpresa[];
}

@Controller('comandos')
export class ComandosController {
  constructor(
    private readonly comandos: ComandosService,
    private readonly cache: CacheService,
  ) {}

  /** O console faz poll aqui — nunca o contrário. */
  @Get('pendentes')
  pendentes() {
    return this.comandos.buscarPendentesEMarcarProcessando();
  }

  /** O console reporta o resultado depois de gravar (ou falhar). */
  @Post(':id/resultado')
  resultado(@Param('id') id: string, @Body() body: ResultadoComandoBody) {
    this.comandos.registrarResultado(id, body);
    return { ok: true };
  }

  /** O FRONT faz poll aqui para saber se o pedido já foi gravado (ou, para os tipos 'Consultar...', se a consulta já voltou). */
  @Get(':id')
  status(@Param('id') id: string) {
    const comando = this.comandos.buscarPorId(id);
    return {
      id: comando.id,
      status: comando.status,
      referenciaExterna: comando.referenciaExterna,
      erro: comando.erro,
      // Descrição/característica não vêm do console (cadmov não tem esses
      // campos) — preenchidas aqui a partir do catálogo já sincronizado,
      // sem precisar de mais uma consulta ao VFP.
      ultimasCompras: comando.ultimasCompras?.map((item) => {
        const produto = this.cache.buscarProduto(comando.codigoEmpresa, item.grupo, item.referencia);
        return { ...item, produtoDescricao: produto?.descricao, produtoCaracter: produto?.caracter };
      }),
      // Idem para o nome do cliente em "Giro" (cadmov só tem o código).
      giro: comando.giro?.map((item) => ({
        ...item,
        clienteNome: this.cache.buscarCliente(item.cliFor)?.razaoSoc,
      })),
      saldoGeral: comando.saldoGeral,
    };
  }
}
