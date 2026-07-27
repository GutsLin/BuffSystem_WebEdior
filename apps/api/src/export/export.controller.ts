import { Controller, Get, Header, Res } from '@nestjs/common';
import type { Response } from 'express';
import { ExportService } from './export.service';

@Controller('export')
export class ExportController {
  constructor(private readonly exportService: ExportService) {}

  @Get('unity/preview')
  preview() {
    return this.exportService.createUnityPayload();
  }

  @Get('unity')
  @Header('Content-Type', 'application/json; charset=utf-8')
  async download(@Res() response: Response) {
    const payload = await this.exportService.createUnityPayload();
    const date = new Date().toISOString().slice(0, 10).replaceAll('-', '');
    response.setHeader('Content-Disposition', `attachment; filename="buff-system-${date}.json"`);
    response.send(`${JSON.stringify(payload, null, 2)}\n`);
  }
}
