import { ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { randomUUID } from 'node:crypto';
import type { BuffTemplate } from '../common/buff.types';
import { demoBuffs } from '../common/demo-data';
import { JsonStorageService } from '../storage/storage.service';
import { UpsertBuffDto } from './dto/upsert-buff.dto';

@Injectable()
export class BuffsService {
  private readonly fileName = 'buffs.json';

  constructor(private readonly storage: JsonStorageService) {}

  async findAll(): Promise<BuffTemplate[]> {
    const buffs = await this.storage.read(this.fileName, demoBuffs);
    return buffs.sort((a, b) => b.updatedAt.localeCompare(a.updatedAt));
  }

  async findOne(id: string): Promise<BuffTemplate> {
    const buff = (await this.findAll()).find((item) => item.id === id);
    if (!buff) {
      throw new NotFoundException(`未找到效果配置 ${id}`);
    }
    return buff;
  }

  async create(dto: UpsertBuffDto): Promise<BuffTemplate> {
    const buffs = await this.findAll();
    this.assertUniqueKey(buffs, dto.key);
    const timestamp = new Date().toISOString();
    const buff: BuffTemplate = {
      ...dto,
      id: randomUUID(),
      createdAt: timestamp,
      updatedAt: timestamp,
    };
    await this.storage.write(this.fileName, [...buffs, buff]);
    return buff;
  }

  async update(id: string, dto: UpsertBuffDto): Promise<BuffTemplate> {
    const buffs = await this.findAll();
    const index = buffs.findIndex((item) => item.id === id);
    if (index < 0) {
      throw new NotFoundException(`未找到效果配置 ${id}`);
    }
    this.assertUniqueKey(buffs, dto.key, id);
    const updated: BuffTemplate = {
      ...buffs[index],
      ...dto,
      id,
      updatedAt: new Date().toISOString(),
    };
    buffs[index] = updated;
    await this.storage.write(this.fileName, buffs);
    return updated;
  }

  async duplicate(id: string): Promise<BuffTemplate> {
    const source = await this.findOne(id);
    const buffs = await this.findAll();
    const key = this.createCopyKey(source.key, buffs);
    const timestamp = new Date().toISOString();
    const copy: BuffTemplate = {
      ...structuredClone(source),
      id: randomUUID(),
      key,
      displayName: `${source.displayName} 副本`,
      attributeModifiers: source.attributeModifiers.map((item) => ({ ...item, id: randomUUID() })),
      effectActions: source.effectActions.map((item) => ({ ...item, id: randomUUID() })),
      createdAt: timestamp,
      updatedAt: timestamp,
    };
    await this.storage.write(this.fileName, [...buffs, copy]);
    return copy;
  }

  async remove(id: string): Promise<void> {
    const buffs = await this.findAll();
    const remaining = buffs.filter((item) => item.id !== id);
    if (remaining.length === buffs.length) {
      throw new NotFoundException(`未找到效果配置 ${id}`);
    }
    await this.storage.write(this.fileName, remaining);
  }

  private assertUniqueKey(buffs: BuffTemplate[], key: string, ignoredId?: string) {
    if (buffs.some((item) => item.key === key && item.id !== ignoredId)) {
      throw new ConflictException(`效果标识 ${key} 已存在`);
    }
  }

  private createCopyKey(sourceKey: string, buffs: BuffTemplate[]): string {
    let suffix = 1;
    let candidate = `${sourceKey}_Copy`;
    while (buffs.some((item) => item.key === candidate)) {
      suffix += 1;
      candidate = `${sourceKey}_Copy${suffix}`;
    }
    return candidate;
  }
}
