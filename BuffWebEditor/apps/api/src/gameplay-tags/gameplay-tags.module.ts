import { Module } from '@nestjs/common';
import { BuffsModule } from '../buffs/buffs.module';
import { GameplayTagsController } from './gameplay-tags.controller';
import { GameplayTagsService } from './gameplay-tags.service';

@Module({
  imports: [BuffsModule],
  controllers: [GameplayTagsController],
  providers: [GameplayTagsService],
  exports: [GameplayTagsService],
})
export class GameplayTagsModule {}
