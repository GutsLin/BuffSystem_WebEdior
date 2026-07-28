import { Body, Controller, Delete, Get, HttpCode, Param, Post, Put } from '@nestjs/common';
import { BuffsService } from './buffs.service';
import { UpsertBuffDto } from './dto/upsert-buff.dto';

@Controller('buffs')
export class BuffsController {
  constructor(private readonly buffsService: BuffsService) {}

  @Get()
  findAll() {
    return this.buffsService.findAll();
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
