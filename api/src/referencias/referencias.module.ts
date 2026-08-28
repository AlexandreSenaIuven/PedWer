import { Module } from '@nestjs/common';
import { CacheModule } from '../cache/cache.module';
import { ReferenciasController } from './referencias.controller';

@Module({
  imports: [CacheModule],
  controllers: [ReferenciasController],
})
export class ReferenciasModule {}
