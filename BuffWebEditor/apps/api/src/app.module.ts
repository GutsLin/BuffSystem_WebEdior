import { Module } from '@nestjs/common';
import { ServeStaticModule } from '@nestjs/serve-static';
import { join } from 'node:path';
import { AppController } from './app.controller';
import { BuffsModule } from './buffs/buffs.module';
import { ExportModule } from './export/export.module';
import { GameplayTagsModule } from './gameplay-tags/gameplay-tags.module';
import { SettingsModule } from './settings/settings.module';
import { StorageModule } from './storage/storage.module';

@Module({
  imports: [
    ServeStaticModule.forRoot({
      rootPath: join(__dirname, '..', '..', 'web', 'dist'),
      exclude: ['/api/*path'],
    }),
    StorageModule,
    BuffsModule,
    SettingsModule,
    ExportModule,
    GameplayTagsModule,
  ],
  controllers: [AppController],
})
export class AppModule {}
