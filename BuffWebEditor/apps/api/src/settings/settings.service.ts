import { Injectable } from '@nestjs/common';
import type { AttributeFormulaConfig } from '../common/buff.types';
import { defaultAttributeFormula } from '../common/demo-data';
import { JsonStorageService } from '../storage/storage.service';
import { UpdateAttributeFormulaDto } from './dto/update-attribute-formula.dto';

@Injectable()
export class SettingsService {
  private readonly fileName = 'attribute-formula.json';

  constructor(private readonly storage: JsonStorageService) {}

  getAttributeFormula(): Promise<AttributeFormulaConfig> {
    return this.storage.read(this.fileName, defaultAttributeFormula);
  }

  async updateAttributeFormula(dto: UpdateAttributeFormulaDto): Promise<AttributeFormulaConfig> {
    const config: AttributeFormulaConfig = { ...dto };
    await this.storage.write(this.fileName, config);
    return config;
  }
}
