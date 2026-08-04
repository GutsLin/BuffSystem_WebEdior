import { Body, Controller, Logger, Post } from '@nestjs/common';
import { AiService } from './ai.service';

@Controller('ai')
export class AiController {
  private readonly logger = new Logger(AiController.name);

  constructor(private readonly aiService: AiService) {}

  @Post('buff')
  buff(@Body() body: { prompt: string }) {
    this.logger.log('buff called');
    return this.aiService.generate(body);
  }
}
