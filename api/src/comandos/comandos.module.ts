import { Module } from '@nestjs/common';
import { ComandosController } from './comandos.controller';
import { ComandosService } from './comandos.service';

@Module({
  controllers: [ComandosController],
  providers: [ComandosService],
  exports: [ComandosService],
})
export class ComandosModule {}
