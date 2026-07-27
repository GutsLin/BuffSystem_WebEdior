import { Body, Controller, Get, Put } from '@nestjs/common';
import { UpdateAttributeFormulaDto } from './dto/update-attribute-formula.dto';
import { SettingsService } from './settings.service';

@Controller('settings')
export class SettingsController {
  constructor(private readonly settingsService: SettingsService) {}

  @Get('attribute-formula')
  getAttributeFormula() {
    return this.settingsService.getAttributeFormula();
  }

  @Put('attribute-formula')
  updateAttributeFormula(@Body() dto: UpdateAttributeFormulaDto) {
    return this.settingsService.updateAttributeFormula(dto);
  }
}
