import { Module } from '@nestjs/common';
import { CacheModule } from '../cache/cache.module';
import { ProdutosController } from './produtos.controller';

@Module({
  imports: [CacheModule],
  controllers: [ProdutosController],
})
export class ProdutosModule {}
