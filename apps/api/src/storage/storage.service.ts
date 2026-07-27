import { Injectable } from '@nestjs/common';
import { mkdir, readFile, rename, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';

@Injectable()
export class JsonStorageService {
  private readonly dataDir = resolve(
    process.env.DATA_DIR ?? resolve(__dirname, '..', '..', '..', '..', 'data'),
  );
  private readonly writeQueues = new Map<string, Promise<void>>();

  async read<T>(fileName: string, fallback: T): Promise<T> {
    const filePath = this.getPath(fileName);
    try {
      const raw = await readFile(filePath, 'utf8');
      return JSON.parse(raw) as T;
    } catch (error) {
      const code = (error as NodeJS.ErrnoException).code;
      if (code !== 'ENOENT') {
        throw error;
      }
      await this.write(fileName, fallback);
      return structuredClone(fallback);
    }
  }

  async write<T>(fileName: string, data: T): Promise<void> {
    const filePath = this.getPath(fileName);
    const previous = this.writeQueues.get(filePath) ?? Promise.resolve();
    const next = previous.then(async () => {
      await mkdir(dirname(filePath), { recursive: true });
      const temporaryPath = `${filePath}.${process.pid}.tmp`;
      await writeFile(temporaryPath, `${JSON.stringify(data, null, 2)}\n`, 'utf8');
      await rename(temporaryPath, filePath);
    });
    this.writeQueues.set(filePath, next);
    try {
      await next;
    } finally {
      if (this.writeQueues.get(filePath) === next) {
        this.writeQueues.delete(filePath);
      }
    }
  }

  private getPath(fileName: string): string {
    return resolve(this.dataDir, fileName);
  }
}
