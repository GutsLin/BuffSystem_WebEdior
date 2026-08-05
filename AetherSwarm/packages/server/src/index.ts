import express from 'express';
import cors from 'cors';
import Database from 'better-sqlite3';
import { serveStatic } from './serve-static.js';

const app = express();
const PORT = Number(process.env.PORT ?? 3001);

app.use(cors());
app.use(express.json());

const db = new Database('/app/data/game.db');
db.exec(`CREATE TABLE IF NOT EXISTS scores (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  heroType TEXT NOT NULL,
  time REAL NOT NULL,
  kills INTEGER NOT NULL,
  createdAt TEXT NOT NULL DEFAULT (datetime('now'))
)`);

app.post('/api/score', (req, res) => {
  const { name, heroType, time, kills } = req.body;
  if (!name || !heroType || time == null || kills == null) {
    return res.status(400).json({ error: '缺少必填字段' });
  }
  db.prepare('INSERT INTO scores (name, heroType, time, kills) VALUES (?, ?, ?, ?)').run(
    String(name).slice(0, 12), String(heroType), Number(time), Math.floor(Number(kills))
  );
  res.json({ ok: true });
});

app.get('/api/leaderboard', (_req, res) => {
  const rows = db.prepare('SELECT name, heroType, time, kills FROM scores ORDER BY time DESC, kills DESC LIMIT 50').all();
  res.json(rows.map((r: any, i: number) => ({ ...r, rank: i + 1 })));
});

serveStatic(app);

app.listen(PORT, '0.0.0.0', () => {
  console.log(`AetherSwarm server on http://0.0.0.0:${PORT}`);
});
