import { Module } from '@nestjs/common';
import { BuffsModule } from '../buffs/buffs.module';
import { SettingsModule } from '../settings/settings.module';
import { ExportController } from './export.controller';
import { ExportService } from './export.service';

@Module({
  imports: [BuffsModule, SettingsModule],
  controllers: [ExportController],
  providers: [ExportService],
})
export class ExportModule {}
