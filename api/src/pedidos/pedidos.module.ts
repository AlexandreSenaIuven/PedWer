import { Module } from '@nestjs/common';
import { CacheModule } from '../cache/cache.module';
import { ComandosModule } from '../comandos/comandos.module';
import { NucleoModule } from '../nucleo/nucleo.module';
import { PedidosController } from './pedidos.controller';

@Module({
  imports: [CacheModule, NucleoModule, ComandosModule],
  controllers: [PedidosController],
})
export class PedidosModule {}
