import { ConflictException, Injectable, NotFoundException, UnprocessableEntityException } from '@nestjs/common';
import { randomUUID } from 'node:crypto';
import type { BuffTemplate } from '../common/buff.types';
import { defaultGameplayTags } from '../common/gameplay-tags-data';
import {
  gameplayTagSchemaVersion,
  type GameplayTagExport,
  type GameplayTagRecord,
  type GameplayTagsExportPayload,
  type GameplayTagsStorageData,
} from '../common/gameplay-tags.types';
import { BuffsService } from '../buffs/buffs.service';
import { JsonStorageService } from '../storage/storage.service';
import { UpsertGameplayTagDto } from './dto/upsert-gameplay-tag.dto';

const tagNamePattern = /^[A-Za-z][A-Za-z0-9]*(?:\.[A-Za-z][A-Za-z0-9]*)*$/;

@Injectable()
export class GameplayTagsService {
  private readonly fileName = 'gameplay-tags.json';

  constructor(
    private readonly storage: JsonStorageService,
    private readonly buffsService: BuffsService,
  ) {}

  async findAll(): Promise<GameplayTagRecord[]> {
    const data = await this.readData();
    return [...data.tags].sort((left, right) => left.name.localeCompare(right.name));
  }

  async getVersion() {
    const data = await this.readData();
    return {
      schemaVersion: gameplayTagSchemaVersion,
      version: data.version,
      publishedVersion: data.publishedVersion,
      publishedAt: data.publishedAt,
      tagCount: data.tags.filter((tag) => !tag.deprecated).length,
    };
  }

  async create(dto: UpsertGameplayTagDto): Promise<GameplayTagRecord> {
    const data = await this.readData();
    this.assertName(dto.name);
    this.assertUniqueName(data.tags, dto.name);
    const timestamp = new Date().toISOString();
    const tag: GameplayTagRecord = {
      id: randomUUID(),
      name: dto.name,
      displayName: dto.displayName,
      description: dto.description ?? '',
      flags: dto.flags ?? 0,
      source: dto.source ?? 'web',
      deprecated: dto.deprecated ?? false,
      createdAt: timestamp,
      updatedAt: timestamp,
    };
    const tags = this.addMissingParents([...data.tags, tag], tag.name, timestamp);
    await this.storage.write(this.fileName, { ...data, tags });
    return tag;
  }

  async update(id: string, dto: UpsertGameplayTagDto): Promise<GameplayTagRecord> {
    const data = await this.readData();
    const index = data.tags.findIndex((tag) => tag.id === id);
    if (index < 0) {
      throw new NotFoundException(`未找到 GameplayTag ${id}`);
    }

    const current = data.tags[index];
    if (current.name !== dto.name) {
      throw new ConflictException('标签完整名称发布后不可修改，请新建标签并保留旧标签用于迁移。');
    }
    this.assertName(dto.name);
    const updated: GameplayTagRecord = {
      ...current,
      displayName: dto.displayName,
      description: dto.description ?? '',
      flags: dto.flags ?? current.flags,
      source: dto.source ?? current.source,
      deprecated: dto.deprecated ?? current.deprecated,
      updatedAt: new Date().toISOString(),
    };
    data.tags[index] = updated;
    await this.storage.write(this.fileName, data);
    return updated;
  }

  async export(): Promise<GameplayTagsExportPayload> {
    const data = await this.readData();
    await this.validateForExport(data.tags);
    return this.toExportPayload(data);
  }

  async publish(): Promise<GameplayTagsExportPayload> {
    const data = await this.readData();
    await this.validateForExport(data.tags);
    const nextVersion = Math.max(data.version, data.publishedVersion) + 1;
    const publishedAt = new Date().toISOString();
    const nextData: GameplayTagsStorageData = {
      ...data,
      version: nextVersion,
      publishedVersion: nextVersion,
      publishedAt,
    };
    await this.storage.write(this.fileName, nextData);
    return this.toExportPayload(nextData);
  }

