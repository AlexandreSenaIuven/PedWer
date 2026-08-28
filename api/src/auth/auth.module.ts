import { Module } from '@nestjs/common';
import { CacheModule } from '../cache/cache.module';
import { AuthController } from './auth.controller';

@Module({
  imports: [CacheModule],
  controllers: [AuthController],
})
export class AuthModule {}
