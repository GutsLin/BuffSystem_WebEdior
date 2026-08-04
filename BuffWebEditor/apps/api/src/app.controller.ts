import { Controller, Get, Query } from '@nestjs/common';

@Controller()
export class AppController {
  @Get('health')
  getHealth() {
    return { status: 'ok', service: 'buffwork-api', timestamp: new Date().toISOString() };
  }
}
