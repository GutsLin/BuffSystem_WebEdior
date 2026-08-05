import express from 'express';
import cors from 'cors';
import { readFileSync, writeFileSync, existsSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { serveStatic } from './serve-static.js';

const app = express();
const PORT = Number(process.env.PORT ?? 3001);
const DATA_DIR = process.env.DATA_DIR ?? join(process.cwd(), 'data');
const SCORE_FILE = join(DATA_DIR, 'scores.json');

app.use(cors());
app.use(express.json());

if (!existsSync(DATA_DIR)) mkdirSync(DATA_DIR, { recursive: true });

function readScores(): any[] {
  if (!existsSync(SCORE_FILE)) return [];
  try { return JSON.parse(readFileSync(SCORE_FILE, 'utf-8')); } catch { return []; }
}

function writeScores(scores: any[]): void {
  writeFileSync(SCORE_FILE, JSON.stringify(scores), 'utf-8');
}

app.post('/api/score', (req, res) => {
  const { name, heroType, time, kills } = req.body;
  if (!name || !heroType || time == null || kills == null) {
    return res.status(400).json({ error: 'missing fields' });
  }
  const scores = readScores();
  scores.push({
    name: String(name).slice(0, 12),
    heroType: String(heroType),
    time: Number(time),
    kills: Math.floor(Number(kills)),
    createdAt: new Date().toISOString(),
  });
  writeScores(scores);
  res.json({ ok: true });
});

app.get('/api/leaderboard', (_req, res) => {
  const rows = readScores()
    .sort((a, b) => b.time - a.time || b.kills - a.kills)
    .slice(0, 50)
    .map((r, i) => ({ ...r, rank: i + 1 }));
  res.json(rows);
});

serveStatic(app);

app.listen(PORT, '0.0.0.0', () => {
  console.log(`AetherSwarm server on http://0.0.0.0:${PORT}`);
});
