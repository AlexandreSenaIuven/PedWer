import { Module } from '@nestjs/common';
import { CacheModule } from '../cache/cache.module';
import { SincronizacaoController } from './sincronizacao.controller';

@Module({
  imports: [CacheModule],
  controllers: [SincronizacaoController],
})
export class SincronizacaoModule {}
