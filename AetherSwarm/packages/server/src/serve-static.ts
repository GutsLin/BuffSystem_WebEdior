import type { Express, Request, Response } from 'express';
import express from 'express';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));

export function serveStatic(app: Express): void {
  const distPath = join(__dirname, '..', '..', 'game', 'dist');
  app.use(express.static(distPath));
  app.get('/{*splat}', (_req: Request, res: Response) => {
    res.sendFile(join(distPath, 'index.html'));
  });
}
