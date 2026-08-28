import { Module } from '@nestjs/common';
import { CacheModule } from '../cache/cache.module';
import { ClientesController } from './clientes.controller';

@Module({
  imports: [CacheModule],
  controllers: [ClientesController],
})
export class ClientesModule {}
