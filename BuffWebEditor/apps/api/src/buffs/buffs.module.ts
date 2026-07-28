import { Module } from '@nestjs/common';
import { BuffsController } from './buffs.controller';
import { BuffsService } from './buffs.service';

@Module({
  controllers: [BuffsController],
  providers: [BuffsService],
  exports: [BuffsService],
})
export class BuffsModule {}
