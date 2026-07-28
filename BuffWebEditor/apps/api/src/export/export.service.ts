import { Injectable } from '@nestjs/common';
import type { UnityExportPayload } from '../common/buff.types';
import { BuffsService } from '../buffs/buffs.service';
import { SettingsService } from '../settings/settings.service';

@Injectable()
export class ExportService {
  constructor(
    private readonly buffsService: BuffsService,
    private readonly settingsService: SettingsService,
  ) {}

  async createUnityPayload(): Promise<UnityExportPayload> {
    const [buffs, attributeFormula] = await Promise.all([
      this.buffsService.findAll(),
      this.settingsService.getAttributeFormula(),
    ]);

    return {
      schemaVersion: '2.0.0',
      exportedAt: new Date().toISOString(),
      attributeFormula,
      buffs: buffs.map(({ createdAt: _createdAt, updatedAt: _updatedAt, ...buff }) => buff),
    };
  }
}