  async remove(id: string): Promise<void> {
    const data = await this.readData();
    const tag = data.tags.find((item) => item.id === id);
    if (!tag) {
      throw new NotFoundException(`未找到 GameplayTag ${id}`);
    }
    if (tag.source === 'system') {
      throw new ConflictException('系统标签不能删除，请标记为废弃。');
    }
    const buffs = await this.buffsService.findAll();
    if (buffs.some((buff) => buff.tags.includes(tag.name))) {
      throw new ConflictException(`标签 ${tag.name} 仍被 Buff 引用，请先迁移引用。`);
    }
    tag.deprecated = true;
    tag.updatedAt = new Date().toISOString();
    await this.storage.write(this.fileName, data);
  }

  private async readData(): Promise<GameplayTagsStorageData> {
    const data = await this.storage.read(this.fileName, defaultGameplayTags);
    data.tags ??= [];
    data.version = Math.max(1, Number(data.version) || 1);
    data.publishedVersion = Math.max(data.version, Number(data.publishedVersion) || data.version);
    return data;
  }

  private async validateForExport(tags: GameplayTagRecord[]): Promise<void> {
    const activeTags = tags.filter((tag) => !tag.deprecated);
    const names = new Set<string>();
    for (const tag of activeTags) {
      this.assertName(tag.name);
      const normalizedName = tag.name.toLowerCase();
      if (names.has(normalizedName)) {
        throw new UnprocessableEntityException(`标签名称重复：${tag.name}`);
      }
      names.add(normalizedName);
    }

    for (const tag of activeTags) {
      const parentNames = this.getParentNames(tag.name);
      for (const parentName of parentNames) {
        if (!names.has(parentName.toLowerCase())) {
          throw new UnprocessableEntityException(`标签 ${tag.name} 缺少父级标签 ${parentName}`);
        }
      }
    }

    const buffs = await this.buffsService.findAll();
    for (const buff of buffs) {
      for (const tagName of buff.tags ?? []) {
        if (!names.has(tagName)) {
          throw new UnprocessableEntityException(`Buff ${buff.key} 引用了不存在或已废弃的标签：${tagName}`);
        }
      }
    }
  }

  private toExportPayload(data: GameplayTagsStorageData): GameplayTagsExportPayload {
    const tags: GameplayTagExport[] = data.tags
      .filter((tag) => !tag.deprecated)
      .sort((left, right) => left.name.localeCompare(right.name))
      .map(({ id: _id, createdAt: _createdAt, updatedAt: _updatedAt, ...tag }) => tag);
    return {
      schemaVersion: gameplayTagSchemaVersion,
      version: data.publishedVersion,
      exportedAt: new Date().toISOString(),
      tags,
    };
  }

  private assertUniqueName(tags: GameplayTagRecord[], name: string, ignoredId?: string): void {
    if (tags.some((tag) => tag.name.toLowerCase() === name.toLowerCase() && tag.id !== ignoredId)) {
      throw new ConflictException(`标签名称 ${name} 已存在`);
    }
  }

  private assertName(name: string): void {
    if (!tagNamePattern.test(name)) {
      throw new UnprocessableEntityException(`标签名称格式不正确：${name}`);
    }
  }

  private addMissingParents(tags: GameplayTagRecord[], name: string, timestamp: string): GameplayTagRecord[] {
    const byName = new Map(tags.map((tag) => [tag.name, tag]));
    for (const parentName of this.getParentNames(name)) {
      if (byName.has(parentName)) continue;
      const parent: GameplayTagRecord = {
        id: randomUUID(),
        name: parentName,
        displayName: parentName.split('.').at(-1) ?? parentName,
        description: '自动补齐的父级标签。',
        flags: 0,
        source: 'web',
        deprecated: false,
        createdAt: timestamp,
        updatedAt: timestamp,
      };
      tags.push(parent);
      byName.set(parentName, parent);
    }
    return tags;
  }

  private getParentNames(name: string): string[] {
    const parts = name.split('.');
    return parts.slice(1).map((_, index) => parts.slice(0, index + 1).join('.'));
  }
}
