import { Body, Controller, Delete, Get, HttpCode, Param, Post, Put, Query } from '@nestjs/common';
import { AiService } from '../ai/ai.service';
import { BuffsService } from './buffs.service';
import { UpsertBuffDto } from './dto/upsert-buff.dto';

@Controller('buffs')
export class BuffsController {
  constructor(
    private readonly buffsService: BuffsService,
    private readonly aiService: AiService,
  ) {}

  @Get()
  findAll() {
    return this.buffsService.findAll();
  }

  @Get('ai-generate')
  aiGenerate(@Query('prompt') prompt: string) {
    if (!prompt || prompt.trim().length === 0) {
      return { error: '请提供 prompt 参数' };
    }
    return this.aiService.generate({ prompt: prompt.trim() });
  }

  @Get(':id')
  findOne(@Param('id') id: string) {
    return this.buffsService.findOne(id);
  }

  @Post()
  create(@Body() dto: UpsertBuffDto) {
    return this.buffsService.create(dto);
  }

  @Put(':id')
  update(@Param('id') id: string, @Body() dto: UpsertBuffDto) {
    return this.buffsService.update(id, dto);
  }

  @Post(':id/duplicate')
  duplicate(@Param('id') id: string) {
    return this.buffsService.duplicate(id);
  }

  @Delete(':id')
  @HttpCode(204)
  remove(@Param('id') id: string) {
    return this.buffsService.remove(id);
  }
}
