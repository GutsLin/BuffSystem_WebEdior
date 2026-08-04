import { Body, Controller, Post } from '@nestjs/common';
import { AiService } from './ai.service';
import { GenerateBuffDto } from './dto/generate.dto';

@Controller('ai')
export class AiController {
  constructor(private readonly aiService: AiService) {}

  @Post('generate')
  generate(@Body() dto: GenerateBuffDto) {
    return this.aiService.generate(dto);
  }
}
