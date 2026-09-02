import { Module } from '@nestjs/common';
import { CacheModule } from '../cache/cache.module';
import { ComandosController } from './comandos.controller';
import { ComandosService } from './comandos.service';

@Module({
  imports: [CacheModule],
  controllers: [ComandosController],
  providers: [ComandosService],
  exports: [ComandosService],
})
export class ComandosModule {}
