import { Application, Container, Text, TextStyle } from 'pixi.js';

interface DamageEntry {
  x: number; y: number; life: number; text: Text;
}

export class DamageNumbers {
  private app: Application;
  private entries: DamageEntry[] = [];
  private container: Container;

  constructor(app: Application) {
    this.app = app;
    this.container = new Container();
    this.container.zIndex = 200;
  }

  init(parent: Container): void {
    parent.addChild(this.container);
  }

  spawn(x: number, y: number, damage: number, type: 'physical' | 'magical' | 'pure'): void {
    const color = type === 'physical' ? '#e05461' : type === 'magical' ? '#6d5dfc' : '#fbbf24';
    const prefix = type === 'physical' ? '' : type === 'magical' ? '◆' : '★';
    const text = new Text({
      text: `${prefix}${Math.floor(damage)}`,
      style: new TextStyle({
        fontSize: 14 + Math.floor(Math.random() * 4),
        fill: color,
        fontFamily: 'monospace',
        fontWeight: 'bold',
        dropShadow: { color: '#000000', blur: 2, distance: 0 },
      }),
    });
    text.anchor.set(0.5);
    text.x = x + (Math.random() - 0.5) * 20;
    text.y = y;

    this.container.addChild(text);
    this.entries.push({ x: text.x, y: text.y, life: 1, text });
  }

  update(dt: number): void {
    for (let i = this.entries.length - 1; i >= 0; i--) {
      const e = this.entries[i];
      e.life -= dt;
      e.text.y -= 40 * dt;
      e.text.alpha = Math.max(0, e.life);
      if (e.life <= 0) {
        e.text.destroy();
        this.entries.splice(i, 1);
      }
    }
  }
}
