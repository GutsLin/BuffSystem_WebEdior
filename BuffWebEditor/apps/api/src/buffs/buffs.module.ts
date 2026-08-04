import { Module } from '@nestjs/common';
import { AiService } from '../ai/ai.service';
import { BuffsController } from './buffs.controller';
import { BuffsService } from './buffs.service';

@Module({
  controllers: [BuffsController],
  providers: [BuffsService, AiService],
  exports: [BuffsService],
})
export class BuffsModule {}
