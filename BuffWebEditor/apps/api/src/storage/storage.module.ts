import { Global, Module } from '@nestjs/common';
import { JsonStorageService } from './storage.service';

@Global()
@Module({
  providers: [JsonStorageService],
  exports: [JsonStorageService],
})
export class StorageModule {}
