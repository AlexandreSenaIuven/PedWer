import { Body, Controller, Get, Param, Post } from '@nestjs/common';
import { ComandosService } from './comandos.service';
import type { ItemCompra } from './comandos.types';

interface ResultadoComandoBody {
  sucesso: boolean;
  referenciaExterna?: string;
  erro?: string;
  ultimasCompras?: ItemCompra[];
}

@Controller('comandos')
export class ComandosController {
  constructor(private readonly comandos: ComandosService) {}

  /** O console faz poll aqui — nunca o contrário. */
  @Get('pendentes')
  pendentes() {
    return this.comandos.buscarPendentesEMarcarProcessando();
  }

  /** O console reporta o resultado depois de gravar (ou falhar). */
  @Post(':id/resultado')
  resultado(@Param('id') id: string, @Body() body: ResultadoComandoBody) {
    this.comandos.registrarResultado(id, body.sucesso, body.referenciaExterna, body.erro, body.ultimasCompras);
    return { ok: true };
  }

  /** O FRONT faz poll aqui para saber se o pedido já foi gravado (ou, para 'ConsultarUltimasCompras', se a consulta já voltou). */
  @Get(':id')
  status(@Param('id') id: string) {
    const comando = this.comandos.buscarPorId(id);
    return {
      id: comando.id,
      status: comando.status,
      referenciaExterna: comando.referenciaExterna,
      erro: comando.erro,
      ultimasCompras: comando.ultimasCompras,
    };
  }
}
