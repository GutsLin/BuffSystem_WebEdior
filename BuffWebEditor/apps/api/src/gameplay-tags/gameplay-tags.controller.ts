import { Body, Controller, Get, Header, Param, Post, Put, Res } from '@nestjs/common';
import type { Response } from 'express';
import { UpsertGameplayTagDto } from './dto/upsert-gameplay-tag.dto';
import { GameplayTagsService } from './gameplay-tags.service';

@Controller('gameplay-tags')
export class GameplayTagsController {
  constructor(private readonly gameplayTagsService: GameplayTagsService) {}

  @Get()
  findAll() {
    return this.gameplayTagsService.findAll();
  }

  @Get('version')
  getVersion() {
    return this.gameplayTagsService.getVersion();
  }

  @Get('export')
  @Header('Content-Type', 'application/json; charset=utf-8')
  async download(@Res() response: Response) {
    const payload = await this.gameplayTagsService.export();
    response.setHeader('Content-Disposition', 'attachment; filename="gameplay-tags.json"');
    response.send(`${JSON.stringify(payload, null, 2)}\n`);
  }

  @Post()
  create(@Body() dto: UpsertGameplayTagDto) {
    return this.gameplayTagsService.create(dto);
  }

  @Put(':id')
  update(@Param('id') id: string, @Body() dto: UpsertGameplayTagDto) {
    return this.gameplayTagsService.update(id, dto);
  }

  @Post('publish')
  publish() {
    return this.gameplayTagsService.publish();
  }
}
